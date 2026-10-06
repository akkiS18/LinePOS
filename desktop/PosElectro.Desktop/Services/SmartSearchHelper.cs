using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Aqlli qidiruv yordamchisi:
    /// 1. Tartibga bog'liq bo'lmagan ko'p so'zli token qidiruvi (unordered multi-word search).
    /// 2. Kril <-> Lotin ikki tomonlama transliteratsiyasi (ruscha klaviaturada yozsa ham topadi).
    /// 3. O'lchov birliklari va sonlar tolerantligi (2.5 <-> 2,5).
    /// 4. Relevance ranking (eng aniq mos kelgan tovarlar eng yuqorida turadi).
    /// </summary>
    public static class SmartSearchHelper
    {
        private static readonly Dictionary<string, string> CyrillicToLatinMulti = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ш", "sh" }, { "ч", "ch" }, { "ў", "o'" }, { "ғ", "g'" },
            { "ё", "yo" }, { "ю", "yu" }, { "я", "ya" }, { "ц", "ts" },
            { "ж", "j" },  { "қ", "q" },  { "ҳ", "h" },  { "х", "x" }
        };

        private static readonly Dictionary<char, string> CyrillicToLatinSingle = new()
        {
            { 'а', "a" }, { 'б', "b" }, { 'в', "v" }, { 'г', "g" }, { 'д', "d" },
            { 'е', "e" }, { 'з', "z" }, { 'и', "i" }, { 'й', "y" }, { 'к', "k" },
            { 'л', "l" }, { 'м', "m" }, { 'н', "n" }, { 'о', "o" }, { 'п', "p" },
            { 'р', "r" }, { 'с', "s" }, { 'т', "t" }, { 'у', "u" }, { 'ф', "f" },
            { 'х', "x" }, { 'э', "e" }, { 'ы', "i" }, { 'ь', "" },  { 'ъ', "'" }
        };

        private static readonly Dictionary<string, string> LatinToCyrillicMulti = new(StringComparer.OrdinalIgnoreCase)
        {
            { "sh", "ш" }, { "ch", "ч" }, { "o'", "ў" }, { "oʻ", "ў" }, { "o`", "ў" },
            { "g'", "ғ" }, { "gʻ", "ғ" }, { "g`", "ғ" }, { "yo", "ё" }, { "yu", "ю" },
            { "ya", "я" }, { "ts", "ц" }
        };

        private static readonly Dictionary<char, char> LatinToCyrillicSingle = new()
        {
            { 'a', 'а' }, { 'b', 'б' }, { 'v', 'в' }, { 'g', 'г' }, { 'd', 'д' },
            { 'e', 'е' }, { 'z', 'з' }, { 'i', 'и' }, { 'y', 'й' }, { 'k', 'к' },
            { 'l', 'л' }, { 'm', 'м' }, { 'n', 'н' }, { 'o', 'о' }, { 'p', 'п' },
            { 'r', 'р' }, { 's', 'с' }, { 't', 'т' }, { 'u', 'у' }, { 'f', 'ф' },
            { 'x', 'х' }, { 'h', 'ҳ' }, { 'q', 'қ' }, { 'j', 'ж' }
        };

        /// <summary>
        /// Matnni qidiruv uchun tozalash va normallashtirish:
        /// - Apostroflarni bitta standart ' ga keltirish
        /// - Sonlar orasidagi vergulni nuqtaga aylantirish (2,5 -> 2.5)
        /// - Ortiqcha probellarni tozalash
        /// </summary>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim().ToLowerInvariant();

            // Standart apostrof
            s = Regex.Replace(s, @"[''`ʹʻʼ’‘]", "'");

            // Sonlar orasidagi vergulni nuqtaga aylantirish (masalan: 2,5 -> 2.5)
            s = Regex.Replace(s, @"(?<=\d),(?=\d)", ".");

            // Ketma-ket probellarni bittaga keltirish
            s = Regex.Replace(s, @"\s+", " ");

            return s;
        }

        /// <summary>
        /// Krilcha matnni Lotinchaga o'tkazish
        /// </summary>
        public static string CyrillicToLatin(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var sb = new StringBuilder(input.ToLowerInvariant());

            // 1. Ko'p harfli kombinatsiyalar
            foreach (var kvp in CyrillicToLatinMulti)
            {
                sb.Replace(kvp.Key, kvp.Value);
            }

            // 2. Bir harfliklar
            var result = new StringBuilder();
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (CyrillicToLatinSingle.TryGetValue(c, out var lat))
                {
                    result.Append(lat);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Lotincha matnni Krilchaga o'tkazish
        /// </summary>
        public static string LatinToCyrillic(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var sb = new StringBuilder(input.ToLowerInvariant());

            // 1. Ko'p harfli kombinatsiyalar
            foreach (var kvp in LatinToCyrillicMulti)
            {
                sb.Replace(kvp.Key, kvp.Value);
            }

            // 2. Bir harfliklar
            var result = new StringBuilder();
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (LatinToCyrillicSingle.TryGetValue(c, out var cyr))
                {
                    result.Append(cyr);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        public class SearchTokenVariants
        {
            public string Original { get; set; } = string.Empty;
            public string Latin { get; set; } = string.Empty;
            public string Cyrillic { get; set; } = string.Empty;
            public string LayoutFixed { get; set; } = string.Empty;
        }

        /// <summary>
        /// Qidiruv so'rovidan barcha token variantlarini tayyorlash
        /// </summary>
        public static List<SearchTokenVariants> PrepareTokens(string query)
        {
            var clean = Normalize(query);
            if (string.IsNullOrWhiteSpace(clean)) return new List<SearchTokenVariants>();

            var rawTokens = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<SearchTokenVariants>();

            foreach (var raw in rawTokens)
            {
                var enLayout = KeyboardLayoutHelper.ConvertRuToEn(raw).ToLowerInvariant();
                result.Add(new SearchTokenVariants
                {
                    Original = raw,
                    Latin = CyrillicToLatin(raw),
                    Cyrillic = LatinToCyrillic(raw),
                    LayoutFixed = enLayout
                });
            }

            return result;
        }

        /// <summary>
        /// Berilgan mahsulot tokenlar to'plamiga mos keladimi?
        /// Barcha tokenlar mahsulot nomi, shtrix-kodi yoki izohida istalgan tartibda topilishi kerak.
        /// </summary>
        public static bool IsMatch(
            string normalizedName, 
            string? barcode, 
            string? note, 
            List<SearchTokenVariants> tokens,
            out int score)
        {
            score = 0;
            if (tokens.Count == 0) return true;

            var normBarcode = barcode?.Trim().ToLowerInvariant() ?? string.Empty;
            var normNote = note != null ? Normalize(note) : string.Empty;

            // Shtrix-kodga to'liq yoki boshlanish mosligi tekshiruvi (eng yuqori ball)
            if (!string.IsNullOrEmpty(normBarcode))
            {
                var firstToken = tokens[0].Original;
                if (normBarcode == firstToken || normBarcode == tokens[0].LayoutFixed)
                {
                    score += 5000;
                    return true;
                }
                if (firstToken.Length >= 3 && normBarcode.StartsWith(firstToken))
                {
                    score += 2500;
                }
            }

            // Har bir token mahsulot maydonlarida mavjud bo'lishi shart
            foreach (var token in tokens)
            {
                bool tokenMatched = false;

                // 1. Nomi bo'yicha tekshirish (Original, Lotin, Kril, Klaviatura)
                if (normalizedName.Contains(token.Original))
                {
                    tokenMatched = true;
                    score += (normalizedName.StartsWith(token.Original) ? 100 : 50);
                }
                else if (!string.IsNullOrEmpty(token.Latin) && normalizedName.Contains(token.Latin))
                {
                    tokenMatched = true;
                    score += (normalizedName.StartsWith(token.Latin) ? 90 : 45);
                }
                else if (!string.IsNullOrEmpty(token.Cyrillic) && normalizedName.Contains(token.Cyrillic))
                {
                    tokenMatched = true;
                    score += 40;
                }
                else if (!string.IsNullOrEmpty(token.LayoutFixed) && normalizedName.Contains(token.LayoutFixed))
                {
                    tokenMatched = true;
                    score += 35;
                }

                // 2. Shtrix-kod bo'yicha tekshirish
                if (!tokenMatched && !string.IsNullOrEmpty(normBarcode))
                {
                    if (normBarcode.Contains(token.Original) || (!string.IsNullOrEmpty(token.LayoutFixed) && normBarcode.Contains(token.LayoutFixed)))
                    {
                        tokenMatched = true;
                        score += 80;
                    }
                }

                // 3. Izoh bo'yicha tekshirish
                if (!tokenMatched && !string.IsNullOrEmpty(normNote))
                {
                    if (normNote.Contains(token.Original) || normNote.Contains(token.Latin) || normNote.Contains(token.Cyrillic))
                    {
                        tokenMatched = true;
                        score += 20;
                    }
                }

                // Agar birorta token topilmasa, butun mahsulot mos kelmaydi
                if (!tokenMatched)
                {
                    score = 0;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Mahsulotlar ro'yxatini aqlli tarzda filtrlash va reyting bo'yicha saralash
        /// </summary>
        public static List<T> FilterAndRank<T>(
            IEnumerable<T> source,
            string query,
            Func<T, string> nameSelector,
            Func<T, string?> barcodeSelector,
            Func<T, string?> noteSelector)
        {
            var tokens = PrepareTokens(query);
            if (tokens.Count == 0) return source.ToList();

            var matches = new List<(T item, int score)>();

            foreach (var item in source)
            {
                var normName = Normalize(nameSelector(item));
                var barcode = barcodeSelector(item);
                var note = noteSelector(item);

                if (IsMatch(normName, barcode, note, tokens, out int score))
                {
                    matches.Add((item, score));
                }
            }

            // Ball bo'yicha kamayish tartibida, keyin nomi bo'yicha alifboda
            return matches
                .OrderByDescending(m => m.score)
                .ThenBy(m => nameSelector(m.item), StringComparer.CurrentCultureIgnoreCase)
                .Select(m => m.item)
                .ToList();
        }
    }
}
