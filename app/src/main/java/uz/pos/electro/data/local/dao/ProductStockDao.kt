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

    @Query("DELETE FROM product_stocks WHERE warehouse_guid = :warehouseGuid")
    suspend fun deleteStocksForWarehouse(warehouseGuid: String): Int

    suspend fun insertOrUpdateStock(stock: ProductStockEntity): Long =
        upsertStock(stock.productGuid, stock.warehouseGuid, stock.quantity, stock.updatedAt)

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun ensureStock(stock: ProductStockEntity): Long

    @Query("UPDATE product_stocks SET quantity=quantity-:qty,updated_at=:now WHERE product_guid=:productGuid AND warehouse_guid=:warehouseGuid")
    suspend fun deductExistingStock(productGuid: String,warehouseGuid: String,qty: Double,now: Long): Int

    @Query("UPDATE product_stocks SET quantity=quantity+:qty,updated_at=:now WHERE product_guid=:productGuid AND warehouse_guid=:warehouseGuid")
    suspend fun addExistingStock(productGuid: String,warehouseGuid: String,qty: Double,now: Long): Int

    @Query("UPDATE product_stocks SET quantity=:qty,updated_at=:now WHERE product_guid=:productGuid AND warehouse_guid=:warehouseGuid")
    suspend fun setExistingStock(productGuid: String,warehouseGuid: String,qty: Double,now: Long): Int

    // INSERT OR IGNORE + UPDATE also works with SQLite shipped on Android 8 (API 26).
    @Transaction
    suspend fun deductStock(productGuid: String,warehouseGuid: String,qty: Double,now: Long): Long {
        ensureStock(ProductStockEntity(productGuid=productGuid,warehouseGuid=warehouseGuid,quantity=0.0,updatedAt=now))
        return deductExistingStock(productGuid,warehouseGuid,qty,now).toLong()
    }

    @Transaction
    suspend fun addStock(productGuid: String,warehouseGuid: String,qty: Double,now: Long): Long {
        ensureStock(ProductStockEntity(productGuid=productGuid,warehouseGuid=warehouseGuid,quantity=0.0,updatedAt=now))
        return addExistingStock(productGuid,warehouseGuid,qty,now).toLong()
    }

    @Transaction
    suspend fun upsertStock(productGuid: String,warehouseGuid: String,quantity: Double,now: Long = System.currentTimeMillis()): Long {
        ensureStock(ProductStockEntity(productGuid=productGuid,warehouseGuid=warehouseGuid,quantity=0.0,updatedAt=now))
        return setExistingStock(productGuid,warehouseGuid,quantity,now).toLong()
    }

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
