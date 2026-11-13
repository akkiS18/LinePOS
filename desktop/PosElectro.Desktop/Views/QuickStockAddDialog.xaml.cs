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
            // Maket 2.5: "Tovar · birlik · ombor"
            TxtProductName.Text = $"{_product.Name} · {_unitLabel} · {warehouseName}";

            var currStock = _product.StockQuantity;
            TxtCurrentStock.Text = $"{currStock:0.##}";
            TxtCurrentStock.SetResourceReference(TextBlock.ForegroundProperty, currStock > 0 ? "Ink" : "Bad");

            UpdatePreview(0);
        }

        private void QuickStockAddDialog_Loaded(object sender, RoutedEventArgs e)
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
            TxtQuantity.Focus();
        }

        /// <summary>+10 / +50 / +100: maydondagi songa qo'shadi (faqat kiritishni tezlashtiradi).</summary>
        private void QuickAdd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || !double.TryParse(b.Tag?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var add)) return;
            var text = TxtQuantity.Text.Trim().Replace(',', '.');
            double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var cur);
            if (cur < 0) cur = 0;
            TxtQuantity.Text = (cur + add).ToString("0.##", CultureInfo.InvariantCulture);
            TxtQuantity.CaretIndex = TxtQuantity.Text.Length;
            TxtQuantity.Focus();
        }

        private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtQtyPlaceholder.Visibility = string.IsNullOrEmpty(TxtQuantity.Text) ? Visibility.Visible : Visibility.Collapsed;
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
                TxtCalculationPreview.Text = "—";
                TxtCalculationPreview.SetResourceReference(TextBlock.ForegroundProperty, "Bad");
                TxtError.Text = "Raqamni to'g'ri kiriting";
                TxtError.Visibility = Visibility.Visible;
            }
        }

        private void UpdatePreview(double addedQty)
        {
            var current = _product.StockQuantity;
            var total = current + addedQty;
            TxtCalculationPreview.Text = $"{total:0.##}";
            TxtCalculationPreview.SetResourceReference(TextBlock.ForegroundProperty, "Ok");
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                BtnCancel_Click(this, null!);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                BtnConfirm_Click(this, null!);
                e.Handled = true;
            }
        }
    }
}
