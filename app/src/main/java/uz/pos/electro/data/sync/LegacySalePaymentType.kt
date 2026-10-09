package uz.pos.electro.data.sync

import uz.pos.electro.data.model.PaymentType
import java.util.Locale

/** The old sale-only protocol cannot carry a debt account or its immutable event. */
internal object LegacySalePaymentType {
    private fun debtUnsupported(): Nothing = throw IllegalArgumentException(
        "Nasiya cheki uchun qarz daftarini qo‘llovchi sinxron protokoli kerak")

    fun encode(type: PaymentType): Int = when (type) {
        PaymentType.CASH -> 0
        PaymentType.CARD -> 1
        PaymentType.SPLIT -> 2
        PaymentType.BRAK -> 6
        PaymentType.RETURN -> 7
        PaymentType.RETURN_REVERSAL -> 8
        PaymentType.DEBT -> debtUnsupported()
    }

    // Preserve existing legacy defaults; DEBT must never silently become CASH.
    fun decode(raw: Any?): PaymentType = when (raw) {
        is Number -> when (raw.toInt()) {
            1 -> PaymentType.CARD
            2 -> PaymentType.SPLIT
            3 -> debtUnsupported()
            6 -> PaymentType.BRAK
            7 -> PaymentType.RETURN
            8 -> PaymentType.RETURN_REVERSAL
            else -> PaymentType.CASH
        }
        is String -> when (raw.trim().uppercase(Locale.ROOT)) {
            "CARD" -> PaymentType.CARD
            "SPLIT" -> PaymentType.SPLIT
            "DEBT", "3" -> debtUnsupported()
            "BRAK" -> PaymentType.BRAK
            "RETURN" -> PaymentType.RETURN
            "RETURN_REVERSAL" -> PaymentType.RETURN_REVERSAL
            else -> PaymentType.CASH
        }
        else -> PaymentType.CASH
    }
}
