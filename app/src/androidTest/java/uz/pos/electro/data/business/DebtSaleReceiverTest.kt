package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.SaleAccounting
import java.util.UUID

class DebtSaleReceiverTest {
    private fun g(n: Int)="00000000-0000-0000-0000-"+n.toString().padStart(12,'0')
    private fun repo(db: AppDatabase)=DebtRepository(db,g(1),g(2)){true}
    private fun inbox(db: AppDatabase,map: (String)->Long?={ if(it==g(2))2L else null })=
        DebtEnvelopeInbox(db,g(1),{true},{true},map)
    private fun sql(db: AppDatabase)=db.openHelper.writableDatabase
    private fun exec(db: AppDatabase,s: String)=sql(db).execSQL(s)
    private fun value(db: AppDatabase,s: String)=DebtRepository.scalar(sql(db),s)
    private fun count(db: AppDatabase,table: String)=value(db,"SELECT COUNT(*) FROM $table") as Long
    private fun stock(db: AppDatabase)=(value(db,"SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble()
    private fun user(db: AppDatabase)=exec(db,"INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(2,'Cashier','2222','CASHIER')")
    private fun product(db: AppDatabase)=exec(db,"INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('${g(11)}','Current product',999,999,5,'METR',500)")
    private fun warehouse(db: AppDatabase)=exec(db,"INSERT INTO warehouses(guid,name,updated_at) VALUES('${g(12)}','Current warehouse',500),('${g(14)}','Other',500)")
    private fun stocks(db: AppDatabase)=exec(db,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('${g(11)}','${g(12)}',1,500),('${g(11)}','${g(14)}',4,500)")
    private fun dependencies(db: AppDatabase){user(db);product(db);warehouse(db);stocks(db)}
    private fun empty(db: AppDatabase) {
        for(t in listOf("sales","sale_items","debt_customers","debt_accounts","debt_events"))assertEquals(t,0L,count(db,t))
        assertEquals(0L,value(db,"SELECT applying FROM sync_control"));assertEquals("",value(db,"SELECT current_group FROM sync_control"))
    }
    private suspend fun reject(action: suspend ()->Unit) {
        try { action() } catch(_: IllegalArgumentException){return} catch(_: IllegalStateException){return}
        catch(_: android.database.sqlite.SQLiteException){return}
        fail("Invalid sale import accepted")
    }
    private fun sale()=DebtSaleSnapshot(g(4),100,20000,12000,2000,3000,60,"2","12000","DEBT",listOf(
        DebtSaleItem(g(10),g(11),"Old cable","Old category","METR",g(12),"Old warehouse","1.25","100","0.005","USD",g(13),"-1.25"),
        DebtSaleItem(g(20),g(11),"Old cable","Old category","METR",g(12),"Old warehouse","0.75","100","60","UZS",g(23),"-0.75")))
    private suspend fun components(db: AppDatabase): Pair<String,String> {
        val sw=DebtEnvelope.encodeSale(sale())
        repo(db).createCustomer(DebtCustomerDraft(g(3),"Ali","","",1))
        repo(db).openSale(DebtSaleCommand(g(5),g(3),g(4),DebtWire.fingerprint(sw),20000,2000,3000,100)) {
            it.execSQL("INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES(?,200,120,'DEBT',20,30,0.6,2,12000,100,1,0)",arrayOf(g(4)))
        }
        val bridge=DebtSyncStore(db,g(1),{true},{true})
        return bridge.exportCustomer(g(3)) to bridge.exportEvent(g(5))
    }
    private fun packet(c: Pair<String,String>,s: DebtSaleSnapshot=sale()): String {
        val sw=DebtEnvelope.encodeSale(s);val e=DebtWire.decodeEvent(c.second,g(1));val fields=DebtWire.commandFields(e.payload).toMutableList()
        fields[6]=DebtWire.fingerprint(sw);fields[7]=s.totalMinor.toString()
        val payload=DebtRepository.canonical(*fields.toTypedArray())
        val changed=e.copy(payload=payload,payloadHash=DebtWire.fingerprint(payload),account=e.account!!.copy(originalDebtMinor=s.totalMinor-s.cashMinor-s.cardMinor))
        return DebtEnvelope.encode(DebtEnvelopePacket(e.guid,g(1),c.first,DebtWire.encodeEvent(changed,g(1)),sw),g(1))
    }

    @Test fun roomReceivesFrozenSaleAndRollsBackEveryBoundary() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val dbs=mutableListOf<AppDatabase>();val names=mutableListOf<String>()
        suspend fun fresh(): AppDatabase {
            val name="debt-sale-${UUID.randomUUID()}.db";names.add(name)
            return AppDatabase.buildDatabase(context,scope,name).also { dbs.add(it);repo(it).bindStore() }
        }
        try {
            val c=components(fresh());val wire=packet(c);var db=fresh()
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(db){null}.receive(wire,1));empty(db)
            product(db);warehouse(db);stocks(db)
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(db).receive(wire,2));empty(db) // user 2 is absent
            user(db)
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(db){0}.receive(wire,3));empty(db)
            assertEquals(1L,inbox(db).readPending(g(5))!!.receivedAt)
            val before=count(db,"sync_journal")
            assertEquals(DebtReceiveStatus.Applied,inbox(db).receive(wire,4))
            assertEquals(-1.0,stock(db),0.0);assertEquals(3.0,(value(db,"SELECT stock_quantity FROM products WHERE guid='${g(11)}'") as Number).toDouble(),0.0)
            assertEquals(500L,value(db,"SELECT updated_at FROM product_stocks WHERE warehouse_guid='${g(12)}'"))
            assertEquals(2L,count(db,"sale_items"));assertEquals(15000L,repo(db).readAccounts(g(3)).single().balanceMinor)
            assertEquals(before+5,count(db,"sync_journal"));assertEquals(0L,value(db,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1"))
            val receipt=db.saleDao().getSalesListBetween(0,100).single()
            assertEquals(PaymentType.DEBT,receipt.sale.paymentType);assertEquals(2L,receipt.sale.userId)
            assertEquals("Old cable",receipt.items[0].productName);assertEquals("Old warehouse",receipt.items[0].warehouseName)
            assertEquals("USD",receipt.items[0].costCurrency);assertEquals(12000.0,receipt.sale.usdRate,0.0)
            val report=SaleAccounting.summary(SaleAccounting.lines(receipt),99999.0)
            assertEquals(200.0,report.totalRevenue,0.0);assertEquals(20.0,report.totalCashAmount,0.0)
            assertEquals(30.0,report.totalCardAmount,0.0);assertEquals(79.4,report.netProfit,0.000001)
            assertNull(inbox(db).readPending(g(5)));assertEquals(wire,inbox(db).exportApplied(g(5)))
            val index=dbs.indexOf(db);db.close();db=AppDatabase.buildDatabase(context,scope,names[index]);dbs[index]=db
            val journal=count(db,"sync_journal")
            assertEquals(DebtReceiveStatus.AlreadyApplied,inbox(db).receive(wire,5));assertEquals(journal,count(db,"sync_journal"));assertEquals(-1.0,stock(db),0.0)
            exec(db,"UPDATE product_stocks SET quantity=7 WHERE warehouse_guid='${g(12)}';")
            exec(db,"UPDATE products SET stock_quantity=11")
            assertEquals(DebtReceiveStatus.AlreadyApplied,inbox(db).receive(wire,6));assertEquals(7.0,stock(db),0.0)
            reject { DebtEnvelopeInbox(db,g(1),{true},{false},{2}).receive(wire,6) }
            val changed=packet(c,sale().copy(items=listOf(sale().items[0].copy(productName="Changed"),sale().items[1])))
            reject { inbox(db).receive(changed,7) }
            exec(db,"UPDATE sale_items SET price_at_sale=101 WHERE id=(SELECT MIN(id) FROM sale_items)")
            reject { inbox(db).receive(wire,8) };reject { inbox(db).exportApplied(g(5)) }
            val concurrent=fresh();dependencies(concurrent)
            val results=awaitAll(async(Dispatchers.IO){inbox(concurrent).receive(wire,1)},async(Dispatchers.IO){inbox(concurrent).receive(wire,1)})
            assertEquals(1,results.count { it==DebtReceiveStatus.Applied });assertEquals(1,results.count { it==DebtReceiveStatus.AlreadyApplied })
            assertEquals(-1.0,stock(concurrent),0.0)
            exec(concurrent,"UPDATE sync_journal SET delta=-9 WHERE op_id='${g(13)}'");reject { inbox(concurrent).receive(wire,2) }
            for(trigger in listOf("BEFORE INSERT ON sales","BEFORE INSERT ON sale_items",
                "BEFORE INSERT ON sale_items WHEN NEW.guid='${g(20)}'","BEFORE INSERT ON sync_journal WHEN NEW.kind='debt_stock'",
                "BEFORE UPDATE ON product_stocks","BEFORE UPDATE ON products","BEFORE INSERT ON debt_events",
                "BEFORE INSERT ON debt_accounts","BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%'","BEFORE DELETE ON debt_sync_inbox")) {
                val failed=fresh();dependencies(failed)
                assertEquals(DebtReceiveStatus.WaitingForSaleAdapter,DebtEnvelopeInbox(failed,g(1),{true},{true}).receive(wire,1))
                val old=count(failed,"sync_journal")
                exec(failed,"CREATE TRIGGER fail_boundary $trigger BEGIN SELECT RAISE(ABORT,'Injected failure'); END")
                reject { inbox(failed).receive(wire,2) };empty(failed)
                assertEquals(1.0,stock(failed),0.0);assertEquals(old,count(failed,"sync_journal"))
                assertEquals("sale_adapter_pending",inbox(failed).readPending(g(5))!!.reason);assertNull(inbox(failed).exportApplied(g(5)))
                exec(failed,"DROP TRIGGER fail_boundary")
                assertEquals(DebtReceiveStatus.Applied,inbox(failed).receive(wire,3));assertEquals(-1.0,stock(failed),0.0)
            }
            val collision=fresh();dependencies(collision)
            exec(collision,"INSERT INTO sync_journal(op_id,kind,entity_guid) VALUES('${g(13)}','stock','other')")
            reject { inbox(collision).receive(wire,1) };empty(collision);assertEquals(1.0,stock(collision),0.0)
            val legacy=fresh();dependencies(legacy)
            exec(legacy,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES('${g(4)}',200,120,'DEBT',20,30,0.6,2,12000,100,2,0)")
            reject { inbox(legacy).receive(wire,1) };assertEquals(0L,count(legacy,"debt_accounts"));assertEquals(1.0,stock(legacy),0.0)
            val absent=fresh();user(absent)
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(absent).receive(wire,1));empty(absent)
            product(absent)
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(absent).receive(wire,2));empty(absent)
            warehouse(absent)
            assertEquals(DebtReceiveStatus.Applied,inbox(absent).receive(wire,3));assertEquals(-2.0,stock(absent),0.0)
            val unsafe=fresh();dependencies(unsafe)
            val unsafeSale=sale().copy(totalMinor=199999999999800,items=sale().items.map { it.copy(price="999999999999.00000001") })
            reject { inbox(unsafe).receive(packet(c,unsafeSale),1) };empty(unsafe);assertEquals(1.0,stock(unsafe),0.0)
            exec(unsafe,"UPDATE product_stocks SET quantity=9007199254740992 WHERE warehouse_guid='${g(12)}'")
            reject { inbox(unsafe).receive(wire,2) };empty(unsafe)
        } finally { dbs.forEach { it.close() };scope.cancel();names.forEach { context.deleteDatabase(it) } }
    }

