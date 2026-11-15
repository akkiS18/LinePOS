using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PosElectro.Desktop.Data;

namespace PosElectro.Desktop.Services
{
    public enum ThemeMode
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Yorug' / qorong'i temani butun dastur uchun bitta joydan boshqaradi.
    /// Ranglar Theme.Light.xaml / Theme.Dark.xaml da (kalitlari bir xil). Ekranlar ularni faqat DynamicResource
    /// orqali ishlatadi, shuning uchun App darajasidagi lug'atni almashtirish hamma oynani darhol qayta bo'yaydi.
    /// Tanlov bazadagi app_settings ga ("ui_theme") yoziladi.
    /// </summary>
    public static class ThemeManager
    {
        public const string SettingKey = "ui_theme";
        private const string LightUri = "/PosElectro.Desktop;component/Themes/Theme.Light.xaml";
        private const string DarkUri = "/PosElectro.Desktop;component/Themes/Theme.Dark.xaml";

        private static DatabaseContext? _db;
        private static bool _systemHooked;

        public static ThemeMode Mode { get; private set; } = ThemeMode.Light;
        public static bool IsDark { get; private set; }

        /// <summary>Tema o'zgarganda (masalan, sarlavha qatorini qayta bo'yash uchun).</summary>
        public static event Action? ThemeChanged;

        /// <summary>Dastur ishga tushganda, birinchi oyna ochilishidan oldin chaqiriladi.</summary>
        public static void Initialize(DatabaseContext? db)
        {
            _db = db;
            var mode = ThemeMode.Light;
            try
            {
                var saved = db?.GetSetting(SettingKey, "light") ?? "light";
                mode = Parse(saved);
            }
            catch
            {
                // Sozlamani o'qib bo'lmasa — yorug' tema
            }
            Apply(mode, persist: false);
        }

        public static ThemeMode Parse(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "dark" => ThemeMode.Dark,
            "system" => ThemeMode.System,
            _ => ThemeMode.Light
        };

        public static string ToSetting(ThemeMode mode) => mode switch
        {
            ThemeMode.Dark => "dark",
            ThemeMode.System => "system",
            _ => "light"
        };

        public static void Apply(ThemeMode mode, bool persist = true)
        {
            Mode = mode;
            if (mode == ThemeMode.System) HookSystemChanges();

            var dark = mode == ThemeMode.Dark || (mode == ThemeMode.System && IsSystemDark());
            SwapDictionary(dark);

            if (persist)
            {
                try { _db?.SetSetting(SettingKey, ToSetting(mode)); }
                catch { /* saqlanmasa ham tema shu seansda ishlaydi */ }
            }
        }

        private static void SwapDictionary(bool dark)
        {
            var app = Application.Current;
            IsDark = dark;
            if (app != null)
            {
                var merged = app.Resources.MergedDictionaries;
                var target = new Uri(dark ? DarkUri : LightUri, UriKind.Relative);
                var existing = merged.FirstOrDefault(IsThemeDictionary);
                if (existing == null || existing.Source == null || existing.Source.OriginalString != target.OriginalString)
                {
                    var fresh = new ResourceDictionary { Source = target };
                    if (existing != null)
                    {
                        merged[merged.IndexOf(existing)] = fresh;
                    }
                    else
                    {
                        merged.Insert(0, fresh);
                    }
                }

                foreach (Window w in app.Windows)
                {
                    WindowStyleHelper.ApplyTitleBar(w);
                }
            }
            ThemeChanged?.Invoke();
        }

        private static bool IsThemeDictionary(ResourceDictionary d)
        {
            var src = d.Source?.OriginalString ?? string.Empty;
            return src.EndsWith("Theme.Light.xaml", StringComparison.OrdinalIgnoreCase)
                || src.EndsWith("Theme.Dark.xaml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Windows "ilovalar rejimi" qorong'imi (Sozlamalar → Shaxsiylashtirish → Ranglar).</summary>
        public static bool IsSystemDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void HookSystemChanges()
        {
            if (_systemHooked) return;
            _systemHooked = true;
            try
            {
                SystemEvents.UserPreferenceChanged += (_, e) =>
                {
                    if (Mode != ThemeMode.System || e.Category != UserPreferenceCategory.General) return;
                    var dark = IsSystemDark();
                    if (dark == IsDark) return;
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => SwapDictionary(dark)));
                };
            }
            catch
            {
                // Tizim hodisalariga ulanib bo'lmasa, tema dastur qayta ochilganda yangilanadi
            }
        }

        /// <summary>Joriy temadagi rang (sarlavha qatori uchun).</summary>
        public static Color GetColor(string key, Color fallback)
        {
            try
            {
                if (Application.Current?.TryFindResource(key) is SolidColorBrush b) return b.Color;
            }
            catch { }
            return fallback;
        }
    }
}
