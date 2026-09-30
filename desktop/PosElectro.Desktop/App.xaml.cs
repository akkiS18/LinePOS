using System;
using System.Windows;

namespace PosElectro.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            PosElectro.Desktop.Services.KeyboardLayoutHelper.ForceEnglishLayout();

            bool isShowingError = false;
            DispatcherUnhandledException += (s, args) =>
            {
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
