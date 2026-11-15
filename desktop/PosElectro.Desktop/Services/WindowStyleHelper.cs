using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PosElectro.Desktop.Services
{
    public static class WindowStyleHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        /// <summary>Eski nom: endi joriy temaga mos sarlavha qatorini qo'yadi.</summary>
        public static void ApplyDarkTheme(Window window) => ApplyTitleBar(window);

        /// <summary>
        /// Oyna sarlavha qatorini (Title bar) joriy temaga moslaydi: qorong'i temada qorong'i rejim, rangi ekran
        /// yuzasi (Surface) bilan bir xil. Windows tugmalari (-, [ ], X) va sarlavha joylashuvi o'zgarmaydi.
        /// </summary>
        public static void ApplyTitleBar(Window window)
        {
            if (window == null) return;

            void Apply()
            {
                try
                {
                    var helper = new WindowInteropHelper(window);
                    var hwnd = helper.Handle;
                    if (hwnd == IntPtr.Zero) return;

                    bool dark = ThemeManager.IsDark;
                    int darkVal = dark ? 1 : 0;
                    // Windows 10 (17763+)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkVal, sizeof(int));
                    // Windows 10 (18985+) va Windows 11
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkVal, sizeof(int));

                    // Windows 11: sarlavha rangi = Surface, matni = Ink (COLORREF 0x00BBGGRR)
                    var caption = ThemeManager.GetColor("Surface", dark ? Color.FromRgb(0x12, 0x1B, 0x1F) : Colors.White);
                    var text = ThemeManager.GetColor("Ink", dark ? Color.FromRgb(0xE7, 0xEE, 0xF1) : Color.FromRgb(0x0F, 0x1B, 0x21));
                    int captionColor = caption.R | (caption.G << 8) | (caption.B << 16);
                    int textColor = text.R | (text.G << 8) | (text.B << 16);
                    DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
                    DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
                }
                catch
                {
                    // DWM yoki eski OS versiyalarida xatolik chiqmasligi uchun
                }
            }

            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            {
                Apply();
            }
            else
            {
                window.SourceInitialized += (s, e) => Apply();
            }
        }
    }
}
