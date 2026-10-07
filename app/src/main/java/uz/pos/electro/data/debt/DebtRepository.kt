package uz.pos.electro.data.debt

import androidx.room.withTransaction
import androidx.sqlite.db.SupportSQLiteDatabase
import uz.pos.electro.data.local.AppDatabase
import java.math.BigDecimal
import java.math.RoundingMode
import java.nio.CharBuffer
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.LocalDate
import java.util.Base64
import java.util.UUID

// Local commands, never receiver-side replay of another device's allocation decision.
data class DebtCustomerDraft(val guid: String, val name: String, val phone: String, val note: String, val createdAt: Long)
data class DebtSaleCommand(val requestGuid: String, val customerGuid: String, val saleGuid: String, val saleFingerprint: String,
    val totalMinor: Long, val cashMinor: Long, val cardMinor: Long, val occurredAt: Long, val dueDate: String? = null)
data class DebtPaymentCommand(val requestGuid: String, val customerGuid: String, val cashMinor: Long, val cardMinor: Long,
    val feeMinor: Long, val occurredAt: Long, val targetAccountGuid: String? = null, val feeUsdRate: String? = null)

class DebtRepository(private val database: AppDatabase, private val store: String, private val actor: String, private val canWrite: () -> Boolean) {
    // New writer epoch each instance: a copied/restored DB cannot reuse device sequence pairs.
    // Retry identity excludes this epoch; already-persisted events retain theirs.
    private val device = UUID.randomUUID().toString()
    init { id(store); id(actor) }
    companion object {
        private fun id(value: String) { require(UUID.fromString(value).toString() == value && value != "00000000-0000-0000-0000-000000000000") }
        private fun text(value: String, limit: Int, required: Boolean = false) { require(value.length <= limit && (!required || value.isNotBlank()) && value.none { it < ' ' }) }
        private fun utf8(value: String): ByteArray {
            val encoded=StandardCharsets.UTF_8.newEncoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).encode(CharBuffer.wrap(value))
            return ByteArray(encoded.remaining()).also { encoded.get(it) }
        }
        fun canonical(vararg fields: String): String = "[\"debt-command-v1\"," + fields.joinToString(",") { "\""+Base64.getEncoder().encodeToString(utf8(it))+"\"" } + "]"
        private fun hash(payload: String) = MessageDigest.getInstance("SHA-256").digest(utf8(payload)).joinToString("") { "%02x".format(it.toInt() and 255) }
        internal fun exec(db: SupportSQLiteDatabase, sql: String, vararg args: Any?) = db.execSQL(sql,args)
        internal fun rows(db: SupportSQLiteDatabase, sql: String, vararg args: Any?): List<List<Any?>> = db.query(sql,args).use { c ->
            buildList { while(c.moveToNext()) add((0 until c.columnCount).map { i -> when(c.getType(i)) {
                android.database.Cursor.FIELD_TYPE_NULL -> null
                android.database.Cursor.FIELD_TYPE_INTEGER -> c.getLong(i)
                android.database.Cursor.FIELD_TYPE_FLOAT -> c.getDouble(i)
                else -> c.getString(i)
            } }) }
        }
        internal fun scalar(db: SupportSQLiteDatabase, sql: String, vararg args: Any?) = rows(db,sql,*args).firstOrNull()?.firstOrNull()
        internal fun storedMinor(value: Any?): Long {
            val d=(value as Number).toDouble(); require(d.isFinite() && d>=0)
            return BigDecimal.valueOf(d).setScale(2,RoundingMode.HALF_UP).movePointRight(2).longValueExact()
        }
    }
    private suspend fun <T> write(action: (SupportSQLiteDatabase) -> T): T = database.withTransaction {
        check(canWrite()) { "Debt write permission required" }
        val db=database.openHelper.writableDatabase
        check(scalar(db,"PRAGMA foreign_keys")==1L)
        check(scalar(db,"SELECT version FROM debt_schema WHERE id=1")==1L)
        check(scalar(db,"SELECT applying FROM sync_control WHERE id=1")==0L)
        check(scalar(db,"SELECT current_group FROM sync_control WHERE id=1")=="")
        action(db)
    }
    private fun scope(db: SupportSQLiteDatabase) { require(scalar(db,"SELECT store_guid FROM debt_scope WHERE id=1")==store) }
    suspend fun bindStore() { write { db ->
        val existing=scalar(db,"SELECT store_guid FROM debt_scope WHERE id=1")
        if(existing==null)exec(db,"INSERT INTO debt_scope(id,store_guid) VALUES(1,@p0)",store) else require(existing==store)
    } }
    private fun customer(db: SupportSQLiteDatabase, guid: String, active: Boolean = true): String {
        val row=rows(db,"SELECT name,archived FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",guid,store).singleOrNull()
        require(row!=null && (!active || row[1]==0L)); return row[0] as String
    }
    private fun outbox(db: SupportSQLiteDatabase, request: String, kind: String, entity: String, payload: String) {
        // -1 is HELD, not acked; legacy transport reads only acked=0. Stage 3 releases a
        // whole debt group only after capability negotiation, never just its sale/stock.
        exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,@p1,@p2,@p3,@p4,-1)","debt:$request",kind,entity,payload,request)
    }
    suspend fun createCustomer(d: DebtCustomerDraft): String {
        id(d.guid);text(d.name,256,true);text(d.phone,64);text(d.note,2048);require(d.createdAt>=0)
        val payload=canonical("customer",store,actor,d.guid,d.name,d.phone,d.note,d.createdAt.toString())
        return write { db ->
            scope(db);val key="debt_customer_create:${d.guid}"
            val previous=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0",key)
            if(previous!=null) { require(previous==payload);customer(db,d.guid,false);d.guid }
            else {
                require(scalar(db,"SELECT 1 FROM debt_events WHERE request_guid=@p0",d.guid)==null)
                exec(db,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",d.guid,store,d.name,d.phone,d.note,d.createdAt,device)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,payload)
                outbox(db,d.guid,"debt_customer",d.guid,payload);d.guid
            }
        }
    }
    private fun replay(db: SupportSQLiteDatabase, request: String, payload: String): String? {
        require(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:$request")==null)
        val row=rows(db,"SELECT r.payload_hash,r.result,e.payload FROM debt_command_receipts r JOIN debt_events e ON e.guid=r.event_guid WHERE r.request_guid=@p0",request).singleOrNull() ?: return null
        require(row[0]==hash(payload) && row[2]==payload && row[1]==request);return row[1] as String
    }
    private fun event(db: SupportSQLiteDatabase, request: String, customer: String, kind: String, at: Long, payload: String, cash: Long, card: Long, fee: Long, rate: String?) {
        val seq=Math.addExact(scalar(db,"SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0",device) as Long,1L)
        exec(db,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p0,1,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",request,kind,customer,store,actor,device,seq,at,payload,hash(payload),cash,card,fee,rate)
    }
    private fun finish(db: SupportSQLiteDatabase, request: String, payload: String) {
        exec(db,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p0,@p0)",request,hash(payload))
        outbox(db,request,"debt_event",request,payload)
    }
    private fun accounts(db: SupportSQLiteDatabase, customer: String): List<DebtAccount> = rows(db,"SELECT a.guid,a.sale_guid,a.original_debt_minor,s.created_at FROM debt_accounts a JOIN sales s ON s.guid=a.sale_guid WHERE a.customer_guid=@p0 AND a.store_guid=@p1",customer,store).map { row ->
        val guid=row[0] as String
        val deltas=rows(db,"SELECT debt_delta_minor FROM debt_event_lines WHERE account_guid=@p0",guid).map { it[0] as Long }
        DebtAccount(guid,row[1] as String,customer,row[3] as Long,DebtAccounting.balance(row[2] as Long,deltas))
    }
    suspend fun readAccounts(customerGuid: String): List<DebtAccount> {
        id(customerGuid);return write { db -> scope(db);customer(db,customerGuid,false);accounts(db,customerGuid) }
    }
    suspend fun openSale(q: DebtSaleCommand, writeSale: (SupportSQLiteDatabase) -> Unit): String {
        id(q.requestGuid);id(q.customerGuid);id(q.saleGuid);require(q.occurredAt>=0)
        require(q.saleFingerprint.matches(Regex("[0-9a-f]{64}")))
        if(q.dueDate!=null)require(LocalDate.parse(q.dueDate).year in 1..9999 && LocalDate.parse(q.dueDate).toString()==q.dueDate && q.dueDate.matches(Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}")))
        val debt=DebtAccounting.newDebt(q.totalMinor,q.cashMinor,q.cardMinor);require(debt>0)
        val payload=canonical("sale_open",store,actor,q.requestGuid,q.customerGuid,q.saleGuid,q.saleFingerprint,q.totalMinor.toString(),q.cashMinor.toString(),q.cardMinor.toString(),q.occurredAt.toString(),q.dueDate ?: "")
        return write { db ->
            scope(db);val old=replay(db,q.requestGuid,payload)
            if(old!=null)old else {
                val name=customer(db,q.customerGuid)
                require(scalar(db,"SELECT 1 FROM sales WHERE guid=@p0",q.saleGuid)==null)
                require(scalar(db,"SELECT current_group FROM sync_control WHERE id=1")=="")
                exec(db,"UPDATE sync_control SET current_group=@p0 WHERE id=1",q.requestGuid)
                writeSale(db) // Trusted cashier adapter MUST use this connection for sale/items/stock.
                val sale=rows(db,"SELECT total_amount,cash_amount,card_amount,created_at FROM sales WHERE guid=@p0",q.saleGuid).single()
                require(storedMinor(sale[0])==q.totalMinor && storedMinor(sale[1])==q.cashMinor && storedMinor(sale[2])==q.cardMinor && sale[3]==q.occurredAt)
                event(db,q.requestGuid,q.customerGuid,"sale_open",q.occurredAt,payload,0,0,0,null)
                exec(db,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p0,@p1,@p2,@p3,@p4,@p5,@p6)",q.saleGuid,q.customerGuid,store,q.requestGuid,debt,q.dueDate,name)
                finish(db,q.requestGuid,payload)
                exec(db,"UPDATE sync_journal SET acked=-1 WHERE group_id=@p0",q.requestGuid)
                exec(db,"UPDATE sync_control SET current_group='' WHERE id=1");q.requestGuid
            }
        }
    }
    suspend fun takePayment(q: DebtPaymentCommand): String {
        id(q.requestGuid);id(q.customerGuid);q.targetAccountGuid?.let { id(it) };require(q.occurredAt>=0)
        require(q.cashMinor>=0 && q.cardMinor>=0 && q.feeMinor>=0 && q.feeMinor<=q.cardMinor)
        val total=Math.addExact(q.cashMinor,q.cardMinor);require(total>0)
        if(q.feeUsdRate!=null)require(q.feeUsdRate.matches(Regex("[0-9]{1,12}(?:\\.[0-9]{1,8})?")) && q.feeUsdRate.toBigDecimal()>BigDecimal.ZERO)
        val payload=canonical("payment",store,actor,q.requestGuid,q.customerGuid,q.cashMinor.toString(),q.cardMinor.toString(),q.feeMinor.toString(),q.occurredAt.toString(),q.targetAccountGuid ?: "",q.feeUsdRate ?: "")
        return write { db ->
            scope(db);val old=replay(db,q.requestGuid,payload)
            if(old!=null)old else {
                customer(db,q.customerGuid)
                val allocation=DebtAccounting.allocate(total,q.customerGuid,accounts(db,q.customerGuid),q.targetAccountGuid)
                val effect=DebtAccounting.payment(q.cashMinor,q.cardMinor,q.feeMinor,allocation)
                event(db,q.requestGuid,q.customerGuid,"payment",q.occurredAt,payload,q.cashMinor,q.cardMinor,q.feeMinor,q.feeUsdRate)
                effect.lines.forEachIndexed { i,line -> exec(db,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",q.requestGuid,i,line.accountGuid,q.customerGuid,store,line.deltaMinor) }
                finish(db,q.requestGuid,payload);q.requestGuid
            }
        }
    }
}
