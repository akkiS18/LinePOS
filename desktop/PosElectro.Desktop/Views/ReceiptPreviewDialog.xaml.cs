using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public class A4PreviewItemRow
    {
        public int IndexDisplay { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string QuantityDisplay { get; set; } = string.Empty;
        public string PriceDisplay { get; set; } = string.Empty;
        public string TotalDisplay { get; set; } = string.Empty;
    }

    public partial class ReceiptPreviewDialog : Window
    {
        private readonly Sale _sale;
        private readonly PrinterService _printerService;

        public ReceiptPreviewDialog(Sale sale, bool startWithA4 = false, PrinterService? printerService = null)
        {
            InitializeComponent();
            _sale = sale;
            _printerService = printerService ?? new PrinterService();

            LoadSaleData();

            if (startWithA4)
            {
                RbA4.IsChecked = true;
            }
            else
            {
                Rb58mm.IsChecked = true;
            }
            UpdateViewFormat();
        }

        private void LoadSaleData()
        {
            // 1. 58mm Matnli ko'rinish
            Txt58mmContent.Text = PrinterService.BuildReceiptText(_sale);

            // 2. A4 Ko'rinishi
            TxtA4ChekNum.Text = $"Hujjat: Chek #{_sale.ReceiptNumber}" + (string.IsNullOrEmpty(_sale.OriginalReceiptNumber) ? "" : "\nAsl chek: " + _sale.OriginalReceiptNumber);
            TxtA4Date.Text = $"Sana: {_sale.CreatedDateTime:dd.MM.yyyy HH:mm}";
            TxtA4Payment.Text = $"To'lov: {_sale.PaymentTypeDisplay}";
            TxtA4Total.Text = $"{_sale.TotalAmount:N0} SO'M";

            int idx = 1;
            var rows = new List<A4PreviewItemRow>();
            foreach (var item in _sale.Items)
            {
                rows.Add(new A4PreviewItemRow
                {
                    IndexDisplay = idx++,
                    ProductName = item.ProductName,
                    QuantityDisplay = $"{item.Quantity:0.##}",
                    PriceDisplay = $"{item.PriceAtSale:N0}",
                    TotalDisplay = $"{item.TotalPrice:N0}"
                });
            }
            GridA4Items.ItemsSource = rows;

            // 3. Printerlar ro'yxatini to'ldirish
            var printers = PrinterService.GetInstalledPrinters();
            CmbReceiptPrinters.ItemsSource = printers;
            var detected = _printerService.FindReceiptPrinter();
            if (!string.IsNullOrWhiteSpace(detected) && printers.Contains(detected))
            {
                CmbReceiptPrinters.SelectedItem = detected;
            }
            else if (printers.Count > 0)
            {
                CmbReceiptPrinters.SelectedIndex = 0;
            }
        }

        private void CmbReceiptPrinters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbReceiptPrinters.SelectedItem is string p && !string.IsNullOrWhiteSpace(p))
            {
                _printerService.SaveReceiptPrinterSetting(p);
            }
        }

        private void FormatRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateViewFormat();
            }
        }

        private void UpdateViewFormat()
        {
            if (Rb58mm.IsChecked == true)
            {
                Border58mm.Visibility = Visibility.Visible;
                BorderA4.Visibility = Visibility.Collapsed;
                CmbReceiptPrinters.Visibility = Visibility.Visible;
                TxtSubtitle.Text = "Lenta cheki ko'rinishi (Termo printer)";
                BtnPrint.Content = "🖨️ Chek chiqarish";
            }
            else
            {
                Border58mm.Visibility = Visibility.Collapsed;
                BorderA4.Visibility = Visibility.Visible;
                CmbReceiptPrinters.Visibility = Visibility.Collapsed;
                TxtSubtitle.Text = "A4 standart hisob-faktura qog'ozi ko'rinishi";
                BtnPrint.Content = "📄 A4 Printerga chiqarish";
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (Rb58mm.IsChecked == true)
            {
                string? targetPrinter = CmbReceiptPrinters.SelectedItem as string ?? _printerService.FindReceiptPrinter();
                bool printed = _printerService.PrintReceipt(_sale, targetPrinter);
                if (printed)
                {
                    TxtPreviewStatus.Text = "✅ Chek printerga chiqarildi!";
                    TxtPreviewStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
                }
                else
                {
                    TxtPreviewStatus.Text = "❌ Chek printeri topilmadi!";
                    TxtPreviewStatus.Foreground = System.Windows.Media.Brushes.Tomato;
                }
            }
            else
            {
                bool printed = _printerService.PrintInvoiceA4(_sale, this);
                if (printed)
                {
                    TxtPreviewStatus.Text = "✅ A4 hujjat printerga yuborildi!";
                    TxtPreviewStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
                }
                else
                {
                    TxtPreviewStatus.Text = "ℹ️ Chop etish bekor qilindi yoki printer tanlanmadi.";
                    TxtPreviewStatus.Foreground = System.Windows.Media.Brushes.SkyBlue;
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
