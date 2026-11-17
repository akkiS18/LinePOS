using System;
using System.IO;
using System.Windows;

namespace PosElectro.Desktop
{
    public partial class App : Application
    {
        private static void LogUnhandledException(Exception exception)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PosElectro", "Logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, $"errors-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"{DateTimeOffset.Now:O}\n{exception}\n\n");
            }
            catch { /* Logging must not replace the original error. */ }
        }

        private const string InstanceMutexName = @"Local\SMARTkassa_PosElectro_SingleInstance";
        private const string ShowEventName = @"Local\SMARTkassa_PosElectro_ShowWindow";
        private static System.Threading.Mutex? _instanceMutex;
        private static System.Threading.EventWaitHandle? _showEvent;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Faqat bitta nusxa: dastur trayda turganda ikonkasi qayta bosilsa, yangisi ochilmaydi —
            // mavjud oyna oldinga chiqadi (ikki nusxa bitta bazaga yozmasligi uchun).
            _instanceMutex = new System.Threading.Mutex(true, InstanceMutexName, out bool isFirst);
            if (!isFirst)
            {
                try { System.Threading.EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
                Shutdown();
                return;
            }
            _showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, ShowEventName);
            var listener = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    try { _showEvent.WaitOne(); } catch { return; }
                    Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.ShowFromTray()));
                }
            }) { IsBackground = true, Name = "SingleInstanceListener" };
            listener.Start();

            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            }
            catch { }

            // Barcha ochiladigan oynalarga avtomatik to'q sarlavha paneli (#0F172A) berish
            try
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
                {
                    if (s is Window w)
                    {
                        Services.WindowStyleHelper.ApplyDarkTheme(w);
                    }
                }));
            }
            catch { }

            PosElectro.Desktop.Services.KeyboardLayoutHelper.ForceEnglishLayout();

            bool isShowingError = false;
            DispatcherUnhandledException += (s, args) =>
            {
                LogUnhandledException(args.Exception);
                Console.WriteLine($"DISPATCHER EXCEPTION: {args.Exception}");
                if (isShowingError)
                {
                    args.Handled = true;
                    return;
                }
                isShowingError = true;
                try
                {
                    MessageBox.Show(
                        $"Kutilmagan xatolik yuz berdi:\n{args.Exception.Message}",
                        "Xatolik",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }
                finally
                {
                    isShowingError = false;
                }
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogUnhandledException(ex);
                    MessageBox.Show(
                        $"Kritik xatolik:\n{ex.Message}",
                        "Kritik Xatolik",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
    }
}
