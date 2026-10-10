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
data class DebtCustomerRecord(val guid: String, val storeGuid: String, val name: String, val phone: String, val note: String, val archived: Boolean, val revision: Long, val createdAt: Long)
data class DebtCustomerUpdate(val customerGuid: String, val name: String, val phone: String, val note: String, val revision: Long)
data class DebtSaleCommand(val requestGuid: String, val customerGuid: String, val saleGuid: String, val saleFingerprint: String,
    val totalMinor: Long, val cashMinor: Long, val cardMinor: Long, val occurredAt: Long, val dueDate: String? = null)
data class DebtPaymentCommand(val requestGuid: String, val customerGuid: String, val cashMinor: Long, val cardMinor: Long,
    val feeMinor: Long, val occurredAt: Long, val targetAccountGuid: String? = null, val feeUsdRate: String? = null)
data class DebtOpenSaleCommand(val requestGuid: String, val customerGuid: String, val sale: DebtSaleSnapshot,
    val dueDate: String? = null, val newCustomer: DebtCustomerDraft? = null, val userId: Long? = null)
data class DebtPaymentReversalCommand(val requestGuid: String, val paymentEventGuid: String, val reason: String,
    val refundedFeeMinor: Long, val occurredAt: Long)
data class DebtCreditRefundCommand(val requestGuid: String, val customerGuid: String, val accountGuid: String,
    val cashMinor: Long, val cardMinor: Long, val occurredAt: Long, val reason: String)
