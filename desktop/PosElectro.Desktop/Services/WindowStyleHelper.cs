using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

        /// <summary>
        /// Oyna sarlavha panelini (Title bar) loyiha to'q rangiga (#0F172A) o'tkazish.
        /// Windows native tugmalari (-, [ ], X) va sarlavha matni/logotipi joylashuvi mutlaqo buzilmaydi.
        /// </summary>
        public static void ApplyDarkTheme(Window window)
        {
            if (window == null) return;

            void Apply()
            {
                try
                {
                    var helper = new WindowInteropHelper(window);
                    var hwnd = helper.Handle;
                    if (hwnd == IntPtr.Zero)
                    {
                        hwnd = helper.EnsureHandle();
                    }

                    if (hwnd != IntPtr.Zero)
                    {
                        int trueVal = 1;
                        // Windows 10 (17763+)
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueVal, sizeof(int));
                        // Windows 10 (18985+) va Windows 11
                        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueVal, sizeof(int));

                        // Windows 11: Exact caption color in COLORREF format (0x00BBGGRR) -> #0F172A = 0x002A170F
                        int captionColor = 0x002A170F;
                        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                        // Windows 11: Title text color in COLORREF -> White = 0x00FFFFFF
                        int textColor = 0x00FFFFFF;
                        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
                    }
                }
                catch
                {
                    // DWM yoki eski OS versiyalarida xatolik chiqmasligi uchun
                }
            }

            if (window.IsLoaded)
            {
                Apply();
            }
            else
            {
                window.SourceInitialized += (s, e) => Apply();
                window.Loaded += (s, e) => Apply();
            }
        }
    }
}
