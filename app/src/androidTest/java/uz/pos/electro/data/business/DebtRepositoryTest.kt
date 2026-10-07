package uz.pos.electro.data.business

import android.database.sqlite.SQLiteDatabase
import androidx.sqlite.db.SupportSQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import java.security.MessageDigest
import java.util.UUID

class DebtRepositoryTest {
    private fun g(i: Int) = "00000000-0000-0000-0000-" + i.toString().padStart(12,'0')
    private fun value(db: SupportSQLiteDatabase, query: String): String = db.query(query).use { check(it.moveToFirst());it.getString(0) }
    private fun count(db: SupportSQLiteDatabase, table: String) = value(db,"SELECT COUNT(*) FROM $table").toLong()
    private suspend fun reject(action: suspend () -> Unit) {
        try { action() } catch (_: IllegalArgumentException) { return }
        catch (_: IllegalStateException) { return }
        catch (_: android.database.sqlite.SQLiteException) { return }
        fail("Invalid command accepted")
    }
    private fun sale(db: SupportSQLiteDatabase, guid: String) {
        db.execSQL("INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at,user_id,is_synced,usd_rate,cash_amount,card_amount,tax_amount,tax_rate) VALUES(?,100,60,'DEBT',1,1,0,12000,20,0,0,0)",arrayOf(guid))
        db.execSQL("UPDATE transaction_probe SET quantity=quantity-1")
    }
    @Test fun atomicCommandsDurableRetriesAndSharedCanonicalFixtures() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val name="debt-repository-${UUID.randomUUID()}.db"
        val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
        var database=AppDatabase.buildDatabase(context,scope,name)
        try {
            var sql=database.openHelper.writableDatabase
            sql.execSQL("INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
            sql.execSQL("CREATE TABLE transaction_probe(quantity INTEGER NOT NULL)")
            sql.execSQL("INSERT INTO transaction_probe VALUES(10)")
            fun repo(allowed: Boolean=true, store: String=g(1))=DebtRepository(database,store,g(2)){allowed}
            repo().bindStore();repo().bindStore();reject { repo(store=g(99)).bindStore() }
            val customer=DebtCustomerDraft(g(3),"Ali Oʻgʻli","","",1)
            repo().createCustomer(customer);repo().createCustomer(customer)
            assertEquals(1L,count(sql,"debt_customers"))
            reject { repo().createCustomer(customer.copy(name="Changed")) }
            reject { repo(false).createCustomer(customer.copy(guid=g(30))) }
            val open=DebtSaleCommand(g(5),g(3),g(4),"0".repeat(64),10000,2000,0,1,"2030-01-01")
            repo().openSale(open){sale(it,g(4))}
            // Close/reopen Room, not just another repository instance.
            database.close();database=AppDatabase.buildDatabase(context,scope,name);sql=database.openHelper.writableDatabase
            repo().openSale(open){error("Retry called sale writer")}
            assertEquals(8000L,repo().readAccounts(g(3)).single().balanceMinor)
            assertEquals("9",value(sql,"SELECT quantity FROM transaction_probe"))
            assertEquals("0",value(sql,"SELECT COUNT(*) FROM sync_journal WHERE group_id='${g(5)}' AND acked<>-1"))
            reject { repo().openSale(open.copy(saleFingerprint="1".repeat(64))){} }
            reject { repo().openSale(open.copy(requestGuid=g(55))){} }
            val payment=DebtPaymentCommand(g(6),g(3),3000,1000,20,2,feeUsdRate="12000")
            repo().takePayment(payment);repo().takePayment(payment)
            assertEquals(4000L,repo().readAccounts(g(3)).single().balanceMinor)
            assertEquals(2L,count(sql,"debt_events"));assertEquals(1L,count(sql,"debt_event_lines"));assertEquals(1L,count(sql,"sales"))
            reject { repo().takePayment(payment.copy(cashMinor=3001)) }
            reject { repo().takePayment(payment.copy(requestGuid=g(70),targetAccountGuid=g(90))) }
            reject { repo().takePayment(payment.copy(requestGuid=g(70),cashMinor=9000)) }
            reject { repo(store=g(99)).takePayment(payment) }
            sql.execSQL("UPDATE debt_customers SET name='Renamed',archived=1")
            repo().createCustomer(customer);repo().takePayment(payment)
            reject { repo().takePayment(payment.copy(requestGuid=g(70))) }
            sql.execSQL("UPDATE debt_customers SET archived=0")
            val before=count(sql,"sync_journal")
            reject { repo().openSale(open.copy(requestGuid=g(7),saleGuid=g(8))){sale(it,g(8));error("Injected crash")} }
            assertEquals(1L,count(sql,"sales"));assertEquals(before,count(sql,"sync_journal"))
            assertEquals("9",value(sql,"SELECT quantity FROM transaction_probe"))
            assertEquals("",value(sql,"SELECT current_group FROM sync_control"))
            sql.execSQL("CREATE TRIGGER fail_debt_outbox BEFORE INSERT ON sync_journal WHEN NEW.kind='debt_event' BEGIN SELECT RAISE(ABORT,'Injected failure'); END")
            reject { repo().takePayment(payment.copy(requestGuid=g(9),cashMinor=1000,cardMinor=0,feeMinor=0)) }
            assertEquals(2L,count(sql,"debt_events"));assertEquals(2L,count(sql,"debt_command_receipts"))
            assertEquals(1L,count(sql,"debt_event_lines"));assertEquals(before,count(sql,"sync_journal"))
            sql.execSQL("DROP TRIGGER fail_debt_outbox")
            val duplicate=payment.copy(requestGuid=g(10),cashMinor=1000,cardMinor=0,feeMinor=0)
            awaitAll(async(Dispatchers.IO){repo().takePayment(duplicate)},async(Dispatchers.IO){repo().takePayment(duplicate)})
            assertEquals(3000L,repo().readAccounts(g(3)).single().balanceMinor);assertEquals(3L,count(sql,"debt_events"))
            val fixtures=JSONObject(InstrumentationRegistry.getInstrumentation().context.assets.open("debt-repository-fixtures.json").bufferedReader().use { it.readText() })
            for(kind in listOf("customer","sale_open","payment")) {
                val expected=fixtures.getJSONObject(kind);val request=expected.getString("request")
                val payload=value(sql,if(kind=="customer")"SELECT value FROM sync_meta WHERE key='debt_customer_create:$request'" else "SELECT payload FROM debt_events WHERE request_guid='$request'")
                assertEquals(expected.getString("payload"),payload)
                assertEquals(expected.getString("hash"),MessageDigest.getInstance("SHA-256").digest(payload.toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it.toInt() and 255)})
            }
        } finally { database.close();scope.cancel();SQLiteDatabase.deleteDatabase(context.getDatabasePath(name)) }
    }
}
