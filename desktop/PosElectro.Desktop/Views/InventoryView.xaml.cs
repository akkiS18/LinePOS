using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Views
{
    public partial class InventoryView : UserControl
    {
        private static readonly NumberFormatInfo SpaceGroupFormat = new()
        {
            NumberGroupSeparator = " ",
            NumberGroupSizes = new[] { 3 },
            NumberDecimalDigits = 0
        };

        public InventoryView()
        {
            InitializeComponent();
        }

        private void PriceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            var text = tb.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            // Barcha bo'shliqlar (\u00A0 non-breaking space, oddiy bo'shliq), vergul va apostroflarni tozalash
            var clean = Regex.Replace(text, @"[\s,\u00A0']", "");
            clean = Regex.Replace(clean, @"[^\d.]", "");

            if (string.IsNullOrEmpty(clean)) return;

            if (clean.Contains('.'))
            {
                var parts = clean.Split('.', 2);
                if (long.TryParse(parts[0], out var intPart))
                {
                    var formatted = $"{intPart.ToString("N0", SpaceGroupFormat)}.{parts[1]}";
                    if (formatted != text)
                    {
                        var caret = tb.CaretIndex;
                        var oldLen = text.Length;
                        tb.Text = formatted;
                        var newLen = formatted.Length;
                        tb.CaretIndex = Math.Max(0, Math.Min(newLen, caret + (newLen - oldLen)));
                    }
                }
            }
            else if (long.TryParse(clean, out var intVal))
            {
                var formatted = intVal.ToString("N0", SpaceGroupFormat);
                if (formatted != text)
                {
                    var caret = tb.CaretIndex;
                    var oldLen = text.Length;
                    tb.Text = formatted;
                    var newLen = formatted.Length;
                    tb.CaretIndex = Math.Max(0, Math.Min(newLen, caret + (newLen - oldLen)));
                }
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is PosElectro.Desktop.Models.Product product && DataContext is InventoryViewModel vm)
            {
                vm.OpenEditProductCommand.Execute(product);
            }
        }
    }
}
