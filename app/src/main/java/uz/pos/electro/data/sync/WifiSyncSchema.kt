package uz.pos.electro.data.sync

import androidx.sqlite.db.SupportSQLiteDatabase

/** SQLite triggers capture business writes atomically, including writes outside repositories. */
object WifiSyncSchema {
    fun install(db: SupportSQLiteDatabase, schema: String) {
        db.beginTransaction()
        try {
            schema.split("-- statement").forEach { db.execSQL(it) }
            val initialized = db.query("SELECT value FROM sync_meta WHERE key='initialized'").use { it.moveToFirst() }
            if (!initialized) {
                // Existing stock is a baseline, not a new incoming shipment. Never upload it over
                // an existing desktop product. Legacy unsynced receipts are reconciled by GUID.
                db.execSQL("INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision) SELECT lower(hex(randomblob(16))),'warehouse',guid,-1 FROM warehouses")
                db.execSQL("INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision) SELECT lower(hex(randomblob(16))),'product',guid,-1 FROM products")
                db.execSQL("INSERT INTO sync_journal(op_id,kind,entity_guid) SELECT 'legacy-' || guid,'legacy_sale',guid FROM sales WHERE is_synced=0")
                db.execSQL("INSERT INTO sync_meta(key,value) VALUES('initialized','1')")
            }
            db.setTransactionSuccessful()
        } finally { db.endTransaction() }
    }
}
