package uz.pos.electro.util

import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import java.text.DecimalFormat
import java.text.DecimalFormatSymbols
import java.text.NumberFormat
import java.util.Locale

/**
 * Raqamlarni (narx/summa) yozayotganda avtomatik ravishda 0 larni
 * vergul (masalan: 1,500,000) bilan chiroyli formatlab ko'rsatuvchi VisualTransformation.
 */
class ThousandsSeparatorVisualTransformation : VisualTransformation {

    override fun filter(text: AnnotatedString): TransformedText {
        val originalText = text.text
        if (originalText.isEmpty()) {
            return TransformedText(text, OffsetMapping.Identity)
        }

        // Faqat raqamlar va nuqta/vergul
        val isDecimal = originalText.contains(".")
        val parts = originalText.split(".")
        val integerPart = parts[0]
        val fractionalPart = if (parts.size > 1) "." + parts[1] else ""

        val formattedIntPart = formatWithCommas(integerPart)
        val formattedText = formattedIntPart + fractionalPart

        val offsetMapping = object : OffsetMapping {
            override fun originalToTransformed(offset: Int): Int {
                if (offset <= 0) return 0
                if (offset > originalText.length) return formattedText.length

                val subOriginal = originalText.substring(0, offset)
                val subParts = subOriginal.split(".")
                val subIntPart = subParts[0]
                val subFracPart = if (subParts.size > 1) "." + subParts[1] else ""

                return formatWithCommas(subIntPart).length + subFracPart.length
            }

            override fun transformedToOriginal(offset: Int): Int {

                if (offset <= 0) return 0
                if (offset >= formattedText.length) return originalText.length

                val subFormatted = formattedText.substring(0, offset)
                val rawCount = subFormatted.count { it.isDigit() || it == '.' }
                return rawCount.coerceAtMost(originalText.length)
            }
        }

        return TransformedText(AnnotatedString(formattedText), offsetMapping)
    }

    private fun formatWithCommas(str: String): String {
        if (str.isEmpty()) return ""
        val cleanDigits = str.filter { it.isDigit() }
        if (cleanDigits.isEmpty()) return str

        val sb = StringBuilder()
        val len = cleanDigits.length
        for (i in 0 until len) {
            if (i > 0 && (len - i) % 3 == 0) {
                sb.append(',')
            }
            sb.append(cleanDigits[i])
        }
        return sb.toString()
    }
}

/**
 * Xom matndan faqat toza son/o'nlik qiymatni ajratib olish
 */
fun String.cleanNumberString(): String {
    return this.filter { it.isDigit() || it == '.' }
}
