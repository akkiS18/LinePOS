package uz.pos.electro.data.repository

import androidx.room.withTransaction
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
        taxRate: Double = 0.0
    ): Long {
        val (saleId, savedEntity, savedItems) = database.withTransaction {
            val currentUsdRate = currencyRepository.getCachedUsdRate()
            val totalAmount = items.sumOf { it.priceAtSale * it.quantity }
            val totalCost = items.sumOf { item ->
                val costInUzs = if (item.product.costCurrency == "USD") {
                    item.product.costPrice * currentUsdRate
                } else {
                    item.product.costPrice
                }
                costInUzs * item.quantity
            }

            val saleGuid = java.util.UUID.randomUUID().toString()
            val now = System.currentTimeMillis()

            val saleEntity = SaleEntity(
                guid = saleGuid,
                totalAmount = totalAmount,
                totalCost = totalCost,
                paymentType = paymentType,
                cashAmount = if (cashAmount == 0.0 && cardAmount == 0.0 && paymentType == PaymentType.CASH) totalAmount else cashAmount,
                cardAmount = if (cashAmount == 0.0 && cardAmount == 0.0 && paymentType == PaymentType.CARD) totalAmount else cardAmount,
                taxAmount = taxAmount,
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

            val saleId = saleDao.insertSaleWithItems(saleEntity, saleItems)

            Triple(saleId, saleEntity, saleItems)
        }

        // Kompyuterga zudlik bilan jonli uzatish (fonda)
        localSyncManager.sendLiveSale(savedEntity, savedItems)

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
        val productsMap = productDao.getAllProductsList().associateBy { it.id }
        val currentUsdRate = currencyRepository.getCachedUsdRate()

        val reportItems = mutableListOf<SaleReportItem>()

        for (saleWithItems in sales) {
            val sale = saleWithItems.sale
            for (saleItem in saleWithItems.items) {
                val product = productsMap[saleItem.productId]
                val category = product?.category ?: "Boshqa"
                val whName = saleItem.warehouseName.ifBlank { "Asosiy ombor" }
                val whGuid = saleItem.warehouseGuid

                // Filtrlash: Kategoriya
                if (!categoryFilter.isNullOrBlank() && categoryFilter != "Barchasi" && !category.equals(categoryFilter, ignoreCase = true)) {
                    continue
                }
                // Filtrlash: Ombor
                if (!warehouseGuidFilter.isNullOrBlank() && warehouseGuidFilter != "Barchasi" && whGuid.isNotBlank() && whGuid != warehouseGuidFilter) {
                    continue
                }

                val productName = product?.name ?: saleItem.productName.ifBlank { "Mahsulot #${saleItem.productId}" }
                val unitType = product?.unitType ?: UnitType.DONA

                val costInUzs = if (saleItem.costCurrency == "USD") {
                    saleItem.costAtSale * currentUsdRate
                } else {
                    saleItem.costAtSale
                }

                val totalPrice = saleItem.priceAtSale * saleItem.quantity
                val totalCost = costInUzs * saleItem.quantity
                val profit = totalPrice - totalCost

                reportItems.add(
                    SaleReportItem(
                        saleId = sale.id,
                        productId = saleItem.productId,
                        productName = productName,
                        quantity = saleItem.quantity,
                        unitType = unitType,
                        costPrice = costInUzs,
                        sellingPrice = saleItem.priceAtSale,
                        totalPrice = totalPrice,
                        profit = profit,
                        category = category,
                        warehouseName = whName,
                        warehouseGuid = whGuid,
                        timestamp = sale.createdAt
                    )
                )
            }
        }

        return reportItems
    }
}
