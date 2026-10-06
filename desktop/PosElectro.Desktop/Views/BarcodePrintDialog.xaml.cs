using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        private double _currentOffsetMm = 0.0;
        private double _currentNameOffsetMm = 0.0;
        private string _currentNameAlignment = "left";
        private int _currentNameLines = 1;

        private static readonly Brush ActiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B6477"));
        private static readonly Brush ActiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DD4BF"));
        private static readonly Brush ActiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DD4BF"));

        private static readonly Brush InactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
        private static readonly Brush InactiveBorder = Brushes.Transparent;
        private static readonly Brush InactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

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

            _currentOffsetMm = _printerService.GetLabelOffsetSetting(matchedConfig.Id);
            _currentNameOffsetMm = _printerService.GetLabelNameOffsetSetting(matchedConfig.Id);
            _currentNameAlignment = _printerService.GetLabelNameAlignSetting(matchedConfig.Id);
            _currentNameLines = _printerService.GetLabelNameLinesSetting(matchedConfig.Id);

            UpdateOffsetDisplay();
            UpdateNameOffsetDisplay();
            UpdateNameAlignDisplay();
            UpdateNameLinesDisplay();
            UpdatePreview();

            UpdateCopiesDisplay(1);
        }

        private void SetButtonActive(Button btn, bool isActive)
        {
            btn.Background = isActive ? ActiveBg : InactiveBg;
            btn.BorderBrush = isActive ? ActiveBorder : InactiveBorder;
            btn.BorderThickness = new Thickness(isActive ? 1.5 : 1);
            btn.Foreground = isActive ? ActiveFg : InactiveFg;
            btn.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
        }

        private void UpdateNameAlignDisplay()
        {
            bool isLeft = _currentNameAlignment.Equals("left", StringComparison.OrdinalIgnoreCase);
            SetButtonActive(BtnAlignLeft, isLeft);
            SetButtonActive(BtnAlignCenter, !isLeft);
        }

        private void UpdateNameLinesDisplay()
        {
            SetButtonActive(BtnLines1, _currentNameLines == 1);
            SetButtonActive(BtnLines2, _currentNameLines > 1);
        }

        private void UpdateOffsetDisplay()
        {
            TxtOffset.Text = Math.Abs(_currentOffsetMm) < 0.01 ? "0.0 mm" : $"{_currentOffsetMm:+#0.0;-#0.0;0.0} mm";
        }

        private void UpdateNameOffsetDisplay()
        {
            TxtNameOffset.Text = Math.Abs(_currentNameOffsetMm) < 0.01 ? "0.0 mm" : $"{_currentNameOffsetMm:+#0.0;-#0.0;0.0} mm";
        }

        /// <summary>
        /// Jonli Preview ni yangilash.
        /// DrawingVisual orqali 100% haqiqiy printer chiqishi bilan bir xil (WYSIWYG) render hosil qilinadi.
        /// </summary>
        private void UpdatePreview()
        {
            if (CmbLabelSize.SelectedItem is not LabelSizeConfig config) return;

            var visual = PrinterService.CreateLabelVisual(
                _barcode,
                _productName,
                _sellingPrice,
                showName: ChkShowName.IsChecked == true,
                showPrice: ChkShowPrice.IsChecked == true,
                showBarcode: ChkShowBarcode.IsChecked == true,
                config: config,
                offsetMm: _currentOffsetMm,
                nameOffsetMm: _currentNameOffsetMm,
                nameAlignment: _currentNameAlignment,
                maxNameLines: _currentNameLines);

            // Yuqori tiniqlikda render qilish (2x DPI scale)
            double previewDpiScale = 2.0;
            int pxW = (int)Math.Max(1, Math.Round(config.VisualWidth * previewDpiScale));
            int pxH = (int)Math.Max(1, Math.Round(config.VisualHeight * previewDpiScale));

            var rtb = new RenderTargetBitmap(pxW, pxH, 96 * previewDpiScale, 96 * previewDpiScale, PixelFormats.Pbgra32);
            rtb.Render(visual);
            ImgPreview.Source = rtb;

            // Stiker kartochkasi o'lchamini ekranda qulay ko'rinishga moslash
            double displayScale = 1.4;
            StickerCard.Width = config.VisualWidth * displayScale;
            StickerCard.Height = config.VisualHeight * displayScale;
            TxtLabelSizeCaption.Text = config.DisplayName;
        }

        // ================= MAHSULOT NOMI SOZLAMALARI =================

        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            _currentNameAlignment = "left";
            UpdateNameAlignDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            _currentNameAlignment = "center";
            UpdateNameAlignDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnNameOffsetLeft_Click(object sender, RoutedEventArgs e)
        {
            _currentNameOffsetMm -= 0.5;
            UpdateNameOffsetDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnNameOffsetRight_Click(object sender, RoutedEventArgs e)
        {
            _currentNameOffsetMm += 0.5;
            UpdateNameOffsetDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnNameOffsetReset_Click(object sender, RoutedEventArgs e)
        {
            _currentNameOffsetMm = 0.0;
            UpdateNameOffsetDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnLines1_Click(object sender, RoutedEventArgs e)
        {
            _currentNameLines = 1;
            UpdateNameLinesDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void BtnLines2_Click(object sender, RoutedEventArgs e)
        {
            _currentNameLines = 2;
            UpdateNameLinesDisplay();
            SaveCurrentNameSettings();
            UpdatePreview();
        }

        private void SaveCurrentNameSettings()
        {
            if (CmbLabelSize.SelectedItem is LabelSizeConfig config)
            {
                _printerService.SaveLabelNameOffsetSetting(config.Id, _currentNameOffsetMm);
                _printerService.SaveLabelNameAlignSetting(config.Id, _currentNameAlignment);
                _printerService.SaveLabelNameLinesSetting(config.Id, _currentNameLines);
            }
        }

        // ================= UMUMIY SILJITISH (PRINTER KALIBROVKASI) =================

        private void BtnOffsetLeft_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm -= 0.5;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
            UpdatePreview();
        }

        private void BtnOffsetRight_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm += 0.5;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
            UpdatePreview();
        }

        private void BtnOffsetReset_Click(object sender, RoutedEventArgs e)
        {
            _currentOffsetMm = 0.0;
            UpdateOffsetDisplay();
            SaveCurrentOffset();
            UpdatePreview();
        }

        private void SaveCurrentOffset()
        {
            if (CmbLabelSize.SelectedItem is LabelSizeConfig config)
            {
                _printerService.SaveLabelOffsetSetting(config.Id, _currentOffsetMm);
            }
        }

        // ================= O'LCHAM VA PRINTER O'ZGARISHI =================

        private void CmbLabelSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (CmbLabelSize.SelectedItem is LabelSizeConfig config)
            {
                _printerService.SaveLabelSizeSetting(config.Id);
                _currentOffsetMm = _printerService.GetLabelOffsetSetting(config.Id);
                _currentNameOffsetMm = _printerService.GetLabelNameOffsetSetting(config.Id);
                _currentNameAlignment = _printerService.GetLabelNameAlignSetting(config.Id);
                _currentNameLines = _printerService.GetLabelNameLinesSetting(config.Id);

                UpdateOffsetDisplay();
                UpdateNameOffsetDisplay();
                UpdateNameAlignDisplay();
                UpdateNameLinesDisplay();
                UpdatePreview();
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
            UpdatePreview();
        }

        // ================= NUSXALAR SONI =================

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

        // ================= CHOP ETISH =================

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
                _currentOffsetMm,
                _currentNameOffsetMm,
                _currentNameAlignment,
                _currentNameLines);

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
