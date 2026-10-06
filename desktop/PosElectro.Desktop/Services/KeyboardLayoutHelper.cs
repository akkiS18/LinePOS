using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace PosElectro.Desktop.Services
{
    public static class KeyboardLayoutHelper
    {
        private const uint KLF_ACTIVATE = 0x00000001;
        private const string EN_US_KLID = "00000409";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        private static readonly Dictionary<char, char> RuToEnMap = new()
        {
            {'й', 'q'}, {'ц', 'w'}, {'у', 'e'}, {'к', 'r'}, {'е', 't'}, {'н', 'y'}, {'г', 'u'}, {'ш', 'i'}, {'щ', 'o'}, {'з', 'p'}, {'х', '['}, {'ъ', ']'},
            {'ф', 'a'}, {'ы', 's'}, {'в', 'd'}, {'а', 'f'}, {'п', 'g'}, {'р', 'h'}, {'о', 'j'}, {'л', 'k'}, {'д', 'l'}, {'ж', ';'}, {'э', '\''},
            {'я', 'z'}, {'ч', 'x'}, {'с', 'c'}, {'м', 'v'}, {'и', 'b'}, {'т', 'n'}, {'ь', 'm'}, {'б', ','}, {'ю', '.'},
            {'Й', 'Q'}, {'Ц', 'W'}, {'У', 'E'}, {'К', 'R'}, {'Е', 'T'}, {'Н', 'Y'}, {'Г', 'U'}, {'Ш', 'I'}, {'Щ', 'O'}, {'З', 'P'}, {'Х', '{'}, {'Ъ', '}'},
            {'Ф', 'A'}, {'Ы', 'S'}, {'В', 'D'}, {'А', 'F'}, {'П', 'G'}, {'Р', 'H'}, {'О', 'J'}, {'Л', 'K'}, {'Д', 'L'}, {'Ж', ':'}, {'Э', '"'},
            {'Я', 'Z'}, {'Ч', 'X'}, {'С', 'C'}, {'М', 'V'}, {'И', 'B'}, {'Т', 'N'}, {'Ь', 'M'}, {'Б', '<'}, {'Ю', '>'}
        };

        public static void ForceEnglishLayout()
        {
            try
            {
                IntPtr hkl = LoadKeyboardLayout(EN_US_KLID, KLF_ACTIVATE);
                if (hkl != IntPtr.Zero)
                {
                    ActivateKeyboardLayout(hkl, KLF_ACTIVATE);
                }
                InputLanguageManager.Current.CurrentInputLanguage = CultureInfo.GetCultureInfo("en-US");
            }
            catch
            {
                // Fallback gracefully
            }
        }

        public static string ConvertRuToEn(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                sb.Append(RuToEnMap.TryGetValue(c, out var enChar) ? enChar : c);
            }
            return sb.ToString();
        }

        public static char ConvertCharRuToEn(char c)
        {
            return RuToEnMap.TryGetValue(c, out var enChar) ? enChar : c;
        }

        public static string FixBarcodeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;
            
            var shiftNumberMap = new Dictionary<char, char>() {
                {'!', '1'}, {'@', '2'}, {'#', '3'}, {'$', '4'}, {'%', '5'}, 
                {'^', '6'}, {'&', '7'}, {'*', '8'}, {'(', '9'}, {')', '0'},
                // Russian layout shift numbers:
                {'"', '2'}, {'№', '3'}, {';', '4'}, {':', '6'}, {'?', '7'}
            };

            bool hasSymbol = false;
            bool hasNonConvertible = false;
            var sb = new StringBuilder();
            
            string trimmed = input.Trim();
            foreach (var c in trimmed)
            {
                if (shiftNumberMap.TryGetValue(c, out var num))
                {
                    sb.Append(num);
                    hasSymbol = true;
                }
                else if (char.IsDigit(c))
                {
                    sb.Append(c);
                }
                else
                {
                    hasNonConvertible = true;
                    break;
                }
            }

            // Agar kamida bitta belgi bo'lsa, ichida yot belgi bo'lmasa va uzunligi yetarli bo'lsa, to'g'rilanganini qaytaramiz
            if (!hasNonConvertible && hasSymbol && sb.Length >= 3)
            {
                return sb.ToString();
            }
            
            return input;
        }
    }
}
