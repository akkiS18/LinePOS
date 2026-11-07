package uz.pos.electro.util

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Hisobotlar qidiruvi: chek raqami, mahsulot nomi, shtrix-kod.
 * Desktop testlari (SalesSearchTests) bilan bir xil holatlar.
 */
class SaleSearchMatcherTest {

    private val guid = "8f04798c-2432-44ea-b3dd-0575ecedfc64"
    private val saleId = 15L

    private fun item(query: String, sold: String = "Kabel VVG 3x2.5", current: String? = null, barcode: String? = "4780001111111") =
        SaleSearchMatcher.prepare(query).matchesItem(sold, current, barcode)

    private fun receipt(query: String, id: Long = saleId) = SaleSearchMatcher.prepare(query).matchesReceipt(guid, id)

    // ---- Mahsulot nomi ----
    @Test fun findsByName() {
        for (q in listOf("kabel", "KABEL", "  kabel  ", "vvg", "3x2.5", "3x2,5", "2.5 kabel")) assertTrue(q, item(q))
    }

    @Test fun nameDoesNotMatchOtherProduct() {
        assertFalse(item("lampa"))
    }

    @Test fun findsCyrillicProductByCyrillicOrLatin() {
        assertTrue(item("лампочка", sold = "Лампочка LED 9W", barcode = null))
        assertTrue(item("lampochka", sold = "Лампочка LED 9W", barcode = null))
    }

    @Test fun findsByRenamedCurrentName() {
        assertTrue(item("yangi nom", sold = "Eski nom", current = "Yangi nom"))
    }

    // ---- Shtrix-kod ----
    @Test fun findsByBarcode() {
        for (q in listOf("4780001111111", "47800011", " 4780001111111 ")) assertTrue(q, item(q))
        assertFalse(item("4780009999999"))
    }

    // ---- Chek raqami ----
    @Test fun findsByReceiptNumberVariants() {
        val hex = guid.replace("-", "").uppercase()
        assertTrue(receipt("LP-$hex"))
        assertTrue(receipt("RT-$hex"))
        assertTrue(receipt(hex.lowercase()))
        assertTrue(receipt("LP-" + hex.substring(0, 8)))
        assertTrue(receipt("#" + hex.substring(4, 12)))
    }

    @Test fun findsBySaleId() {
        assertTrue(receipt("#15"))
        assertTrue(receipt("15"))
        assertFalse(receipt("#99", id = 15))
    }

    @Test fun longNumericBarcodeIsNotASaleId() {
        assertFalse(receipt("4780009999999", id = 4780009999999))
    }

    @Test fun hexLookingWordDoesNotMatchRandomGuid() {
        // "cab", "face" — hex harflaridan iborat so'z, lekin chek raqami emas
        assertFalse(receipt("cab"))
        assertFalse(receipt("face"))
    }

    // ---- Chegaraviy holatlar ----
    @Test fun blankAndSpecialQueriesMatchNothing() {
        for (q in listOf("", "   ", "%", "_", "'; DROP TABLE sales; --", "#", "LP-")) {
            assertFalse("'$q' chek", receipt(q))
            assertFalse("'$q' tovar", item(q, barcode = null))
        }
    }

    @Test fun receiptPatternDetection() {
        assertEquals("15" to true, SaleSearchMatcher.receiptPattern("#15"))
        assertEquals("15" to true, SaleSearchMatcher.receiptPattern("15"))
        assertEquals("AB12CD34" to false, SaleSearchMatcher.receiptPattern("LP-AB12CD34"))
        assertEquals("4780001111111" to false, SaleSearchMatcher.receiptPattern("4780001111111"))
        assertNull(SaleSearchMatcher.receiptPattern("cab").first)
        assertNull(SaleSearchMatcher.receiptPattern("kabel").first)
        assertNull(SaleSearchMatcher.receiptPattern("kabel 15").first)
    }
}
