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
            DataContext = sale; // faqat sarlavhadagi to'lov turi belgisi uchun
            if (sale.PaymentType == PaymentType.RETURN) TxtReturn.Text = "Bekor qilish";
            BtnReturn.Visibility = database != null && ((int)sale.PaymentType < 6 || sale.PaymentType == PaymentType.RETURN) ? Visibility.Visible : Visibility.Collapsed;
            _printerService = printerService ?? new PrinterService();

            TxtTitle.Text = $"Chek {sale.ReceiptNumber}";
            TxtDate.Text = sale.CreatedDateTime.ToString("dd.MM.yyyy HH:mm");
            TxtTotalAmount.Text = $"{sale.TotalAmount:N0} so'm";
            if (sale.PaymentType == PaymentType.SPLIT)
            {
                TxtSplit.Text = $"{sale.CashAmount:N0} / {sale.CardAmount:N0}";
                RowSplit.Visibility = Visibility.Visible;
            }
            TxtProfitLabel.Text = sale.PaymentType is PaymentType.RETURN or PaymentType.RETURN_REVERSAL ? "Foydaga ta’siri" : "Sof foyda";
            TxtTotalProfit.Text = $"{(sale.Profit > 0 ? "+" : "")}{sale.Profit:N0} so'm";
            TxtTotalProfit.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, sale.Profit < 0 ? "Bad" : "Ok");
            if (PosElectro.Desktop.Services.AdminGate.Current.IsLocked)
            {
                // Admin PIN qulfi yopiq: foyda va tan narx kassirga ko'rinmaydi
                TxtTotalProfit.Text = "••••••";
                TxtTotalProfit.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Ink3");
            }

            GridItems.ItemsSource = sale.Items;
            if (database != null) {
                var returns = new PosElectro.Desktop.Returns.ReturnStore(database.DatabaseFilePath); returns.Install();
                sale.OriginalReceiptNumber = returns.OriginalReceipt(sale.Guid);
                var history = returns.History(sale.Guid);
                if (!string.IsNullOrEmpty(history)) ShowStatus(history, "Ink2");
            }
        }

        private void ShowStatus(string text, string brushKey)
        {
            TxtStatus.Text = text;
            TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, brushKey);
            TxtStatus.Visibility = Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Oyna egasini to'liq qoplaydi (orqa fon xiralashadi, karta markazda)
            if (Owner != null)
            {
                SizeToContent = SizeToContent.Manual;
                if (Owner.WindowState == WindowState.Maximized)
                {
                    Left = Owner.Left; Top = Owner.Top;
                    WindowState = WindowState.Maximized;
                }
                else if (Owner.ActualWidth > ActualWidth && Owner.ActualHeight > ActualHeight)
                {
                    Left = Owner.Left; Top = Owner.Top;
                    Width = Owner.ActualWidth; Height = Owner.ActualHeight;
                }
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ReceiptPreviewDialog(_sale, startWithA4: false, _printerService)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void BtnPrintA4_Click(object sender, RoutedEventArgs e)
        {
            bool printed = _printerService.PrintInvoiceA4(_sale, this);
            if (printed) ShowStatus("A4 hujjat printerga yuborildi", "Ok");
            else ShowStatus("Chop etish bekor qilindi yoki printer tanlanmadi", "Ink2");
        }

        private void BtnPrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            // Xprinter 58mm
            bool printed = _printerService.PrintReceipt(_sale);
            if (printed) ShowStatus("Chek printerga yuborildi", "Ok");
            else ShowStatus("Chek printeri topilmadi", "Bad");
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_database == null) return;
            try {
                bool? completed = _sale.PaymentType == PaymentType.RETURN
                    ? new ReturnReversalDialog(_database, _sale) { Owner = this }.ShowDialog()
                    : new ReturnDialog(_database, _sale) { Owner = this }.ShowDialog();
                if (completed == true) Close();
            }
            catch (System.Exception ex) { MessageBox.Show(this, ex.Message, "Qaytarish"); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnPrintReceipt_Click(this, null!);
                e.Handled = true;
            }
        }
    }
}
