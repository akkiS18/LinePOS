using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.ViewModels
{
    public class SyncActivityItem
    {
        public string TimeText { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // "success", "info", "warning", "error"

        public string Icon => Type switch
        {
            "success" => "✔",
            "warning" => "⚠",
            "error" => "✖",
            _ => "ℹ"
        };

        public string ColorHex => Type switch
        {
            "success" => "#34D399",
            "warning" => "#FBBF24",
            "error" => "#F87171",
            _ => "#38BDF8"
        };

        public string BadgeBgHex => Type switch
        {
            "success" => "#064E3B",
            "warning" => "#451A03",
            "error" => "#450A0A",
            _ => "#0C4A6E"
        };

        public string BorderColorHex => Type switch
        {
            "success" => "#059669",
            "warning" => "#D97706",
            "error" => "#DC2626",
            _ => "#0284C7"
        };
    }

    public class SyncViewModel : ViewModelBase
    {
        private readonly LocalSyncServer _server;
        private readonly DatabaseContext _db;
        private string _serverIp = "127.0.0.1";
        private int _serverPort = 8080;
        private bool _isRunning;
        private BitmapImage? _qrCodeImage;

        private string _connectedDeviceName = "Kutilmoqda...";
        private string _connectedDeviceStatus = "Mobil ilovadan QR kodni skaner qiling";
        private bool _isDeviceConnected;

        private string _desktopProductsCount = "0 ta tovar";
        private string _desktopSalesCount = "0 ta savdo";
        private string _phoneProductsCount = "--";
        private string _phoneSalesCount = "--";
        private string _lastTransferTime = "Hali o'tkazilmagan";

        public ObservableCollection<SyncActivityItem> Activities { get; } = new();
        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<ConnectedClientInfo> ConnectedClients { get; } = new();

        public int ConnectedClientsCount => ConnectedClients.Count;
        public bool HasConnectedClients => ConnectedClients.Count > 0;
        public bool HasNoConnectedClients => ConnectedClients.Count == 0;
        public string ConnectedClientsCountText => $"Ulangan: {ConnectedClientsCount} ta qurilma";

        private bool _isHelpModalOpen;
        public bool IsHelpModalOpen
        {
            get => _isHelpModalOpen;
            set => SetProperty(ref _isHelpModalOpen, value);
        }

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand GenerateQrCommand { get; }
        public ICommand OpenFirewallCommand { get; }
        public ICommand RefreshStatsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand BackupDatabaseCommand { get; }
        public ICommand OpenHelpModalCommand { get; }
        public ICommand CloseHelpModalCommand { get; }

        public SyncViewModel(LocalSyncServer server, DatabaseContext db)
        {
            _server = server;
            _db = db;
            _serverIp = _server.GetLocalIpAddress();
            _serverPort = _server.Port;

            StartServerCommand = new RelayCommand(StartServer);
            StopServerCommand = new RelayCommand(StopServer);
            GenerateQrCommand = new RelayCommand(GenerateQrCode);
            OpenFirewallCommand = new RelayCommand(OpenFirewallRule);
            RefreshStatsCommand = new RelayCommand(RefreshStats);
            ClearLogsCommand = new RelayCommand(() =>
            {
                Activities.Clear();
                Logs.Clear();
            });
            BackupDatabaseCommand = new RelayCommand(PerformBackup);
            OpenHelpModalCommand = new RelayCommand(() => IsHelpModalOpen = true);
            CloseHelpModalCommand = new RelayCommand(() => IsHelpModalOpen = false);

            _server.ConnectedClientsListChanged += clients =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Clear();
                    foreach (var c in clients)
                    {
                        ConnectedClients.Add(c);
                    }
                    OnPropertyChanged(nameof(ConnectedClientsCount));
                    OnPropertyChanged(nameof(HasConnectedClients));
                    OnPropertyChanged(nameof(HasNoConnectedClients));
                    OnPropertyChanged(nameof(ConnectedClientsCountText));
                });
            };

            _server.ActivityLogged += (type, msg) =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    Activities.Insert(0, new SyncActivityItem
                    {
                        TimeText = DateTime.Now.ToString("HH:mm:ss"),
                        Message = msg,
                        Type = type
                    });
                    if (Activities.Count > 50) Activities.RemoveAt(Activities.Count - 1);
                });
            };

            _server.ClientStatusUpdated += client =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    ConnectedDeviceName = string.IsNullOrWhiteSpace(client.DeviceName) ? "Android Telefon" : client.DeviceName;
                    ConnectedDeviceStatus = $"🟢 Bog'langan ({client.IpAddress}) — Faol";
                    IsDeviceConnected = true;
                    if (client.PhoneProductsCount > 0)
                    {
                        PhoneProductsCount = $"{client.PhoneProductsCount} ta tovar";
                    }
                    if (client.PhoneSalesCount > 0)
                    {
                        PhoneSalesCount = $"{client.PhoneSalesCount} ta savdo";
                    }
                });
            };

            _server.DataSynced += () =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    LastTransferTime = DateTime.Now.ToString("HH:mm (Bugun)");
                    RefreshStats();
                });
            };

            _server.LogMessageReceived += msg =>
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                    if (Logs.Count > 50) Logs.RemoveAt(Logs.Count - 1);
                });
            };

            RefreshStats();
            StartServer();
        }

        public string ServerIp
        {
            get => _serverIp;
            set => SetProperty(ref _serverIp, value);
        }

        public int ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public BitmapImage? QrCodeImage
        {
            get => _qrCodeImage;
            set => SetProperty(ref _qrCodeImage, value);
        }

        public string ConnectedDeviceName
        {
            get => _connectedDeviceName;
            set => SetProperty(ref _connectedDeviceName, value);
        }

        public string ConnectedDeviceStatus
        {
            get => _connectedDeviceStatus;
            set => SetProperty(ref _connectedDeviceStatus, value);
        }

        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set => SetProperty(ref _isDeviceConnected, value);
        }

        public string DesktopProductsCount
        {
            get => _desktopProductsCount;
            set => SetProperty(ref _desktopProductsCount, value);
        }

        public string DesktopSalesCount
        {
            get => _desktopSalesCount;
            set => SetProperty(ref _desktopSalesCount, value);
        }

        public string PhoneProductsCount
        {
            get => _phoneProductsCount;
            set => SetProperty(ref _phoneProductsCount, value);
        }

        public string PhoneSalesCount
        {
            get => _phoneSalesCount;
            set => SetProperty(ref _phoneSalesCount, value);
        }

        public string LastTransferTime
        {
            get => _lastTransferTime;
            set => SetProperty(ref _lastTransferTime, value);
        }

        public void RefreshStats()
        {
            try
            {
                int pCount = _db.GetActiveProductsCount();
                int sCount = _db.GetTotalSalesCount();
                DesktopProductsCount = $"{pCount:N0} ta tovar";
                DesktopSalesCount = $"{sCount:N0} ta savdo";
            }
            catch { }
        }

        public void StartServer()
        {
            _server.Start();
            IsRunning = true;
            ServerIp = _server.GetLocalIpAddress();
            GenerateQrCode();
        }

        public void StopServer()
        {
            _server.Stop();
            IsRunning = false;
        }

        public void GenerateQrCode()
        {
            try
            {
                var payload = new
                {
                    serverUrl = $"http://{ServerIp}:{ServerPort}",
                    type = "POS_ELECTRO_LOCAL_SYNC",
                    name = "SMART Kassa"
                };
                var json = JsonConvert.SerializeObject(payload);
                QrCodeImage = QrCodeService.GenerateQrBitmap(json);
            }
            catch (Exception ex)
            {
                Activities.Insert(0, new SyncActivityItem
                {
                    TimeText = DateTime.Now.ToString("HH:mm:ss"),
                    Message = $"QR kod yaratishda xatolik: {ex.Message}",
                    Type = "warning"
                });
            }
        }

        public void OpenFirewallRule()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall add rule name=\"SMART Kassa Desktop Sync\" dir=in action=allow protocol=TCP localport=8080",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                Activities.Insert(0, new SyncActivityItem
                {
                    TimeText = DateTime.Now.ToString("HH:mm:ss"),
                    Message = "Windows Firewall 8080 portiga ruxsat berildi!",
                    Type = "success"
                });
            }
            catch (Exception ex)
            {
                Activities.Insert(0, new SyncActivityItem
                {
                    TimeText = DateTime.Now.ToString("HH:mm:ss"),
                    Message = $"Firewall ruxsati berilmadi: {ex.Message}",
                    Type = "warning"
                });
            }
        }

        public void PerformBackup()
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
                    _db.BackupDatabase(sfd.FileName);
                    var fi = new FileInfo(sfd.FileName);
                    var sizeKb = fi.Exists ? (fi.Length / 1024.0) : 0;

                    Activities.Insert(0, new SyncActivityItem
                    {
                        TimeText = DateTime.Now.ToString("HH:mm:ss"),
                        Message = $"💾 To'liq baza zaxirasi saqlandi: {Path.GetFileName(sfd.FileName)} ({sizeKb:N1} KB)",
                        Type = "success"
                    });

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
    }
}

