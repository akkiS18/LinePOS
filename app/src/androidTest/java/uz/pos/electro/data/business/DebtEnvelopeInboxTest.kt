package uz.pos.electro.data.business

import android.database.sqlite.SQLiteDatabase
import androidx.sqlite.db.SupportSQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import java.util.UUID

class DebtEnvelopeInboxTest {
    private fun g(n: Int)="00000000-0000-0000-0000-"+n.toString().padStart(12,'0')
    private fun repo(db: AppDatabase)=DebtRepository(db,g(1),g(2)){true}
    private fun bridge(db: AppDatabase)=DebtSyncStore(db,g(1),{true},{true})
    private fun inbox(db: AppDatabase,allowed: Boolean=true,actor: Boolean=true)=DebtEnvelopeInbox(db,g(1),{allowed},{actor})
    private fun sql(db: AppDatabase)=db.openHelper.writableDatabase
    private fun number(db: AppDatabase,query: String)=sql(db).query(query).use { check(it.moveToFirst());it.getLong(0) }
    private fun count(db: AppDatabase,table: String)=number(db,"SELECT COUNT(*) FROM $table")
    private suspend fun reject(action: suspend ()->Unit){try{action()}catch(_: IllegalArgumentException){return}catch(_: IllegalStateException){return}catch(_: android.database.sqlite.SQLiteException){return};fail("Invalid inbox operation accepted")}
    private fun sale(db: SupportSQLiteDatabase,guid: String) {
        db.execSQL("INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at,user_id,is_synced,usd_rate,cash_amount,card_amount,tax_amount,tax_rate) VALUES(?,100,60,'DEBT',1,1,0,12000,20,0,0,0)",arrayOf(guid))
    }
    private fun packet(customer: String,event: String,sale: String="")=DebtEnvelope.encode(DebtEnvelopePacket(if(event.isEmpty())g(3) else DebtWire.decodeEvent(event,g(1)).guid,g(1),customer,event,sale),g(1))
    private fun another(payment: String,customer: String,request: Int): String {
        val e=DebtWire.decodeEvent(payment,g(1));val p=DebtRepository.canonical("payment",g(1),g(2),g(request),g(3),"4000","0","0","2","","")
        return packet(customer,DebtWire.encodeEvent(e.copy(guid=g(request),requestGuid=g(request),deviceSequence=request.toLong(),payload=p,payloadHash=DebtWire.fingerprint(p)),g(1)))
    }
    @Test fun durableInboxCompletesAtomicallyAndNeverAcknowledgesMissingDependencies() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext;val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val names=List(4){"debt-inbox-${UUID.randomUUID()}.db"};val dbs=names.map { AppDatabase.buildDatabase(context,scope,it) }.toMutableList()
        try {
            for(db in dbs){sql(db).execSQL("INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')");repo(db).bindStore()}
            val a=dbs[0];var b=dbs[1];var c=dbs[2];val q=dbs[3]
            val sw=DebtEnvelope.encodeSale(DebtSaleSnapshot(g(4),1,10000,6000,2000,0,0,"0","12000","DEBT",listOf(DebtSaleItem(g(10),g(11),"Wire","Electric","m",g(12),"Main","1","100","60","UZS",g(13),"-1"))))
            repo(a).createCustomer(DebtCustomerDraft(g(3),"Ali","","",1));repo(a).openSale(DebtSaleCommand(g(5),g(3),g(4),DebtWire.fingerprint(sw),10000,2000,0,1)){sale(it,g(4))}
            repo(a).takePayment(DebtPaymentCommand(g(6),g(3),4000,0,0,2))
            val cw=bridge(a).exportCustomer(g(3));val ew=bridge(a).exportEvent(g(5));val pw=bridge(a).exportEvent(g(6))
            val incoming=packet(cw,pw);val opening=packet(cw,ew,sw);val customer=packet(cw,"")
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(b).receive(incoming,10))
            assertEquals(0L,count(b,"debt_customers"));assertEquals(0L,count(b,"debt_events"));assertEquals(0L,count(b,"sales"))
            b.close();b=AppDatabase.buildDatabase(context,scope,names[1]);dbs[1]=b
            assertEquals(DebtPendingPacket(incoming,10,"missing_dependency"),inbox(b).readPending(g(6)))
            sql(b).execSQL("UPDATE debt_sync_inbox SET error='retry_needed'")
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(b).receive(incoming,99));assertEquals(10L,inbox(b).readPending(g(6))!!.receivedAt);assertEquals("missing_dependency",inbox(b).readPending(g(6))!!.reason)
            reject { inbox(b).receive(packet("",pw),99) };reject { inbox(b,actor=false).readPending(g(6)) };reject { inbox(b,allowed=false).receive(incoming,1) }
            assertEquals(DebtReceiveStatus.WaitingForSaleAdapter,inbox(b).receive(opening,11));assertNull(inbox(b).exportApplied(g(5)))
            assertEquals(0L,count(b,"debt_customers"));assertEquals(0L,count(b,"sales"))
            // Already-tested component API supplies a fixture dependency, not an opening receiver.
            bridge(b).apply(listOf(cw),listOf(ew)){db,e->sale(db,e.account!!.saleGuid)}
            sql(b).execSQL("CREATE TRIGGER fail_outer BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected outer receipt failure'); END")
            val journal=count(b,"sync_journal");reject { inbox(b).receive(incoming,12) }
            assertEquals(1L,count(b,"debt_events"));assertEquals(journal,count(b,"sync_journal"));assertNotNull(inbox(b).readPending(g(6)));assertEquals(0L,number(b,"SELECT applying FROM sync_control"))
            sql(b).execSQL("DROP TRIGGER fail_outer")
            sql(b).execSQL("CREATE TRIGGER fail_inbox_delete BEFORE DELETE ON debt_sync_inbox BEGIN SELECT RAISE(ABORT,'Injected completion failure'); END")
            reject { inbox(b).receive(incoming,12) };assertEquals(1L,count(b,"debt_events"));assertNull(inbox(b).exportApplied(g(6)))
            sql(b).execSQL("DROP TRIGGER fail_inbox_delete")
            assertEquals(DebtReceiveStatus.Applied,inbox(b).receive(incoming,12));assertNull(inbox(b).readPending(g(6)))
            assertEquals(4000L,repo(b).readAccounts(g(3)).single().balanceMinor);assertEquals(incoming,inbox(b).exportApplied(g(6)))
            sql(b).execSQL("UPDATE debt_customers SET name='Renamed',archived=1")
            b.close();b=AppDatabase.buildDatabase(context,scope,names[1]);dbs[1]=b
            val before=count(b,"sync_journal");assertEquals(DebtReceiveStatus.AlreadyApplied,inbox(b).receive(incoming,20));assertEquals(before,count(b,"sync_journal"))
            reject { inbox(b).receive(packet("",pw),20) };reject { inbox(b,actor=false).receive(incoming,20) };reject { inbox(b,actor=false).exportApplied(g(6)) }
            val next=another(pw,cw,7)
            val results=awaitAll(async(Dispatchers.IO){inbox(b).receive(next,21)},async(Dispatchers.IO){inbox(b).receive(next,21)})
            assertEquals(1,results.count { it==DebtReceiveStatus.Applied });assertEquals(1,results.count { it==DebtReceiveStatus.AlreadyApplied })
            assertEquals(0L,repo(b).readAccounts(g(3)).single().balanceMinor)
            assertEquals(0L,number(b,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1"))
            sql(c).execSQL("CREATE TRIGGER fail_customer_outer BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected customer receipt failure'); END")
            reject { inbox(c).receive(customer,1) };assertEquals(0L,count(c,"debt_customers"));assertEquals(0L,number(c,"SELECT COUNT(*) FROM sync_meta WHERE key LIKE 'debt_%'"))
            sql(c).execSQL("DROP TRIGGER fail_customer_outer")
            assertEquals(DebtReceiveStatus.Applied,inbox(c).receive(customer,1));assertEquals(DebtReceiveStatus.AlreadyApplied,inbox(c).receive(customer,2))
            reject { inbox(c).receive(incoming.replace("debt-envelope-v1","debt-envelope-v2"),1) };reject { inbox(c).receive(incoming,-1) };assertEquals(0L,count(c,"debt_sync_inbox"))
            val largeSale=DebtEnvelope.decodeSale(sw)
            val largeItems=(0 until 1000).map { i->largeSale.items[0].copy(guid=g(1000+i),stockOperationGuid=g(10000+i),productName="界".repeat(180),category="界".repeat(100),warehouseName="界".repeat(120)) }
            val largeWire=DebtEnvelope.encodeSale(largeSale.copy(totalMinor=10000000,costMinor=6000000,items=largeItems))
            val largeEvent=DebtWire.decodeEvent(ew,g(1))
            val largePayload=DebtRepository.canonical("sale_open",g(1),g(2),g(5),g(3),g(4),DebtWire.fingerprint(largeWire),"10000000","2000","0","1","")
            val largePacket=packet(cw,DebtWire.encodeEvent(largeEvent.copy(payload=largePayload,payloadHash=DebtWire.fingerprint(largePayload),account=largeEvent.account!!.copy(originalDebtMinor=9998000)),g(1)),largeWire)
            assertTrue("Large body fixture too small",largePacket.length>2*1024*1024)
            assertEquals(DebtReceiveStatus.WaitingForSaleAdapter,inbox(c).receive(largePacket,30))
            c.close();c=AppDatabase.buildDatabase(context,scope,names[2]);dbs[2]=c
            assertEquals(largePacket,inbox(c).readPending(g(5))!!.wire)
            assertEquals(DebtReceiveStatus.WaitingForSaleAdapter,inbox(c).receive(largePacket,31));assertEquals(30L,inbox(c).readPending(g(5))!!.receivedAt)
            for(i in 100..227)assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(q).receive(another(pw,cw,i),1))
            var full=false;try{inbox(q).receive(another(pw,cw,300),1)}catch(_: DebtInboxFullException){full=true};assertTrue(full);assertEquals(128L,count(q,"debt_sync_inbox"))
            assertEquals(DebtReceiveStatus.WaitingForDependency,inbox(q).receive(another(pw,cw,100),2));assertEquals(DebtReceiveStatus.Applied,inbox(q).receive(customer,1))
            // Deliberately occupied resident bytes count against quota; no silent eviction.
            sql(q).execSQL("DELETE FROM debt_sync_inbox WHERE packet_guid<>'${g(100)}'")
            sql(q).execSQL("UPDATE debt_sync_inbox SET payload=hex(zeroblob(16777216))")
            full=false;try{inbox(q).receive(another(pw,cw,300),1)}catch(_: DebtInboxFullException){full=true};assertTrue(full);assertEquals(1L,count(q,"debt_sync_inbox"))
            sql(b).execSQL("UPDATE sync_meta SET value='corrupt' WHERE key='debt_envelope_v1:${g(6)}'")
            reject { inbox(b).exportApplied(g(6)) };reject { inbox(b).receive(incoming,1) }
        } finally {dbs.forEach { it.close() };scope.cancel();names.forEach { SQLiteDatabase.deleteDatabase(context.getDatabasePath(it)) }}
    }
}
