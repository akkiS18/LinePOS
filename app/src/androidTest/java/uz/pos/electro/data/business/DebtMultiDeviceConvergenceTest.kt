package uz.pos.electro.data.business

import android.content.Context
import androidx.room.Room
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import java.util.UUID

class DebtMultiDeviceConvergenceTest {
    private fun g(n: Int) = "00000000-0000-0000-0000-" + n.toString().padStart(12, '0')
    private val storeGuid = g(1)
    private val actorGuid = g(2)

    private fun freshDb(context: Context, name: String): AppDatabase {
        context.deleteDatabase(name)
        val db = Room.databaseBuilder(context, AppDatabase::class.java, name)
            .allowMainThreadQueries()
            .build()
        val sql = db.openHelper.writableDatabase
        sql.execSQL("INSERT OR IGNORE INTO debt_scope(id, store_guid) VALUES(1, '$storeGuid')")
        sql.execSQL("INSERT OR IGNORE INTO users(id, name, pin_code, role) VALUES(1, 'Admin', '0000', 'ADMIN')")
        return db
    }

    private fun product(db: AppDatabase, pGuid: String, stock: Double = 20.0) {
        val sql = db.openHelper.writableDatabase
        sql.execSQL("INSERT INTO products(guid, name, cost_price, selling_price, stock_quantity, unit_type, updated_at) VALUES('$pGuid', 'Kabel', 60, 100, $stock, 'dona', 500)")
    }

    private fun warehouse(db: AppDatabase, wGuid: String) {
        val sql = db.openHelper.writableDatabase
        sql.execSQL("INSERT INTO warehouses(guid, name, updated_at) VALUES('$wGuid', 'Asosiy Ombor', 500)")
    }

    private fun stocks(db: AppDatabase, pGuid: String, wGuid: String, stock: Double = 20.0) {
        val sql = db.openHelper.writableDatabase
        sql.execSQL("INSERT INTO product_stocks(product_guid, warehouse_guid, quantity, updated_at) VALUES('$pGuid', '$wGuid', $stock, 500)")
    }

    @Test
    fun testOfflineExcessPaymentConvergenceOnAndroidRoom() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val dbName = "test_excess_debt_" + UUID.randomUUID()
        val db = freshDb(context, dbName)

