using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PosElectro.Desktop.Data;

namespace PosElectro.Desktop.Services
{
    public static class BarcodeGeneratorHelper
    {
        private static readonly Random _random = new();

        /// <summary>
        /// Bazada mavjud bo'lmagan, do'kon ichki standarti (20 prefiksi) bo'yicha 13 xonali EAN-13 shtrix-kod yaratadi.
        /// </summary>
        public static string GenerateUniqueEan13(DatabaseContext db)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                // 20 prefiksi (Do'kon ichki foydalanish uchun GS1 standarti)
                // Keyingi 10 ta raqam
                long numPart = (long)(_random.NextDouble() * 9000000000L) + 1000000000L;
                string first12 = $"20{numPart}";

                int checksum = CalculateEan13Checksum(first12);
                string candidate = $"{first12}{checksum}";

                var existing = db.GetProductByBarcode(candidate);
                if (existing == null)
                {
                    return candidate;
                }
            }

            // Agar tasodifiy to'qnashsa, vaqt tamg'asi asosida
            string timeBase = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string sub = timeBase.Length > 10 ? timeBase.Substring(timeBase.Length - 10) : timeBase.PadLeft(10, '0');
            string f12 = $"20{sub}";
            return $"{f12}{CalculateEan13Checksum(f12)}";
        }

        public static int CalculateEan13Checksum(string first12)
        {
            if (first12.Length < 12) first12 = first12.PadRight(12, '0');
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int val = first12[i] - '0';
                sum += (i % 2 == 0) ? val : val * 3;
            }
            return (10 - (sum % 10)) % 10;
        }

        /// <summary>
        /// Berilgan matn/raqamli shtrix-kod uchun 0 va 1 lardan iborat modul ketma-ketligini qaytaradi.
        /// 13 xonali raqamlar uchun dunyo standarti bo'yicha EAN-13 (95 modul) ishlatiladi.
        /// Bu har qanday skanerda 100% tez va xatosiz o'qilishini ta'minlaydi.
        /// </summary>
        public static string GetBarcodePattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) text = "2000000000000";

            // Agar aynan 13 xonali raqam bo'lsa -> EAN-13 (95 modul, optimal va o'ta aniq)
            if (text.Length == 13 && text.All(char.IsDigit))
            {
                return EncodeEan13(text);
            }

            // Boshqa har qanday matn / kod uchun -> Code-128
            return EncodeCode128B(text);
        }

        private static readonly string[] EanParity = {
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
        };

        private static readonly string[] EanL = {
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011"
        };

        private static readonly string[] EanG = {
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111"
        };

        private static readonly string[] EanR = {
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100"
        };

        private static string EncodeEan13(string text)
        {
            int d0 = text[0] - '0';
            string parity = EanParity[d0];

            var sb = new System.Text.StringBuilder();
            // Start Guard
            sb.Append("101");

            // Left 6 digits
            for (int i = 1; i <= 6; i++)
            {
                int digit = text[i] - '0';
                sb.Append(parity[i - 1] == 'L' ? EanL[digit] : EanG[digit]);
            }

            // Center Guard
            sb.Append("01010");

            // Right 6 digits
            for (int i = 7; i <= 12; i++)
            {
                int digit = text[i] - '0';
                sb.Append(EanR[digit]);
            }

            // End Guard
            sb.Append("101");

            return sb.ToString();
        }

        /// <summary>
        /// Berilgan matn/raqamli shtrix-kodni chizib BitmapSource qaytaradi (WPF Image uchun).
        /// </summary>
        public static BitmapSource GenerateBarcodeImage(string text, int width = 300, int height = 80)
        {
            if (string.IsNullOrWhiteSpace(text)) text = "00000000";

            string patterns = GetBarcodePattern(text);

            int moduleCount = patterns.Length;
            int dpi = 96;
            int stride = (width * 4); // 32bpp BGRA
            byte[] pixelData = new byte[stride * height];

            // Fonni oq qilish
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i] = 255;     // B
                pixelData[i + 1] = 255; // G
                pixelData[i + 2] = 255; // R
                pixelData[i + 3] = 255; // A
            }

            // Chiziqlarni chizish
            int margin = Math.Max(4, width / 25);
            int printableWidth = width - (margin * 2);
            double moduleWidth = (double)printableWidth / moduleCount;

            for (int m = 0; m < moduleCount; m++)
            {
                if (patterns[m] == '1')
                {
                    int startX = margin + (int)Math.Floor(m * moduleWidth);
                    int endX = margin + (int)Math.Ceiling((m + 1) * moduleWidth);
                    if (endX > width - margin) endX = width - margin;

                    for (int x = startX; x < endX; x++)
                    {
                        for (int y = 2; y < height - 2; y++)
                        {
                            int idx = (y * stride) + (x * 4);
                            if (idx + 3 < pixelData.Length)
                            {
                                pixelData[idx] = 0;     // B
                                pixelData[idx + 1] = 0; // G
                                pixelData[idx + 2] = 0; // R
                                pixelData[idx + 3] = 255;
                            }
                        }
                    }
                }
            }

            var bitmap = BitmapSource.Create(
                width, height, dpi, dpi,
                PixelFormats.Bgra32, null, pixelData, stride);
            bitmap.Freeze();
            return bitmap;
        }

        // Code 128 Type B kodlash jadvallari
        private static readonly string[] Code128Patterns = {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        private static string EncodeCode128B(string text)
        {
            var codes = new List<int>();
            codes.Add(104); // Start Code B

            int checksum = 104;
            for (int i = 0; i < text.Length; i++)
            {
                int code = text[i] - 32;
                if (code < 0 || code > 95) code = 0;
                codes.Add(code);
                checksum += code * (i + 1);
            }

            codes.Add(checksum % 103);
            codes.Add(106); // Stop Code

            var sb = new System.Text.StringBuilder();
            // Quiet zone
            sb.Append("0000000000");

            foreach (var code in codes)
            {
                string p = Code128Patterns[code];
                for (int i = 0; i < p.Length; i++)
                {
                    int width = p[i] - '0';
                    char bit = (i % 2 == 0) ? '1' : '0';
                    sb.Append(new string(bit, width));
                }
            }

            // Final quiet zone
            sb.Append("0000000000");
            return sb.ToString();
        }
    }
}
