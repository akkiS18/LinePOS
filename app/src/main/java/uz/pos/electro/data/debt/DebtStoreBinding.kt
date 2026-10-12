package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase
import org.json.JSONArray
import uz.pos.electro.data.debt.DebtRepository.Companion.exec
import uz.pos.electro.data.debt.DebtRepository.Companion.scalar
import uz.pos.electro.data.debt.DebtRepository.Companion.rows

/** One-time registration of an unpublished phone ledger; never re-label published history. */
internal object DebtStoreBinding {
    fun bind(db: SupportSQLiteDatabase, newStore: String) {
        DebtWire.id(newStore)
        db.beginTransaction()
        try {
            val old=scalar(db,"SELECT store_guid FROM debt_scope WHERE id=1") as? String
            if(old!=null && old!=newStore) {
                require(scalar(db,"SELECT 1 FROM debt_sync_inbox LIMIT 1")==null) { "Qarz paketlari kutilmoqda; do‘kon identifikatorini almashtirib bo‘lmaydi" }
                val hasData=scalar(db,"SELECT 1 FROM debt_customers LIMIT 1")!=null
                if(hasData) {
                    val actor=scalar(db,"SELECT value FROM sync_meta WHERE key='mobile_actor_guid'") as? String
                    require(actor!=null && scalar(db,"SELECT 1 FROM sync_meta WHERE key IN ('server_id','debt_bound_store')")==null &&
                        scalar(db,"SELECT 1 FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1 LIMIT 1")==null &&
                        scalar(db,"SELECT 1 FROM debt_sync_inbox LIMIT 1")==null &&
                        scalar(db,"SELECT 1 FROM debt_events WHERE actor_guid<>? LIMIT 1",actor)==null) {
                        "Qarz tarixi boshqa do‘kon identifikatorida. Ma’lumotlar saqlandi; mos baza zaxirasini tiklash kerak."
                    }
                    for(r in rows(db,"SELECT value FROM sync_meta WHERE key GLOB 'debt_customer_create:*'"))
                        require(DebtWire.commandFields(r[0] as String)[2]==actor) { "Import qilingan mijoz tarixini qayta bog‘lab bo‘lmaydi" }
                }
                // Defer FK checks, keep FK enforcement enabled, restore every trigger in
                // this transaction. Only a never-published ledger may change its namespace.
                exec(db,"PRAGMA defer_foreign_keys=ON")
                val triggers=rows(db,"SELECT name,sql FROM sqlite_master WHERE type='trigger' AND name IN ('debt_scope_no_update','debt_events_no_update','debt_accounts_no_update','debt_lines_no_update','debt_receipts_no_update')")
                require(triggers.size==5) { "Qarz sxemasi kutilgan holatda emas" }
                for(t in triggers)exec(db,"DROP TRIGGER "+t[0])
                exec(db,"UPDATE debt_scope SET store_guid=? WHERE id=1",newStore)
                for(table in listOf("debt_customers","debt_events","debt_accounts","debt_event_lines"))
                    exec(db,"UPDATE $table SET store_guid=? WHERE store_guid=?",newStore,old)
                fun command(payload: String): String {
                    val fields=DebtWire.commandFields(payload).toMutableList()
                    require(fields[1]==old);fields[1]=newStore
                    return DebtRepository.canonical(*fields.toTypedArray())
                }
                fun customer(wire: String): String {
                    val c=DebtWire.decodeCustomer(wire,old);val body=command(c.payload)
                    return DebtWire.encodeCustomer(c.copy(storeGuid=newStore,payload=body,payloadHash=DebtWire.fingerprint(body)),newStore)
                }
                fun event(wire: String): String {
                    val e=DebtWire.decodeEvent(wire,old);val body=command(e.payload)
                    return DebtWire.encodeEvent(e.copy(storeGuid=newStore,payload=body,payloadHash=DebtWire.fingerprint(body)),newStore)
                }
                for(r in rows(db,"SELECT guid,payload FROM debt_events")) {
                    val body=command(r[1] as String)
                    exec(db,"UPDATE debt_events SET payload=?,payload_hash=? WHERE guid=?",body,DebtWire.fingerprint(body),r[0])
                }
                exec(db,"UPDATE debt_command_receipts SET payload_hash=(SELECT payload_hash FROM debt_events WHERE guid=event_guid)")
                // Read potentially large envelopes in bounded chunks (Android CursorWindow).
                val keys=rows(db,"SELECT key FROM sync_meta WHERE key GLOB 'debt_customer_create:*' OR key GLOB 'debt_wire_v1:*' OR key GLOB 'debt_envelope_v1:*' OR key GLOB 'debt_customer_edits:*'").map { it[0] as String }
                for(key in keys) {
                    val length=(scalar(db,"SELECT length(value) FROM sync_meta WHERE key=?",key) as Long).toInt()
                    require(length<=DebtEnvelope.MAX_ENVELOPE_CHARS+65)
                    val body=StringBuilder()
                    for(offset in 1..length step 65536)body.append(scalar(db,"SELECT substr(value,?,65536) FROM sync_meta WHERE key=?",offset,key) as String)
                    val value=body.toString()
                    val next=when {
                        key.startsWith("debt_customer_create:") -> command(value)
                        key.startsWith("debt_customer_edits:") -> JSONArray(value).also { a ->
                            for(i in 0 until a.length()) { val q=a.getJSONObject(i);require(q.getString("StoreGuid")==old);q.put("StoreGuid",newStore) }
                        }.toString()
                        else -> {
                            require(value.length>65 && value[64]=='\n' && value.take(64)==DebtWire.fingerprint(value.substring(65)))
                            val wire=value.substring(65)
                            val mapped=when {
                                key.startsWith("debt_wire_v1:customer:") -> customer(wire)
                                key.startsWith("debt_wire_v1:event:") -> event(wire)
                                else -> {
                                    val p=DebtEnvelope.decode(wire,old)
                                    DebtEnvelope.encode(p.copy(storeGuid=newStore,customerWire=if(p.customerWire.isEmpty())"" else customer(p.customerWire),eventWire=if(p.eventWire.isEmpty())"" else event(p.eventWire)),newStore)
                                }
                            }
                            DebtWire.fingerprint(mapped)+"\n"+mapped
                        }
                    }
                    exec(db,"UPDATE sync_meta SET value=? WHERE key=?",next,key)
                }
                exec(db,"UPDATE sync_journal SET payload=(SELECT payload FROM debt_events WHERE guid=entity_guid) WHERE kind='debt_event'")
                exec(db,"UPDATE sync_journal SET payload=(SELECT value FROM sync_meta WHERE key='debt_customer_create:'||entity_guid) WHERE kind='debt_customer'")
                for(t in triggers)exec(db,t[1] as String)
                db.query("PRAGMA foreign_key_check").use { require(!it.moveToFirst()) { "Qarz bog‘lanishlarini tekshirish xatosi" } }
            } else if(old==null) exec(db,"INSERT INTO debt_scope(id,store_guid) VALUES(1,?)",newStore)
            exec(db,"INSERT OR REPLACE INTO sync_meta(key,value) VALUES('debt_store_guid',?)",newStore)
            exec(db,"INSERT OR REPLACE INTO sync_meta(key,value) VALUES('debt_bound_store',?)",newStore)
            db.setTransactionSuccessful()
        } finally { db.endTransaction() }
    }
}
