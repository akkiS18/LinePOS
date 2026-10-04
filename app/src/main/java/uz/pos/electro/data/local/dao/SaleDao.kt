package uz.pos.electro.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity
import uz.pos.electro.data.local.relation.SaleWithItems

@Dao
interface SaleDao {

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insertSale(sale: SaleEntity): Long

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insertSaleItems(items: List<SaleItemEntity>)

    @Transaction
    suspend fun insertSaleWithItems(
        sale: SaleEntity,
        items: List<SaleItemEntity>
    ): Long {
        val saleId = insertSale(sale)
        val itemsWithSaleId = items.mapIndexed { index, item -> item.copy(saleId = saleId, saleGuid = sale.guid, guid = item.guid.ifBlank { "${sale.guid}:${index + 1}" }) }
        insertSaleItems(itemsWithSaleId)
        return saleId
    }

    @Transaction
    @Query("SELECT * FROM sales ORDER BY created_at DESC")
    fun getAllSalesWithItems(): Flow<List<SaleWithItems>>

    @Transaction
    @Query("SELECT * FROM sales WHERE id = :saleId LIMIT 1")
    suspend fun getSaleWithItemsById(saleId: Long): SaleWithItems?

    @Transaction
    @Query("SELECT * FROM sales WHERE created_at BETWEEN :startTimestamp AND :endTimestamp ORDER BY created_at DESC")
    fun getSalesBetween(startTimestamp: Long, endTimestamp: Long): Flow<List<SaleWithItems>>

    @Transaction
    @Query("SELECT * FROM sales WHERE created_at BETWEEN :startTimestamp AND :endTimestamp ORDER BY created_at DESC")
    suspend fun getSalesListBetween(startTimestamp: Long, endTimestamp: Long): List<SaleWithItems>

    @Query("SELECT SUM(total_amount) FROM sales WHERE created_at BETWEEN :startTimestamp AND :endTimestamp")

    fun getTotalSalesAmountBetween(startTimestamp: Long, endTimestamp: Long): Flow<Double?>

    @Query("SELECT (SUM(total_amount) - SUM(total_cost) - SUM(tax_amount)) FROM sales WHERE created_at BETWEEN :startTimestamp AND :endTimestamp")
    fun getTotalProfitBetween(startTimestamp: Long, endTimestamp: Long): Flow<Double?>

    @Query("SELECT COUNT(*) FROM sales WHERE created_at BETWEEN :startTimestamp AND :endTimestamp")
    fun getSalesCountBetween(startTimestamp: Long, endTimestamp: Long): Flow<Int>

    @Query("SELECT DISTINCT CASE WHEN category_at_sale = '' THEN 'Tarixiy kategoriya noma’lum' ELSE category_at_sale END FROM sale_items")
    fun getHistoricalCategories(): Flow<List<String>>

    @Query("SELECT COUNT(*) FROM sales")
    suspend fun getAllSalesCount(): Int

    @Query("SELECT COUNT(*) FROM sales WHERE is_synced = 0")
    suspend fun getUnsyncedSalesCount(): Int

    @Transaction
    @Query("SELECT * FROM sales WHERE is_synced = 0 ORDER BY created_at ASC")
    suspend fun getUnsyncedSales(): List<SaleWithItems>

    @Transaction
    @Query("SELECT * FROM sales WHERE created_at > :sinceTimestamp ORDER BY created_at ASC")
    suspend fun getSalesSince(sinceTimestamp: Long): List<SaleWithItems>

    @Query("SELECT * FROM sales WHERE guid = :guid LIMIT 1")
    suspend fun getSaleByGuid(guid: String): SaleEntity?

    @Query("UPDATE sales SET is_synced = 1 WHERE id IN (:saleIds)")
    suspend fun markSalesSynced(saleIds: List<Long>)

    @Query("UPDATE sales SET is_synced = 1 WHERE guid IN (:guids)")
    suspend fun markSalesSyncedByGuids(guids: List<String>)

    @Query("DELETE FROM sales WHERE id = :saleId")
    suspend fun deleteSale(saleId: Long)
}
