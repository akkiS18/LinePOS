package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.converter.Converters
import uz.pos.electro.data.local.entity.*
import uz.pos.electro.data.model.*
import uz.pos.electro.data.repository.*
import uz.pos.electro.data.sync.LegacySalePaymentType
import uz.pos.electro.data.sync.LocalSyncManager
import java.util.UUID

class DebtPaymentTypeTest {
    private fun rejected(action: () -> Unit) {
        try { action() } catch (_: IllegalArgumentException) { return }
        fail("Debt accepted by sale-only path")
    }

    @Test fun legacySaleOnlyProtocolCannotMislabelDebtAsCash() {
        for (raw in listOf<Any>(3, 3L, 3.0, "DEBT", "debt", " DEBT ", "3")) {
            rejected { LegacySalePaymentType.decode(raw) }
        }
        rejected { LegacySalePaymentType.encode(PaymentType.DEBT) }
        val codes = mapOf(PaymentType.CASH to 0, PaymentType.CARD to 1, PaymentType.SPLIT to 2,
            PaymentType.BRAK to 6, PaymentType.RETURN to 7, PaymentType.RETURN_REVERSAL to 8)
        for ((type, code) in codes) {
            assertEquals(code, LegacySalePaymentType.encode(type))
            assertEquals(type, LegacySalePaymentType.decode(code))
            assertEquals(type, LegacySalePaymentType.decode(type.name))
            assertEquals(type, Converters().toPaymentType(Converters().fromPaymentType(type)))
        }
        assertEquals(PaymentType.DEBT, Converters().toPaymentType("DEBT"))
    }

    @Test fun roomReopensDebtReceiptWithoutInventingCashOrCustomerAccount() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val name = "debt-type-${UUID.randomUUID()}.db"
        var db = AppDatabase.buildDatabase(context, scope, name)
        try {
            val guid = UUID.randomUUID().toString()
            val id = db.saleDao().insertSaleWithItems(SaleEntity(guid = guid, totalAmount = 100.0,
                totalCost = 60.0, paymentType = PaymentType.DEBT, cashAmount = 20.0, cardAmount = 10.0,
                taxAmount = 0.2, taxRate = 2.0, usdRate = 12000.0, createdAt = 1),
                listOf(SaleItemEntity(saleId = 0, productId = 1, productName = "Historical name",
                    categoryAtSale = "Cable", unitAtSale = "DONA", quantity = 1.0,
                    priceAtSale = 100.0, costAtSale = 60.0)))
            db.close()
            db = AppDatabase.buildDatabase(context, scope, name)
            val receipt = db.saleDao().getSaleWithItemsById(id)!!
            assertEquals(PaymentType.DEBT, receipt.sale.paymentType)
            assertEquals(PaymentType.DEBT, db.saleDao().getSalesListBetween(0, 2).single().sale.paymentType)
            val summary = SaleAccounting.summary(SaleAccounting.lines(receipt), 99999.0)
            assertEquals(100.0, summary.totalRevenue, 0.0)
            assertEquals(20.0, summary.totalCashAmount, 0.0)
            assertEquals(10.0, summary.totalCardAmount, 0.0)
            assertEquals(39.8, summary.netProfit, 0.000001)
            assertEquals(39.8 / 12000.0, summary.netProfitUsd, 0.000001)
            assertEquals(receipt.sale.receiptNumber, receipt.sale.copy(id = id + 100).receiptNumber)
            for (table in listOf("debt_customers", "debt_accounts", "debt_events")) {
                db.openHelper.writableDatabase.query("SELECT COUNT(*) FROM $table").use {
                    assertTrue(it.moveToFirst()); assertEquals(0, it.getInt(0))
                }
            }
        } finally { db.close(); scope.cancel(); context.deleteDatabase(name) }
    }

    @Test fun ordinaryRepositoryRejectsDebtBeforeAnySaleStockOrJournalWrite() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val name = "debt-checkout-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, name)
        try {
            val currency = CurrencyRepository(context)
            val sync = LocalSyncManager(context, db, db.productDao(), db.saleDao(), db.warehouseDao(),
                db.productStockDao(), currency, TaxSettingsRepository(context))
            val repo = SaleRepository(db, db.saleDao(), db.productDao(), db.warehouseDao(),
                db.productStockDao(), currency, sync)
            val product = ProductEntity(name = "Cable", costPrice = 60.0, sellingPrice = 100.0,
                stockQuantity = 4.0, unitType = UnitType.DONA)
            val sql = db.openHelper.writableDatabase
            val before = sql.query("SELECT COUNT(*) FROM sync_journal").use { it.moveToFirst(); it.getLong(0) }
            // Even a fully paid amount must not bypass the ledger by using the DEBT enum.
            for (cash in listOf(20.0, 100.0)) {
                var failed = false
                try { repo.completeSale(listOf(CartItemModel(product, 1.0, 100.0)),
                    paymentType = PaymentType.DEBT, cashAmount = cash) }
                catch (e: IllegalArgumentException) { failed = e.message!!.contains("qarz daftari") }
                assertTrue(failed)
            }
            assertEquals(0, db.saleDao().getAllSalesCount())
            sql.query("SELECT COUNT(*) FROM sync_journal").use { it.moveToFirst(); assertEquals(before, it.getLong(0)) }
            sql.query("SELECT applying,current_group FROM sync_control WHERE id=1").use {
                it.moveToFirst(); assertEquals(0, it.getInt(0)); assertEquals("", it.getString(1))
            }
            for (type in listOf(PaymentType.CASH, PaymentType.CARD, PaymentType.SPLIT, PaymentType.BRAK))
                type.requireOrdinaryCheckout()
            rejected { PaymentType.RETURN.requireOrdinaryCheckout() }
            rejected { PaymentType.RETURN_REVERSAL.requireOrdinaryCheckout() }
        } finally { db.close(); scope.cancel(); context.deleteDatabase(name) }
    }
}
