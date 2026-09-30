using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;

        public DatabaseContext Database { get; }
        public ProductService ProductService { get; }
        public CurrencyService CurrencyService { get; }
        public BarcodeScannerService ScannerService { get; }
        public LocalSyncServer SyncServer { get; }
        public DeviceLicensingService LicensingService { get; }

        public CashierViewModel CashierVM { get; }
        public InventoryViewModel InventoryVM { get; }
        public ReportsViewModel ReportsVM { get; }
        public SyncViewModel SyncVM { get; }

        public ICommand NavigateCashierCommand { get; }
        public ICommand NavigateInventoryCommand { get; }
        public ICommand NavigateReportsCommand { get; }
        public ICommand NavigateSyncCommand { get; }
        public ICommand BackupDatabaseCommand { get; }

        // Jonli Wi-Fi sinxron holati
        private int _liveClientsCount;
        public int LiveClientsCount
        {
            get => _liveClientsCount;
            set
            {
                if (SetProperty(ref _liveClientsCount, value))
                {
                    OnPropertyChanged(nameof(IsLiveSyncActive));
                    OnPropertyChanged(nameof(LiveSyncStatusText));
                }
            }
        }
        public bool IsLiveSyncActive => LiveClientsCount > 0;
        public string LiveSyncStatusText => LiveClientsCount > 0 ? $"🟢 {LiveClientsCount} ta mobil" : "⚪ Wi-Fi faol";

        // Aktivatsiya holatlari va buyruqlari
        private bool _isDeviceLocked;
        private bool _isPinModalOpen;
        private string _pinInputText = string.Empty;
        private bool _isPinVisible;
        private string _pinErrorMessage = string.Empty;
        private bool _isCopiedNoticeVisible;
        private int _logoClickCount;
        private long _lastLogoClickTime;

        public ICommand OpenPinModalCommand { get; }
        public ICommand ClosePinModalCommand { get; }
        public ICommand SubmitPinCommand { get; }
        public ICommand TogglePinVisibilityCommand { get; }
        public ICommand CopyDeviceIdCommand { get; }
        public ICommand LogoClickCommand { get; }

        // Ishlab chiqaruvchi bilan bog'lanish modali
        private bool _isContactModalOpen;
        public ICommand OpenContactModalCommand { get; }
        public ICommand CloseContactModalCommand { get; }
        public ICommand CopyPhoneCommand { get; }
        public ICommand OpenTelegramCommand { get; }

        // Karta to'lovi soliq foizini sozlash modali
        private bool _isCardTaxModalOpen;
        private string _cardTaxRateInput = "1.8";
        private string _cardTaxErrorMessage = string.Empty;
        public ICommand OpenCardTaxModalCommand { get; }
        public ICommand CloseCardTaxModalCommand { get; }
        public ICommand SaveCardTaxCommand { get; }

        public MainViewModel()
        {
            Database = new DatabaseContext();
            ProductService = new ProductService(Database);
            CurrencyService = new CurrencyService(Database);
            ScannerService = new BarcodeScannerService();
            SyncServer = new LocalSyncServer(Database, CurrencyService);
            LicensingService = new DeviceLicensingService(Database);

            _isDeviceLocked = !LicensingService.IsActivated;

            LicensingService.ActivationStateChanged += activated =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    IsDeviceLocked = !activated;
                    if (activated)
                    {
                        IsPinModalOpen = false;
                        PinErrorMessage = string.Empty;
                    }
                });
            };

            CashierVM = new CashierViewModel(Database, ProductService, CurrencyService);
            InventoryVM = new InventoryViewModel(Database, ProductService, CurrencyService);

            // Kassada topilmagan shtrix-kod uchun "Ha, qo'shish" tanlanganda omborga yangi mahsulot qo'shish oynasiga o'tish
            CashierVM.RequestOpenAddProductWithBarcode += barcode =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    InventoryVM.Refresh();
                    InventoryVM.OpenAddProduct(barcode);
                    CurrentView = InventoryVM;
                });
            };

            ReportsVM = new ReportsViewModel(Database, CurrencyService);
            SyncVM = new SyncViewModel(SyncServer, Database);

            SyncServer.LiveClientsCountChanged += count =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    LiveClientsCount = count;
                });
            };

            // Orqa fonda Markaziy bank kursini yangilash
            _ = CurrencyService.FetchLatestUsdRateAsync();

            // Standart boshlang'ich oyna: Har doim KASSA bo'lib ochiladi!
            _currentView = CashierVM;

            NavigateCashierCommand = new RelayCommand(() =>
            {
                CashierVM.RefreshCategories();
                CashierVM.RefreshProducts();
                CurrentView = CashierVM;
            });
            NavigateInventoryCommand = new RelayCommand(() =>
            {
                InventoryVM.Refresh();
                CurrentView = InventoryVM;
            });
            NavigateReportsCommand = new RelayCommand(() =>
            {
                ReportsVM.LoadData();
                CurrentView = ReportsVM;
            });
            NavigateSyncCommand = new RelayCommand(() => CurrentView = SyncVM);
            BackupDatabaseCommand = new RelayCommand(PerformDatabaseBackup);

            OpenPinModalCommand = new RelayCommand(() =>
            {
                PinInputText = string.Empty;
                PinErrorMessage = string.Empty;
                IsPinVisible = false; // Xavfsiz: har doim yashirin holda ochiladi
                IsPinModalOpen = true;
            });

            ClosePinModalCommand = new RelayCommand(() =>
            {
                IsPinModalOpen = false;
                PinInputText = string.Empty;
                PinErrorMessage = string.Empty;
                IsPinVisible = false;
            });

            TogglePinVisibilityCommand = new RelayCommand(() =>
            {
                IsPinVisible = !IsPinVisible;
            });

            SubmitPinCommand = new RelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(PinInputText))
                {
                    PinErrorMessage = "Iltimos, PIN kodni kiriting";
                    return;
                }

                if (await LicensingService.ActivateWithAdminPinAsync(PinInputText))
                {
                    IsPinModalOpen = false;
                    PinInputText = string.Empty;
                    PinErrorMessage = string.Empty;
                }
                else
                {
                    PinErrorMessage = "❌ PIN kod noto'g'ri (yoki internet yo'q)!";
                }
            });

            CopyDeviceIdCommand = new RelayCommand(() =>
            {
                try
                {
                    Clipboard.SetText(DeviceId);
                    IsCopiedNoticeVisible = true;
                    System.Threading.Tasks.Task.Delay(2500).ContinueWith(_ =>
                    {
                        App.Current?.Dispatcher.Invoke(() => IsCopiedNoticeVisible = false);
                    });
                }
                catch { }
            });

            LogoClickCommand = new RelayCommand(() =>
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _lastLogoClickTime < 600)
                {
                    _logoClickCount++;
                    if (_logoClickCount >= 3)
                    {
                        _logoClickCount = 0;
                        PinInputText = string.Empty;
                        PinErrorMessage = string.Empty;
                        IsPinModalOpen = true;
                    }
                }
                else
                {
                    _logoClickCount = 1;
                }
                _lastLogoClickTime = now;
            });

            OpenContactModalCommand = new RelayCommand(() => IsContactModalOpen = true);
            CloseContactModalCommand = new RelayCommand(() => IsContactModalOpen = false);

            OpenCardTaxModalCommand = new RelayCommand(() =>
            {
                CardTaxRateInput = Database.GetCardTaxRate().ToString("0.##");
                CardTaxErrorMessage = string.Empty;
                IsCardTaxModalOpen = true;
            });

            CloseCardTaxModalCommand = new RelayCommand(() =>
            {
                IsCardTaxModalOpen = false;
                CardTaxErrorMessage = string.Empty;
            });

            SaveCardTaxCommand = new RelayCommand(() =>
            {
                var raw = (CardTaxRateInput ?? string.Empty).Replace(" ", "").Replace("\u00A0", "").Replace(',', '.').Trim();
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate >= 0 && rate <= 100)
                {
                    rate = Math.Round(rate, 2);
                    Database.SetCardTaxRate(rate);
                    CashierVM.CurrentCardTaxRate = rate;
                    IsCardTaxModalOpen = false;
                    CashierVM.ShowToast("Soliq foizi saqlandi! 🎉", $"Karta to'lovlari uchun belgilandi: {rate}%");
                }
                else
                {
                    CardTaxErrorMessage = "0 dan 100 gacha to'g'ri foiz kiriting (masalan: 1.8)";
                }
            });

            CopyPhoneCommand = new RelayCommand(() =>
            {
                try
                {
                    Clipboard.SetText("+998957208833");
                    MessageBox.Show("Telefon raqam nusxalandi: +998 95 720 88 33", "Nusxalandi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            });

            OpenTelegramCommand = new RelayCommand(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://t.me/S18_2003",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Clipboard.SetText("@S18_2003");
                    MessageBox.Show("Telegram username nusxalandi: @S18_2003", "Telegram", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            // Baza mahsulotlari o'zgarganda barcha ekranlarni real-vaqtda yangilash
            Database.ProductsChanged += () =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    CashierVM.RefreshCategories();
                    CashierVM.RefreshProducts();
                    InventoryVM.Refresh();
                    ReportsVM.LoadData();
                });
            };

            // Shtrix-kod o'qilganda: FAQAT KASSA oynasida bo'lsagina savatga qo'shish!
            ScannerService.BarcodeScanned += barcode =>
            {
                if (CurrentView == CashierVM)
                {
                    CashierVM.HandleBarcodeScanned(barcode);
                }
            };

            // Telefondan sinxronizatsiya bo'lganda ekranlarni yangilash
            SyncServer.DataSynced += () =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    CashierVM.RefreshCategories();
                    CashierVM.RefreshProducts();
                    InventoryVM.Refresh();
                    ReportsVM.LoadData();
                });
            };
        }

        public void PerformDatabaseBackup()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "SMART Kassa ma'lumotlar bazasini saqlash (Zaxira nusxa)",
                    Filter = "SQLite Baza fayli (*.db)|*.db|Barcha fayllar (*.*)|*.*",
                    FileName = $"SMARTKassa_Baza_{DateTime.Now:yyyy-MM-dd_HHmm}.db"
                };

                if (sfd.ShowDialog() == true)
                {
                    Database.BackupDatabase(sfd.FileName);
                    var fi = new FileInfo(sfd.FileName);
                    var sizeKb = fi.Exists ? (fi.Length / 1024.0) : 0;

                    var res = MessageBox.Show(
                        $"✅ Baza nusxasi muvaffaqiyatli saqlandi!\n\n" +
                        $"Fayl joylashuvi:\n{sfd.FileName}\n\n" +
                        $"Hajmi: {sizeKb:N1} KB\n\n" +
                        $"Fayl saqlangan papkani ochishni xohlaysizmi?",
                        "Zaxira nusxa olindi",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{sfd.FileName}\"",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Zaxira nusxa olishda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsCashierSelected));
                    OnPropertyChanged(nameof(IsInventorySelected));
                    OnPropertyChanged(nameof(IsReportsSelected));
                    OnPropertyChanged(nameof(IsSyncSelected));
                }
            }
        }

        public bool IsCashierSelected => CurrentView == CashierVM;
        public bool IsInventorySelected => CurrentView == InventoryVM;
        public bool IsReportsSelected => CurrentView == ReportsVM;
        public bool IsSyncSelected => CurrentView == SyncVM;

        public bool IsDeviceLocked
        {
            get => _isDeviceLocked;
            set => SetProperty(ref _isDeviceLocked, value);
        }

        public string DeviceId => LicensingService.DeviceId;
        public string DeviceModel => LicensingService.DeviceModel;

        public bool IsPinModalOpen
        {
            get => _isPinModalOpen;
            set => SetProperty(ref _isPinModalOpen, value);
        }

        public string PinInputText
        {
            get => _pinInputText;
            set => SetProperty(ref _pinInputText, value);
        }

        public bool IsPinVisible
        {
            get => _isPinVisible;
            set
            {
                if (SetProperty(ref _isPinVisible, value))
                {
                    OnPropertyChanged(nameof(PasswordBoxVisibility));
                    OnPropertyChanged(nameof(TextBoxVisibility));
                    OnPropertyChanged(nameof(EyeButtonIcon));
                    OnPropertyChanged(nameof(EyeButtonTooltip));
                }
            }
        }

        public Visibility PasswordBoxVisibility => IsPinVisible ? Visibility.Collapsed : Visibility.Visible;
        public Visibility TextBoxVisibility => IsPinVisible ? Visibility.Visible : Visibility.Collapsed;
        public string EyeButtonIcon => IsPinVisible ? "🔒" : "👁️";
        public string EyeButtonTooltip => IsPinVisible ? "Kodni yashirish" : "Kodni ko'rsatish";

        public string PinErrorMessage
        {
            get => _pinErrorMessage;
            set => SetProperty(ref _pinErrorMessage, value);
        }

        public bool IsCopiedNoticeVisible
        {
            get => _isCopiedNoticeVisible;
            set => SetProperty(ref _isCopiedNoticeVisible, value);
        }

        public bool IsContactModalOpen
        {
            get => _isContactModalOpen;
            set => SetProperty(ref _isContactModalOpen, value);
        }

        public bool IsCardTaxModalOpen
        {
            get => _isCardTaxModalOpen;
            set => SetProperty(ref _isCardTaxModalOpen, value);
        }

        public string CardTaxRateInput
        {
            get => _cardTaxRateInput;
            set => SetProperty(ref _cardTaxRateInput, value);
        }

        public string CardTaxErrorMessage
        {
            get => _cardTaxErrorMessage;
            set => SetProperty(ref _cardTaxErrorMessage, value);
        }
    }
}
