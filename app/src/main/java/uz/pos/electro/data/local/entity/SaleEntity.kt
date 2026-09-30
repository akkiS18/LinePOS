package uz.pos.electro.data.local.entity

import androidx.room.ColumnInfo
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import uz.pos.electro.data.model.PaymentType
import java.util.UUID

@Entity(
    tableName = "sales",
    indices = [
        Index(value = ["guid"], unique = true),
        Index(value = ["user_id"]),
        Index(value = ["created_at"])
    ]
)
data class SaleEntity(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,

    @ColumnInfo(name = "guid")
    val guid: String = UUID.randomUUID().toString(),

    @ColumnInfo(name = "total_amount")
    val totalAmount: Double,

    @ColumnInfo(name = "total_cost")
    val totalCost: Double,

    @ColumnInfo(name = "payment_type")
    val paymentType: PaymentType,

    @ColumnInfo(name = "cash_amount", defaultValue = "0.0")
    val cashAmount: Double = 0.0,

    @ColumnInfo(name = "card_amount", defaultValue = "0.0")
    val cardAmount: Double = 0.0,

    @ColumnInfo(name = "tax_amount", defaultValue = "0.0")
    val taxAmount: Double = 0.0,

    @ColumnInfo(name = "tax_rate", defaultValue = "0.0")
    val taxRate: Double = 0.0,

    @ColumnInfo(name = "created_at")
    val createdAt: Long = System.currentTimeMillis(),

    @ColumnInfo(name = "user_id")
    val userId: Long = 1L,

    @ColumnInfo(name = "is_synced", defaultValue = "0")
    val isSynced: Boolean = false
)

