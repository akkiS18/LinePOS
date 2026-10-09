package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.debt.DebtRepository.Companion.exec
import uz.pos.electro.data.debt.DebtRepository.Companion.rows
import uz.pos.electro.data.debt.DebtRepository.Companion.scalar

enum class DebtReceiveStatus { Applied, AlreadyApplied, WaitingForDependency, WaitingForSaleAdapter }
data class DebtPendingPacket(val wire: String,val receivedAt: Long,val reason: String)
class DebtInboxFullException: IllegalStateException("Debt inbox capacity exceeded; retain sender packet")

// Validated durable inbox. Openings opt in with a trusted actor/user resolver.
// No public sale callback, transport ACK/cursor, or HELD release is exposed here.
class DebtEnvelopeInbox(database: AppDatabase,private val store: String,
    canSync: ()->Boolean,canImportActor: (String)->Boolean,
    private val resolveActorUser: ((String)->Long?)?=null) {
    companion object { const val MAX_PENDING_PACKETS=128;const val MAX_PENDING_CHARS=32L*1024*1024 }
    private val bridge=DebtSyncStore(database,store,canSync,canImportActor)
    private fun need(ok: Boolean) { check(ok) { "Debt envelope integrity conflict" } }
    private fun key(id: String)="debt_envelope_v1:$id"
    private fun seal(wire: String)=DebtWire.fingerprint(wire)+"\n"+wire
    // Keep individual cursor rows small even when a canonical envelope is several MiB.
    // Identifiers below are internal constants, never wire-provided SQL names.
    private fun body(db: SupportSQLiteDatabase,table: String,column: String,idColumn: String,id: String,max: Int): String? {
        val length=scalar(db,"SELECT length($column) FROM $table WHERE $idColumn=@p0",id) as? Long ?: return null
        need(length in 1L..max.toLong())
        val result=StringBuilder(length.toInt())
        var offset=1L
        while(offset<=length) {
            result.append(scalar(db,"SELECT substr($column,@p0,65536) FROM $table WHERE $idColumn=@p1",offset,id) as String)
            offset+=65536
        }
        return result.toString()
    }
    private fun authorize(p: DebtEnvelopePacket) {
        if(p.customerWire.isNotEmpty())bridge.authorize(DebtWire.commandFields(DebtWire.decodeCustomer(p.customerWire,store).payload)[2])
        if(p.eventWire.isNotEmpty())bridge.authorize(DebtWire.decodeEvent(p.eventWire,store).actorGuid)
    }
    private fun verifyApplied(db: SupportSQLiteDatabase,p: DebtEnvelopePacket) {
        if(p.saleWire.isNotEmpty())DebtSaleReceiver.verify(db,p.saleWire,p.guid)
        if(p.customerWire.isNotEmpty())need(bridge.customerWire(db,DebtWire.decodeCustomer(p.customerWire,store).guid)==p.customerWire)
        if(p.eventWire.isNotEmpty())need(bridge.eventWire(db,p.guid)==p.eventWire)
    }
    suspend fun receive(wire: String,receivedAt: Long): DebtReceiveStatus {
        require(receivedAt>=0);val packet=DebtEnvelope.decode(wire,store)
        return bridge.write { db -> receive(db,packet,wire,receivedAt) }
    }
    private fun receive(db: SupportSQLiteDatabase,p: DebtEnvelopePacket,wire: String,receivedAt: Long): DebtReceiveStatus {
        authorize(p);val old=rows(db,"SELECT store_guid FROM debt_sync_inbox WHERE packet_guid=@p0",p.guid)
        if(old.isNotEmpty())need(old[0][0]==store && body(db,"debt_sync_inbox","payload","packet_guid",p.guid,DebtEnvelope.MAX_ENVELOPE_CHARS)==wire)
        val receipt=body(db,"sync_meta","value","key",key(p.guid),DebtEnvelope.MAX_ENVELOPE_CHARS+65)
        if(receipt!=null) { need(receipt==seal(wire) && old.isEmpty());verifyApplied(db,p);return DebtReceiveStatus.AlreadyApplied }
        // Same writer transaction for preflight and mutation; failures after mutation
        // propagate and roll back the entire operation, never become a partial pending row.
        var missing=false
        if(p.customerWire.isNotEmpty()) {
            val c=DebtWire.decodeCustomer(p.customerWire,store)
            need(scalar(db,"SELECT 1 FROM debt_events WHERE request_guid=@p0",c.guid)==null)
            if(scalar(db,"SELECT 1 FROM debt_customers WHERE guid=@p0",c.guid)!=null)need(bridge.customerWire(db,c.guid)==p.customerWire)
        }
        if(p.eventWire.isNotEmpty()) {
            val e=DebtWire.decodeEvent(p.eventWire,store)
            need(scalar(db,"SELECT 1 FROM debt_customers WHERE guid=@p0",e.guid)==null)
            val sequence=scalar(db,"SELECT guid FROM debt_events WHERE device_guid=@p0 AND device_sequence=@p1",e.deviceGuid,e.deviceSequence)
            need(sequence==null || sequence==e.guid)
            if(scalar(db,"SELECT 1 FROM debt_events WHERE guid=@p0",e.guid)!=null)need(bridge.eventWire(db,e.guid)==p.eventWire)
            val owner=scalar(db,"SELECT store_guid FROM debt_customers WHERE guid=@p0",e.customerGuid)
            if(owner==null)missing=p.customerWire.isEmpty() else need(owner==store)
            for(line in e.lines) {
                val account=rows(db,"SELECT customer_guid,store_guid FROM debt_accounts WHERE guid=@p0",line.accountGuid)
                if(account.isEmpty())missing=true else need(account[0][0]==e.customerGuid && account[0][1]==store)
            }
        }
        val gated=p.saleWire.isNotEmpty() && resolveActorUser==null
        val sale=if(p.saleWire.isNotEmpty() && !gated)
            DebtSaleReceiver.prepare(db,p.saleWire,DebtWire.decodeEvent(p.eventWire,store),resolveActorUser!!) else null
        if(p.saleWire.isNotEmpty() && !gated && sale==null)missing=true
        if(gated || missing) {
            val reason=if(gated)"sale_adapter_pending" else "missing_dependency"
            if(old.isEmpty()) {
                val count=scalar(db,"SELECT COUNT(*) FROM debt_sync_inbox") as Long
                val chars=scalar(db,"SELECT COALESCE(SUM(length(payload)),0) FROM debt_sync_inbox") as Long
                if(count>=MAX_PENDING_PACKETS || chars>MAX_PENDING_CHARS-wire.length)throw DebtInboxFullException()
                exec(db,"INSERT INTO debt_sync_inbox(packet_guid,store_guid,payload,received_at,error) VALUES(@p0,@p1,@p2,@p3,@p4)",p.guid,store,wire,receivedAt,reason)
            } else exec(db,"UPDATE debt_sync_inbox SET error=@p0 WHERE packet_guid=@p1",reason,p.guid)
            return if(gated)DebtReceiveStatus.WaitingForSaleAdapter else DebtReceiveStatus.WaitingForDependency
        }
        exec(db,"UPDATE sync_control SET applying=1 WHERE id=1")
        if(p.customerWire.isNotEmpty())bridge.customer(db,DebtWire.decodeCustomer(p.customerWire,store),p.customerWire)
        if(p.eventWire.isNotEmpty())bridge.event(db,DebtWire.decodeEvent(p.eventWire,store),p.eventWire,if(sale==null)null else { c,_ -> sale.apply(c) })
        exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key(p.guid),seal(wire))
        exec(db,"DELETE FROM debt_sync_inbox WHERE packet_guid=@p0",p.guid)
        exec(db,"UPDATE sync_control SET applying=0 WHERE id=1")
        return DebtReceiveStatus.Applied
    }

    suspend fun readPending(guid: String): DebtPendingPacket? {
        DebtWire.id(guid)
        return bridge.write { db ->
            val row=rows(db,"SELECT store_guid,received_at,error FROM debt_sync_inbox WHERE packet_guid=@p0",guid)
            if(row.isEmpty())null else {
                need(row[0][0]==store);val wire=body(db,"debt_sync_inbox","payload","packet_guid",guid,DebtEnvelope.MAX_ENVELOPE_CHARS) ?: error("Missing pending body")
                val p=DebtEnvelope.decode(wire,store);need(p.guid==guid);authorize(p)
                need(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0",key(guid))==null)
                DebtPendingPacket(wire,row[0][1] as Long,row[0][2] as String)
            }
        }
    }
    suspend fun exportApplied(guid: String): String? {
        DebtWire.id(guid)
        return bridge.write { db ->
            val value=body(db,"sync_meta","value","key",key(guid),DebtEnvelope.MAX_ENVELOPE_CHARS+65)
            if(value==null)null else {
                need(value.length>65 && value[64]=='\n');val wire=value.substring(65);need(value==seal(wire))
                val p=DebtEnvelope.decode(wire,store);need(p.guid==guid);authorize(p);verifyApplied(db,p)
                need(scalar(db,"SELECT 1 FROM debt_sync_inbox WHERE packet_guid=@p0",guid)==null);wire
            }
        }
    }
}
