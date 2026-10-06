package uz.pos.electro.data.repository

import kotlinx.coroutines.flow.Flow
import androidx.room.withTransaction
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.dao.WarehouseDao
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.sync.LocalSyncManager
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ProductRepository @Inject constructor(
    private val productDao: ProductDao,
    private val database: AppDatabase,
    private val productStockDao: ProductStockDao,
    private val warehouseDao: WarehouseDao,
    private val localSyncManager: LocalSyncManager
) {
    fun getAllProducts(): Flow<List<ProductEntity>> = productDao.getAllProducts()

    fun searchProducts(query: String): Flow<List<ProductEntity>> = productDao.searchProducts(query)

    fun getDistinctCategories(): Flow<List<String>> = productDao.getDistinctCategories()

    suspend fun updateProductsCategory(productIds: List<Long>, category: String) {
        val now = System.currentTimeMillis()
        productDao.updateProductsCategory(productIds, category, now)
        for (id in productIds) {
            val p = productDao.getProductById(id)
            if (p != null) {
                localSyncManager.sendLiveProduct(p)
            }
        }
    }

    fun getLowStockProducts(): Flow<List<ProductEntity>> = productDao.getLowStockProducts()

    fun getLowStockCount(): Flow<Int> = productDao.getLowStockCount()

    suspend fun getProductById(id: Long): ProductEntity? = productDao.getProductById(id)

    suspend fun getProductByBarcode(barcode: String): ProductEntity? = productDao.getProductByBarcode(barcode)

    suspend fun saveProduct(product: ProductEntity, warehouseGuid: String? = null): Long = database.withTransaction {
        database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group=? WHERE id=1", arrayOf(java.util.UUID.randomUUID().toString()))
        val id = saveMetadata(product, warehouseGuid)
        val saved = productDao.getProductById(id) ?: error("Tovar saqlanmadi")
        val wh = warehouseGuid ?: warehouseDao.getPrimaryWarehouse()?.guid ?: "main-default-warehouse"
        productStockDao.upsertStock(saved.guid, wh, product.stockQuantity, System.currentTimeMillis())
        val total = productStockDao.getTotalStockForProduct(saved.guid)
        productDao.updateStock(id,total,System.currentTimeMillis())
        database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group='' WHERE id=1")
        id
    }

    private suspend fun saveMetadata(product: ProductEntity, warehouseGuid: String?): Long {
        val now = System.currentTimeMillis()
        if (product.id == 0L) {
            // 1. Shtrix-kod bo'yicha tekshirish (o'chirilganlar ichidan ham)
            val existingByBc = if (!product.barcode.isNullOrBlank()) {
                productDao.getProductByBarcodeAny(product.barcode.trim())
            } else null

            if (existingByBc != null) {
                val toUpdate = product.copy(
                    id = existingByBc.id,
                    guid = existingByBc.guid,
                    isDeleted = false,
                    updatedAt = now
                )
                productDao.updateProduct(toUpdate)
                localSyncManager.sendLiveProduct(toUpdate, warehouseGuid)
                return toUpdate.id
            }

            // 2. Shtrix-kodsiz bo'lsa, o'chirilganlar orasida xuddi shu nomdagi tovar bormi
            val existingByName = productDao.getDeletedProductByName(product.name.trim())
            if (existingByName != null) {
                val toUpdate = product.copy(
                    id = existingByName.id,
                    guid = existingByName.guid,
                    isDeleted = false,
                    updatedAt = now
                )
                productDao.updateProduct(toUpdate)
                localSyncManager.sendLiveProduct(toUpdate, warehouseGuid)
                return toUpdate.id
            }

            val toInsert = if (product.guid.isBlank()) product.copy(guid = java.util.UUID.randomUUID().toString(), updatedAt = now) else product.copy(updatedAt = now)
            val insertedId = productDao.insertProduct(toInsert)
            localSyncManager.sendLiveProduct(toInsert.copy(id = insertedId), warehouseGuid)
            return insertedId
        } else {
            val existing = productDao.getProductById(product.id)
            val guidToUse = if (existing != null && existing.guid.isNotBlank()) existing.guid else (if (product.guid.isNotBlank()) product.guid else java.util.UUID.randomUUID().toString())
            val toUpdate = product.copy(guid = guidToUse, isDeleted = false, updatedAt = now)
            productDao.updateProduct(toUpdate)
            localSyncManager.sendLiveProduct(toUpdate, warehouseGuid)
            return toUpdate.id
        }
    }

    suspend fun deleteProduct(product: ProductEntity) {
        val now = System.currentTimeMillis()
        productDao.softDeleteProduct(product.id, now)
        localSyncManager.sendLiveProduct(product.copy(isDeleted = true, updatedAt = now))
    }

    suspend fun updateStock(productId: Long, newStock: Double) = database.withTransaction {
        val p = productDao.getProductById(productId) ?: return@withTransaction
        val wh = warehouseDao.getPrimaryWarehouse()?.guid ?: "main-default-warehouse"
        productStockDao.upsertStock(p.guid,wh,newStock,System.currentTimeMillis())
        productDao.updateStock(productId,productStockDao.getTotalStockForProduct(p.guid),System.currentTimeMillis())
    }

    suspend fun addProductStock(productGuid: String, warehouseGuid: String? = null, additionalQuantity: Double): Double = database.withTransaction {
        if (additionalQuantity == 0.0) return@withTransaction 0.0
        database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group=? WHERE id=1", arrayOf(java.util.UUID.randomUUID().toString()))
        val now = System.currentTimeMillis()
        val wh = if (!warehouseGuid.isNullOrBlank() && warehouseGuid != "null") {
            warehouseGuid
        } else {
            warehouseDao.getPrimaryWarehouse()?.guid ?: "main-default-warehouse"
        }

        productStockDao.addStock(productGuid, wh, additionalQuantity, now)
        val total = productStockDao.getTotalStockForProduct(productGuid)
        val product = productDao.getProductByGuid(productGuid)
        if (product != null) {
            productDao.updateStock(product.id, total, now)
            localSyncManager.sendLiveProduct(product.copy(stockQuantity = total, updatedAt = now), wh)
        }
        database.openHelper.writableDatabase.execSQL("UPDATE sync_control SET current_group='' WHERE id=1")
        total
    }
}
