package uz.pos.electro.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import androidx.room.Update
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.entity.ProductStockEntity

@Dao
interface ProductStockDao {

    @Query("SELECT * FROM product_stocks WHERE warehouse_guid = :warehouseGuid")
    fun getStocksForWarehouse(warehouseGuid: String): Flow<List<ProductStockEntity>>

    @Query("SELECT * FROM product_stocks")
    fun getAllProductStocks(): Flow<List<ProductStockEntity>>

    @Query("SELECT * FROM product_stocks")
    suspend fun getAllProductStocksList(): List<ProductStockEntity>

    @Query("SELECT * FROM product_stocks WHERE warehouse_guid = :warehouseGuid")
    suspend fun getStocksListForWarehouse(warehouseGuid: String): List<ProductStockEntity>

    @Query("SELECT * FROM product_stocks WHERE product_guid = :productGuid")
    suspend fun getStocksForProduct(productGuid: String): List<ProductStockEntity>

    @Query("SELECT quantity FROM product_stocks WHERE product_guid = :productGuid AND warehouse_guid = :warehouseGuid LIMIT 1")
    suspend fun getProductStockInWarehouse(productGuid: String, warehouseGuid: String): Double?

    @Query("DELETE FROM product_stocks WHERE warehouse_guid IS NULL OR warehouse_guid = 'null' OR warehouse_guid = ''")
    suspend fun deleteInvalidStocks(): Int

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrUpdateStock(stock: ProductStockEntity): Long

    @Query("""
        INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
        VALUES (:productGuid, :warehouseGuid, -:qty, :now)
        ON CONFLICT(product_guid, warehouse_guid)
        DO UPDATE SET quantity = quantity - :qty, updated_at = :now
    """)
    suspend fun deductStock(productGuid: String, warehouseGuid: String, qty: Double, now: Long): Long

    @Query("""
        INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
        VALUES (:productGuid, :warehouseGuid, :qty, :now)
        ON CONFLICT(product_guid, warehouse_guid)
        DO UPDATE SET quantity = quantity + :qty, updated_at = :now
    """)
    suspend fun addStock(productGuid: String, warehouseGuid: String, qty: Double, now: Long): Long

    @Query("""
        INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
        VALUES (:productGuid, :warehouseGuid, :quantity, :now)
        ON CONFLICT(product_guid, warehouse_guid)
        DO UPDATE SET quantity = :quantity, updated_at = :now
    """)
    suspend fun upsertStock(productGuid: String, warehouseGuid: String, quantity: Double, now: Long = System.currentTimeMillis()): Long

    @Query("SELECT COALESCE(SUM(quantity), 0.0) FROM product_stocks WHERE product_guid = :productGuid")
    suspend fun getTotalStockForProduct(productGuid: String): Double

    @Transaction
    suspend fun transferStock(productGuid: String, fromWh: String, toWh: String, quantity: Double, now: Long) {
        val currentFrom = getProductStockInWarehouse(productGuid, fromWh) ?: 0.0
        val currentTo = getProductStockInWarehouse(productGuid, toWh) ?: 0.0

        insertOrUpdateStock(ProductStockEntity(productGuid = productGuid, warehouseGuid = fromWh, quantity = currentFrom - quantity, updatedAt = now))
        insertOrUpdateStock(ProductStockEntity(productGuid = productGuid, warehouseGuid = toWh, quantity = currentTo + quantity, updatedAt = now))
    }
}
