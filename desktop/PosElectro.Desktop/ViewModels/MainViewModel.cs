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
        private ViewModelBase _currentView = null!;

        public DatabaseContext Database { get; }
        public ProductService ProductService { get; }
        public CurrencyService CurrencyService { get; }
        public BarcodeScannerService ScannerService { get; }
        public LocalSyncServer SyncServer { get; }
        public DeviceLicensingService LicensingService { get; }

        public CashierViewModel CashierVM { get; }
        public InventoryViewModel InventoryVM { get; }
        public DebtsViewModel DebtsVM { get; }
        public ReportsViewModel ReportsVM { get; }
        public SyncViewModel SyncVM { get; }
        public LabelPrintService PrintService => LabelPrintService.Instance;

        public ICommand NavigateCashierCommand { get; }
        public ICommand NavigateInventoryCommand { get; }
        public ICommand NavigateDebtsCommand { get; }
        public ICommand NavigateReportsCommand { get; }
        public ICommand NavigateSyncCommand { get; }
        public ICommand BackupDatabaseCommand { get; }
        public ICommand RestoreDatabaseCommand { get; }
        public ICommand OpenAdminPinSettingsCommand { get; }
        public ICommand LockAdminCommand { get; }
        public ICommand ExportWarehouseCostReportCommand { get; }
        public ICommand CloseImagePreviewCommand { get; }

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
        public string LiveSyncStatusText => LiveClientsCount > 0 ? $"🟢 {LiveClientsCount} ta mobil" : "Wi-Fi faol";

        // Yuqori panel soati (maket TopBar: "14:32")
        public string ClockText => DateTime.Now.ToString("HH:mm");
        private System.Windows.Threading.DispatcherTimer? _clockTimer;

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
            Instance = this;
            Database = new DatabaseContext();
            Services.AdminGate.Current.Initialize(Database);
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

            _clockTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _clockTimer.Tick += (_, _) => OnPropertyChanged(nameof(ClockText));
            _clockTimer.Start();
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
            DebtsVM = new DebtsViewModel(Database);
            DebtsVM.RequestOpenCashierForCustomer += customer =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    CashierVM.SetDebtCustomer(customer);
                    CurrentView = CashierVM;
                });
            };
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
            CurrentView = CashierVM;

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
            NavigateDebtsCommand = new RelayCommand(() =>
            {
                DebtsVM.RefreshSummary();
                DebtsVM.RefreshCustomers();
                CurrentView = DebtsVM;
            });
            NavigateReportsCommand = new RelayCommand(() =>
            {
                ReportsVM.LoadData();
                CurrentView = ReportsVM;
            });
            NavigateSyncCommand = new RelayCommand(() => CurrentView = SyncVM);
            BackupDatabaseCommand = new RelayCommand(PerformDatabaseBackup);
            RestoreDatabaseCommand = new RelayCommand(() =>
            {
                if (Services.AdminGate.Current.EnsureUnlocked("Bazani tiklash uchun Admin PIN kerak.")) PerformDatabaseRestore();
            });
            ExportWarehouseCostReportCommand = new RelayCommand(() =>
            {
                if (Services.AdminGate.Current.EnsureUnlocked("Tan narx hisobotini ochish uchun Admin PIN kerak."))
                    ExcelExportService.ExportWarehouseCostReport(Database, CurrencyService);
            });
            OpenAdminPinSettingsCommand = new RelayCommand(() =>
                new Views.AdminPinSettingsDialog { Owner = Application.Current?.MainWindow }.ShowDialog());
            LockAdminCommand = new RelayCommand(() => Services.AdminGate.Current.Lock());
            CloseImagePreviewCommand = new RelayCommand(CloseImage);

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
                    PinErrorMessage = "Aktivatsiya parolini kiriting.";
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
                    PinErrorMessage = "Parol noto'g'ri yoki internet yo'q. Qayta urinib ko'ring.";
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
                if (!Services.AdminGate.Current.EnsureUnlocked("Karta foizini o'zgartirish uchun Admin PIN kerak.")) return;
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
                    DebtsVM.RefreshSummary();
                    DebtsVM.RefreshCustomers();
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
                    DebtsVM.RefreshSummary();
                    DebtsVM.RefreshCustomers();
                });
            };
        }

        // ---- Maket 6.6: "Zaxira nusxa saqlandi" bildirishnomasi (faqat ko'rsatish) ----
        private string _backupToastPath = string.Empty;
        private bool _isBackupToastVisible;
        public bool IsBackupToastVisible { get => _isBackupToastVisible; set => SetProperty(ref _isBackupToastVisible, value); }
        private string _backupToastText = string.Empty;
        public string BackupToastText { get => _backupToastText; set => SetProperty(ref _backupToastText, value); }
        private ICommand? _closeBackupToastCommand;
        public ICommand CloseBackupToastCommand => _closeBackupToastCommand ??= new RelayCommand(() => IsBackupToastVisible = false);
        private ICommand? _openBackupFolderCommand;
        public ICommand OpenBackupFolderCommand => _openBackupFolderCommand ??= new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_backupToastPath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
            IsBackupToastVisible = false;
        });

        public void PerformDatabaseBackup()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Line kassa ma'lumotlar bazasini saqlash (Zaxira nusxa)",
                    Filter = "SQLite Baza fayli (*.db)|*.db|Barcha fayllar (*.*)|*.*",
                    FileName = $"LineKassa_Baza_{DateTime.Now:yyyy-MM-dd_HHmm}.db"
                };

                if (sfd.ShowDialog() == true)
                {
                    Database.BackupDatabase(sfd.FileName);
                    var fi = new FileInfo(sfd.FileName);
                    var sizeKb = fi.Exists ? (fi.Length / 1024.0) : 0;

                    // Maket 6.6: natija pastdagi bildirishnomada (avval MessageBox edi). Zaxiralashning o'zi o'zgarmagan.
                    _backupToastPath = sfd.FileName;
                    BackupToastText = $"{sfd.FileName} · {(sizeKb >= 1024 ? $"{sizeKb / 1024:N1} MB" : $"{sizeKb:N1} KB")}";
                    IsBackupToastVisible = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Zaxira nusxa olishda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool _isRestoringDatabase;
        public async void PerformDatabaseRestore()
        {
            if(_isRestoringDatabase)return;
            _isRestoringDatabase=true;
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Line kassa ma'lumotlar bazasini tiklash (Zaxira fayldan)",
                    Filter = "SQLite Baza fayli (*.db)|*.db|Barcha fayllar (*.*)|*.*"
                };

                if (ofd.ShowDialog() == true)
                {
                    var (backupLastSale, lostSales, lostAmount) = Database.PreviewRestoreLoss(ofd.FileName);
                    var dlg = new Views.RestoreConfirmDialog(ofd.FileName, backupLastSale, lostSales, lostAmount)
                    {
                        Owner = Application.Current?.MainWindow
                    };
                    if (dlg.ShowDialog() != true) return;

                    var wasRunning=SyncServer.IsRunning;
                    try {
                        await SyncServer.StopAndWaitAsync();
                        Database.RestoreDatabase(ofd.FileName);
                        CashierVM.ReloadDebtDatabase();
                        DebtsVM.ReloadDebtDatabase();
                        Services.AdminGate.Current.Reload();
                    } finally { if(wasRunning)SyncServer.Start(); }

                    // Re-initialize view models
                    CashierVM.RefreshProducts();
                    CashierVM.RefreshCategories();
                    CashierVM.RefreshActiveCustomers();
                    InventoryVM.Refresh();
                    DebtsVM.RefreshAll();
                    ReportsVM.LoadData();

                    MessageBox.Show(
                        "✅ Ma'lumotlar bazasi zaxiradan muvaffaqiyatli tiklandi!\n\n" +
                        "Barcha tovarlar, qarzlar va hisobotlar yangilandi.",
                        "Muvaffaqiyatli tiklandi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bazani tiklashda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _isRestoringDatabase=false; }
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
                    OnPropertyChanged(nameof(IsDebtsSelected));
                    OnPropertyChanged(nameof(IsReportsSelected));
                    OnPropertyChanged(nameof(IsSyncSelected));
                }
            }
        }

        public bool IsCashierSelected => CurrentView == CashierVM;
        public bool IsInventorySelected => CurrentView == InventoryVM;
        public bool IsDebtsSelected => CurrentView == DebtsVM;
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

        // --- Mahsulot rasmini to'liq ekranda ko'rish (Lightbox) ---
        public static MainViewModel? Instance { get; private set; }

        public static void ShowImage(string? imagePath, string title)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath)) return;
            if (Instance == null) return;
            Instance.PreviewImagePath = imagePath;
            Instance.PreviewImageTitle = string.IsNullOrWhiteSpace(title) ? "Mahsulot rasmi" : title;
            Instance.PreviewImageMeta = string.Empty;
            Instance.PreviewImagePrice = string.Empty;
            Instance._previewAddToCart = null;
            Instance.OnPropertyChanged(nameof(HasPreviewAddToCart));
            Instance.IsImagePreviewOpen = true;
        }

        /// <summary>Kassadan ochilganda (maket 1.12): qoldiq/shtrix-kod, narx va "Savatga +1" (mavjud savatga qo'shish).</summary>
        public static void ShowImage(string? imagePath, string title, string meta, string priceText, Action? addToCart)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath)) return;
            if (Instance == null) return;
            ShowImage(imagePath, title);
            Instance.PreviewImageMeta = meta ?? string.Empty;
            Instance.PreviewImagePrice = priceText ?? string.Empty;
            Instance._previewAddToCart = addToCart;
            Instance.OnPropertyChanged(nameof(HasPreviewAddToCart));
        }

        private Action? _previewAddToCart;
        public bool HasPreviewAddToCart => _previewAddToCart != null;
        public ICommand PreviewAddToCartCommand => _previewAddToCartCommand ??= new RelayCommand(() => _previewAddToCart?.Invoke());
        private ICommand? _previewAddToCartCommand;

        private string _previewImageMeta = string.Empty;
        public string PreviewImageMeta { get => _previewImageMeta; set => SetProperty(ref _previewImageMeta, value); }

        private string _previewImagePrice = string.Empty;
        public string PreviewImagePrice { get => _previewImagePrice; set => SetProperty(ref _previewImagePrice, value); }

        public static void CloseImage()
        {
            if (Instance == null) return;
            Instance.IsImagePreviewOpen = false;
            Instance.PreviewImagePath = null;
        }

        private bool _isImagePreviewOpen;
        public bool IsImagePreviewOpen
        {
            get => _isImagePreviewOpen;
            set => SetProperty(ref _isImagePreviewOpen, value);
        }

        private string? _previewImagePath;
        public string? PreviewImagePath
        {
            get => _previewImagePath;
            set => SetProperty(ref _previewImagePath, value);
        }

        private string _previewImageTitle = string.Empty;
        public string PreviewImageTitle
        {
            get => _previewImageTitle;
            set => SetProperty(ref _previewImageTitle, value);
        }
    }
}
