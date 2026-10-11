package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

/** Mutable contact edits have their own durable queue; financial envelopes stay immutable. */
internal object DebtCustomerEdits {
    const val CAPABILITY = "debt_customer_edit_v1"
    const val PREFIX = "debt_customer_edits:"
    const val CONFLICT_PREFIX = "debt_customer_edit_conflict:"
    fun archived(row: JSONObject): Boolean = when (val value = row.get("archived")) {
        is Boolean -> value
        is Number -> value.toInt() == 1
        else -> value.toString() == "1" || value.toString().equals("true", true)
    }
    fun read(db: SupportSQLiteDatabase, guid: String): JSONObject = db.query(
        "SELECT name,phone,note,archived FROM debt_customers WHERE guid=?", arrayOf(guid)
    ).use { c ->
        check(c.moveToFirst()) { "Mijoz topilmadi" }
        JSONObject().put("name",c.getString(0)).put("phone",c.getString(1))
            .put("note",c.getString(2)).put("archived",c.getInt(3) != 0)
    }
    fun pending(db: SupportSQLiteDatabase, guid: String): JSONArray =
        JSONArray(DebtRepository.scalar(db,"SELECT value FROM sync_meta WHERE key=?",PREFIX+guid) as? String ?: "[]")

    // Called inside the same repository transaction as the local edit.
    fun enqueue(db: SupportSQLiteDatabase, store: String, guid: String, before: JSONObject,
                name: String, phone: String, note: String, archived: Boolean) {
        val shadow = DebtRepository.scalar(db,"SELECT value FROM sync_meta WHERE key=?",CONFLICT_PREFIX+guid) as? String
        // Saving again after an observed conflict explicitly chooses the phone's current values.
        val base = if (shadow == null) before else JSONObject(shadow)
        val queue = if (shadow == null) pending(db,guid) else JSONArray()
        val command = JSONObject().put("RequestGuid",UUID.randomUUID().toString())
            .put("StoreGuid",store).put("CustomerGuid",guid)
            .put("BeforeName",base.getString("name")).put("BeforePhone",base.getString("phone"))
            .put("BeforeNote",base.getString("note")).put("BeforeArchived",DebtCustomerEdits.archived(base))
            .put("Name",name).put("Phone",phone).put("Note",note).put("Archived",archived)
        queue.put(command)
        val body = queue.toString()
        require(body.toByteArray(Charsets.UTF_8).size <= 256*1024) { "Mijoz tahrirlari navbati to'ldi. Avval kompyuterga sinxronlang." }
        db.execSQL("INSERT OR REPLACE INTO sync_meta(key,value) VALUES(?,?)",arrayOf(PREFIX+guid,body))
        db.execSQL("DELETE FROM sync_meta WHERE key=?",arrayOf(CONFLICT_PREFIX+guid))
    }
}
