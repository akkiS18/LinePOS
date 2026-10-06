package uz.pos.electro.util

import java.util.Locale

/**
 * Android uchun Aqlli Qidiruv (Smart Search) yordamchisi:
 * 1. Tartibga bog'liq bo'lmagan ko'p so'zli token qidiruvi (unordered multi-word search).
 * 2. Kril <-> Lotin ikki tomonlama transliteratsiyasi (ruscha klaviaturada yozsa ham topadi).
 * 3. O'lchov birliklari va sonlar tolerantligi (2.5 <-> 2,5).
 * 4. Relevance ranking (aniq mos kelganlar eng yuqorida turadi).
 */
object SmartSearchHelper {

    private val cyrillicToLatinMulti = mapOf(
        "ш" to "sh", "ч" to "ch", "ў" to "o'", "ғ" to "g'",
        "ё" to "yo", "ю" to "yu", "я" to "ya", "ц" to "ts",
        "ж" to "j",  "қ" to "q",  "ҳ" to "h",  "х" to "x"
    )

    private val cyrillicToLatinSingle = mapOf(
        'а' to "a", 'б' to "b", 'в' to "v", 'г' to "g", 'д' to "d",
        'е' to "e", 'з' to "z", 'и' to "i", 'й' to "y", 'к' to "k",
        'л' to "l", 'м' to "m", 'н' to "n", 'о' to "o", 'п' to "p",
        'р' to "r", 'с' to "s", 'т' to "t", 'у' to "u", 'ф' to "f",
        'х' to "x", 'э' to "e", 'ы' to "i", 'ь' to "",  'ъ' to "'"
    )

    private val latinToCyrillicMulti = mapOf(
        "sh" to "ш", "ch" to "ч", "o'" to "ў", "oʻ" to "ў", "o`" to "ў",
        "g'" to "ғ", "gʻ" to "ғ", "g`" to "ғ", "yo" to "ё", "yu" to "ю",
        "ya" to "я", "ts" to "ц"
    )

    private val latinToCyrillicSingle = mapOf(
        'a' to 'а', 'b' to 'б', 'v' to 'в', 'g' to 'г', 'd' to 'д',
        'e' to 'е', 'z' to 'з', 'i' to 'и', 'y' to 'й', 'k' to 'к',
        'l' to 'л', 'm' to 'м', 'n' to 'н', 'o' to 'о', 'p' to 'п',
        'r' to 'р', 's' to 'с', 't' to 'т', 'u' to 'у', 'f' to 'ф',
        'x' to 'х', 'h' to 'ҳ', 'q' to 'қ', 'j' to 'ж'
    )

    // Rus klaviaturasi -> Ingliz klaviaturasi xaritasi (JCUKEN -> QWERTY)
    private val ruToEnMap = mapOf(
        'й' to 'q', 'ц' to 'w', 'у' to 'e', 'к' to 'r', 'е' to 't', 'н' to 'y', 'г' to 'u', 'ш' to 'i', 'щ' to 'o', 'з' to 'p', 'х' to '[', 'ъ' to ']',
        'ф' to 'a', 'ы' to 's', 'в' to 'd', 'а' to 'f', 'п' to 'g', 'р' to 'h', 'о' to 'j', 'л' to 'k', 'д' to 'l', 'ж' to ';', 'э' to '\'',
        'я' to 'z', 'ч' to 'x', 'с' to 'c', 'м' to 'v', 'и' to 'b', 'т' to 'n', 'ь' to 'm', 'б' to ',', 'ю' to '.'
    )

    fun convertRuToEn(input: String): String {
        val sb = StringBuilder(input.length)
        for (ch in input.lowercase(Locale.ROOT)) {
            sb.append(ruToEnMap[ch] ?: ch)
        }
        return sb.toString()
    }

    /**
     * Matnni normallashtirish:
     * Apostroflarni bittaga keltirish, sonlar orasidagi vergulni nuqtaga aylantirish (2,5 -> 2.5)
     */
    fun normalize(input: String?): String {
        if (input.isNullOrBlank()) return ""
        var s = input.trim().lowercase(Locale.ROOT)
        s = s.replace(Regex("[''`ʹʻʼ’‘]"), "'")
        s = s.replace(Regex("(?<=\\d),(?=\\d)"), ".")
        s = s.replace(Regex("\\s+"), " ")
        return s
    }

