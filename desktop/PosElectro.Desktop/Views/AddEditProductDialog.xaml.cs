using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public partial class AddEditProductDialog : Window
    {
        private readonly ProductService _productService;
        private readonly CurrencyService? _currencyService;
        private readonly Product? _editingProduct;
        private readonly double _usdRate;

        public Product? ResultProduct { get; private set; }

        public AddEditProductDialog(ProductService productService, CurrencyService? currencyService = null, Product? productToEdit = null)
        {
            InitializeComponent();
            _productService = productService;
            _currencyService = currencyService;
            _editingProduct = productToEdit;
            _usdRate = _currencyService?.GetCachedUsdRate() ?? CurrencyService.DEFAULT_USD_RATE;

            if (_editingProduct != null)
            {
                TxtTitle.Text = "Mahsulotni Tahrirlash";
                TxtName.Text = _editingProduct.Name;
                TxtBarcode.Text = _editingProduct.Barcode ?? "";
                TxtCategory.Text = _editingProduct.Category;
                TxtCostPrice.Text = _editingProduct.CostPrice.ToString(CultureInfo.InvariantCulture);
                TxtSellingPrice.Text = _editingProduct.SellingPrice.ToString(CultureInfo.InvariantCulture);
                TxtSellingPrice2.Text = _editingProduct.SellingPrice2?.ToString(CultureInfo.InvariantCulture) ?? "";
                TxtStockQuantity.Text = _editingProduct.StockQuantity.ToString(CultureInfo.InvariantCulture);
                TxtMinStockAlert.Text = _editingProduct.MinStockAlert.ToString(CultureInfo.InvariantCulture);

                if (_editingProduct.CostCurrency == "USD")
                {
                    RbCostUsd.IsChecked = true;
                }
                else
                {
                    RbCostUzs.IsChecked = true;
                }

                CmbUnitType.SelectedIndex = _editingProduct.UnitType switch
                {
                    UnitType.DONA => 0,
                    UnitType.METR => 1,
                    UnitType.KG => 2,
                    _ => 0
                };
            }
            else
            {
                RbCostUzs.IsChecked = true;
            }

            UpdateCostCurrencyDisplay();
            UpdateBarcodeButtons();
        }

        private void TxtBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            
            if (sender is TextBox tb)
            {
                var text = tb.Text;
                var fixedText = Services.KeyboardLayoutHelper.FixBarcodeString(text);
                if (fixedText != text)
                {
                    var caret = tb.CaretIndex;
                    tb.Text = fixedText;
                    tb.CaretIndex = Math.Min(fixedText.Length, caret);
                }
            }
            
            UpdateBarcodeButtons();
        }

        private void UpdateBarcodeButtons()
        {
            if (BtnPrintBarcode != null)
            {
                bool hasBarcode = !string.IsNullOrWhiteSpace(TxtBarcode.Text);
                BtnPrintBarcode.IsEnabled = hasBarcode;
                BtnPrintBarcode.Opacity = hasBarcode ? 1.0 : 0.5;
            }
        }

        private void BtnGenerateBarcode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newBarcode = _productService.GenerateUniqueBarcode();
                TxtBarcode.Text = newBarcode;
                UpdateBarcodeButtons();
            }
            catch (Exception ex)
            {
                ShowError($"Shtrix-kod generatsiyasida xatolik: {ex.Message}");
            }
        }

        private void BtnPrintBarcode_Click(object sender, RoutedEventArgs e)
        {
            string barcode = TxtBarcode.Text.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
            {
                ShowError("Avval shtrix-kod kiriting yoki 'Yaratish' tugmasini bosing!");
                return;
            }

            string name = TxtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Mahsulot";
            }

            double.TryParse(TxtSellingPrice.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
            double.TryParse(TxtStockQuantity.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stock);

            var printerService = new PrinterService(_productService.Database);
            var dialog = new BarcodePrintDialog(name, barcode, price, stock, printerService)
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private void RbCostCurrency_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateCostCurrencyDisplay();
        }

        private void TxtCostPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateCostCurrencyDisplay();
        }

        private void UpdateCostCurrencyDisplay()
        {
            if (LblCostTitle == null || TxtCostConversion == null) return;

            bool isUsd = RbCostUsd.IsChecked == true;
            LblCostTitle.Text = isUsd ? "Tan narxi ($) *" : "Tan narxi (so'm) *";

            if (isUsd)
            {
                TxtCostConversion.Visibility = Visibility.Visible;
                if (double.TryParse(TxtCostPrice.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var usdVal))
                {
                    double inUzs = usdVal * _usdRate;
                    TxtCostConversion.Text = $"≈ {inUzs:N0} so'm (1$ = {_usdRate:N0})";
                }
                else
                {
                    TxtCostConversion.Text = $"≈ 0 so'm (1$ = {_usdRate:N0})";
                }
            }
            else
            {
                TxtCostConversion.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            var text = tb.Text;
            if (string.IsNullOrEmpty(text)) return;

            var capitalized = PosElectro.Desktop.Views.InventoryView.CapitalizeFirstLetter(text);
            if (capitalized != text)
            {
                var caret = tb.CaretIndex;
                tb.Text = capitalized;
                tb.CaretIndex = Math.Min(capitalized.Length, caret);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            var name = PosElectro.Desktop.Views.InventoryView.CapitalizeFirstLetter(TxtName.Text.Trim());
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Mahsulot nomini kiritish majburiy!");
                return;
            }

            // Aqlli dublikat tekshiruvi (Katta-kichik harf, bo'shliqlar va apostroflarni inobatga olgan holda)
            var duplicate = _productService.FindDuplicateProduct(name, _editingProduct?.Id ?? 0);
            if (duplicate != null)
            {
                ShowError($"⚠️ '{duplicate.Name}' nomli mahsulot omborda allaqachon mavjud! Takroriy tovar qo'shib bo'lmaydi.");
                return;
            }

            if (!double.TryParse(TxtCostPrice.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var costPrice))
            {
                ShowError("Tan narxi to'g'ri raqamda kiritilishi kerak!");
                return;
            }

            if (!double.TryParse(TxtSellingPrice.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var sellingPrice))
            {
                ShowError("Sotish narxi to'g'ri raqamda kiritilishi kerak!");
                return;
            }

            double? sellingPrice2 = null;
            if (!string.IsNullOrWhiteSpace(TxtSellingPrice2.Text))
            {
                if (double.TryParse(TxtSellingPrice2.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var sp2) && sp2 > 0)
                {
                    sellingPrice2 = sp2;
                }
                else
                {
                    ShowError("2-sotish narxi to'g'ri raqamda kiritilishi kerak yoki bo'sh qoldirilishi lozim!");
                    return;
                }
            }

            if (!double.TryParse(TxtStockQuantity.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stockQuantity))
            {
                ShowError("Qoldiq miqdori to'g'ri raqamda kiritilishi kerak!");
                return;
            }

            double.TryParse(TxtMinStockAlert.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var minStockAlert);
            if (minStockAlert <= 0) minStockAlert = 3.0;

            var unitType = CmbUnitType.SelectedIndex switch
            {
                0 => UnitType.DONA,
                1 => UnitType.METR,
                2 => UnitType.KG,
                _ => UnitType.DONA
            };

            var product = _editingProduct ?? new Product();
            product.Name = name;
            product.Barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text) ? null : TxtBarcode.Text.Trim();
            product.Category = string.IsNullOrWhiteSpace(TxtCategory.Text) ? "Barchasi" : TxtCategory.Text.Trim();
            product.CostCurrency = RbCostUsd.IsChecked == true ? "USD" : "UZS";
            product.CostPrice = costPrice;
            product.SellingPrice = sellingPrice;
            product.SellingPrice2 = sellingPrice2;
            product.StockQuantity = stockQuantity;
            product.UnitType = unitType;
            product.MinStockAlert = minStockAlert;

            try
            {
                _productService.SaveProduct(product);
                ResultProduct = product;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
