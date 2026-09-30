package uz.pos.electro.data.sync

import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.withTransaction
import androidx.sqlite.db.SupportSQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.model.UnitType

/** Run on an API 26 device and a current device to verify the actual Room/SQLite driver. */
class WifiSyncCaptureTest {
    private lateinit var database: AppDatabase
    @Before fun open() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        database = Room.inMemoryDatabaseBuilder(context, AppDatabase::class.java)
            .addCallback(object : RoomDatabase.Callback() {
                override fun onOpen(db: SupportSQLiteDatabase) {
                    WifiSyncSchema.install(db,context.assets.open("wifi-sync-schema.sql").bufferedReader().use { it.readText() })
                }
            }).build()
    }
    @After fun close() = database.close()
    private fun count(): Long = database.openHelper.writableDatabase.query("SELECT COUNT(*) FROM sync_journal").use { it.moveToFirst();it.getLong(0) }
    private suspend fun seed() {
        database.productDao().insertProduct(ProductEntity(guid="p",name="Кабель",costPrice=5.0,sellingPrice=10.0,stockQuantity=10.0,unitType=UnitType.DONA))
        database.productStockDao().upsertStock("p","w",10.0,1)
    }
    @Test fun stockReplacementCapturesDifferenceRatherThanWholeQuantity() = runBlocking {
        seed()
        val sql=database.openHelper.writableDatabase
        sql.execSQL("DELETE FROM sync_journal")
        database.productStockDao().upsertStock("p","w",8.0,2)
        sql.query("SELECT SUM(delta) FROM sync_journal WHERE kind='stock'").use { it.moveToFirst();assertEquals(-2.0,it.getDouble(0),0.0) }
    }
    @Test fun businessRollbackAlsoRollsBackOutbox() = runBlocking {
        seed();val before=count()
        try { database.withTransaction { database.productStockDao().deductStock("p","w",2.0,2);error("power loss") } } catch (_: IllegalStateException) { }
        assertEquals(before,count())
        assertEquals(10.0,database.productStockDao().getProductStockInWarehouse("p","w")!!,0.0)
    }
    @Test fun remoteApplicationIsNotCapturedAsLocalWrite() = runBlocking {
        seed();val before=count()
        database.withTransaction {
            val sql=database.openHelper.writableDatabase
            sql.execSQL("UPDATE sync_control SET applying=1 WHERE id=1")
            database.productStockDao().upsertStock("p","w",8.0,2)
            sql.execSQL("UPDATE sync_control SET applying=0 WHERE id=1")
        }
        assertEquals(before,count())
    }
    @Test fun transferSidesShareOneGroupAndRollbackTogether() = runBlocking {
        seed();val sql=database.openHelper.writableDatabase
        sql.execSQL("DELETE FROM sync_journal")
        database.withTransaction {
            sql.execSQL("UPDATE sync_control SET current_group='transfer-1' WHERE id=1")
            database.productStockDao().transferStock("p","w","w2",2.0,2)
            sql.execSQL("UPDATE sync_control SET current_group='' WHERE id=1")
        }
        sql.query("SELECT COUNT(DISTINCT group_id),SUM(delta) FROM sync_journal WHERE kind='stock'").use { it.moveToFirst();assertEquals(1,it.getInt(0));assertEquals(0.0,it.getDouble(1),0.0) }
    }
}
