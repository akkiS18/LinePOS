package uz.pos.electro.data.business

import android.database.sqlite.SQLiteDatabase
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.DebtSchema
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.util.DatabaseBackupExporter
import java.io.File
import java.util.UUID

class DebtMigrationTest {
    @Test fun version13UpgradeCreatesBackupAndPreservesLegacyReceipts() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val name="debt-upgrade-${UUID.randomUUID()}.db"
        val file=context.getDatabasePath(name)
        // Raw Room builder intentionally omits production callbacks: unchanged v13 Room tables.
        Room.databaseBuilder(context,AppDatabase::class.java,name).build().let { it.openHelper.writableDatabase;it.close() }
        SQLiteDatabase.openDatabase(file.path,null,SQLiteDatabase.OPEN_READWRITE).use { old ->
            old.execSQL("INSERT INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
            old.execSQL("INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at,user_id,is_synced,usd_rate,cash_amount,card_amount,tax_amount,tax_rate) VALUES('legacy',123,70,'DEBT',1,1,0,12000,0,0,0,0)")
            old.version=13
        }
        val backupDir=File(context.filesDir,"migration-backups")
        val before=backupDir.listFiles()?.map { it.name }?.toSet() ?: emptySet()
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val db=AppDatabase.buildDatabase(context,scope,name)
        val snapshot=File(context.cacheDir,"debt-copy-${UUID.randomUUID()}.db")
        try {
            val sql=db.openHelper.writableDatabase
            assertEquals(14,sql.version)
            sql.query("SELECT total_amount FROM sales WHERE guid='legacy'").use { assertTrue(it.moveToFirst());assertEquals(123.0,it.getDouble(0),0.0) }
            sql.query("SELECT COUNT(*) FROM debt_accounts").use { it.moveToFirst();assertEquals(0,it.getInt(0)) }
            val backups=backupDir.listFiles()!!.filter { it.name !in before && it.name.startsWith("before-debt-") }
            assertEquals(1,backups.size)
            SQLiteDatabase.openDatabase(backups.single().path,null,SQLiteDatabase.OPEN_READONLY).use { recovery ->
                assertEquals(13,recovery.version)
                recovery.rawQuery("SELECT COUNT(*) FROM sqlite_master WHERE name='debt_schema'",null).use { it.moveToFirst();assertEquals(0,it.getInt(0)) }
                recovery.rawQuery("SELECT total_amount FROM sales WHERE guid='legacy'",null).use { it.moveToFirst();assertEquals(123.0,it.getDouble(0),0.0) }
            }
            // Auxiliary debt records are included even without a new sale.
            sql.execSQL("INSERT INTO debt_scope VALUES(1,'shop')")
            sql.execSQL("INSERT INTO debt_customers(guid,store_guid,name,created_at,device_guid) VALUES('c','shop','Ali',1,'device')")
            DatabaseBackupExporter.copySnapshot(sql,snapshot)
            SQLiteDatabase.openDatabase(snapshot.path,null,SQLiteDatabase.OPEN_READONLY).use { copy ->
                copy.rawQuery("SELECT name FROM debt_customers",null).use { assertTrue(it.moveToFirst());assertEquals("Ali",it.getString(0)) }
                copy.rawQuery("PRAGMA foreign_key_check",null).use { assertFalse(it.moveToFirst()) }
            }
            db.close()
            val reopened=AppDatabase.buildDatabase(context,scope,name)
            try { reopened.openHelper.writableDatabase.query("SELECT version FROM debt_schema").use { it.moveToFirst();assertEquals(1,it.getInt(0)) } }
            finally { reopened.close() }
            assertEquals(1,backupDir.listFiles()!!.count { it.name !in before && it.name.startsWith("before-debt-") })
            backups.forEach { SQLiteDatabase.deleteDatabase(it) }
        } finally { db.close();scope.cancel();SQLiteDatabase.deleteDatabase(file);SQLiteDatabase.deleteDatabase(snapshot) }
    }

    @Test fun freshDatabaseChecksForeignKeysAndFailedInstallRollsBack() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val name="debt-fresh-${UUID.randomUUID()}.db"
        val db=AppDatabase.buildDatabase(context,scope,name)
        try {
            val sql=db.openHelper.writableDatabase
            // A WAL read connection's PRAGMA does not describe the connection that writes.
            sql.beginTransaction()
            try {
                sql.query("PRAGMA foreign_keys").use { it.moveToFirst();assertEquals(1,it.getInt(0)) }
                sql.setTransactionSuccessful()
            } finally { sql.endTransaction() }
            DebtSchema.install(sql,context.assets.open("debt-schema.sql").bufferedReader().use { it.readText() })
            sql.query("SELECT COUNT(*) FROM debt_events").use { it.moveToFirst();assertEquals(0,it.getInt(0)) }
            try {
                sql.execSQL("INSERT INTO debt_customers(guid,store_guid,name,created_at,device_guid) VALUES('c','missing','Ali',1,'device')")
                fail("Missing shop must fail")
            } catch (_: android.database.sqlite.SQLiteConstraintException) { }
            sql.execSQL("DROP TABLE debt_schema;" )
            sql.execSQL("CREATE TABLE debt_schema(id INTEGER PRIMARY KEY,version INTEGER)")
            sql.execSQL("INSERT INTO debt_schema VALUES(1,99)")
            try { DebtSchema.install(sql,context.assets.open("debt-schema.sql").bufferedReader().use { it.readText() });fail("Future version must fail") }
            catch (_: IllegalStateException) { }
            sql.query("SELECT version FROM debt_schema").use { it.moveToFirst();assertEquals(99,it.getInt(0)) }
        } finally { db.close();scope.cancel();SQLiteDatabase.deleteDatabase(context.getDatabasePath(name)) }

