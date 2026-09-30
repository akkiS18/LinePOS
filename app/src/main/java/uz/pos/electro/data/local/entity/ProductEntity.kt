package uz.pos.electro.data.local.entity

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import uz.pos.electro.data.model.UnitType
import java.util.UUID

@Entity(
    tableName = "products",
    indices = [
        Index(value = ["barcode"], unique = true),
        Index(value = ["guid"], unique = true)
    ]
)
data class ProductEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,

    @ColumnInfo(name = "guid")
    val guid: String = UUID.randomUUID().toString(),

    @ColumnInfo(name = "barcode")
    val barcode: String? = null,

    @ColumnInfo(name = "name")
    val name: String,

    @ColumnInfo(name = "category", defaultValue = "Barchasi")
    val category: String = "Barchasi",

    @ColumnInfo(name = "cost_price")
    val costPrice: Double,

    @ColumnInfo(name = "cost_currency", defaultValue = "UZS")
    val costCurrency: String = "UZS",

    @ColumnInfo(name = "selling_price")
    val sellingPrice: Double,

    @ColumnInfo(name = "selling_price_2")
    val sellingPrice2: Double? = null,

    @ColumnInfo(name = "stock_quantity")
    val stockQuantity: Double,

    @ColumnInfo(name = "unit_type")
    val unitType: UnitType,

    @ColumnInfo(name = "min_stock_alert", defaultValue = "3.0")
    val minStockAlert: Double = 3.0,

    @ColumnInfo(name = "is_deleted", defaultValue = "0")
    val isDeleted: Boolean = false,

    @ColumnInfo(name = "note", defaultValue = "''")
    val note: String = "",

    @ColumnInfo(name = "updated_at", defaultValue = "0")
    val updatedAt: Long = System.currentTimeMillis()
)

