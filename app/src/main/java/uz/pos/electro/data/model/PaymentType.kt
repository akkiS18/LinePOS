package uz.pos.electro.data.model

enum class PaymentType {
    CASH,
    CARD,
    SPLIT,
    RETURN,
    RETURN_REVERSAL,
    BRAK,
    // Stored by name in Room; this is not an ordinary checkout option.
    DEBT;

    fun requireOrdinaryCheckout() {
        require(this != DEBT) { "Nasiya savdosi qarz daftari orqali saqlanishi kerak" }
        require(this != RETURN && this != RETURN_REVERSAL) { "Qaytarish mahalliy kompyuter orqali tasdiqlanadi" }
    }
}
