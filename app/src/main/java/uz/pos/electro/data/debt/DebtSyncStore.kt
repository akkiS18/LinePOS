package uz.pos.electro.data.debt

import androidx.room.withTransaction
import androidx.sqlite.db.SupportSQLiteDatabase
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.debt.DebtRepository.Companion.exec
import uz.pos.electro.data.debt.DebtRepository.Companion.rows
import uz.pos.electro.data.debt.DebtRepository.Companion.scalar
import uz.pos.electro.data.debt.DebtRepository.Companion.storedMinor

class DebtDependencyException(message: String): IllegalStateException(message)

// Transactional COMPONENT bridge, not a complete network envelope/inbox/cursor/ACK.
// The future trusted adapter must validate and persist its sale/items/stock body too.
class DebtSyncStore(private val database: AppDatabase, private val store: String,
    private val canSync: () -> Boolean, private val canImportActor: (String) -> Boolean) {
    init { DebtWire.requirePeer(store,store,listOf(DebtWire.CAPABILITY)) }
    private fun need(ok: Boolean) { check(ok) { "Debt integrity conflict" } }
    private suspend fun <T> write(action: (SupportSQLiteDatabase) -> T): T = database.withTransaction {
        check(canSync()) { "Debt sync permission required" }
        val db=database.openHelper.writableDatabase
        need(scalar(db,"PRAGMA foreign_keys")==1L && scalar(db,"SELECT version FROM debt_schema WHERE id=1")==1L)
        need(scalar(db,"SELECT store_guid FROM debt_scope WHERE id=1")==store)
        need(scalar(db,"SELECT applying FROM sync_control WHERE id=1")==0L)
        need(scalar(db,"SELECT current_group FROM sync_control WHERE id=1")=="")
        action(db)
    }
    private fun authorize(actor: String) { check(canImportActor(actor)) { "Debt source actor rejected" } }
    private fun sealKey(kind: String,guid: String)="debt_wire_v1:$kind:$guid"
    private fun seal(db: SupportSQLiteDatabase,kind: String,guid: String,wire: String) {
        val key=sealKey(kind,guid);val value=DebtWire.fingerprint(wire)+"\n"+wire
        val old=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0",key)
        if(old==null)exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,value) else need(old==value)
    }
    private fun journal(db: SupportSQLiteDatabase,id: String,kind: String,payload: String) {
        exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,@p1,@p2,@p3,@p2,-1)","debt:$id",kind,id,payload)
    }
    private fun customerWire(db: SupportSQLiteDatabase,guid: String): String {
        val row=rows(db,"SELECT store_guid,device_guid FROM debt_customers WHERE guid=@p0",guid).singleOrNull()
            ?: throw DebtDependencyException("Debt customer missing")
        need(row[0]==store)
        val payload=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0","debt_customer_create:$guid") as? String
            ?: error("Missing customer creation snapshot")
        return DebtWire.encodeCustomer(DebtWireCustomer(guid,store,row[1] as String,payload,DebtWire.fingerprint(payload)),store)
    }
    private fun eventWire(db: SupportSQLiteDatabase,guid: String): String {
        val h=rows(db,"SELECT request_guid,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,schema_version,reference_guid FROM debt_events WHERE guid=@p0",guid).singleOrNull()
            ?: throw DebtDependencyException("Debt event missing")
        need(h[14]==1L && h[15]==null && h[3]==store)
        val receipt=rows(db,"SELECT payload_hash,event_guid,result FROM debt_command_receipts WHERE request_guid=@p0",h[0]).singleOrNull()
        need(receipt!=null && receipt[0]==h[9] && receipt[1]==guid && receipt[2]==guid)
        val a=rows(db,"SELECT guid,sale_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale,customer_guid,store_guid FROM debt_accounts WHERE opening_event_guid=@p0",guid).singleOrNull()
        val account=if(a==null)null else {
            need(a[6]==h[2] && a[7]==store)
            DebtWireAccount(a[0] as String,a[1] as String,a[2] as String,a[3] as Long,a[4] as String?,a[5] as String)
        }
        val lines=rows(db,"SELECT line_index,account_guid,debt_delta_minor,customer_guid,store_guid FROM debt_event_lines WHERE event_guid=@p0 ORDER BY line_index",guid).mapIndexed { i,row ->
            need(row[0]==i.toLong() && row[3]==h[2] && row[4]==store);DebtLine(row[1] as String,row[2] as Long)
        }
        return DebtWire.encodeEvent(DebtWireEvent(guid,h[0] as String,h[1] as String,h[2] as String,store,h[4] as String,h[5] as String,h[6] as Long,h[7] as Long,h[8] as String,h[9] as String,h[10] as Long,h[11] as Long,h[12] as Long,h[13] as String?,account,lines),store)
    }
    suspend fun exportCustomer(guid: String): String = write { db -> customerWire(db,guid).also { seal(db,"customer",guid,it) } }
    suspend fun exportEvent(guid: String): String = write { db -> eventWire(db,guid).also { seal(db,"event",guid,it) } }
    private fun customer(db: SupportSQLiteDatabase,c: DebtWireCustomer,wire: String) {
        val p=DebtWire.commandFields(c.payload);authorize(p[2])
        need(scalar(db,"SELECT 1 FROM debt_events WHERE request_guid=@p0",c.guid)==null)
        if(scalar(db,"SELECT 1 FROM debt_customers WHERE guid=@p0",c.guid)!=null)need(customerWire(db,c.guid)==wire)
        else {
            need(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0 OR key=@p1","debt_customer_create:${c.guid}",sealKey("customer",c.guid))==null)
            exec(db,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",c.guid,store,p[4],p[5],p[6],p[7].toLong(),c.deviceGuid)
            exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_customer_create:${c.guid}",c.payload)
            journal(db,c.guid,"debt_customer",c.payload)
        }
        seal(db,"customer",c.guid,wire)
    }
    private fun event(db: SupportSQLiteDatabase,e: DebtWireEvent,wire: String,writeSale: ((SupportSQLiteDatabase,DebtWireEvent)->Unit)?) {
        authorize(e.actorGuid)
        need(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:${e.requestGuid}")==null)
        need(scalar(db,"SELECT 1 FROM debt_customers WHERE guid=@p0",e.requestGuid)==null)
        if(scalar(db,"SELECT 1 FROM debt_events WHERE guid=@p0",e.guid)!=null) {
            need(eventWire(db,e.guid)==wire);seal(db,"event",e.guid,wire);return
        }
        need(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0",sealKey("event",e.guid))==null)
        val customerStore=scalar(db,"SELECT store_guid FROM debt_customers WHERE guid=@p0",e.customerGuid)
            ?: throw DebtDependencyException("Debt customer missing")
        need(customerStore==store) // Archive does not invalidate a previously accepted offline event.
        for(line in e.lines) {
            val owner=rows(db,"SELECT customer_guid,store_guid FROM debt_accounts WHERE guid=@p0",line.accountGuid).singleOrNull()
                ?: throw DebtDependencyException("Debt account missing")
            need(owner[0]==e.customerGuid && owner[1]==store)
        }
        if(e.account!=null) {
            need(scalar(db,"SELECT 1 FROM sales WHERE guid=@p0",e.account.saleGuid)==null)
            if(writeSale==null)throw DebtDependencyException("Atomic sale adapter missing")
            writeSale(db,e) // Trusted adapter MUST use this transaction for sale/items/stock.
            val sale=rows(db,"SELECT total_amount,cash_amount,card_amount,created_at FROM sales WHERE guid=@p0",e.account.saleGuid).single()
            val p=DebtWire.commandFields(e.payload)
            need(storedMinor(sale[0])==p[7].toLong() && storedMinor(sale[1])==p[8].toLong() && storedMinor(sale[2])==p[9].toLong() && sale[3]==e.occurredAt)
        }
        exec(db,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p1,1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14)",e.guid,e.requestGuid,e.kind,e.customerGuid,store,e.actorGuid,e.deviceGuid,e.deviceSequence,e.occurredAt,e.payload,e.payloadHash,e.cashMinor,e.cardMinor,e.feeMinor,e.feeUsdRate)
        e.account?.let { a -> exec(db,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",a.guid,a.saleGuid,e.customerGuid,store,a.openingEventGuid,a.originalDebtMinor,a.dueDate,a.customerNameAtSale) }
        e.lines.forEachIndexed { i,line -> exec(db,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",e.guid,i,line.accountGuid,e.customerGuid,store,line.deltaMinor) }
        exec(db,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p2,@p2)",e.requestGuid,e.payloadHash,e.guid)
        journal(db,e.guid,"debt_event",e.payload);seal(db,"event",e.guid,wire)
    }
    suspend fun apply(customerPackets: List<String>,eventPackets: List<String>,writeSale: ((SupportSQLiteDatabase,DebtWireEvent)->Unit)?=null) {
        need(customerPackets.size.toLong()+eventPackets.size<=500)
        val customers=customerPackets.toList();val events=eventPackets.toList()
        need((customers+events).sumOf { it.length.toLong() }<=8*1024*1024)
        val cs=customers.map { it to DebtWire.decodeCustomer(it,store) }
        val es=events.map { it to DebtWire.decodeEvent(it,store) }.sortedBy { if(it.second.kind=="sale_open")0 else 1 }
        write { db ->
            exec(db,"UPDATE sync_control SET applying=1 WHERE id=1")
            cs.forEach { customer(db,it.second,it.first) }
            es.forEach { event(db,it.second,it.first,writeSale) }
            exec(db,"UPDATE sync_control SET applying=0 WHERE id=1")
        }
    }
}
