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
            BtnStockQty.Content = $"Qoldiq soni ({stockInt})";
            BtnStockQty.Tag = stockInt;
            TxtProductName.Text = _productName;
            TxtStockInfo.Text = $"qoldiq {_stockQuantity:0.##}";

            // Printerlar ro'yxatini to'ldirish
            var printers = PrinterService.GetInstalledPrinters();
            CmbPrinters.ItemsSource = printers;

            // Printerni aniqlash
            _detectedPrinter = _printerService.FindLabelPrinter();
            if (!string.IsNullOrWhiteSpace(_detectedPrinter) && printers.Contains(_detectedPrinter))
            {
                CmbPrinters.SelectedItem = _detectedPrinter;
                SetPrinterStatus($"{_detectedPrinter} · tayyor", "Ok");
            }
            else if (printers.Count > 0)
            {
                CmbPrinters.SelectedIndex = 0;
                _detectedPrinter = printers[0];
                SetPrinterStatus($"{_detectedPrinter} · tanlangan", "Accent");
            }
            else
            {
                SetPrinterStatus("Printer o'rnatilmagan", "Warn");
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
            // Ko'rinish uslubda (SegTab): Tag=True — tanlangan. Ranglar tokenlardan.
            btn.Tag = isActive;
        }

        /// <summary>Sarlavhadagi printer belgisi: kind = "Ok" | "Accent" | "Warn".</summary>
        private void SetPrinterStatus(string text, string kind)
        {
            TxtPrinterStatus.Text = text;
            var ink = kind == "Accent" ? "AccentInk" : kind;
            TxtPrinterStatus.SetResourceReference(TextBlock.ForegroundProperty, ink);
            PrinterDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, ink);
            PrinterPill.SetResourceReference(Border.BackgroundProperty, kind + "Soft");
        }

        /// <summary>Sarlavha osti va "Printer va kalibrovka" qisqa yozuvi (faqat ko'rsatish).</summary>
        private void UpdateSummaries()
        {
            if (TxtSubtitle == null || TxtCalibSummary == null) return;
            int copies = GetCurrentCopies();
            TxtSubtitle.Text = $"{_productName} · jami {copies} stiker";
            var size = (CmbLabelSize.SelectedItem as LabelSizeConfig)?.DisplayName ?? "";
            TxtCalibSummary.Text = $"{size} · nom {_currentNameOffsetMm:+0.0;-0.0;0.0} · umumiy {_currentOffsetMm:+0.0;-0.0;0.0}";
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
            TxtCopies.Focus();
            TxtCopies.SelectAll();
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
            UpdateSummaries();
        }

        private void UpdateNameOffsetDisplay()
        {
            TxtNameOffset.Text = Math.Abs(_currentNameOffsetMm) < 0.01 ? "0.0 mm" : $"{_currentNameOffsetMm:+#0.0;-#0.0;0.0} mm";
            UpdateSummaries();
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
            double displayScale = Math.Min(1.6, Math.Min(290.0 / config.VisualWidth, 300.0 / config.VisualHeight));
            StickerCard.Width = config.VisualWidth * displayScale;
            StickerCard.Height = config.VisualHeight * displayScale;
            TxtLabelSizeCaption.Text = $"JONLI KO'RINISH · {config.DisplayName}";
            UpdateSummaries();
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
                SetPrinterStatus($"{selectedPrinter} · tanlangan", "Ok");
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
            BtnPrint.Content = $"{copies} ta chop etish";
            UpdateSummaries();
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
            BtnPrint.Content = $"{copies} ta chop etish";
            UpdateSummaries();
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

            if (isTest)
            {
                bool ok = _printerService.PrintBarcodeLabel(
                    _barcode,
                    _productName,
                    _sellingPrice,
                    1,
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
                    ShowStatus("1 dona sinov stikeri printerga yuborildi", isError: false);
                }
                else
                {
                    ShowStatus("Printerga yuborib bo'lmadi. Printer ulanganini tekshiring.", isError: true);
                }
                return;
            }

            // Asosiy stikerlarni chop etish (Fonda asinxron ishlaydi)
            if (LabelPrintService.Instance.IsPrinting)
            {
                ShowStatus("Hozir boshqa stiker chop etilmoqda. Tugashini kuting.", isError: true);
                return;
            }

            bool started = LabelPrintService.Instance.StartPrintJob(
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

            if (started)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ShowStatus("Printerga yuborib bo'lmadi.", isError: true);
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            TxtStatus.Text = message;
            TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, isError ? "Bad" : "Ok");
            TxtStatus.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                BtnCancel_Click(this, null!);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && !CmbPrinters.IsDropDownOpen && !CmbLabelSize.IsDropDownOpen)
            {
                BtnPrint_Click(this, null!);
                e.Handled = true;
            }
        }
    }
}