    fun cyrillicToLatin(input: String): String {
        if (input.isBlank()) return ""
        var s = input.lowercase(Locale.ROOT)
        for ((cyr, lat) in cyrillicToLatinMulti) {
            s = s.replace(cyr, lat)
        }
        val sb = StringBuilder(s.length)
        for (c in s) {
            sb.append(cyrillicToLatinSingle[c] ?: c)
        }
        return sb.toString()
    }

    fun latinToCyrillic(input: String): String {
        if (input.isBlank()) return ""
        var s = input.lowercase(Locale.ROOT)
        for ((lat, cyr) in latinToCyrillicMulti) {
            s = s.replace(lat, cyr)
        }
        val sb = StringBuilder(s.length)
        for (c in s) {
            sb.append(latinToCyrillicSingle[c] ?: c)
        }
        return sb.toString()
    }

    data class SearchTokenVariants(
        val original: String,
        val latin: String,
        val cyrillic: String,
        val layoutFixed: String
    )

    fun prepareTokens(query: String): List<SearchTokenVariants> {
        val clean = normalize(query)
        if (clean.isBlank()) return emptyList()
        val rawTokens = clean.split(' ').filter { it.isNotBlank() }

        return rawTokens.map { raw ->
            SearchTokenVariants(
                original = raw,
                latin = cyrillicToLatin(raw),
                cyrillic = latinToCyrillic(raw),
                layoutFixed = convertRuToEn(raw)
            )
        }
    }

    fun isMatch(
        normalizedName: String,
        barcode: String?,
        note: String?,
        tokens: List<SearchTokenVariants>
    ): Pair<Boolean, Int> {
        if (tokens.isEmpty()) return true to 0
        var score = 0

        val normBarcode = barcode?.trim()?.lowercase(Locale.ROOT) ?: ""
        val normNote = if (!note.isNullOrBlank()) normalize(note) else ""

        // Shtrix-kodga to'liq yoki boshlanish mosligi tekshiruvi (eng yuqori ball)
        if (normBarcode.isNotEmpty()) {
            val firstToken = tokens[0].original
            if (normBarcode == firstToken || normBarcode == tokens[0].layoutFixed) {
                return true to 5000
            }
            if (firstToken.length >= 3 && normBarcode.startsWith(firstToken)) {
                score += 2500
            }
        }

        for (token in tokens) {
            var tokenMatched = false

            // 1. Nomi bo'yicha tekshirish
            if (normalizedName.contains(token.original)) {
                tokenMatched = true
                score += if (normalizedName.startsWith(token.original)) 100 else 50
            } else if (token.latin.isNotEmpty() && normalizedName.contains(token.latin)) {
                tokenMatched = true
                score += if (normalizedName.startsWith(token.latin)) 90 else 45
            } else if (token.cyrillic.isNotEmpty() && normalizedName.contains(token.cyrillic)) {
                tokenMatched = true
                score += 40
            } else if (token.layoutFixed.isNotEmpty() && normalizedName.contains(token.layoutFixed)) {
                tokenMatched = true
                score += 35
            }

            // 2. Shtrix-kod bo'yicha tekshirish
            if (!tokenMatched && normBarcode.isNotEmpty()) {
                if (normBarcode.contains(token.original) || (token.layoutFixed.isNotEmpty() && normBarcode.contains(token.layoutFixed))) {
                    tokenMatched = true
                    score += 80
                }
            }

            // 3. Izoh bo'yicha tekshirish
            if (!tokenMatched && normNote.isNotEmpty()) {
                if (normNote.contains(token.original) || normNote.contains(token.latin) || normNote.contains(token.cyrillic)) {
                    tokenMatched = true
                    score += 20
                }
            }

            if (!tokenMatched) {
                return false to 0
            }
        }

        return true to score
    }

    fun <T> filterAndRank(
        source: List<T>,
        query: String,
        nameSelector: (T) -> String,
        barcodeSelector: (T) -> String?,
        noteSelector: (T) -> String?
    ): List<T> {
        val tokens = prepareTokens(query)
        if (tokens.isEmpty()) return source

        val matches = mutableListOf<Pair<T, Int>>()

        for (item in source) {
            val normName = normalize(nameSelector(item))
            val barcode = barcodeSelector(item)
            val note = noteSelector(item)

            val (matched, score) = isMatch(normName, barcode, note, tokens)
            if (matched) {
                matches.add(item to score)
            }
        }

        return matches
            .sortedWith(compareByDescending<Pair<T, Int>> { it.second }.thenBy { nameSelector(it.first).lowercase(Locale.ROOT) })
            .map { it.first }
    }
}
