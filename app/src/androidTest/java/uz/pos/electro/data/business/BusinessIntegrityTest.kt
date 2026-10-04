package uz.pos.electro.data.business

import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Test
import org.junit.Assert.*
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.SaleAccounting
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.util.DatabaseBackupExporter
import android.database.sqlite.SQLiteDatabase
import java.io.File

class BusinessIntegrityTest {
    private fun receipt(rate: Double = 12000.0) = SaleWithItems(
        SaleEntity(id = 1, guid = "a1b2c3d4-1234-4321-aaaa-0123456789ab", totalAmount = 150000.0,
            totalCost = 120000.0, paymentType = PaymentType.CARD, cardAmount = 150000.0, taxAmount = 2700.0, usdRate = rate),
        listOf(SaleItemEntity(id = 1, saleId = 1, productId = 1, productName = "Old name", quantity = 1.0,
            priceAtSale = 150000.0, costAtSale = 10.0, costCurrency = "USD", categoryAtSale = "Cable", warehouseGuid = "w")))
    @Test fun historicalProfitAndTaxAreImmutable() {
        val rows = SaleAccounting.lines(receipt())
        assertEquals(27300.0, rows.sumOf { it.profit }, .000001)
        assertEquals(2.275, rows.sumOf { it.profitUsd!! }, .000001)
        assertEquals("Old name", rows[0].productName)
        assertEquals("Cable", rows[0].category)
        assertEquals(2.275, SaleAccounting.lines(receipt(0.0))[0].profitUsd!!, .000001)
    }
    @Test fun oldUzsSaleDoesNotInventRateAndReceiptDoesNotDependOnLocalId() {
        val original = receipt(0.0)
        val old = original.copy(items = original.items.map { it.copy(costAtSale = 120000.0, costCurrency = "UZS") })
        assertNull(SaleAccounting.lines(old)[0].profitUsd)
        assertEquals(old.sale.receiptNumber, old.sale.copy(id = 999).receiptNumber)
        assertEquals("LP-A1B2C3D412344321AAAA0123456789AB", old.sale.receiptNumber)
    }
    @Test fun roundingAndBrakStaySeparate() {
        for (total in listOf(.01, .02, .05, 10.01, -10.01)) {
            val split = SaleAccounting.allocate(total, List(100) { 1.0 })
            assertEquals(total, split.sum(), .000001)
            if (total > 0) assertTrue(split.all { it >= 0 })
        }
        val old = receipt()
        val brak = old.copy(sale = old.sale.copy(paymentType = PaymentType.BRAK, totalAmount = 0.0, cardAmount = 0.0, taxAmount = 0.0))
        val summary = SaleAccounting.summary(SaleAccounting.lines(brak), 15000.0)
        assertEquals(0, summary.salesCount); assertEquals(1, summary.brakCount)
        assertEquals(-120000.0, summary.netProfit, .000001)
    }
    @Test fun returnAndReversalUseHistoricalRateAndDoNotCountAsNewSales() {
        val original=receipt()
        val returned=original.copy(sale=original.sale.copy(id=2,guid="returned",paymentType=PaymentType.RETURN,
            totalAmount=-150000.0,totalCost=-120000.0,cashAmount=-150000.0,cardAmount=0.0,taxAmount=0.0),
            items=original.items.map { it.copy(id=2,saleId=2,priceAtSale=-150000.0,costAtSale=-120000.0,costCurrency="UZS") })
        val summary=SaleAccounting.summary(SaleAccounting.lines(original)+SaleAccounting.lines(returned),99999.0)
        assertEquals(-2700.0,summary.netProfit,0.000001)
        assertEquals(-0.225,summary.netProfitUsd,0.000001)
        assertEquals(1,summary.salesCount)
        assertEquals(150000.0,summary.refundedAmount,0.0)
        assertEquals(120000.0,summary.costReversal,0.0)
        assertEquals("RT-RETURNED",returned.sale.receiptNumber)
        val reversal=returned.copy(sale=returned.sale.copy(id=3,guid="reversal",paymentType=PaymentType.RETURN_REVERSAL,
            totalAmount=150000.0,totalCost=120000.0,cashAmount=150000.0),
            items=returned.items.map { it.copy(id=3,saleId=3,priceAtSale=150000.0,costAtSale=120000.0) })
        assertEquals("RV-REVERSAL",reversal.sale.receiptNumber)
        assertEquals(27300.0,SaleAccounting.summary(SaleAccounting.lines(original)+SaleAccounting.lines(returned)+SaleAccounting.lines(reversal),99999.0).netProfit,0.000001)
    }
    @Test fun snapshotContainsUncheckpointedChangesAndDoesNotFireSyncTriggers() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val sourceFile = context.getDatabasePath("snapshot-test.db")
        SQLiteDatabase.deleteDatabase(sourceFile)
        val source = Room.databaseBuilder(context, AppDatabase::class.java, sourceFile.name).build()
        val destination = File(context.cacheDir, "snapshot-test-copy.db")
        SQLiteDatabase.deleteDatabase(destination)
        try {
            val db = source.openHelper.writableDatabase
            uz.pos.electro.data.sync.WifiSyncSchema.install(db,context.assets.open("wifi-sync-schema.sql").bufferedReader().use { it.readText() })
            db.execSQL("INSERT INTO return_drafts(sale_guid,request_guid,authority_guid,payload,state) VALUES('sale','request','desktop','{}','submitted')")
            db.execSQL("CREATE TABLE backup_probe(id INTEGER PRIMARY KEY AUTOINCREMENT, payload BLOB, note TEXT)")
            db.execSQL("INSERT INTO backup_probe VALUES(100, X'00FF01', 'old')")
            db.execSQL("DELETE FROM backup_probe")
            db.execSQL("INSERT INTO backup_probe(payload,note) VALUES(X'00FF01','new without sales')")
            DatabaseBackupExporter.copySnapshot(db, destination)
            SQLiteDatabase.openDatabase(destination.path, null, SQLiteDatabase.OPEN_READONLY).use { backup ->
                backup.rawQuery("SELECT id,hex(payload),note FROM backup_probe", null).use { c ->
                    assertTrue(c.moveToFirst()); assertEquals(101, c.getInt(0)); assertEquals("00FF01", c.getString(1)); assertEquals("new without sales", c.getString(2))
                }
                backup.rawQuery("SELECT request_guid,state FROM return_drafts",null).use { c ->
                    assertTrue(c.moveToFirst()); assertEquals("request",c.getString(0)); assertEquals("submitted",c.getString(1))
                }
                assertEquals(13, backup.version)
                backup.rawQuery("PRAGMA integrity_check", null).use { c -> c.moveToFirst(); assertEquals("ok", c.getString(0)) }
            }
        } finally { source.close(); SQLiteDatabase.deleteDatabase(sourceFile); SQLiteDatabase.deleteDatabase(destination) }
    }
    @Test fun version12MigrationPreservesReceiptLinesAndCreatesRecoverySnapshot() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val name = "return-migration-${java.util.UUID.randomUUID()}.db"
        val file = context.getDatabasePath(name)
        val initial = Room.databaseBuilder(context, AppDatabase::class.java, name).build()
        initial.openHelper.writableDatabase
        initial.close()
        SQLiteDatabase.openDatabase(file.path, null, SQLiteDatabase.OPEN_READWRITE).use { old ->
            val tableSql = old.rawQuery("SELECT sql FROM sqlite_master WHERE name='sale_items'", null).use { it.moveToFirst(); it.getString(0) }
            val indexes = mutableListOf<String>()
            old.rawQuery("SELECT sql FROM sqlite_master WHERE type='index' AND tbl_name='sale_items' AND name!='index_sale_items_guid' AND sql IS NOT NULL", null).use { while(it.moveToNext()) indexes.add(it.getString(0)) }
            val legacySql = tableSql.replace(Regex("`guid` TEXT NOT NULL DEFAULT '',\\s*"), "")
            assertNotEquals(tableSql, legacySql)
            old.execSQL("DROP TABLE sale_items")
            old.execSQL(legacySql)
            indexes.forEach { old.execSQL(it) }
            old.execSQL("INSERT INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
            old.execSQL("INSERT INTO sales(id,guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,created_at,user_id,is_synced,usd_rate) VALUES(77,'legacy-guid',100,60,'CASH',100,0,0,0,123,1,0,12000)")
            old.execSQL("INSERT INTO sale_items(id,sale_id,sale_guid,product_id,quantity,price_at_sale,cost_at_sale) VALUES(501,77,'legacy-guid',1,0.7,100,60)")
            old.version = 12
        }
        val backupDir = File(context.filesDir, "migration-backups")
        val before = backupDir.listFiles()?.map { it.name }?.toSet() ?: emptySet()
        val scope = kotlinx.coroutines.CoroutineScope(kotlinx.coroutines.SupervisorJob() + kotlinx.coroutines.Dispatchers.IO)
        val migrated = AppDatabase.buildDatabase(context, scope, name)
        try {
            val db = migrated.openHelper.writableDatabase
            db.query("SELECT guid,quantity FROM sale_items WHERE id=501").use { c ->
                assertTrue(c.moveToFirst()); assertEquals("legacy-guid:1", c.getString(0)); assertEquals(0.7, c.getDouble(1), 0.0)
            }
            db.query("SELECT total_amount,total_cost FROM sales WHERE id=77").use { c ->
                c.moveToFirst(); assertEquals(100.0, c.getDouble(0), 0.0); assertEquals(60.0, c.getDouble(1), 0.0)
            }
            val backups = backupDir.listFiles()!!.filter { it.name !in before }
            assertEquals(1, backups.size)
            SQLiteDatabase.openDatabase(backups.single().path, null, SQLiteDatabase.OPEN_READONLY).use { recovery ->
                assertEquals(12, recovery.version)
                recovery.rawQuery("SELECT COUNT(*) FROM sale_items WHERE id=501",null).use { c -> c.moveToFirst(); assertEquals(1,c.getInt(0)) }
            }
            backups.forEach { SQLiteDatabase.deleteDatabase(it) }
        } finally {
            migrated.close(); scope.coroutineContext[kotlinx.coroutines.Job]?.cancel(); SQLiteDatabase.deleteDatabase(file)
        }
    }

}
