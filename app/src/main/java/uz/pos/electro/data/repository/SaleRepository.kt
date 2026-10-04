package uz.pos.electro.data.repository

import androidx.room.withTransaction
import uz.pos.electro.data.model.SaleAccounting
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.SaleDao
import uz.pos.electro.data.local.dao.WarehouseDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.CartItemModel
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.ReportsSummary
import uz.pos.electro.data.model.SaleReportItem
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.sync.LocalSyncManager
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SaleRepository @Inject constructor(
    private val database: AppDatabase,
    private val saleDao: SaleDao,
    private val productDao: ProductDao,
    private val warehouseDao: WarehouseDao,
    private val productStockDao: ProductStockDao,
    private val currencyRepository: CurrencyRepository,
    private val localSyncManager: LocalSyncManager
) {
    fun getHistoricalCategories() = saleDao.getHistoricalCategories()

    fun getAllSales(): Flow<List<SaleWithItems>> = saleDao.getAllSalesWithItems()

    fun getSalesBetween(startTimestamp: Long, endTimestamp: Long): Flow<List<SaleWithItems>> =
        saleDao.getSalesBetween(startTimestamp, endTimestamp)

    suspend fun getSaleById(id: Long): SaleWithItems? = saleDao.getSaleWithItemsById(id)

    /**
     * Savdoni yakunlash:
     * 1. Savdo va uning mahsulotlarini bazaga saqlash (valyuta konvertatsiyasi bilan).
     * 2. Ombordan sotilgan tovarlar sonini ayirish (Variant 1: Asosiy ombordan, yetmagani ikkinchi ombordan auto-split).
     */
    suspend fun completeSale(
        items: List<CartItemModel>,
        userId: Long = 1L,
        paymentType: PaymentType = PaymentType.CASH,
        cashAmount: Double = 0.0,
        cardAmount: Double = 0.0,
        taxAmount: Double = 0.0,
        taxRate: Double = 0.0,
        saleGuid: String = java.util.UUID.randomUUID().toString()
    ): Long {
        require(items.isNotEmpty() && items.all { it.quantity.isFinite() && it.quantity > 0 && it.priceAtSale.isFinite() && it.priceAtSale >= 0 && it.product.costPrice.isFinite() && it.product.costPrice >= 0 }) { "Miqdor yoki narx noto‘g‘ri" }
        require(taxRate.isFinite() && taxRate in 0.0..100.0 && cashAmount.isFinite() && cardAmount.isFinite() && cashAmount >= 0 && cardAmount >= 0) { "To‘lov summasi noto‘g‘ri" }
        val (saleId, savedEntity, savedItems) = database.withTransaction {
            database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group=? WHERE id=1", arrayOf(java.util.UUID.randomUUID().toString()))
            val currentUsdRate = currencyRepository.getCachedUsdRate()
            require(currentUsdRate.isFinite() && currentUsdRate > 0) { "Dollar kursi noto‘g‘ri" }
            val totalAmount = SaleAccounting.money(items.sumOf { it.totalPrice })
            val totalCost = SaleAccounting.money(items.sumOf { item ->
                val costInUzs = if (item.product.costCurrency == "USD") {
                    item.product.costPrice * currentUsdRate
                } else {
                    item.product.costPrice
                }
                costInUzs * item.quantity
            })

            val paidCash = when (paymentType) { PaymentType.CASH -> totalAmount; PaymentType.BRAK -> 0.0; else -> SaleAccounting.money(cashAmount) }
            val paidCard = when (paymentType) { PaymentType.CARD -> totalAmount; PaymentType.BRAK -> 0.0; else -> SaleAccounting.money(cardAmount) }
            require(paymentType == PaymentType.BRAK || SaleAccounting.money(paidCash + paidCard) == totalAmount) { "To‘lov jami chek summasiga teng emas" }
            val now = System.currentTimeMillis()

            val saleEntity = SaleEntity(
                guid = saleGuid,
                usdRate = currentUsdRate,
                totalAmount = totalAmount,
                totalCost = totalCost,
                paymentType = paymentType,
                cashAmount = paidCash,
                cardAmount = paidCard,
                taxAmount = SaleAccounting.money(paidCard * taxRate / 100.0),
                taxRate = taxRate,
                createdAt = now,
                userId = userId,
                isSynced = false
            )

            val primaryWh = warehouseDao.getPrimaryWarehouse()
                ?: warehouseDao.getAllWarehousesList().firstOrNull()
            val primaryGuid = primaryWh?.guid ?: "main-default-warehouse"
            val primaryName = primaryWh?.name ?: "Do'kondagi ombor"
            val allWarehouses = warehouseDao.getAllWarehousesList()

            val saleItems = mutableListOf<SaleItemEntity>()

            for (item in items) {
                var remainingNeeded = item.quantity
                val pGuid = item.product.guid

                val primaryStock = productStockDao.getProductStockInWarehouse(pGuid, primaryGuid) ?: 0.0

                if (primaryStock >= remainingNeeded) {
                    productStockDao.deductStock(pGuid, primaryGuid, remainingNeeded, now)
                    saleItems.add(
                        SaleItemEntity(
                            saleId = 0L,
                            saleGuid = saleGuid,
                            productId = item.product.id,
                            productGuid = item.product.guid,
                            productName = item.product.name,
                            categoryAtSale = item.product.category,
                            unitAtSale = item.product.unitType.name,
                            quantity = remainingNeeded,
                            priceAtSale = item.priceAtSale,
                            costAtSale = item.product.costPrice,
                            costCurrency = item.product.costCurrency,
                            warehouseGuid = primaryGuid,
                            warehouseName = primaryName
                        )
                    )
                    remainingNeeded = 0.0
                } else {
                    if (primaryStock > 0.0) {
                        productStockDao.deductStock(pGuid, primaryGuid, primaryStock, now)
                        saleItems.add(
                            SaleItemEntity(
                                saleId = 0L,
                                saleGuid = saleGuid,
                                productId = item.product.id,
                                productGuid = item.product.guid,
                                productName = item.product.name,
                            categoryAtSale = item.product.category,
                            unitAtSale = item.product.unitType.name,
                                quantity = primaryStock,
                                priceAtSale = item.priceAtSale,
                                costAtSale = item.product.costPrice,
                                costCurrency = item.product.costCurrency,
                                warehouseGuid = primaryGuid,
                                warehouseName = primaryName
                            )
                        )
                        remainingNeeded -= primaryStock
                    }

                    // Check other warehouses
                    val otherWarehouses = allWarehouses.filter { it.guid != primaryGuid }
                    for (secWh in otherWarehouses) {
                        if (remainingNeeded <= 0.0) break
                        val secStock = productStockDao.getProductStockInWarehouse(pGuid, secWh.guid) ?: 0.0
                        if (secStock > 0.0) {
                            val take = minOf(secStock, remainingNeeded)
                            productStockDao.deductStock(pGuid, secWh.guid, take, now)
                            saleItems.add(
                                SaleItemEntity(
                                    saleId = 0L,
                                    saleGuid = saleGuid,
                                    productId = item.product.id,
                                    productGuid = item.product.guid,
                                    productName = item.product.name,
                            categoryAtSale = item.product.category,
                            unitAtSale = item.product.unitType.name,
                                    quantity = take,
                                    priceAtSale = item.priceAtSale,
                                    costAtSale = item.product.costPrice,
                                    costCurrency = item.product.costCurrency,
                                    warehouseGuid = secWh.guid,
                                    warehouseName = secWh.name
                                )
                            )
                            remainingNeeded -= take
                        }
                    }

                    // If still remaining, deduct from primary warehouse (e.g. 0 stock or negative)
                    if (remainingNeeded > 0.0) {
                        productStockDao.deductStock(pGuid, primaryGuid, remainingNeeded, now)
                        saleItems.add(
                            SaleItemEntity(
                                saleId = 0L,
                                saleGuid = saleGuid,
                                productId = item.product.id,
                                productGuid = item.product.guid,
                                productName = item.product.name,
                            categoryAtSale = item.product.category,
                            unitAtSale = item.product.unitType.name,
                                quantity = remainingNeeded,
                                priceAtSale = item.priceAtSale,
                                costAtSale = item.product.costPrice,
                                costCurrency = item.product.costCurrency,
                                warehouseGuid = primaryGuid,
                                warehouseName = primaryName
                            )
                        )
                        remainingNeeded = 0.0
                    }
                }

                // Ombordagi umumiy qoldiqlarni kamaytirish va o'zgarish vaqtini belgilash
                if (pGuid.isNotBlank()) {
                    productDao.decreaseStockByGuid(pGuid, item.quantity, now)
                } else if (item.product.id > 0) {
                    productDao.decreaseStock(item.product.id, item.quantity, now)
                }
            }

            val saleId = saleDao.insertSaleWithItems(saleEntity, saleItems.mapIndexed { index, item -> item.copy(guid = "${saleEntity.guid}:${index + 1}") })

            database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group='' WHERE id=1")
            Triple(saleId, saleEntity, saleItems)
        }

        // Kompyuterga zudlik bilan jonli uzatish (fonda)
        runCatching { localSyncManager.sendLiveSale(savedEntity, savedItems) }

        return saleId
    }

    /**
     * Tanlangan vaqt oralig'idagi barcha savdolarni tovarlar tafsilotlari bilan olish
     */
    suspend fun getDetailedReportItems(
        startTimestamp: Long,
        endTimestamp: Long,
        categoryFilter: String? = null,
        warehouseGuidFilter: String? = null
    ): List<SaleReportItem> {
        val sales = saleDao.getSalesListBetween(startTimestamp, endTimestamp)
        return sales.flatMap { uz.pos.electro.data.model.SaleAccounting.lines(it) }.filter { item ->
            (categoryFilter.isNullOrBlank() || categoryFilter == "Barchasi" || item.category.equals(categoryFilter, true)) &&
            (warehouseGuidFilter.isNullOrBlank() || warehouseGuidFilter == "Barchasi" || item.warehouseGuid == warehouseGuidFilter)
        }
    }
}
