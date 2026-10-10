package uz.pos.electro.data.model

import java.util.UUID

data class HeldCart(
    val id: String = UUID.randomUUID().toString(),
    val name: String,
    val items: List<CartItemModel>,
    val heldAt: Long = System.currentTimeMillis(),
    val selectedPaymentType: PaymentType = PaymentType.CASH,
    val customerGuid: String? = null,
    val customerName: String? = null,
    val dueDate: String? = null,
    val debtCashAdvance: String? = null,
    val debtCardAdvance: String? = null
) {
    val totalAmount: Double
        get() = items.sumOf { it.totalPrice }

    val itemCount: Int
        get() = items.size
}
