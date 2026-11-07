package uz.pos.electro.util

import java.util.Locale

/**
 * Hisobotlar → cheklar tarixi qidiruvi: chek raqami, chek ichidagi mahsulot nomi yoki shtrix-kod.
 * Faqat xotiradagi ma'lumot ustida ishlaydi (bazaga yozmaydi). Desktop (`DatabaseContext.SearchSales`) bilan bir xil qoidalar.
 */
object SaleSearchMatcher {

    class Query internal constructor(
        private val raw: String,
        private val receiptPattern: String?,
        private val matchSaleId: Boolean,
        private val barcodeQuery: String,
        private val tokens: List<SmartSearchHelper.SearchTokenVariants>
    ) {
        val isBlank: Boolean get() = raw.isBlank()

        /** Chek raqami / guid bo'lagi / chek ID bo'yicha mosligi. */
        fun matchesReceipt(guid: String, id: Long): Boolean {
            val pattern = receiptPattern ?: return false
            if (guid.replace("-", "").contains(pattern, ignoreCase = true)) return true
            return matchSaleId && id.toString().contains(pattern)
        }

        /**
         * Chekdagi bitta tovar mosligi.
         * @param soldName sotuv paytidagi nom, @param currentName mahsulotning hozirgi nomi (o'zgartirilgan bo'lishi mumkin)
         */
        fun matchesItem(soldName: String, currentName: String?, barcode: String?): Boolean {
            if (tokens.isEmpty()) return false
            val normBarcode = barcode?.trim()?.lowercase(Locale.ROOT).orEmpty()
            if (barcodeQuery.isNotEmpty() && normBarcode.isNotEmpty() && normBarcode.contains(barcodeQuery)) return true
            val sold = SmartSearchHelper.normalize(soldName)
            if (SmartSearchHelper.isMatch(sold, barcode, null, tokens).first) return true
            val current = SmartSearchHelper.normalize(currentName)
            return current.isNotEmpty() && SmartSearchHelper.isMatch(current, barcode, null, tokens).first
        }
    }

    fun prepare(rawQuery: String): Query {
        val raw = rawQuery.trim()
        val (pattern, matchId) = receiptPattern(raw)
        return Query(
            raw = raw,
            receiptPattern = pattern,
            matchSaleId = matchId,
            barcodeQuery = raw.replace(" ", "").lowercase(Locale.ROOT),
            tokens = if (raw.isBlank()) emptyList() else SmartSearchHelper.prepareTokens(raw)
        )
    }

    /**
     * So'rov chek raqamiga o'xshaydimi: prefiks/#/defis bor, yoki sof raqam (chek ID), yoki >= 6 ta hex belgi.
     * Ko'p so'zli so'rov hech qachon chek raqami emas ("cab" kabi so'z tasodifan guid bilan mos kelmasin).
     * @return (qidiriladigan hex/raqam naqshi yoki null, chek ID bo'yicha ham qidirilsinmi)
     */
    internal fun receiptPattern(raw: String): Pair<String?, Boolean> {
        if (raw.isEmpty() || raw.any { it.isWhitespace() }) return null to false
        val upper = raw.uppercase(Locale.ROOT)
        val explicit = upper.startsWith("#") || upper.startsWith("LP-") || upper.startsWith("RT-") || upper.startsWith("RV-")
        var clean = upper.trimStart('#')
        for (prefix in listOf("LP-", "RT-", "RV-")) if (clean.startsWith(prefix)) { clean = clean.removePrefix(prefix); break }
        clean = clean.replace("-", "")
        if (clean.isEmpty() || !clean.all { it in '0'..'9' || it in 'A'..'F' }) return null to false
        val digitsOnly = clean.all { it in '0'..'9' }
        if (!explicit && !digitsOnly && !raw.contains('-') && clean.length < 6) return null to false
        // Uzun raqam (shtrix-kod) chek ID sifatida qidirilmaydi
        return clean to (digitsOnly && clean.length <= 7)
    }
}