data class DebtCreditTransferCommand(val requestGuid: String, val customerGuid: String, val sourceAccountGuid: String,
    val targetAccountGuid: String, val amountMinor: Long, val occurredAt: Long, val reason: String)

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
            if(previous!=null) {
                require(previous==payload)
                customer(db,d.guid,false)
                val prevEnv=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0","debt_envelope_v1:${d.guid}") as? String
                if(prevEnv!=null) {
                    require(prevEnv.length>65 && prevEnv[64]=='\n')
                    val pw=prevEnv.substring(65)
                    require(prevEnv==DebtWire.fingerprint(pw)+"\n"+pw)
                }
                d.guid
            }
            else {
                require(scalar(db,"SELECT 1 FROM debt_events WHERE request_guid=@p0",d.guid)==null)
                exec(db,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",d.guid,store,d.name,d.phone,d.note,d.createdAt,device)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,payload)
                outbox(db,d.guid,"debt_customer",d.guid,payload)

                val cWireObj=DebtWireCustomer(d.guid,store,device,payload,DebtWire.fingerprint(payload))
                val customerWire=DebtWire.encodeCustomer(cWireObj,store)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:customer:${d.guid}",DebtWire.fingerprint(customerWire)+"\n"+customerWire)

                val envelope=DebtEnvelopePacket(d.guid,store,customerWire,"","")
                val envelopeWire=DebtEnvelope.encode(envelope,store)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:${d.guid}",DebtWire.fingerprint(envelopeWire)+"\n"+envelopeWire)

                d.guid
            }
        }
    }
    private fun replay(db: SupportSQLiteDatabase, request: String, payload: String): String? {
        require(scalar(db,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:$request")==null)
        val row=rows(db,"SELECT r.payload_hash,r.result,e.payload FROM debt_command_receipts r JOIN debt_events e ON e.guid=r.event_guid WHERE r.request_guid=@p0",request).singleOrNull() ?: return null
        require(row[0]==hash(payload) && row[2]==payload && row[1]==request)
        val env=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0","debt_envelope_v1:$request") as? String
        if(env!=null) {
            require(env.length>65 && env[64]=='\n')
            val wire=env.substring(65)
            require(env==DebtWire.fingerprint(wire)+"\n"+wire)
        }
        return row[1] as String
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
    suspend fun readCustomer(customerGuid: String): DebtCustomerRecord? {
        id(customerGuid);return write { db ->
            scope(db)
            val row = rows(db,"SELECT guid,store_guid,name,phone,note,archived,revision,created_at FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",customerGuid,store).singleOrNull()
                ?: return@write null
            DebtCustomerRecord(
                guid = row[0] as String,
                storeGuid = row[1] as String,
                name = row[2] as String,
                phone = row[3] as String,
                note = row[4] as String,
                archived = (row[5] as Long) != 0L,
                revision = row[6] as Long,
                createdAt = row[7] as Long
            )
        }
    }
    suspend fun updateCustomer(update: DebtCustomerUpdate): Boolean {
        id(update.customerGuid)
        text(update.name, 256, true)
        text(update.phone, 64)
        text(update.note, 2048)
        require(update.revision >= 0)
        return write { db ->
            scope(db)
            val existing = rows(db,"SELECT revision FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",update.customerGuid,store).singleOrNull()
                ?: throw IllegalArgumentException("Mijoz topilmadi")
            val currentRev = existing[0] as Long
            require(update.revision == currentRev) { "Customer revision conflict" }
            exec(db,"UPDATE debt_customers SET name=@p0, phone=@p1, note=@p2, revision=revision+1 WHERE guid=@p3 AND store_guid=@p4",
                update.name, update.phone, update.note, update.customerGuid, store)
            true
        }
    }
    suspend fun archiveCustomer(customerGuid: String, archive: Boolean = true): Boolean {
        id(customerGuid)
        return write { db ->
            scope(db)
            val existing = rows(db,"SELECT 1 FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",customerGuid,store).singleOrNull()
                ?: throw IllegalArgumentException("Mijoz topilmadi")
            exec(db,"UPDATE debt_customers SET archived=@p0, revision=revision+1 WHERE guid=@p1 AND store_guid=@p2",
                if (archive) 1L else 0L, customerGuid, store)
            true
        }
    }
    suspend fun openSale(cmd: DebtOpenSaleCommand): String {
        id(cmd.requestGuid);id(cmd.customerGuid);id(cmd.sale.guid);require(cmd.sale.occurredAt>=0)
        if(cmd.dueDate!=null)
            require(LocalDate.parse(cmd.dueDate).year in 1..9999 && LocalDate.parse(cmd.dueDate).toString()==cmd.dueDate && cmd.dueDate.matches(Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}")))
        if(cmd.newCustomer!=null) {
            require(cmd.newCustomer.guid==cmd.customerGuid)
            text(cmd.newCustomer.name,256,true);text(cmd.newCustomer.phone,64);text(cmd.newCustomer.note,2048);require(cmd.newCustomer.createdAt>=0)
        }
        val s=cmd.sale
        val saleWire=DebtEnvelope.encodeSale(s)
        val saleFingerprint=DebtWire.fingerprint(saleWire)
        val debt=DebtAccounting.newDebt(s.totalMinor,s.cashMinor,s.cardMinor);require(debt>0)
        val payload=canonical("sale_open",store,actor,cmd.requestGuid,cmd.customerGuid,s.guid,saleFingerprint,
            s.totalMinor.toString(),s.cashMinor.toString(),s.cardMinor.toString(),s.occurredAt.toString(),cmd.dueDate ?: "")

        DebtSaleReceiver.money(s.totalMinor);DebtSaleReceiver.money(s.costMinor);DebtSaleReceiver.money(s.cashMinor);DebtSaleReceiver.money(s.cardMinor);DebtSaleReceiver.money(s.feeMinor)
        DebtSaleReceiver.real(DebtSaleReceiver.d(s.feeRate));DebtSaleReceiver.real(DebtSaleReceiver.d(s.usdRate))
        for(i in s.items) {
            DebtSaleReceiver.real(DebtSaleReceiver.d(i.quantity));DebtSaleReceiver.real(DebtSaleReceiver.d(i.price));DebtSaleReceiver.real(DebtSaleReceiver.d(i.cost));DebtSaleReceiver.real(DebtSaleReceiver.d(i.stockDelta))
        }

        val user=cmd.userId ?: 1L;require(user>0)

        return write { db ->
            scope(db);val old=replay(db,cmd.requestGuid,payload);if(old!=null)return@write old
            require(scalar(db,"SELECT 1 FROM sales WHERE guid=@p0",s.guid)==null)
            require(scalar(db,"SELECT 1 FROM debt_events WHERE guid=@p0",cmd.requestGuid)==null)
            require(scalar(db,"SELECT 1 FROM sync_journal WHERE op_id=@p0",DebtSaleReceiver.marker(s.guid))==null)
            require(scalar(db,"SELECT current_group FROM sync_control WHERE id=1")=="")
            require(scalar(db,"SELECT 1 FROM users WHERE id=@p0",user)!=null)

            var customerWire=""
            val customerName=if(cmd.newCustomer!=null) {
                val cKey="debt_customer_create:${cmd.newCustomer.guid}"
                val cPayload=canonical("customer",store,actor,cmd.newCustomer.guid,cmd.newCustomer.name,cmd.newCustomer.phone,cmd.newCustomer.note,cmd.newCustomer.createdAt.toString())
                val prev=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0",cKey)
                val cName=if(prev!=null) {
                    require(prev==cPayload);customer(db,cmd.customerGuid,false)
                } else {
                    require(scalar(db,"SELECT 1 FROM debt_events WHERE request_guid=@p0",cmd.newCustomer.guid)==null)
                    exec(db,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                        cmd.newCustomer.guid,store,cmd.newCustomer.name,cmd.newCustomer.phone,cmd.newCustomer.note,cmd.newCustomer.createdAt,device)
                    exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",cKey,cPayload)
                    outbox(db,cmd.newCustomer.guid,"debt_customer",cmd.newCustomer.guid,cPayload)
                    cmd.newCustomer.name
                }
                val cWireObj=DebtWireCustomer(cmd.newCustomer.guid,store,device,cPayload,DebtWire.fingerprint(cPayload))
                customerWire=DebtWire.encodeCustomer(cWireObj,store)
                val cWireKey="debt_wire_v1:customer:${cmd.newCustomer.guid}"
                val cWireVal=DebtWire.fingerprint(customerWire)+"\n"+customerWire
                val prevWire=scalar(db,"SELECT value FROM sync_meta WHERE key=@p0",cWireKey)
                if(prevWire==null)exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",cWireKey,cWireVal)
                else require(prevWire==cWireVal)
                cName
            } else {
                customer(db,cmd.customerGuid)
            }

            val products=linkedMapOf<String,Long>()
            val stocks=linkedMapOf<Pair<String,String>,BigDecimal>()
            for(i in s.items) {
                require(scalar(db,"SELECT 1 FROM sale_items WHERE guid=@p0",i.guid)==null)
                require(scalar(db,"SELECT 1 FROM sync_journal WHERE op_id=@p0",i.stockOperationGuid)==null)
                val pId=scalar(db,"SELECT id FROM products WHERE guid=@p0",i.productGuid) as? Long
                require(pId!=null);products[i.productGuid]=pId
                require(scalar(db,"SELECT 1 FROM warehouses WHERE guid=@p0",i.warehouseGuid)!=null)
                val key=i.productGuid to i.warehouseGuid
                if(!stocks.containsKey(key)) {
                    val oldStock=scalar(db,"SELECT quantity FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.first,key.second)
                    stocks[key]=if(oldStock==null)BigDecimal.ZERO else DebtSaleReceiver.stored(oldStock)
                }
                stocks[key]=stocks.getValue(key)+DebtSaleReceiver.d(i.stockDelta)
                DebtSaleReceiver.real(stocks.getValue(key))
            }

            val totals=linkedMapOf<String,BigDecimal>()
            for(p in products.keys) {
                var total=BigDecimal.ZERO
                for(row in rows(db,"SELECT warehouse_guid,quantity FROM product_stocks WHERE product_guid=@p0",p)) {
                    if(!stocks.containsKey(p to (row[0] as String)))total+=DebtSaleReceiver.stored(row[1])
                }
                for((key,value) in stocks)if(key.first==p)total+=value
                DebtSaleReceiver.real(total);totals[p]=total
            }

            exec(db,"UPDATE sync_control SET current_group=@p0 WHERE id=1",cmd.requestGuid)

            exec(db,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES(@p0,@p1,@p2,'DEBT',@p3,@p4,@p5,@p6,@p7,@p8,@p9,0)",
                s.guid,DebtSaleReceiver.money(s.totalMinor),DebtSaleReceiver.money(s.costMinor),DebtSaleReceiver.money(s.cashMinor),
                DebtSaleReceiver.money(s.cardMinor),DebtSaleReceiver.money(s.feeMinor),DebtSaleReceiver.real(DebtSaleReceiver.d(s.feeRate)),
                DebtSaleReceiver.real(DebtSaleReceiver.d(s.usdRate)),s.occurredAt,user)
            val saleId=scalar(db,"SELECT id FROM sales WHERE guid=@p0",s.guid) as Long

            for(i in s.items) {
                exec(db,"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                    i.guid,saleId,s.guid,products.getValue(i.productGuid),i.productGuid,i.productName,i.category,i.unit,i.warehouseGuid,i.warehouseName,
                    DebtSaleReceiver.real(DebtSaleReceiver.d(i.quantity)),DebtSaleReceiver.real(DebtSaleReceiver.d(i.price)),
                    DebtSaleReceiver.real(DebtSaleReceiver.d(i.cost)),i.costCurrency)
                exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,payload,group_id,acked) VALUES(@p0,'debt_stock',@p1,@p2,@p3,@p4,@p5,-1)",
                    i.stockOperationGuid,i.productGuid,i.warehouseGuid,DebtSaleReceiver.real(DebtSaleReceiver.d(i.stockDelta)),i.guid,cmd.requestGuid)
            }

            for((key,value) in stocks) {
                val exists=scalar(db,"SELECT 1 FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.first,key.second)!=null
                if(exists)exec(db,"UPDATE product_stocks SET quantity=@p0, updated_at=MAX(updated_at,@p1) WHERE product_guid=@p2 AND warehouse_guid=@p3",
                    DebtSaleReceiver.real(value),s.occurredAt,key.first,key.second)
                else exec(db,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3)",
                    key.first,key.second,DebtSaleReceiver.real(value),s.occurredAt)
            }
            for((p,tot) in totals)exec(db,"UPDATE products SET stock_quantity=@p0 WHERE guid=@p1",DebtSaleReceiver.real(tot),p)

            exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,'debt_sale',@p1,@p2,@p3,-1)",
                DebtSaleReceiver.marker(s.guid),s.guid,DebtSaleReceiver.payload(saleWire,user),cmd.requestGuid)

            val seq=Math.addExact(scalar(db,"SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0",device) as Long,1L)
            exec(db,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p0,1,'sale_open',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,0,0,0,NULL)",
                cmd.requestGuid,cmd.customerGuid,store,actor,device,seq,s.occurredAt,payload,hash(payload))
            exec(db,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                s.guid,cmd.customerGuid,store,cmd.requestGuid,debt,cmd.dueDate,customerName)
            exec(db,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p0,@p0)",
                cmd.requestGuid,hash(payload))
            outbox(db,cmd.requestGuid,"debt_event",cmd.requestGuid,payload)

            exec(db,"UPDATE sync_journal SET acked=-1 WHERE group_id=@p0",cmd.requestGuid)
            exec(db,"UPDATE sync_control SET current_group='' WHERE id=1")

            val accObj=DebtWireAccount(s.guid,s.guid,cmd.requestGuid,debt,cmd.dueDate,customerName)
            val evObj=DebtWireEvent(cmd.requestGuid,cmd.requestGuid,"sale_open",cmd.customerGuid,store,actor,device,seq,s.occurredAt,
                payload,hash(payload),0,0,0,null,accObj,emptyList())
            val eventWire=DebtWire.encodeEvent(evObj,store)
            exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:event:${cmd.requestGuid}",DebtWire.fingerprint(eventWire)+"\n"+eventWire)

            val envelope=DebtEnvelopePacket(cmd.requestGuid,store,customerWire,eventWire,saleWire)
            val envelopeWire=DebtEnvelope.encode(envelope,store)
            exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:${cmd.requestGuid}",DebtWire.fingerprint(envelopeWire)+"\n"+envelopeWire)

            DebtSaleReceiver.verify(db,saleWire,cmd.requestGuid)

            cmd.requestGuid
        }
    }
    suspend fun openSale(sale: DebtSaleSnapshot, requestGuid: String, customerGuid: String, dueDate: String? = null, newCustomer: DebtCustomerDraft? = null, userId: Long? = null): String
        = openSale(DebtOpenSaleCommand(requestGuid,customerGuid,sale,dueDate,newCustomer,userId))
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
                val seq=Math.addExact(scalar(db,"SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0",device) as Long,1L)
                exec(db,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p0,1,'payment',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)",
                    q.requestGuid,q.customerGuid,store,actor,device,seq,q.occurredAt,payload,hash(payload),q.cashMinor,q.cardMinor,q.feeMinor,q.feeUsdRate)
                effect.lines.forEachIndexed { i,line -> exec(db,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",q.requestGuid,i,line.accountGuid,q.customerGuid,store,line.deltaMinor) }
                finish(db,q.requestGuid,payload)

                val lines=effect.lines.map { DebtLine(it.accountGuid,it.deltaMinor) }
                val evObj=DebtWireEvent(q.requestGuid,q.requestGuid,"payment",q.customerGuid,store,actor,device,seq,q.occurredAt,payload,hash(payload),q.cashMinor,q.cardMinor,q.feeMinor,q.feeUsdRate,null,lines)
                val eventWire=DebtWire.encodeEvent(evObj,store)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:event:${q.requestGuid}",DebtWire.fingerprint(eventWire)+"\n"+eventWire)

                val envelope=DebtEnvelopePacket(q.requestGuid,store,"",eventWire,"")
                val envelopeWire=DebtEnvelope.encode(envelope,store)
                exec(db,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:${q.requestGuid}",DebtWire.fingerprint(envelopeWire)+"\n"+envelopeWire)

                q.requestGuid
            }
        }
    }

    suspend fun reversePayment(q: DebtPaymentReversalCommand): String {
        id(q.requestGuid); id(q.paymentEventGuid); text(q.reason, 1000, true); require(q.occurredAt >= 0)
        require(q.refundedFeeMinor >= 0)
        val payload = canonical("payment_reversal", store, actor, q.requestGuid, q.paymentEventGuid, q.refundedFeeMinor.toString(), q.occurredAt.toString(), q.reason)
        return write { db ->
            scope(db); val old = replay(db, q.requestGuid, payload)
            if (old != null) old else {
                require(scalar(db, "SELECT 1 FROM debt_events WHERE kind='payment_reversal' AND reference_guid=@p0", q.paymentEventGuid) == null)
                val evRow = rows(db, "SELECT customer_guid, cash_minor, card_minor, fee_minor, fee_usd_rate FROM debt_events WHERE guid=@p0 AND kind='payment' AND store_guid=@p1", q.paymentEventGuid, store).singleOrNull()
                require(evRow != null)
                val customerGuid = evRow[0] as String
                val cashMinor = evRow[1] as Long
                val cardMinor = evRow[2] as Long
                val feeMinor = evRow[3] as Long
                val feeUsdRate = evRow[4] as? String

                val lineRows = rows(db, "SELECT account_guid, debt_delta_minor FROM debt_event_lines WHERE event_guid=@p0 ORDER BY line_index", q.paymentEventGuid)
                require(lineRows.isNotEmpty())
                val origLines = lineRows.map { DebtLine(it[0] as String, it[1] as Long) }
                val origEffect = DebtEffect(origLines, cashMinor, cardMinor, feeMinor)
                val revEffect = DebtAccounting.reversePayment(origEffect, q.refundedFeeMinor)

                val seq = Math.addExact(scalar(db, "SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0", device) as Long, 1L)
                exec(db, "INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid) VALUES(@p0,@p0,1,'payment_reversal',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                    q.requestGuid, customerGuid, store, actor, device, seq, q.occurredAt, payload, hash(payload), revEffect.cashMinor, revEffect.cardMinor, revEffect.feeExpenseMinor, feeUsdRate, q.paymentEventGuid)

                revEffect.lines.forEachIndexed { i, line ->
                    exec(db, "INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",
                        q.requestGuid, i, line.accountGuid, customerGuid, store, line.deltaMinor)
                }

                finish(db, q.requestGuid, payload)
                q.requestGuid
            }
        }
    }

    suspend fun refundCredit(q: DebtCreditRefundCommand): String {
        id(q.requestGuid); id(q.customerGuid); id(q.accountGuid); text(q.reason, 1000, true); require(q.occurredAt >= 0)
        require(q.cashMinor >= 0 && q.cardMinor >= 0); val total = Math.addExact(q.cashMinor, q.cardMinor); require(total > 0)
        val payload = canonical("credit_refund", store, actor, q.requestGuid, q.customerGuid, q.accountGuid, q.cashMinor.toString(), q.cardMinor.toString(), q.occurredAt.toString(), q.reason)
        return write { db ->
            scope(db); val old = replay(db, q.requestGuid, payload)
            if (old != null) old else {
                customer(db, q.customerGuid, false)
                val accList = accounts(db, q.customerGuid)
                val account = accList.singleOrNull { it.accountGuid == q.accountGuid }
                require(account != null)
                val effect = DebtAccounting.refundCredit(account, q.cashMinor, q.cardMinor)

                val seq = Math.addExact(scalar(db, "SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0", device) as Long, 1L)
                exec(db, "INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid) VALUES(@p0,@p0,1,'credit_refund',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,0,NULL,NULL)",
                    q.requestGuid, q.customerGuid, store, actor, device, seq, q.occurredAt, payload, hash(payload), effect.cashMinor, effect.cardMinor)

                exec(db, "INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,0,@p1,@p2,@p3,@p4)",
                    q.requestGuid, effect.lines[0].accountGuid, q.customerGuid, store, effect.lines[0].deltaMinor)

                finish(db, q.requestGuid, payload)
                q.requestGuid
            }
        }
    }

    suspend fun transferCredit(q: DebtCreditTransferCommand): String {
        id(q.requestGuid); id(q.customerGuid); id(q.sourceAccountGuid); id(q.targetAccountGuid); text(q.reason, 1000, true)
        require(q.occurredAt >= 0); require(q.amountMinor > 0)
        val payload = canonical("credit_transfer", store, actor, q.requestGuid, q.customerGuid, q.sourceAccountGuid, q.targetAccountGuid, q.amountMinor.toString(), q.occurredAt.toString(), q.reason)
        return write { db ->
            scope(db); val old = replay(db, q.requestGuid, payload)
            if (old != null) old else {
                customer(db, q.customerGuid, false)
                val accList = accounts(db, q.customerGuid)
                val source = accList.singleOrNull { it.accountGuid == q.sourceAccountGuid }
                val target = accList.singleOrNull { it.accountGuid == q.targetAccountGuid }
                require(source != null && target != null)
                val effect = DebtAccounting.transferCredit(source, target, q.amountMinor)

                val seq = Math.addExact(scalar(db, "SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0", device) as Long, 1L)
                exec(db, "INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid) VALUES(@p0,@p0,1,'credit_transfer',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,0,0,0,NULL,NULL)",
                    q.requestGuid, q.customerGuid, store, actor, device, seq, q.occurredAt, payload, hash(payload))

                effect.lines.forEachIndexed { i, line ->
                    exec(db, "INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",
                        q.requestGuid, i, line.accountGuid, q.customerGuid, store, line.deltaMinor)
                }

                finish(db, q.requestGuid, payload)
                q.requestGuid
            }
        }
    }
}
