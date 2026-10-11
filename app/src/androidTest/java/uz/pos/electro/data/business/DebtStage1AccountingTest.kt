package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.repository.CurrencyRepository
import uz.pos.electro.data.repository.TaxSettingsRepository
import java.math.BigDecimal
import java.util.UUID

class DebtStage1AccountingTest {
    @Test fun configuredFeesHistoricalFxRetryAndSignedReversal() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val name = "debt-stage1-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, name)
        val tax = TaxSettingsRepository(context)
        val currency = CurrencyRepository(context)
        val oldTax = tax.getCardTaxRate(); val oldRate = currency.getCachedUsdRate()
        val service = DebtService(db, context, tax, currency)
        fun guid() = UUID.randomUUID().toString()
        try {
            tax.setCardTaxRate(1.8); currency.updateCachedRate(12000.0)
            val sql = db.openHelper.writableDatabase
            sql.execSQL("INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
            val product = guid(); val warehouse = guid()
            sql.execSQL("INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('$product','Cable',50000,100000,10,'METR',100)")
            sql.execSQL("INSERT INTO warehouses(guid,name,updated_at) VALUES('$warehouse','Main',100)")
            sql.execSQL("INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('$product','$warehouse',10,100)")
            val customer = service.createCustomer("Fee test", "", "")
            val snapshot = DebtService.buildSaleSnapshot(guid(),1000,
                listOf(DebtCartItemDto(product,"Cable","Cable","METR",warehouse,"Main",1.0,100000.0,50000.0,"UZS")),
                0,0,12000.0,0.0)
            service.openDebtSale(guid(),customer,snapshot)
            val request = guid()
            service.recordPayment(customer,4000000,6000000,requestGuid=request,occurredAt=2000)
            val paid = service.getDebtPeriodSummary(2000,3000)
            assertEquals(100000.0, paid.netDebtCollectedTotalUz, 0.0)
            assertEquals(1080.0, paid.netDebtFeeUz, 0.0)
            assertEquals(.09, paid.netDebtFeeUsd!!, 1e-12)
            assertEquals(0L, paid.closingDebtMinor)
            tax.setCardTaxRate(3.0); currency.updateCachedRate(24000.0)
            assertEquals(request, service.recordPayment(customer,4000000,6000000,requestGuid=request))
            assertEquals(100000.0, service.getDebtPeriodSummary(2000,3000).netDebtCollectedTotalUz, 0.0)
            var rejected = false
            try { service.recordPayment(customer,4000001,6000000,requestGuid=request) }
            catch (_: IllegalArgumentException) { rejected = true }
            assertTrue("Changed replay amount must fail", rejected)
            val store = DebtRepository.scalar(sql,"SELECT store_guid FROM debt_scope WHERE id=1") as String
            val actor = DebtRepository.scalar(sql,"SELECT value FROM sync_meta WHERE key='mobile_actor_guid'") as String
            DebtRepository(db,store,actor){true}.reversePayment(DebtPaymentReversalCommand(guid(),request,"Correction",54000,3000))
            val reversed = service.getDebtPeriodSummary(2000,4000)
            assertEquals(0.0, reversed.netDebtCollectedTotalUz, 0.0)
            assertEquals(540.0, reversed.netDebtFeeUz, 0.0)
            assertEquals(.045, reversed.netDebtFeeUsd!!, 1e-12)
            assertEquals(10000000L, reversed.closingDebtMinor)
            val reversalOnly = service.getDebtPeriodSummary(3000,4000)
            assertEquals(-100000.0, reversalOnly.netDebtCollectedTotalUz, 0.0)
            assertEquals(-540.0, reversalOnly.netDebtFeeUz, 0.0)
            assertEquals(100000.0, service.getDebtPeriodSummary(2000,3000).netDebtCollectedTotalUz, 0.0)
            service.recordPayment(customer,10000000,0,requestGuid=guid(),occurredAt=4000)
            assertEquals(0.0, service.getDebtPeriodSummary(4000,5000).netDebtFeeUz, 0.0)
            assertEquals(1L, DebtService.calculatePaymentFee(25,BigDecimal("2")))
            assertEquals(Long.MAX_VALUE, DebtService.calculatePaymentFee(Long.MAX_VALUE,BigDecimal("100")))
        } finally {
            tax.setCardTaxRate(oldTax); currency.updateCachedRate(oldRate)
            db.close(); context.deleteDatabase(name); scope.cancel()
        }
    }
}
