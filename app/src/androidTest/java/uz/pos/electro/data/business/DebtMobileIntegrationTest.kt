package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.DebtCartItemDto
import uz.pos.electro.data.debt.DebtFilter
import uz.pos.electro.data.debt.DebtService
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.model.HeldCart
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.UnitType
import java.util.UUID

class DebtMobileIntegrationTest {

    @Test
    fun testCustomerLifecycleAndActiveFilter() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val dbName = "debt-mobile-cust-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, dbName)
        val debtService = DebtService(db, context)

        try {
            // 1. Create customer
            val custGuid = debtService.createCustomer("Ali Valiyev", "+998901234567", "Doimiy xaridor")
            assertNotNull(custGuid)

            val list1 = debtService.getCustomerList("", DebtFilter.ALL)
            val created = list1.find { it.guid == custGuid }
            assertNotNull(created)
            assertEquals("Ali Valiyev", created!!.name)
            assertEquals("+998901234567", created.phone)
            assertFalse(created.archived)

            // 2. Update customer
            val updated = debtService.updateCustomer(
                customerGuid = custGuid,
                name = "Ali Valiyev (VIP)",
                phone = "+998909999999",
                note = "Yangi izoh",
                revision = created.revision
            )
            assertTrue(updated)

            val list2 = debtService.getCustomerList("", DebtFilter.ALL)
            val updatedItem = list2.find { it.guid == custGuid }!!
            assertEquals("Ali Valiyev (VIP)", updatedItem.name)
            assertEquals("+998909999999", updatedItem.phone)

            // 3. Archive customer
            val archived = debtService.archiveCustomer(custGuid, true)
            assertTrue(archived)

            val activeCustomers = debtService.getActiveCustomers()
            assertNull(activeCustomers.find { it.guid == custGuid })

            // Unarchive
            debtService.archiveCustomer(custGuid, false)
            val activeAgain = debtService.getActiveCustomers()
            assertNotNull(activeAgain.find { it.guid == custGuid })
        } finally {
            db.close()
            context.deleteDatabase(dbName)
        }
    }

    @Test
    fun testOfflineDebtSaleWithNegativeStockAllowanceAndAdvances() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val dbName = "debt-mobile-sale-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, dbName)
        val debtService = DebtService(db, context)

        try {
            // Seed a product with stock = 2.0
            val prodGuid = UUID.randomUUID().toString()
            val prodId = db.productDao().insertProduct(
                ProductEntity(
                    guid = prodGuid,
                    barcode = "88001122",
                    name = "Kabel 3x2.5",
                    category = "Kabellar",
                    costPrice = 10000.0,
                    costCurrency = "UZS",
                    sellingPrice = 15000.0,
                    stockQuantity = 2.0,
                    unitType = UnitType.METR
                )
            )
            assertTrue(prodId > 0)

            val custGuid = debtService.createCustomer("Sardor Qodirov", "+998931112233", "Qarzga oldi")

            // Sell 5.0 units on debt (Initial stock: 2.0, sold: 5.0 -> negative stock allowed: -3.0)
            val saleGuid = UUID.randomUUID().toString()
            val requestGuid = UUID.randomUUID().toString()
            val cartItems = listOf(
                DebtCartItemDto(
                    productGuid = prodGuid,
                    productName = "Kabel 3x2.5",
                    category = "Kabellar",
                    unitDisplay = "METR",
                    warehouseGuid = DebtService.CANONICAL_DEFAULT_WAREHOUSE_GUID,
                    warehouseName = "Asosiy Ombor",
                    quantity = 5.0,
                    priceAtSale = 15000.0,
                    costPrice = 10000.0,
                    costCurrency = "UZS"
                )
            )

            // Total: 75,000 UZS (7,500,000 tiyin)
            // Advance: 20,000 UZS Cash (2,000,000 tiyin), 10,000 UZS Card (1,000,000 tiyin)
            // Remaining debt: 45,000 UZS (4,500,000 tiyin)
            val snapshot = DebtService.buildSaleSnapshot(
                saleGuid = saleGuid,
                occurredAt = System.currentTimeMillis(),
                items = cartItems,
                cashMinor = 2000000L,
                cardMinor = 1000000L,
                usdRate = 12800.0,
                cardTaxRate = 0.0
            )

            assertEquals(7500000L, snapshot.totalMinor)
            assertEquals(2000000L, snapshot.cashMinor)
            assertEquals(1000000L, snapshot.cardMinor)

            val openedSaleGuid = debtService.openDebtSale(
                requestGuid = requestGuid,
                customerGuid = custGuid,
                saleSnapshot = snapshot,
                dueDate = "2026-12-01",
                userId = 1L
            )
            assertEquals(saleGuid, openedSaleGuid)

            // Check details
            val details = debtService.getCustomerDetails(custGuid)
            assertNotNull(details)
            assertEquals(4500000L, details!!.balanceMinor)
            assertEquals(1, details.accounts.size)

            val acc = details.accounts[0]
            assertEquals(4500000L, acc.originalDebtMinor)
            assertEquals(4500000L, acc.balanceMinor)
            assertEquals(0L, acc.totalPaidMinor)
            assertEquals("2026-12-01", acc.dueDate)

            // Check stock deduction in product_stocks (preserves negative stock!)
            val wdb = db.openHelper.readableDatabase
            val stockCur = wdb.query(
                "SELECT quantity FROM product_stocks WHERE product_guid = ? AND warehouse_guid = ?",
                arrayOf(prodGuid, DebtService.CANONICAL_DEFAULT_WAREHOUSE_GUID)
            )
            assertTrue(stockCur.moveToFirst())
            val remainingQty = stockCur.getDouble(0)
            stockCur.close()
            // Initial 2.0 - 5.0 = -3.0
            assertEquals(-3.0, remainingQty, 0.0001)

            // 4. Payment preview and execution
            // Pay 25,000 UZS (2,500,000 tiyin)
            val preview = debtService.previewPayment(
                customerGuid = custGuid,
                paymentMinor = 2500000L
            )
            assertEquals(2500000L, preview.totalPaymentMinor)
            assertEquals(2000000L, preview.remainingBalanceMinor)
            assertEquals(1, preview.lines.size)
            assertEquals(2500000L, preview.lines[0].paidMinor)
            assertEquals(2000000L, preview.lines[0].newBalanceMinor)

            val payReq = UUID.randomUUID().toString()
            debtService.recordPayment(
                customerGuid = custGuid,
                cashMinor = 1500000L,
                cardMinor = 1000000L,
                requestGuid = payReq
            )

            // Verify updated customer balance
            val detailsAfterPay = debtService.getCustomerDetails(custGuid)!!
            assertEquals(2000000L, detailsAfterPay.balanceMinor)
            assertEquals(2500000L, detailsAfterPay.accounts[0].totalPaidMinor)
            assertEquals(2000000L, detailsAfterPay.accounts[0].balanceMinor)
            assertEquals(2, detailsAfterPay.events.size) // 1 sale_open, 1 payment

            // Idempotency: replay same payment command
            val replayedGuid = debtService.recordPayment(
                customerGuid = custGuid,
                cashMinor = 1500000L,
                cardMinor = 1000000L,
                requestGuid = payReq
            )
            assertNotNull(replayedGuid)
            val detailsAfterReplay = debtService.getCustomerDetails(custGuid)!!
            assertEquals(2000000L, detailsAfterReplay.balanceMinor) // Not paid twice!
        } finally {
            db.close()
            context.deleteDatabase(dbName)
        }
    }

    @Test
    fun testHeldCartPreservesDebtFields() {
        val heldCart = HeldCart(
            name = "Mijoz Nasiya Savat",
            items = emptyList(),
            selectedPaymentType = PaymentType.DEBT,
            customerGuid = "cust-123",
            customerName = "Bekzod Umarov",
            dueDate = "2026-11-15",
            debtCashAdvance = "50000",
            debtCardAdvance = "25000"
        )

        assertEquals(PaymentType.DEBT, heldCart.selectedPaymentType)
        assertEquals("cust-123", heldCart.customerGuid)
        assertEquals("Bekzod Umarov", heldCart.customerName)
        assertEquals("2026-11-15", heldCart.dueDate)
        assertEquals("50000", heldCart.debtCashAdvance)
        assertEquals("25000", heldCart.debtCardAdvance)
    }
}
