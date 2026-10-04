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
    @Test fun snapshotContainsUncheckpointedChangesAndDoesNotFireSyncTriggers() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val sourceFile = context.getDatabasePath("snapshot-test.db")
        SQLiteDatabase.deleteDatabase(sourceFile)
        val source = Room.databaseBuilder(context, AppDatabase::class.java, sourceFile.name).build()
        val destination = File(context.cacheDir, "snapshot-test-copy.db")
        SQLiteDatabase.deleteDatabase(destination)
        try {
            val db = source.openHelper.writableDatabase
            db.execSQL("CREATE TABLE backup_probe(id INTEGER PRIMARY KEY AUTOINCREMENT, payload BLOB, note TEXT)")
            db.execSQL("INSERT INTO backup_probe VALUES(100, X'00FF01', 'old')")
            db.execSQL("DELETE FROM backup_probe")
            db.execSQL("INSERT INTO backup_probe(payload,note) VALUES(X'00FF01','new without sales')")
            DatabaseBackupExporter.copySnapshot(db, destination)
            SQLiteDatabase.openDatabase(destination.path, null, SQLiteDatabase.OPEN_READONLY).use { backup ->
                backup.rawQuery("SELECT id,hex(payload),note FROM backup_probe", null).use { c ->
                    assertTrue(c.moveToFirst()); assertEquals(101, c.getInt(0)); assertEquals("00FF01", c.getString(1)); assertEquals("new without sales", c.getString(2))
                }
                assertEquals(12, backup.version)
                backup.rawQuery("PRAGMA integrity_check", null).use { c -> c.moveToFirst(); assertEquals("ok", c.getString(0)) }
            }
        } finally { source.close(); SQLiteDatabase.deleteDatabase(sourceFile); SQLiteDatabase.deleteDatabase(destination) }
    }
}