    @Test fun largeAppliedEnvelopeSurvivesRoomRestartAndExactRelay() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val names=List(2){"debt-large-sale-${UUID.randomUUID()}.db"}
        val source=AppDatabase.buildDatabase(context,scope,names[0]);var receiver=AppDatabase.buildDatabase(context,scope,names[1])
        try {
            repo(source).bindStore();repo(receiver).bindStore();val c=components(source);dependencies(receiver)
            val items=(0 until 1000).map { sale().items[0].copy(guid=g(1000+it),stockOperationGuid=g(10000+it),
                productName="界".repeat(180),category="界".repeat(100),warehouseName="界".repeat(120)) }
            val big=sale().copy(totalMinor=12500000,costMinor=7500000,items=items)
            val wire=packet(c,big);assertTrue(wire.length>2*1024*1024)
            assertEquals(DebtReceiveStatus.Applied,inbox(receiver).receive(wire,1))
            assertEquals(-1249.0,stock(receiver),0.0);assertEquals(1000L,count(receiver,"sale_items"))
            receiver.close();receiver=AppDatabase.buildDatabase(context,scope,names[1])
            assertEquals(wire,inbox(receiver).exportApplied(g(5)))
            assertEquals(DebtReceiveStatus.AlreadyApplied,inbox(receiver).receive(wire,2))
            assertEquals(-1249.0,stock(receiver),0.0);assertEquals(1000,receiver.saleDao().getSalesListBetween(0,100).single().items.size)
        } finally {source.close();receiver.close();scope.cancel();names.forEach { context.deleteDatabase(it) }}
    }
}
