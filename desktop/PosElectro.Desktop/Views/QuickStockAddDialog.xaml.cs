using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Views
{
    public partial class QuickStockAddDialog : Window
    {
        private readonly Product _product;
        private readonly Action<double> _onConfirm;
        private readonly string _unitLabel;

        public QuickStockAddDialog(Product product, string warehouseName, Action<double> onConfirm)
        {
            InitializeComponent();
            _product = product ?? throw new ArgumentNullException(nameof(product));
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));

            _unitLabel = product.UnitType switch
            {
                UnitType.METR => "metr",
                UnitType.KG => "kg",
                _ => "dona"
            };

            Loaded += QuickStockAddDialog_Loaded;
            InitializeData(warehouseName);
        }

        private void InitializeData(string warehouseName)
        {
            TxtProductName.Text = _product.Name;
            if (!string.IsNullOrWhiteSpace(_product.Barcode))
            {
                TxtBarcode.Text = $"Shtrix-kod: {_product.Barcode}";
                TxtBarcode.Visibility = Visibility.Visible;
            }
            else
            {
                TxtBarcode.Visibility = Visibility.Collapsed;
            }

            TxtWarehouseName.Text = $"Ombor: {warehouseName}";
            TxtUnitSuffix.Text = _unitLabel;

            var currStock = _product.StockQuantity;
            TxtCurrentStock.Text = $"{currStock:0.##} {_unitLabel}";
            TxtCurrentStock.Foreground = currStock > 0 
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)) 
                : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

            UpdatePreview(0);
        }

        private void QuickStockAddDialog_Loaded(object sender, RoutedEventArgs e)
        {
            TxtQuantity.Focus();
        }

        private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            var text = TxtQuantity.Text.Trim().Replace(',', '.');

            if (string.IsNullOrWhiteSpace(text))
            {
                UpdatePreview(0);
                return;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) && qty >= 0)
            {
                UpdatePreview(qty);
            }
            else
            {
                TxtCalculationPreview.Text = "Raqamni to'g'ri kiriting";
                TxtCalculationPreview.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
            }
        }

        private void UpdatePreview(double addedQty)
        {
            var current = _product.StockQuantity;
            var total = current + addedQty;
            TxtCalculationPreview.Text = $"{current:0.##} + {addedQty:0.##} = {total:0.##} {_unitLabel}";
            TxtCalculationPreview.Foreground = new SolidColorBrush(Color.FromRgb(0x2D, 0xD4, 0xBF));
        }

        private void TxtQuantity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ConfirmAdd();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ConfirmAdd();
        }

        private void ConfirmAdd()
        {
            var text = TxtQuantity.Text.Trim().Replace(',', '.');
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowError("Iltimos, yangi kelgan tovar sonini kiriting!");
                return;
            }

            if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                ShowError("Iltimos, 0 dan katta to'g'ri son kiriting!");
                return;
            }

            try
            {
                _onConfirm.Invoke(qty);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Xatolik yuz berdi: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
            TxtQuantity.Focus();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
