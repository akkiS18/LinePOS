using System.Windows;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public partial class SaleDetailDialog : Window
    {
        private readonly Sale _sale;
        private readonly PosElectro.Desktop.Data.DatabaseContext? _database;
        private readonly PrinterService _printerService;

        public SaleDetailDialog(Sale sale, PrinterService? printerService = null, PosElectro.Desktop.Data.DatabaseContext? database = null)
        {
            InitializeComponent();
            _sale = sale;
            _database = database;
            BtnReturn.Visibility = database != null && (int)sale.PaymentType < 6 ? Visibility.Visible : Visibility.Collapsed;
            _printerService = printerService ?? new PrinterService();

            TxtTitle.Text = $"Chek #{sale.ReceiptNumber} Tafsilotlari";
            TxtDate.Text = sale.CreatedDateTime.ToString("dd.MM.yyyy HH:mm");
            TxtTotalAmount.Text = $"{sale.TotalAmount:N0} SO'M";
            TxtTotalProfit.Text = $"+{sale.Profit:N0} SO'M";

            GridItems.ItemsSource = sale.Items;
            if (database != null) {
                var returns = new PosElectro.Desktop.Returns.ReturnStore(database.DatabaseFilePath); returns.Install();
                var history = returns.History(sale.Guid);
                if (!string.IsNullOrEmpty(history)) { TxtStatus.Text = history; TxtStatus.Visibility = Visibility.Visible; }
            }
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

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try { new ReturnDialog(_database, _sale) { Owner = this }.ShowDialog(); }
            catch (System.Exception ex) { MessageBox.Show(this, ex.Message, "Qaytarish"); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
