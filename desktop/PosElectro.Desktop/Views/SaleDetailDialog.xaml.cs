using System.Windows;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public partial class SaleDetailDialog : Window
    {
        private readonly Sale _sale;
        private readonly PrinterService _printerService;

        public SaleDetailDialog(Sale sale, PrinterService? printerService = null)
        {
            InitializeComponent();
            _sale = sale;
            _printerService = printerService ?? new PrinterService();

            TxtTitle.Text = $"Chek #{sale.ReceiptNumber} Tafsilotlari";
            TxtDate.Text = sale.CreatedDateTime.ToString("dd.MM.yyyy HH:mm");
            TxtTotalAmount.Text = $"{sale.TotalAmount:N0} SO'M";
            TxtTotalProfit.Text = $"+{sale.Profit:N0} SO'M";

            GridItems.ItemsSource = sale.Items;
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            bool isA4 = CmbPrinterFormat.SelectedIndex == 1;
            var dlg = new ReceiptPreviewDialog(_sale, startWithA4: isA4, _printerService)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void BtnPrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPrinterFormat.SelectedIndex == 1)
            {
                // A4 chop etish
                bool printed = _printerService.PrintInvoiceA4(_sale, this);
                if (printed)
                {
                    TxtStatus.Text = "✅ A4 hujjat printerga yuborildi!";
                    TxtStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
                    TxtStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtStatus.Text = "ℹ️ Chop etish bekor qilindi yoki printer tanlanmadi.";
                    TxtStatus.Foreground = System.Windows.Media.Brushes.SkyBlue;
                    TxtStatus.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Xprinter 58mm
                bool printed = _printerService.PrintReceipt(_sale);
                if (printed)
                {
                    TxtStatus.Text = "✅ Chek printerga yuborildi!";
                    TxtStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
                    TxtStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtStatus.Text = "❌ Chek printeri topilmadi!";
                    TxtStatus.Foreground = System.Windows.Media.Brushes.Tomato;
                    TxtStatus.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
