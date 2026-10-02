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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
        }
    }
}
