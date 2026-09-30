package uz.pos.electro.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.entity.RefundEntity

@Dao
interface RefundDao {

    @Insert(onConflict = OnConflictStrategy.ABORT)
    suspend fun insertRefund(refund: RefundEntity): Long

    @Query("SELECT * FROM refunds ORDER BY refund_date DESC")
    fun getAllRefunds(): Flow<List<RefundEntity>>

    @Query("SELECT * FROM refunds WHERE sale_id = :saleId ORDER BY refund_date DESC")
    fun getRefundsBySaleId(saleId: Long): Flow<List<RefundEntity>>

    @Query("SELECT * FROM refunds WHERE refund_date BETWEEN :startTimestamp AND :endTimestamp ORDER BY refund_date DESC")
    fun getRefundsBetween(startTimestamp: Long, endTimestamp: Long): Flow<List<RefundEntity>>

    @Query("SELECT SUM(refund_amount) FROM refunds WHERE refund_date BETWEEN :startTimestamp AND :endTimestamp")
    fun getTotalRefundAmountBetween(startTimestamp: Long, endTimestamp: Long): Flow<Double?>
}
