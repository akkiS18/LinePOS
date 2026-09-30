package uz.pos.electro.data.repository

import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.dao.WarehouseDao
import uz.pos.electro.data.local.entity.ProductStockEntity
import uz.pos.electro.data.local.entity.WarehouseEntity
import uz.pos.electro.data.sync.LocalSyncManager
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

import kotlinx.coroutines.flow.combine

data class WarehouseWithStats(
    val warehouse: WarehouseEntity,
    val productCount: Int,
    val totalQuantity: Double,
    val totalStockValue: Double
)

@Singleton
class WarehouseRepository @Inject constructor(
    private val warehouseDao: WarehouseDao,
    private val productStockDao: ProductStockDao,
    private val productDao: ProductDao,
    private val localSyncManager: LocalSyncManager
) {

    fun getAllWarehouses(): Flow<List<WarehouseEntity>> = warehouseDao.getAllWarehouses()

    fun getWarehousesWithStats(): Flow<List<WarehouseWithStats>> {
        return combine(
            warehouseDao.getAllWarehouses(),
            productStockDao.getAllProductStocks(),
            productDao.getAllProducts()
        ) { warehouses, allStocks, allProducts ->
            val productsByGuid = allProducts.filter { !it.isDeleted }.associateBy { it.guid }
            val stocksByWh = allStocks.groupBy { it.warehouseGuid }

            warehouses.map { wh ->
                val whStocks = (stocksByWh[wh.guid] ?: emptyList()).filter { productsByGuid.containsKey(it.productGuid) }
                val positiveStocks = whStocks.filter { it.quantity > 0.0 }
                val count = positiveStocks.size
                val totalQty = positiveStocks.sumOf { it.quantity }
                val totalVal = positiveStocks.sumOf { s ->
                    val prod = productsByGuid[s.productGuid]
                    (prod?.costPrice ?: 0.0) * s.quantity
                }

                WarehouseWithStats(
                    warehouse = wh,
                    productCount = count,
                    totalQuantity = totalQty,
                    totalStockValue = totalVal
                )
            }
        }
    }

    suspend fun getAllWarehousesList(): List<WarehouseEntity> = warehouseDao.getAllWarehousesList()

    suspend fun getPrimaryWarehouse(): WarehouseEntity? = warehouseDao.getPrimaryWarehouse()

    suspend fun saveWarehouse(name: String, isPrimary: Boolean, id: Long = 0L, guid: String = "") {
        val now = System.currentTimeMillis()
        val whGuid = if (guid.isBlank()) "wh_${UUID.randomUUID()}" else guid

        if (isPrimary) {
            warehouseDao.clearPrimaryStatus()
        }

        val entity = WarehouseEntity(
            id = id,
            guid = whGuid,
            name = name.trim(),
            isPrimary = isPrimary,
            isDeleted = false,
            updatedAt = now
        )
        val insertedId = warehouseDao.insertWarehouse(entity)
        val toSend = entity.copy(id = if (id == 0L) insertedId else id)
        localSyncManager.sendLiveWarehouse(toSend)
    }

    suspend fun setPrimaryWarehouse(guid: String) {
        val now = System.currentTimeMillis()
        warehouseDao.clearPrimaryStatus()
        warehouseDao.setPrimaryWarehouse(guid, now)
        val wh = warehouseDao.getWarehouseByGuid(guid)
        if (wh != null) {
            localSyncManager.sendLiveWarehouse(wh)
        }
    }

    suspend fun deleteWarehouse(guid: String) {
        val now = System.currentTimeMillis()
        warehouseDao.softDeleteWarehouse(guid, now)
        val wh = warehouseDao.getWarehouseByGuid(guid)
        if (wh != null) {
            localSyncManager.sendLiveWarehouse(wh)
        }
    }

    fun getStocksForWarehouse(warehouseGuid: String): Flow<List<ProductStockEntity>> {
        return productStockDao.getStocksForWarehouse(warehouseGuid)
    }

    suspend fun getProductStockInWarehouse(productGuid: String, warehouseGuid: String): Double {
        return productStockDao.getProductStockInWarehouse(productGuid, warehouseGuid) ?: 0.0
    }

    suspend fun updateProductStockInWarehouse(productGuid: String, warehouseGuid: String, quantity: Double) {
        val now = System.currentTimeMillis()
        productStockDao.insertOrUpdateStock(
            ProductStockEntity(
                productGuid = productGuid,
                warehouseGuid = warehouseGuid,
                quantity = quantity,
                updatedAt = now
            )
        )
        // Umumiy tovar qoldig'ini yangilash
        val total = productStockDao.getTotalStockForProduct(productGuid)
        val product = productDao.getProductByGuid(productGuid)
        if (product != null) {
            productDao.updateProduct(product.copy(stockQuantity = total, updatedAt = now))
        }
    }

    suspend fun transferStock(productGuid: String, fromWh: String, toWh: String, quantity: Double) {
        if (quantity <= 0 || fromWh == toWh) return
        val now = System.currentTimeMillis()
        productStockDao.transferStock(productGuid, fromWh, toWh, quantity, now)
        localSyncManager.sendLiveStockTransfer(productGuid, fromWh, toWh, quantity)
    }
}
