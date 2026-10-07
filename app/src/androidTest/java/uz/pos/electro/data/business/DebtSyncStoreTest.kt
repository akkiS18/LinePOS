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

class DebtSyncStoreTest {
    private fun g(i: Int)="00000000-0000-0000-0000-"+i.toString().padStart(12,'0')
    private fun repo(db: AppDatabase)=DebtRepository(db,g(1),g(2)){true}
    private fun bridge(db: AppDatabase,allowed: Boolean=true,actor: Boolean=true,store: String=g(1))=DebtSyncStore(db,store,{allowed},{actor})
    private fun sql(db: AppDatabase)=db.openHelper.writableDatabase
    private fun value(db: AppDatabase,query: String): String=sql(db).query(query).use { check(it.moveToFirst());it.getString(0) }
    private fun count(db: AppDatabase,table: String)=value(db,"SELECT COUNT(*) FROM $table").toLong()
    private suspend fun reject(action: suspend () -> Unit) {
        try { action() }catch(_: IllegalArgumentException){return}catch(_: IllegalStateException){return}catch(_: android.database.sqlite.SQLiteException){return}
        fail("Invalid import accepted")
    }
    private fun sale(db: SupportSQLiteDatabase,guid: String) {
        db.execSQL("INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at,user_id,is_synced,usd_rate,cash_amount,card_amount,tax_amount,tax_rate) VALUES(?,100,60,'DEBT',1,1,0,12000,20,0,0,0)",arrayOf(guid))
        db.execSQL("UPDATE transaction_probe SET quantity=quantity-1")
    }
    private fun newPayment(e: DebtWireEvent,request: Int,customer: Int,account: String,seq: Long): String {
        val payload=DebtRepository.canonical("payment",g(1),g(2),g(request),g(customer),"3000","1000","20","2","","12000")
        return DebtWire.encodeEvent(e.copy(guid=g(request),requestGuid=g(request),customerGuid=g(customer),deviceSequence=seq,payload=payload,payloadHash=DebtWire.fingerprint(payload),lines=listOf(DebtLine(account,-4000))),g(1))
    }
    @Test fun realRoomReplicationConvergesAndRejectsPartialOrChangedImports() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        val names=List(4){"debt-bridge-${UUID.randomUUID()}.db"}
        val databases=names.map { AppDatabase.buildDatabase(context,scope,it) }.toMutableList()
        try {
            for(db in databases) {
                sql(db).execSQL("INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
                sql(db).execSQL("CREATE TABLE transaction_probe(quantity INTEGER NOT NULL)");sql(db).execSQL("INSERT INTO transaction_probe VALUES(10)")
                repo(db).bindStore()
            }
            val a=databases[0];val b=databases[1];var c=databases[2];val d=databases[3]
            repo(a).createCustomer(DebtCustomerDraft(g(3),"Ali","","",1))
            repo(a).openSale(DebtSaleCommand(g(5),g(3),g(4),"0".repeat(64),10000,2000,0,1)){sale(it,g(4))}
            val customer=bridge(a).exportCustomer(g(3));val opening=bridge(a).exportEvent(g(5))
            bridge(b).apply(listOf(customer),listOf(opening)){db,e->sale(db,e.account!!.saleGuid)}
            val paymentA=DebtPaymentCommand(g(6),g(3),3000,1000,20,2,feeUsdRate="12000")
            repo(a).takePayment(paymentA);repo(b).takePayment(DebtPaymentCommand(g(7),g(3),7000,0,0,3))
            val wa=bridge(a).exportEvent(g(6));val wb=bridge(b).exportEvent(g(7))
            bridge(a).apply(emptyList(),listOf(wb));bridge(b).apply(emptyList(),listOf(wa))
            assertEquals(-3000L,repo(a).readAccounts(g(3)).single().balanceMinor)
            assertEquals(-3000L,repo(b).readAccounts(g(3)).single().balanceMinor)
            val before=count(a,"sync_journal")
            bridge(a).apply(listOf(customer),listOf(opening,wa,wb)){_,_->error("Echo called writer")};repo(a).takePayment(paymentA)
            assertEquals(before,count(a,"sync_journal"));assertEquals(1L,count(a,"sales"))
            bridge(c).apply(listOf(customer),listOf(wa,opening)){db,e->sale(db,e.account!!.saleGuid)}
            assertEquals(4000L,repo(c).readAccounts(g(3)).single().balanceMinor)
            assertEquals("9",value(c,"SELECT quantity FROM transaction_probe"))
            sql(c).execSQL("UPDATE debt_customers SET name='Renamed',archived=1")
            assertEquals(customer,bridge(c).exportCustomer(g(3)))
            c.close();c=AppDatabase.buildDatabase(context,scope,names[2]);databases[2]=c
            bridge(c).apply(listOf(customer),listOf(opening)){_,_->error("Restart replay called writer")}
            assertEquals("Renamed",value(c,"SELECT name FROM debt_customers"))
            awaitAll(async(Dispatchers.IO){bridge(c).apply(emptyList(),listOf(wb))},async(Dispatchers.IO){bridge(c).apply(emptyList(),listOf(wb))})
            assertEquals(-3000L,repo(c).readAccounts(g(3)).single().balanceMinor);assertEquals(3L,count(c,"debt_events"))
            assertEquals(wa,bridge(c).exportEvent(g(6)));assertEquals(wb,bridge(c).exportEvent(g(7)))
            assertEquals("0",value(c,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1"))
            val e=DebtWire.decodeEvent(wa,g(1))
            reject { bridge(c).apply(emptyList(),listOf(DebtWire.encodeEvent(e.copy(lines=listOf(DebtLine(g(90),-4000))),g(1)))) }
            reject { bridge(c).apply(emptyList(),listOf(newPayment(e,80,3,g(4),e.deviceSequence))) }
            reject { bridge(c,actor=false).apply(emptyList(),listOf(wa)) };reject { bridge(c,allowed=false).exportEvent(g(6)) }
            reject { bridge(c,store=g(99)).exportEvent(g(6)) }
            repo(c).createCustomer(DebtCustomerDraft(g(30),"Other","","",1))
            reject { bridge(c).apply(emptyList(),listOf(newPayment(e,8,30,g(4),99))) }
            var missing=false
            try { bridge(d).apply(listOf(customer),listOf(wa)) }catch(_: DebtDependencyException){missing=true}
            assertTrue(missing)
            fun empty() {
                assertEquals(0L,count(d,"debt_customers"));assertEquals(0L,count(d,"debt_events"));assertEquals(0L,count(d,"sync_journal"));assertEquals(0L,count(d,"sales"))
                assertEquals("10",value(d,"SELECT quantity FROM transaction_probe"));assertEquals("0",value(d,"SELECT applying FROM sync_control"))
            }
            empty()
            reject { bridge(d).apply(listOf(customer),listOf(opening)){db,v->sale(db,v.account!!.saleGuid);error("Injected callback failure")} };empty()
            sql(d).execSQL("CREATE TRIGGER fail_seal BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_wire_v1:event:%' BEGIN SELECT RAISE(ABORT,'Injected seal failure'); END")
            reject { bridge(d).apply(listOf(customer),listOf(opening)){db,v->sale(db,v.account!!.saleGuid)} };empty()
            sql(d).execSQL("DROP TRIGGER fail_seal")
            bridge(d).apply(listOf(customer),listOf(opening,wa)){db,v->sale(db,v.account!!.saleGuid)}
            sql(d).execSQL("UPDATE sync_meta SET value='corrupt' WHERE key='debt_wire_v1:event:${g(6)}'")
            reject { bridge(d).exportEvent(g(6)) }
            sql(d).execSQL("INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES('${g(5)}',1,'${g(4)}','${g(3)}','${g(1)}',-1)")
            reject { bridge(d).exportEvent(g(5)) }
        } finally { databases.forEach { it.close() };scope.cancel();names.forEach { SQLiteDatabase.deleteDatabase(context.getDatabasePath(it)) } }
    }
}
