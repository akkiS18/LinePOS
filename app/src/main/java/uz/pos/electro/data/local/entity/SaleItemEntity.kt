package uz.pos.electro.data.local.entity

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "sale_items",
    foreignKeys = [
        ForeignKey(
            entity = SaleEntity::class,
            parentColumns = ["id"],
            childColumns = ["sale_id"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [
        Index(value = ["guid"], unique = true),
        Index(value = ["sale_id"]),
        Index(value = ["product_id"]),
        Index(value = ["sale_guid"]),
        Index(value = ["product_guid"])
    ]
)
data class SaleItemEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,

    @ColumnInfo(name = "guid", defaultValue = "''")
    val guid: String = "",

    @ColumnInfo(name = "sale_id")
    val saleId: Long,

    @ColumnInfo(name = "sale_guid", defaultValue = "''")
    val saleGuid: String = "",

    @ColumnInfo(name = "product_id")
    val productId: Long,

    @ColumnInfo(name = "product_guid", defaultValue = "''")
    val productGuid: String = "",

    @ColumnInfo(name = "product_name", defaultValue = "''")
    val productName: String = "",

    @ColumnInfo(name = "category_at_sale", defaultValue = "''")
    val categoryAtSale: String = "",

    @ColumnInfo(name = "unit_at_sale", defaultValue = "''")
    val unitAtSale: String = "",

    @ColumnInfo(name = "quantity")
    val quantity: Double,

    @ColumnInfo(name = "price_at_sale")
    val priceAtSale: Double,

    @ColumnInfo(name = "cost_at_sale")
    val costAtSale: Double,

    @ColumnInfo(name = "cost_currency", defaultValue = "UZS")
    val costCurrency: String = "UZS",

    @ColumnInfo(name = "warehouse_guid", defaultValue = "''")
    val warehouseGuid: String = "",

    @ColumnInfo(name = "warehouse_name", defaultValue = "''")
    val warehouseName: String = ""
)

