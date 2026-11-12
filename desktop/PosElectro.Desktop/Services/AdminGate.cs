using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows;
using PosElectro.Desktop.Data;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Do'kon egasining Admin PIN'i: maxfiy raqamlarni (foyda, tan narx, karta foizi) kassirdan yashiradi
    /// va bazani tiklash kabi xavfli amallarni himoyalaydi.
    /// Dasturni ochadigan aktivatsiya parolidan (dasturchida) butunlay alohida.
    /// Odatda o'chiq. Qulf faqat shu sessiya uchun ochiladi — dastur qayta ishga tushganda yana yopiladi.
    /// XAML: {Binding CanSeeSecrets, Source={x:Static services:AdminGate.Current}}
    /// </summary>
    public sealed class AdminGate : INotifyPropertyChanged
    {
        private const string PinKey = "admin_pin_hash";
        public static AdminGate Current { get; } = new AdminGate();

        private DatabaseContext? _db;
        private bool _isEnabled;
        private bool _isUnlocked;

        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>Qulf holati o'zgarganda (ochildi/yopildi/yoqildi/o'chirildi).</summary>
        public event Action? Changed;

        public void Initialize(DatabaseContext db)
        {
            _db = db;
            Reload();
        }

        /// <summary>Baza almashtirilganda (tiklash) sozlamani qayta o'qish.</summary>
        public void Reload()
        {
            _isEnabled = !string.IsNullOrEmpty(_db?.GetSetting(PinKey, string.Empty));
            _isUnlocked = false;
            Notify();
        }

        public bool IsEnabled => _isEnabled;
        public bool IsUnlocked => _isUnlocked;
        public bool CanSeeSecrets => !_isEnabled || _isUnlocked;
        public bool IsLocked => !CanSeeSecrets;
        /// <summary>Yuqori panelda "Admin rejimi ochiq · Qulflash" belgisini ko'rsatish uchun.</summary>
        public bool ShowUnlockedChip => _isEnabled && _isUnlocked;

        public bool Verify(string pin)
        {
            var stored = _db?.GetSetting(PinKey, string.Empty) ?? string.Empty;
            var parts = stored.Split(':');
            if (parts.Length != 2) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expected = Convert.FromBase64String(parts[1]);
                var actual = Hash(pin, salt);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch { return false; }
        }

        public bool TryUnlock(string pin)
        {
            if (!_isEnabled) return true;
            if (!Verify(pin)) return false;
            _isUnlocked = true;
            Notify();
            return true;
        }

        public void Lock()
        {
            _isUnlocked = false;
            Notify();
        }

        /// <summary>Yangi PIN o'rnatadi va himoyani yoqadi. O'rnatgan odam uchun qulf ochiq qoladi.</summary>
        public void SetPin(string pin)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Hash(pin, salt);
            _db?.SetSetting(PinKey, Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash));
            _isEnabled = true;
            _isUnlocked = true;
            Notify();
        }

        public void Disable()
        {
            _db?.SetSetting(PinKey, string.Empty);
            _isEnabled = false;
            _isUnlocked = false;
            Notify();
        }

        /// <summary>
        /// Maxfiy amal oldidan chaqiriladi: himoya o'chiq yoki qulf ochiq bo'lsa darhol true,
        /// aks holda PIN so'raydi.
        /// </summary>
        public bool EnsureUnlocked(string reason)
        {
            if (CanSeeSecrets) return true;
            var dlg = new Views.AdminPinPromptDialog(reason) { Owner = Application.Current?.MainWindow };
            return dlg.ShowDialog() == true && CanSeeSecrets;
        }

        /// <summary>Yashirilgan raqam yonidagi "Ko'rish" tugmasi uchun.</summary>
        public System.Windows.Input.ICommand UnlockCommand { get; } =
            new ViewModels.RelayCommand(() => Current.EnsureUnlocked("Foyda va tan narxni ko'rish uchun Admin PIN kerak."));

        private static byte[] Hash(string pin, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(pin ?? string.Empty, salt, 100_000, HashAlgorithmName.SHA256, 32);

        private void Notify()
        {
            foreach (var p in new[] { nameof(IsEnabled), nameof(IsUnlocked), nameof(CanSeeSecrets), nameof(IsLocked), nameof(ShowUnlockedChip) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
            Changed?.Invoke();
        }
    }
}
