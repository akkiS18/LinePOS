package uz.pos.electro.data.business

import android.database.sqlite.SQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.model.PaymentType
import java.util.UUID

class DebtSourceEnvelopeTest {
    private fun g(n: Int) = "00000000-0000-0000-0000-" + n.toString().padStart(12, '0')
    private fun repo(db: AppDatabase) = DebtRepository(db, g(1), g(2)) { true }
    private fun inbox(db: AppDatabase, map: (String) -> Long? = { if (it == g(2)) 1L else null }) =
        DebtEnvelopeInbox(db, g(1), { true }, { true }, map)
    private fun sql(db: AppDatabase) = db.openHelper.writableDatabase
    private fun exec(db: AppDatabase, s: String) = sql(db).execSQL(s)
    private fun value(db: AppDatabase, s: String) = DebtRepository.scalar(sql(db), s)
    private fun count(db: AppDatabase, table: String) = value(db, "SELECT COUNT(*) FROM $table") as Long
    private fun user(db: AppDatabase) = exec(db, "INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
    private fun product(db: AppDatabase, initialStock: Double = 5.0) =
        exec(db, "INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('${g(11)}','Initial Product',60,100,$initialStock,'METR',500)")
    private fun warehouse(db: AppDatabase) =
        exec(db, "INSERT INTO warehouses(guid,name,updated_at) VALUES('${g(12)}','Main Warehouse',500),('${g(14)}','Other',500)")
    private fun stocks(db: AppDatabase, initialQuantity: Double = 1.0) =
        exec(db, "INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('${g(11)}','${g(12)}',$initialQuantity,500),('${g(11)}','${g(14)}',4,500)")
    private fun dependencies(db: AppDatabase, initialProductStock: Double = 5.0, initialWhStock: Double = 1.0) {
        user(db); product(db, initialProductStock); warehouse(db); stocks(db, initialWhStock)
    }

    private suspend fun reject(action: suspend () -> Unit) {
        try { action() }
        catch (_: IllegalArgumentException) { return }
        catch (_: IllegalStateException) { return }
        catch (_: android.database.sqlite.SQLiteException) { return }
        fail("Expected rejection did not occur")
    }

    @Test fun sourceCommitExportPeerReceiveAndSourceEcho() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val dbs = mutableListOf<AppDatabase>()
        val names = mutableListOf<String>()
        suspend fun fresh(): AppDatabase {
            val name = "debt-source-${UUID.randomUUID()}.db"
            names.add(name)
            val db = AppDatabase.buildDatabase(context, scope, name)
            dbs.add(db)
            repo(db).bindStore()
            return db
        }

        try {
            // 1. Full cycle: Customer + Sale + Payment
            val db1 = fresh(); val db2 = fresh()
            dependencies(db1); dependencies(db2)
            val repo1 = repo(db1); val repo2 = repo(db2)
            val inbox1 = inbox(db1); val inbox2 = inbox(db2)

            // Customer creation
            val custDraft = DebtCustomerDraft(g(3), "Hasan Aka", "+998901112233", "doimiy", 100)
            assertEquals(g(3), repo1.createCustomer(custDraft))
            val custWire = inbox1.exportApplied(g(3))
            assertNotNull(custWire)
            assertEquals(DebtReceiveStatus.Applied, inbox2.receive(custWire!!, 101L))
            assertEquals(DebtReceiveStatus.AlreadyApplied, inbox1.receive(custWire, 102L))
            assertEquals(1L, count(db1, "debt_customers"))
            assertEquals(1L, count(db2, "debt_customers"))

            // Sale snapshot & command
            val saleSnapshot = DebtSaleSnapshot(g(4), 200, 20000, 12000, 2000, 3000, 60, "2", "12000", "DEBT", listOf(
                DebtSaleItem(g(10), g(11), "Old cable", "Category", "METR", g(12), "Main Warehouse", "1.25", "100", "0.005", "USD", g(13), "-1.25"),
                DebtSaleItem(g(20), g(11), "Old cable", "Category", "METR", g(12), "Main Warehouse", "0.75", "100", "60", "UZS", g(23), "-0.75")
            ))
            val openCmd = DebtOpenSaleCommand(g(5), g(3), saleSnapshot, "2030-01-01", null, 1L)
            assertEquals(g(5), repo1.openSale(openCmd))

            // Verify local DB 1 mutations & Room native DAO read
            assertEquals(1L, count(db1, "sales"))
            assertEquals(2L, count(db1, "sale_items"))
            val saleId = value(db1, "SELECT id FROM sales WHERE guid='${g(4)}'") as Long
            val saleWithItems = db1.saleDao().getSaleWithItemsById(saleId)
            assertNotNull(saleWithItems)
            assertEquals(PaymentType.DEBT, saleWithItems!!.sale.paymentType)
            assertEquals(2, saleWithItems.items.size)
            assertEquals(-1.0, (value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)
            assertEquals(3.0, (value(db1, "SELECT stock_quantity FROM products WHERE guid='${g(11)}'") as Number).toDouble(), 0.0001)
            assertEquals(1L, count(db1, "debt_events"))
            assertEquals(15000L, repo1.readAccounts(g(3)).single().balanceMinor)
            assertEquals(0L, value(db1, "SELECT COUNT(*) FROM sync_journal WHERE group_id='${g(5)}' AND acked<>-1"))

            // Export from DB 1 and Receive in DB 2
            val saleWire = inbox1.exportApplied(g(5))
            assertNotNull(saleWire)
            assertEquals(DebtReceiveStatus.Applied, inbox2.receive(saleWire!!, 201L))

            // Verify peer DB 2 mutations
            assertEquals(1L, count(db2, "sales"))
            assertEquals(2L, count(db2, "sale_items"))
            assertEquals(-1.0, (value(db2, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)
            assertNull(inbox2.readPending(g(5)))
            assertEquals(15000L, repo2.readAccounts(g(3)).single().balanceMinor)

            // Source echo
            assertEquals(DebtReceiveStatus.AlreadyApplied, inbox1.receive(saleWire, 202L))
            assertEquals(-1.0, (value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)
            assertEquals(1L, count(db1, "sales"))

            // Source replay & conflict
            assertEquals(g(5), repo1.openSale(openCmd))
            assertEquals(-1.0, (value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)
            reject { repo1.openSale(openCmd.copy(dueDate = "2031-01-01")) }

            // Payment
            val payCmd = DebtPaymentCommand(g(6), g(3), 5000, 0, 0, 300)
            assertEquals(g(6), repo1.takePayment(payCmd))
            assertEquals(10000L, repo1.readAccounts(g(3)).single().balanceMinor)

            val payWire = inbox1.exportApplied(g(6))
            assertNotNull(payWire)
            assertEquals(DebtReceiveStatus.Applied, inbox2.receive(payWire!!, 301L))
            assertEquals(10000L, repo2.readAccounts(g(3)).single().balanceMinor)

            // Payment source echo
            assertEquals(DebtReceiveStatus.AlreadyApplied, inbox1.receive(payWire, 302L))
            assertEquals(10000L, repo1.readAccounts(g(3)).single().balanceMinor)

            // 2. Inline Customer creation inside openSale
            val db3 = fresh(); val db4 = fresh()
            dependencies(db3); dependencies(db4)
            val repo3 = repo(db3); val inbox3 = inbox(db3); val inbox4 = inbox(db4)

            val inlineCustomer = DebtCustomerDraft(g(30), "Inline Xaridor", "+998909999999", "inline", 400)
            val inlineSaleSnapshot = DebtSaleSnapshot(g(40), 400, 20000, 12000, 2000, 3000, 60, "2", "12000", "DEBT", listOf(
                DebtSaleItem(g(110), g(11), "Old cable", "Category", "METR", g(12), "Main Warehouse", "2", "100", "60", "UZS", g(123), "-2")
            ))
            val inlineOpenCmd = DebtOpenSaleCommand(g(50), g(30), inlineSaleSnapshot, "2030-01-01", inlineCustomer, 1L)
            assertEquals(g(50), repo3.openSale(inlineOpenCmd))

            val inlineWire = inbox3.exportApplied(g(50))
            assertNotNull(inlineWire)
            assertEquals(DebtReceiveStatus.Applied, inbox4.receive(inlineWire!!, 401L))
            assertEquals(1L, count(db4, "debt_customers"))
            assertEquals(1L, count(db4, "sales"))
            assertEquals(DebtReceiveStatus.AlreadyApplied, inbox3.receive(inlineWire, 402L))

            // 3. Concurrent double submit
            val db5 = fresh()
            dependencies(db5, 20.0, 20.0)
            val repo5 = repo(db5)
            repo5.createCustomer(DebtCustomerDraft(g(31), "Concurrent Customer", "", "", 500))
            val concSale = DebtSaleSnapshot(g(41), 500, 10000, 6000, 1000, 0, 0, "0", "12000", "DEBT", listOf(
                DebtSaleItem(g(111), g(11), "Cable", "Category", "METR", g(12), "Warehouse", "1", "100", "60", "UZS", g(124), "-1")
            ))
            val concCmd = DebtOpenSaleCommand(g(51), g(31), concSale, null, null, 1L)
            awaitAll(
                async(Dispatchers.IO) { repo5.openSale(concCmd) },
                async(Dispatchers.IO) { repo5.openSale(concCmd) }
            )
            assertEquals(1L, count(db5, "sales"))
            assertEquals(19.0, (value(db5, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)

            // 4. Historical metadata immutability: mutating customer/product after sale doesn't alter frozen envelope
            exec(db5, "UPDATE debt_customers SET name='Mutated Customer Name', note='Mutated Note' WHERE guid='${g(31)}'")
            exec(db5, "UPDATE products SET name='Mutated Product Name', selling_price=99999 WHERE guid='${g(11)}'")
            val frozenWire = inbox(db5).exportApplied(g(51))
            assertNotNull(frozenWire)
            val decodedPacket = DebtEnvelope.decode(frozenWire!!, g(1))
            val decodedSale = DebtEnvelope.decodeSale(decodedPacket.saleWire)
            assertEquals("Cable", decodedSale.items[0].productName)
            assertEquals("100", decodedSale.items[0].price)
            assertEquals(g(51), repo5.openSale(concCmd))

            // 5. Full rollback on failure
            val db6 = fresh()
            dependencies(db6, 10.0, 10.0)
            val repo6 = repo(db6)
            repo6.createCustomer(DebtCustomerDraft(g(32), "Rollback Customer", "", "", 600))
            exec(db6, "CREATE TRIGGER fail_envelope BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected envelope failure'); END;")
            val failSale = DebtSaleSnapshot(g(42), 600, 10000, 6000, 1000, 0, 0, "0", "12000", "DEBT", listOf(
                DebtSaleItem(g(112), g(11), "Cable", "Category", "METR", g(12), "Warehouse", "1", "100", "60", "UZS", g(125), "-1")
            ))
            val failCmd = DebtOpenSaleCommand(g(52), g(32), failSale, null, null, 1L)
            reject { repo6.openSale(failCmd) }
            assertEquals(0L, count(db6, "sales"))
            assertEquals(0L, count(db6, "sale_items"))
            assertEquals(0L, count(db6, "debt_events"))
            assertEquals(0L, count(db6, "debt_accounts"))
            assertEquals(0L, count(db6, "debt_command_receipts"))
            assertEquals(0L, value(db6, "SELECT COUNT(*) FROM sync_journal WHERE group_id='${g(52)}'"))
            assertEquals(10.0, (value(db6, "SELECT quantity FROM product_stocks WHERE product_guid='${g(11)}' AND warehouse_guid='${g(12)}'") as Number).toDouble(), 0.0001)
            assertEquals("", value(db6, "SELECT current_group FROM sync_control"))

            // 6. Preflight rejections before commit (size, items, precision loss)
            val db7 = fresh()
            dependencies(db7, 10.0, 10.0)
            val repo7 = repo(db7)
            repo7.createCustomer(DebtCustomerDraft(g(33), "Preflight Customer", "", "", 700))

            val itemsList = (0..1000).map { i ->
                DebtSaleItem(g(2000 + i), g(11), "C", "C", "U", g(12), "W", "1", "1", "1", "UZS", g(4000 + i), "-1")
            }
            val tooManyItems = DebtSaleSnapshot(g(43), 700, 100100, 100100, 0, 0, 0, "0", "12000", "DEBT", itemsList)
            reject { repo7.openSale(DebtOpenSaleCommand(g(53), g(33), tooManyItems, null, null, 1L)) }
            assertEquals(0L, count(db7, "sales"))

            val unsafeSale = concSale.copy(
                totalMinor = 999999999999000001L,
                items = listOf(DebtSaleItem(g(113), g(11), "Cable", "Category", "METR", g(12), "Warehouse", "1", "999999999999.00000001", "60", "UZS", g(126), "-1"))
            )
            reject { repo7.openSale(DebtOpenSaleCommand(g(54), g(33), unsafeSale, null, null, 1L)) }
            assertEquals(0L, count(db7, "sales"))
            reject { DebtSaleReceiver.real(1000000000000000001.toBigDecimal()) }
        } finally {
            for (db in dbs) db.close()
            scope.cancel()
            for (name in names) SQLiteDatabase.deleteDatabase(context.getDatabasePath(name))
        }
    }
}