        try {
            product(db, g(101), 50.0)
            warehouse(db, g(102))
            stocks(db, g(101), g(102), 50.0)

            val repo = DebtRepository(db, storeGuid, actorGuid) { true }
            val inbox = DebtEnvelopeInbox(db, storeGuid, { true }, { true }, { 1L })

            // Customer Ali with 100k debt (10,000,000 minor)
            val custGuid = g(301)
            repo.createCustomer(DebtCustomerDraft(custGuid, "Ali Valiyev", "+998901234567", "", 100))

            val saleItems = listOf(
                DebtSaleItem(g(401), g(101), "Kabel", "Elektr", "METR", g(102), "Asosiy Ombor", "1", "100000", "60000", "UZS", g(501), "-1")
            )
            val saleSnapshot = DebtSaleSnapshot(g(601), 100, 10000000, 6000000, 0, 0, 0, "0", "12850", "DEBT", saleItems)
            val saleReq = g(701)
            repo.openSale(DebtOpenSaleCommand(saleReq, custGuid, saleSnapshot))

            // Fabricate two 100k payment envelopes from two offline phones
            val payReq1 = g(702)
            val pay1Payload = DebtRepository.canonical("payment", storeGuid, actorGuid, payReq1, custGuid, "10000000", "0", "0", "200", g(601), "")
            val pay1Wire = DebtWire.encodeEvent(
                DebtWireEvent(payReq1, payReq1, "payment", custGuid, storeGuid, actorGuid, g(103), 1, 200, pay1Payload, DebtWire.fingerprint(pay1Payload), 10000000, 0, 0, null, null, listOf(DebtLine(g(601), -10000000))),
                storeGuid
            )
            val env1 = DebtEnvelope.encode(DebtEnvelopePacket(payReq1, storeGuid, "", pay1Wire, ""), storeGuid)

            val payReq2 = g(703)
            val pay2Payload = DebtRepository.canonical("payment", storeGuid, actorGuid, payReq2, custGuid, "10000000", "0", "0", "201", g(601), "")
            val pay2Wire = DebtWire.encodeEvent(
                DebtWireEvent(payReq2, payReq2, "payment", custGuid, storeGuid, actorGuid, g(104), 1, 201, pay2Payload, DebtWire.fingerprint(pay2Payload), 10000000, 0, 0, null, null, listOf(DebtLine(g(601), -10000000))),
                storeGuid
            )
            val env2 = DebtEnvelope.encode(DebtEnvelopePacket(payReq2, storeGuid, "", pay2Wire, ""), storeGuid)

            // Both offline payments apply cleanly via inbox
            assertEquals(DebtReceiveStatus.Applied, inbox.receive(env1, 300))
            assertEquals(DebtReceiveStatus.Applied, inbox.receive(env2, 301))

            // Account balance: 10,000,000 - 10,000,000 - 10,000,000 = -10,000,000 minor (-100k credit)
            val accounts = repo.readAccounts(custGuid)
            assertEquals(1, accounts.size)
            assertEquals(-10000000L, accounts[0].balanceMinor)

            // Cash recorded in debt events: 200k (20,000,000 minor)
            val totalCash = DebtRepository.scalar(db.openHelper.writableDatabase, "SELECT SUM(cash_minor) FROM debt_events WHERE kind='payment'") as Long
            assertEquals(20000000L, totalCash)

        } finally {
            db.close()
            context.deleteDatabase(dbName)
        }
    }

    @Test
    fun testWriterEpochIsolationOnClonedDatabase() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val dbName = "test_epoch_debt_" + UUID.randomUUID()
        val db = freshDb(context, dbName)

        try {
            product(db, g(101), 30.0)
            warehouse(db, g(102))
            stocks(db, g(101), g(102), 30.0)

            val custGuid = g(302)
            val repo1 = DebtRepository(db, storeGuid, actorGuid) { true }
            val repo2 = DebtRepository(db, storeGuid, actorGuid) { true }

            repo1.createCustomer(DebtCustomerDraft(custGuid, "Sardor", "+998933334455", "", 100))

            val saleItems = listOf(
                DebtSaleItem(g(402), g(101), "Kabel", "Elektr", "METR", g(102), "Asosiy Ombor", "2", "50000", "30000", "UZS", g(502), "-2")
            )
            val saleSnapshot = DebtSaleSnapshot(g(602), 100, 10000000, 6000000, 0, 0, 0, "0", "12850", "DEBT", saleItems)
            repo1.openSale(DebtOpenSaleCommand(g(704), custGuid, saleSnapshot))

            // Repo 1 writes payment
            val payReq1 = g(705)
            repo1.takePayment(DebtPaymentCommand(payReq1, custGuid, 3000000, 0, 0, 150))

            // Repo 2 writes payment
            val payReq2 = g(706)
            repo2.takePayment(DebtPaymentCommand(payReq2, custGuid, 2000000, 0, 0, 151))

            val sql = db.openHelper.writableDatabase
            val dev1 = DebtRepository.scalar(sql, "SELECT device_guid FROM debt_events WHERE guid='$payReq1'") as String
            val dev2 = DebtRepository.scalar(sql, "SELECT device_guid FROM debt_events WHERE guid='$payReq2'") as String

            assertNotNull(dev1)
            assertNotNull(dev2)
            assertNotEquals("Device epochs must be distinct between repository instances", dev1, dev2)

            val seq1 = DebtRepository.scalar(sql, "SELECT device_sequence FROM debt_events WHERE guid='$payReq1'") as Long
            val seq2 = DebtRepository.scalar(sql, "SELECT device_sequence FROM debt_events WHERE guid='$payReq2'") as Long
            assertEquals("Repo 1 sequence starts at 1 scoped to dev1", 1L, seq1)
            assertEquals("Repo 2 sequence starts at 1 scoped to dev2", 1L, seq2)

        } finally {
            db.close()
            context.deleteDatabase(dbName)
        }
    }

    @Test
    fun testCustomerRevisionAndArchivingSemantics() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val dbName = "test_cust_arch_" + UUID.randomUUID()
        val db = freshDb(context, dbName)

        try {
            product(db, g(101), 20.0)
            warehouse(db, g(102))
            stocks(db, g(101), g(102), 20.0)

            val repo = DebtRepository(db, storeGuid, actorGuid) { true }
            val custGuid = g(303)
            repo.createCustomer(DebtCustomerDraft(custGuid, "Baxrom", "+998971112233", "Izoh 1", 100))

            val record0 = repo.readCustomer(custGuid)
            assertNotNull(record0)
            assertEquals(0L, record0!!.revision)
            assertEquals("Baxrom", record0.name)
            assertFalse(record0.archived)

            // Stale revision update rejected
            var threwConflict = false
            try {
                repo.updateCustomer(DebtCustomerUpdate(custGuid, "Baxrom Yangi", "+998971112233", "Izoh 2", 99))
            } catch (_: IllegalArgumentException) {
                threwConflict = true
            }
            assertTrue("Stale revision update must throw", threwConflict)

            // Valid update increments revision
            assertTrue(repo.updateCustomer(DebtCustomerUpdate(custGuid, "Baxrom Yangi", "+998971112233", "Izoh 2", 0)))
            val record1 = repo.readCustomer(custGuid)
            assertNotNull(record1)
            assertEquals(1L, record1!!.revision)
            assertEquals("Baxrom Yangi", record1.name)

            // Open debt sale
            val saleItems = listOf(
                DebtSaleItem(g(403), g(101), "Kabel", "Elektr", "METR", g(102), "Asosiy Ombor", "1", "30000", "15000", "UZS", g(503), "-1")
            )
            val saleSnapshot = DebtSaleSnapshot(g(603), 100, 3000000, 1500000, 0, 0, 0, "0", "12850", "DEBT", saleItems)
            repo.openSale(DebtOpenSaleCommand(g(707), custGuid, saleSnapshot))

            val sql = db.openHelper.writableDatabase
            val nameAtSale = DebtRepository.scalar(sql, "SELECT customer_name_at_sale FROM debt_accounts WHERE sale_guid='${g(603)}'") as String
            assertEquals("Baxrom Yangi", nameAtSale)

            // Rename customer again
            repo.updateCustomer(DebtCustomerUpdate(custGuid, "Baxrom 3", "+998971112233", "Izoh 3", 1))
            val nameAfterRename = DebtRepository.scalar(sql, "SELECT customer_name_at_sale FROM debt_accounts WHERE sale_guid='${g(603)}'") as String
            assertEquals("Historical name at sale must remain immutable", "Baxrom Yangi", nameAfterRename)

            // Archive customer
            repo.archiveCustomer(custGuid, true)
            val recordArchived = repo.readCustomer(custGuid)
            assertNotNull(recordArchived)
            assertTrue(recordArchived!!.archived)
            assertEquals(3L, recordArchived.revision)

            // New sale on archived customer must be rejected
            var threwSaleOnArchived = false
            try {
                repo.openSale(DebtOpenSaleCommand(g(708), custGuid, saleSnapshot.copy(guid = g(604))))
            } catch (_: IllegalArgumentException) {
                threwSaleOnArchived = true
            }
            assertTrue("New sale on archived customer must be rejected", threwSaleOnArchived)

            // Local payment on archived customer is rejected
            var threwPayOnArchived = false
            try {
                repo.takePayment(DebtPaymentCommand(g(709), custGuid, 1000000, 0, 0, 200))
            } catch (_: IllegalArgumentException) {
                threwPayOnArchived = true
            }
            assertTrue("Local payment on archived customer must be rejected", threwPayOnArchived)

            // But remote offline payment applied via Inbox succeeds and reduces balance!
            val inbox = DebtEnvelopeInbox(db, storeGuid, { true }, { true }, { 1L })
            val payReqPeer = g(710)
            val peerPayPayload = DebtRepository.canonical("payment", storeGuid, actorGuid, payReqPeer, custGuid, "1000000", "0", "0", "205", g(603), "")
            val peerPayWire = DebtWire.encodeEvent(
                DebtWireEvent(payReqPeer, payReqPeer, "payment", custGuid, storeGuid, actorGuid, g(105), 1, 205, peerPayPayload, DebtWire.fingerprint(peerPayPayload), 1000000, 0, 0, null, null, listOf(DebtLine(g(603), -1000000))),
                storeGuid
            )
            val peerEnvelope = DebtEnvelope.encode(DebtEnvelopePacket(payReqPeer, storeGuid, "", peerPayWire, ""), storeGuid)

            assertEquals(DebtReceiveStatus.Applied, inbox.receive(peerEnvelope, 500))
            val accounts = repo.readAccounts(custGuid)
            assertEquals(1, accounts.size)
            assertEquals("Balance reduced by remote offline payment", 2000000L, accounts[0].balanceMinor)

        } finally {
            db.close()
            context.deleteDatabase(dbName)
        }
    }
}
