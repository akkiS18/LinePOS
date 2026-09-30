using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public partial class BarcodePrintDialog : Window
    {
        private readonly string _productName;
        private readonly string _barcode;
        private readonly double _sellingPrice;
        private readonly double _stockQuantity;
        private readonly PrinterService _printerService;
        private string? _detectedPrinter;
        private double _currentOffsetMm = 0;

        public BarcodePrintDialog(
            string productName, 
            string barcode, 
            double sellingPrice, 
            double stockQuantity = 0, 
            PrinterService? printerService = null)
        {
            InitializeComponent();

            _productName = productName;
            _barcode = barcode;
            _sellingPrice = sellingPrice;
            _stockQuantity = stockQuantity;
            _printerService = printerService ?? new PrinterService();

            InitializeData();
        }

        private void InitializeData()
        {
            // Qoldiq miqdori tugmasi
            int stockInt = (int)Math.Max(1, Math.Floor(_stockQuantity));
            BtnStockQty.Content = $"📦 Qoldiqcha ({stockInt})";
            BtnStockQty.Tag = stockInt;

            // Preview ma'lumotlarini yuklash
            PreviewName.Text = string.IsNullOrWhiteSpace(_productName) ? "Mahsulot nomi" : _productName;
            PreviewBarcodeText.Text = _barcode;
            PreviewPrice.Text = $"{_sellingPrice:N0} SO'M";

            try
            {
                var bmp = BarcodeGeneratorHelper.GenerateBarcodeImage(_barcode, 260, 60);
                ImgBarcode.Source = bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Barcode generate error: {ex.Message}");
            }

            // Printerlar ro'yxatini to'ldirish
            var printers = PrinterService.GetInstalledPrinters();
            CmbPrinters.ItemsSource = printers;

            // Printerni aniqlash
            _detectedPrinter = _printerService.FindLabelPrinter();
            if (!string.IsNullOrWhiteSpace(_detectedPrinter) && printers.Contains(_detectedPrinter))
            {
                CmbPrinters.SelectedItem = _detectedPrinter;
                TxtPrinterStatus.Text = $"🟢 Ulangan: {_detectedPrinter}";
                TxtPrinterStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
            }
            else if (printers.Count > 0)
            {
                CmbPrinters.SelectedIndex = 0;
                _detectedPrinter = printers[0];
                TxtPrinterStatus.Text = $"ℹ️ Tanlangan: {_detectedPrinter}";
                TxtPrinterStatus.Foreground = System.Windows.Media.Brushes.SkyBlue;
            }
            else
            {
                TxtPrinterStatus.Text = "⚠️ Kompyuterda hech qanday printer o'rnatilmagan";
                TxtPrinterStatus.Foreground = System.Windows.Media.Brushes.Gold;
            }

            // Stiker o'lchamlari ro'yxati
            var sizePresets = System.Linq.Enumerable.ToList(LabelSizeConfig.Presets.Values);
            CmbLabelSize.ItemsSource = sizePresets;
            CmbLabelSize.DisplayMemberPath = "DisplayName";
            CmbLabelSize.SelectedValuePath = "Id";

            string savedSize = _printerService.GetLabelSizeSetting();
            var matchedConfig = System.Linq.Enumerable.FirstOrDefault(sizePresets, s => s.Id == savedSize) ?? sizePresets[0];
            CmbLabelSize.SelectedItem = matchedConfig;
            ApplyLabelSize(matchedConfig);

            _currentOffsetMm = _printerService.GetLabelOffsetSetting(matchedConfig.Id);
            UpdateOffsetDisplay();

            UpdateCopiesDisplay(1);
        }

        private void BtnOffsetLeft_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm -= 0.5;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
        }

        private void BtnOffsetRight_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm += 0.5;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
        }

        private void BtnOffsetReset_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm = 0.0;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
        }

        private void SaveCurrentOffset()
        {
            if (CmbLabelSize.SelectedItem is LabelSizeConfig config)
            {
                _printerService.SaveLabelOffsetSetting(config.Id, _currentOffsetMm);
            }
        }

        private void UpdateOffsetDisplay()
        {
            TxtOffset.Text = Math.Abs(_currentOffsetMm) < 0.01 ? "0.0 mm" : $"{_currentOffsetMm:+#0.0;-#0.0;0.0} mm";
            double dip = _currentOffsetMm * (96.0 / 25.4);
            PreviewContentPanel.Margin = new Thickness(dip, 0, -dip, 0);
        }

        private void ApplyLabelSize(LabelSizeConfig config)
        {
            if (config == null) return;

            StickerCard.Width = config.VisualWidth;
            StickerCard.MinHeight = config.VisualHeight;
            ImgBarcode.Width = config.BarcodeWidth;
            ImgBarcode.Height = config.BarcodeHeight;

            PreviewName.FontSize = config.NameFontSize;
            PreviewBarcodeText.FontSize = config.BarcodeFontSize;
            PreviewPrice.FontSize = config.PriceFontSize;

            TxtLabelSizeCaption.Text = config.DisplayName;
        }

        private void CmbLabelSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (CmbLabelSize.SelectedItem is LabelSizeConfig config)
            {
                ApplyLabelSize(config);
                _printerService.SaveLabelSizeSetting(config.Id);
                _currentOffsetMm = _printerService.GetLabelOffsetSetting(config.Id);
                UpdateOffsetDisplay();
            }
        }

        private void CmbPrinters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (CmbPrinters.SelectedItem is string selectedPrinter)
            {
                _detectedPrinter = selectedPrinter;
                TxtPrinterStatus.Text = $"🟢 Tanlangan: {selectedPrinter}";
                TxtPrinterStatus.Foreground = System.Windows.Media.Brushes.MediumSpringGreen;
                _printerService.SaveLabelPrinterSetting(selectedPrinter);
            }
        }

        private void OnElementVisibilityChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            PreviewName.Visibility = ChkShowName.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PreviewBarcodePanel.Visibility = ChkShowBarcode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PreviewPrice.Visibility = ChkShowPrice.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnMinus_Click(object sender, RoutedEventArgs e)
        {
            int current = GetCurrentCopies();
            if (current > 1)
            {
                UpdateCopiesDisplay(current - 1);
            }
        }

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
        {
            int current = GetCurrentCopies();
            UpdateCopiesDisplay(current + 1);
        }

        private void BtnQuickQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int qty))
            {
                UpdateCopiesDisplay(qty);
            }
        }

        private void BtnStockQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int qty))
            {
                UpdateCopiesDisplay(qty);
            }
        }

        private void TxtCopies_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            int copies = GetCurrentCopies();
            BtnPrint.Content = $"🖨️ Chop etish ({copies} ta)";
        }

        private int GetCurrentCopies()
        {
            if (int.TryParse(TxtCopies.Text, out int c) && c > 0)
            {
                return c;
            }
            return 1;
        }

        private void UpdateCopiesDisplay(int copies)
        {
            if (copies < 1) copies = 1;
            TxtCopies.Text = copies.ToString();
            BtnPrint.Content = $"🖨️ Chop etish ({copies} ta)";
        }

        private void BtnTestPrint_Click(object sender, RoutedEventArgs e)
        {
            ExecutePrint(1, isTest: true);
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            int copies = GetCurrentCopies();
            ExecutePrint(copies, isTest: false);
        }

        private void ExecutePrint(int copies, bool isTest)
        {
            bool showName = ChkShowName.IsChecked == true;
            bool showBarcode = ChkShowBarcode.IsChecked == true;
            bool showPrice = ChkShowPrice.IsChecked == true;

            string labelSizeId = (CmbLabelSize.SelectedItem as LabelSizeConfig)?.Id ?? "40x30";

            bool ok = _printerService.PrintBarcodeLabel(
                _barcode,
                _productName,
                _sellingPrice,
                copies,
                showName,
                showPrice,
                showBarcode,
                _detectedPrinter,
                labelSizeId,
                _currentOffsetMm);

            if (ok)
            {
                if (isTest)
                {
                    ShowStatus("✅ 1 dona sinov stikeri printerga yuborildi!", isError: false);
                }
                else
                {
                    DialogResult = true;
                    Close();
                }
            }
            else
            {
                ShowStatus("❌ Printerga yuborishda xatolik! Printer ulanganligini tekshiring.", isError: true);
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = isError 
                ? System.Windows.Media.Brushes.Tomato 
                : System.Windows.Media.Brushes.MediumSpringGreen;
            TxtStatus.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
