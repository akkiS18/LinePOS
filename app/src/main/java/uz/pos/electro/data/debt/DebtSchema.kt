package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase

/** Auxiliary tables like return metadata; installed/validated outside Room entities.
 * No business writes or sync capture: repositories arrive in stage 2B-2. */
object DebtSchema {
    val tables = listOf("debt_schema", "debt_scope", "debt_customers", "debt_events", "debt_accounts", "debt_event_lines", "debt_command_receipts", "debt_sync_inbox")

    fun install(db: SupportSQLiteDatabase, schema: String) {
        db.query("PRAGMA foreign_keys").use { check(it.moveToFirst() && it.getInt(0) == 1) { "Debt ledger requires foreign keys" } }
        db.beginTransaction()
        try {
            val existing = db.query("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name GLOB 'debt_*'").use { it.moveToFirst(); it.getInt(0) }
            if (existing != 0) {
                check(existing == tables.size) { "Incomplete or unsupported debt schema" }
                db.query("SELECT version FROM debt_schema WHERE id=1").use {
                    check(it.moveToFirst() && it.getInt(0) == 1) { "Unsupported debt schema version" }
                }
            }
            schema.split("-- statement").filter { it.isNotBlank() }.forEach { statement ->
                db.execSQL(statement)
                val definition = Regex("CREATE (?:UNIQUE )?(?:TABLE|INDEX|TRIGGER) IF NOT EXISTS ([a-z_]+)").find(statement)
                if (definition != null) {
                    val name = definition.groupValues[1]
                    val actual = db.query("SELECT sql FROM sqlite_master WHERE name=?", arrayOf(name)).use {
                        check(it.moveToFirst()); it.getString(0)
                    }
                    fun normalize(sql: String) = sql.replace("IF NOT EXISTS ", "").replace(Regex("\\s+"), " ").trim().trimEnd(';')
                    check(normalize(actual) == normalize(statement.substring(definition.range.first))) { "Debt schema definition mismatch: $name" }
                }
            }
            tables.forEach { table ->
                db.query("PRAGMA foreign_key_check('$table')").use { check(!it.moveToFirst()) { "Debt foreign key integrity failed: $table" } }
            }
            db.query("SELECT version FROM debt_schema WHERE id=1").use { check(it.moveToFirst() && it.getInt(0) == 1) }
            db.setTransactionSuccessful()
        } finally { db.endTransaction() }
    }
}
