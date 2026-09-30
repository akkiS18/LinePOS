package uz.pos.electro.data.local.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Update
import kotlinx.coroutines.flow.Flow
import uz.pos.electro.data.local.entity.WarehouseEntity

@Dao
interface WarehouseDao {

    @Query("SELECT * FROM warehouses WHERE is_deleted = 0 ORDER BY is_primary DESC, name ASC")
    fun getAllWarehouses(): Flow<List<WarehouseEntity>>

    @Query("SELECT * FROM warehouses WHERE is_deleted = 0 ORDER BY is_primary DESC, name ASC")
    suspend fun getAllWarehousesList(): List<WarehouseEntity>

    @Query("SELECT * FROM warehouses WHERE is_primary = 1 AND is_deleted = 0 LIMIT 1")
    suspend fun getPrimaryWarehouse(): WarehouseEntity?

    @Query("SELECT * FROM warehouses WHERE guid = :guid LIMIT 1")
    suspend fun getWarehouseByGuid(guid: String): WarehouseEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertWarehouse(warehouse: WarehouseEntity): Long

    @Update
    suspend fun updateWarehouse(warehouse: WarehouseEntity)

    @Query("UPDATE warehouses SET is_primary = 0")
    suspend fun clearPrimaryStatus()

    @Query("UPDATE warehouses SET is_primary = 1, updated_at = :now WHERE guid = :guid")
    suspend fun setPrimaryWarehouse(guid: String, now: Long)

    @Query("UPDATE warehouses SET is_deleted = 1, updated_at = :now WHERE guid = :guid AND is_primary = 0")
    suspend fun softDeleteWarehouse(guid: String, now: Long)

    @Query("SELECT COUNT(*) FROM warehouses WHERE is_deleted = 0")
    suspend fun getActiveWarehouseCount(): Int
}
