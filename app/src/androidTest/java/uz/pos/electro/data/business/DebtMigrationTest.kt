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
            sql.query("PRAGMA foreign_keys").use { it.moveToFirst();assertEquals(1,it.getInt(0)) }
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
}
