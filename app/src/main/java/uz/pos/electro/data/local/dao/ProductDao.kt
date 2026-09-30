package uz.pos.electro.data.local.dao

import androidx.room.Dao
import androidx.room.Delete
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.entity.ProductEntity

@Dao
interface ProductDao {

    @Query("SELECT * FROM products WHERE is_deleted = 0 ORDER BY name ASC")
    fun getAllProducts(): Flow<List<ProductEntity>>

    @Query("SELECT * FROM products WHERE is_deleted = 0 ORDER BY name ASC")
    suspend fun getAllProductsList(): List<ProductEntity>

    @Query("SELECT * FROM products WHERE is_deleted = 0 AND (name LIKE '%' || :query || '%' OR barcode LIKE '%' || :query || '%') ORDER BY name ASC")
    fun searchProducts(query: String): Flow<List<ProductEntity>>

    @Query("SELECT DISTINCT category FROM products WHERE is_deleted = 0 AND category IS NOT NULL AND category != ''")
    fun getDistinctCategories(): Flow<List<String>>

    @Query("UPDATE products SET category = :category, updated_at = :now WHERE id IN (:productIds)")
    suspend fun updateProductsCategory(productIds: List<Long>, category: String, now: Long = System.currentTimeMillis())

    @Query("SELECT * FROM products WHERE id = :id AND is_deleted = 0 LIMIT 1")
    suspend fun getProductById(id: Long): ProductEntity?

    @Query("SELECT * FROM products WHERE barcode = :barcode AND is_deleted = 0 LIMIT 1")
    suspend fun getProductByBarcode(barcode: String): ProductEntity?

    @Query("SELECT * FROM products WHERE barcode = :barcode LIMIT 1")
    suspend fun getProductByBarcodeAny(barcode: String): ProductEntity?

    @Query("SELECT * FROM products WHERE TRIM(name) = TRIM(:name) AND is_deleted = 1 LIMIT 1")
    suspend fun getDeletedProductByName(name: String): ProductEntity?

    @Query("SELECT * FROM products WHERE is_deleted = 0 AND stock_quantity <= min_stock_alert ORDER BY stock_quantity ASC")
    fun getLowStockProducts(): Flow<List<ProductEntity>>

    @Query("SELECT COUNT(*) FROM products WHERE is_deleted = 0 AND stock_quantity <= min_stock_alert")
    fun getLowStockCount(): Flow<Int>

    @Query("SELECT * FROM products WHERE guid = :guid LIMIT 1")
    suspend fun getProductByGuid(guid: String): ProductEntity?

    @Query("SELECT * FROM products WHERE updated_at > :sinceTimestamp ORDER BY updated_at ASC")
    suspend fun getProductsChangedSince(sinceTimestamp: Long): List<ProductEntity>

    @Query("SELECT * FROM products ORDER BY name ASC")
    suspend fun getAllProductsIncludingDeleted(): List<ProductEntity>

    @Query("SELECT COUNT(*) FROM products WHERE is_deleted = 0")
    suspend fun getActiveProductsCount(): Int

    @Query("SELECT COUNT(*) FROM products WHERE updated_at > :sinceTimestamp")
    suspend fun getModifiedProductsCount(sinceTimestamp: Long): Int

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertProduct(product: ProductEntity): Long

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertProducts(products: List<ProductEntity>)

    @Update
    suspend fun updateProduct(product: ProductEntity)

    @Query("UPDATE products SET is_deleted = 1, barcode = NULL, updated_at = :updatedAt WHERE id = :productId")
    suspend fun softDeleteProduct(productId: Long, updatedAt: Long = System.currentTimeMillis())

    @Delete
    suspend fun deleteProduct(product: ProductEntity)

    @Query("UPDATE products SET stock_quantity = stock_quantity - :quantity, updated_at = :updatedAt WHERE id = :productId")
    suspend fun decreaseStock(productId: Long, quantity: Double, updatedAt: Long = System.currentTimeMillis())

    @Query("UPDATE products SET stock_quantity = stock_quantity - :quantity, updated_at = :updatedAt WHERE guid = :guid")
    suspend fun decreaseStockByGuid(guid: String, quantity: Double, updatedAt: Long = System.currentTimeMillis())

    @Query("UPDATE products SET stock_quantity = stock_quantity + :quantity, updated_at = :updatedAt WHERE id = :productId")
    suspend fun increaseStock(productId: Long, quantity: Double, updatedAt: Long = System.currentTimeMillis())

    @Query("UPDATE products SET stock_quantity = stock_quantity + :quantity, updated_at = :updatedAt WHERE guid = :guid")
    suspend fun increaseStockByGuid(guid: String, quantity: Double, updatedAt: Long = System.currentTimeMillis())

    @Query("UPDATE products SET stock_quantity = :newStock, updated_at = :updatedAt WHERE id = :productId")
    suspend fun updateStock(productId: Long, newStock: Double, updatedAt: Long = System.currentTimeMillis())
}