        val raw=Room.inMemoryDatabaseBuilder(context,AppDatabase::class.java).build()
        try {
            val sql=raw.openHelper.writableDatabase
            val schema=context.assets.open("debt-schema.sql").bufferedReader().use { it.readText() }
            try { DebtSchema.install(sql,schema+"\n-- statement\nINVALID SQL");fail("Broken migration must fail") }
            catch (_: android.database.sqlite.SQLiteException) { }
            sql.query("SELECT COUNT(*) FROM sqlite_master WHERE name GLOB 'debt_*'").use { it.moveToFirst();assertEquals(0,it.getInt(0)) }
        } finally { raw.close() }
    }

    @Test fun restoreDatabasePreservesPaymentOnlyAndFullDebtHistory() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val name = "debt-restore-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, name)
        val backupFile = File(context.cacheDir, "test-backup-${UUID.randomUUID()}.db")
        try {
            val sql = db.openHelper.writableDatabase
            val storeGuid = UUID.randomUUID().toString()
            val custGuid = UUID.randomUUID().toString()

            sql.execSQL("INSERT OR REPLACE INTO debt_scope VALUES(1, '$storeGuid')")
            sql.execSQL("INSERT INTO debt_customers(guid, store_guid, name, phone, note, created_at, device_guid) VALUES('$custGuid', '$storeGuid', 'Rustam', '+998901234567', '', 100, '${UUID.randomUUID()}')")

            // Payment only on day: take a 100k payment
            val payGuid = UUID.randomUUID().toString()
            sql.execSQL("""
                INSERT INTO debt_events(guid, request_guid, schema_version, kind, customer_guid, store_guid, actor_guid, device_guid, device_sequence, occurred_at, payload, payload_hash, cash_minor, card_minor, fee_minor)
                VALUES('$payGuid', '$payGuid', 1, 'payment', '$custGuid', '$storeGuid', '${UUID.randomUUID()}', '${UUID.randomUUID()}', 1, 200, 'payload', 'hash', 10000000, 0, 0)
            """.trimIndent())

            // Unsent local event
            sql.execSQL("INSERT INTO sync_journal(op_id, kind, entity_guid, group_id, acked) VALUES('op-$payGuid', 'debt_payment', '$payGuid', '$payGuid', -1)")

            // Missing-dependency inbox entry
            val inboxGuid = UUID.randomUUID().toString()
            sql.execSQL("INSERT INTO debt_sync_inbox(packet_guid, store_guid, payload, received_at, error) VALUES('$inboxGuid', '$storeGuid', 'test-pending', 300, 'Missing dependency')")

            // Backup database
            DatabaseBackupExporter.copySnapshot(sql, backupFile)
            assertTrue(backupFile.exists())

            // Verify the backup can be read and holds all debt & sync state
            SQLiteDatabase.openDatabase(backupFile.path, null, SQLiteDatabase.OPEN_READONLY).use { copy ->
                copy.rawQuery("SELECT name FROM debt_customers WHERE guid='$custGuid'", null).use {
                    assertTrue(it.moveToFirst())
                    assertEquals("Rustam", it.getString(0))
                }
                copy.rawQuery("SELECT cash_minor FROM debt_events WHERE guid='$payGuid'", null).use {
                    assertTrue(it.moveToFirst())
                    assertEquals(10000000L, it.getLong(0))
                }
                copy.rawQuery("SELECT acked FROM sync_journal WHERE group_id='$payGuid'", null).use {
                    assertTrue(it.moveToFirst())
                    assertEquals(-1, it.getInt(0))
                }
                copy.rawQuery("SELECT error FROM debt_sync_inbox WHERE packet_guid='$inboxGuid'", null).use {
                    assertTrue(it.moveToFirst())
                    assertEquals("Missing dependency", it.getString(0))
                }
                copy.rawQuery("PRAGMA foreign_key_check", null).use {
                    assertFalse(it.moveToFirst())
                }
                copy.rawQuery("PRAGMA integrity_check", null).use {
                    assertTrue(it.moveToFirst())
                    assertEquals("ok", it.getString(0))
                }
            }
        } finally {
            db.close()
            scope.cancel()
            SQLiteDatabase.deleteDatabase(context.getDatabasePath(name))
            SQLiteDatabase.deleteDatabase(backupFile)
        }
    }
}
