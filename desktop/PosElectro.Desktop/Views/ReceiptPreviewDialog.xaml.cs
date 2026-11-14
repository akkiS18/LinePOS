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
            TxtTitle.Text = $"Chek {_sale.ReceiptNumber} · oldindan ko'rish";

            // 1. 58mm Matnli ko'rinish
            Txt58mmContent.Text = PrinterService.BuildReceiptText(_sale);

            // 2. A4 Ko'rinishi
            TxtA4ChekNum.Text = $"Hujjat: Chek #{_sale.ReceiptNumber}" + (string.IsNullOrEmpty(_sale.OriginalReceiptNumber) ? "" : "\nAsl chek: " + _sale.OriginalReceiptNumber);
            TxtA4Date.Text = $"Sana: {_sale.CreatedDateTime:dd.MM.yyyy HH:mm}";
            TxtA4Payment.Text = $"To'lov: {_sale.PaymentTypeDisplay}";
            TxtA4Total.Text = $"{_sale.TotalAmount:N0} so'm";

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
            // Maket 4.6: ikkala qog'oz yonma-yon; tanlangani hoshiya bilan, ikkinchisi xiraroq
            bool is58 = Rb58mm.IsChecked == true;
            Mark(Border58mm, Panel58mm, is58);
            Mark(BorderA4, PanelA4, !is58);
            Cap58mm.Text = is58 ? "58 mm (tanlangan)" : "58 mm";
            CapA4.Text = is58 ? "A4" : "A4 (tanlangan)";
            PrinterPanel.Visibility = is58 ? Visibility.Visible : Visibility.Hidden;
            TxtSubtitle.Text = is58 ? "Lenta cheki ko'rinishi (termo printer)" : "A4 hisob-faktura qog'ozi ko'rinishi";
            BtnPrint.Content = is58 ? "Chek chiqarish" : "A4 chop etish";
        }

        private static void Mark(Border paper, FrameworkElement panel, bool selected)
        {
            if (selected) paper.SetResourceReference(Border.BorderBrushProperty, "Accent");
            else paper.BorderBrush = System.Windows.Media.Brushes.Transparent;
            panel.Opacity = selected ? 1 : 0.55;
        }

        private void Paper58_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => Rb58mm.IsChecked = true;
        private void PaperA4_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => RbA4.IsChecked = true;

        private void ShowStatus(string text, string brushKey)
        {
            TxtPreviewStatus.Text = text;
            TxtPreviewStatus.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Oyna egasini to'liq qoplaydi; egasi o'zi ham qoplama bo'lsa fon ikki marta xiralashmaydi
            if (Owner != null)
            {
                if (Owner.AllowsTransparency)
                    RootLayer.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
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

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (Rb58mm.IsChecked == true)
            {
                string? targetPrinter = CmbReceiptPrinters.SelectedItem as string ?? _printerService.FindReceiptPrinter();
                bool printed = _printerService.PrintReceipt(_sale, targetPrinter);
                if (printed) ShowStatus("Chek printerga chiqarildi", "Ok");
                else ShowStatus("Chek printeri topilmadi", "Bad");
            }
            else
            {
                bool printed = _printerService.PrintInvoiceA4(_sale, this);
                if (printed) ShowStatus("A4 hujjat printerga yuborildi", "Ok");
                else ShowStatus("Chop etish bekor qilindi yoki printer tanlanmadi", "Ink2");
            }
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
                BtnPrint_Click(this, null!);
                e.Handled = true;
            }
        }
    }
}
