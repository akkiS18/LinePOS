package uz.pos.electro.data.repository

import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.sync.LocalSyncManager
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ProductRepository @Inject constructor(
    private val productDao: ProductDao,
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

    suspend fun saveProduct(product: ProductEntity, warehouseGuid: String? = null): Long {
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

    suspend fun updateStock(productId: Long, newStock: Double) {
        val now = System.currentTimeMillis()
        productDao.updateStock(productId, newStock, now)
        val p = productDao.getProductById(productId)
        if (p != null) localSyncManager.sendLiveProduct(p)
    }
}
