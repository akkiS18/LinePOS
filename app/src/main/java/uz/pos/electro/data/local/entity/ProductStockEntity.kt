package uz.pos.electro.data.local.entity

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "product_stocks",
    indices = [
        Index(value = ["product_guid", "warehouse_guid"], unique = true),
        Index(value = ["product_guid"]),
        Index(value = ["warehouse_guid"])
    ]
)
data class ProductStockEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,

    @ColumnInfo(name = "product_guid")
    val productGuid: String,

    @ColumnInfo(name = "warehouse_guid")
    val warehouseGuid: String,

    @ColumnInfo(name = "quantity", defaultValue = "0.0")
    val quantity: Double = 0.0,

    @ColumnInfo(name = "updated_at")
    val updatedAt: Long = System.currentTimeMillis()
)
