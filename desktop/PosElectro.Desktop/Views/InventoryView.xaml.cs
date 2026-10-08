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

        private void InventoryView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is InventoryViewModel vm && vm.IsAddEditFormVisible)
            {
                // Agar ustki modal dialoglar ochiq bo'lsa (kategoriya/ombor modallari), ularga xalaqit bermaslik
                if (vm.IsAddCategoryModalOpen || vm.IsEditCategoryModalOpen || vm.IsAddWarehouseModalOpen || vm.IsTransferModalOpen)
                {
                    return;
                }

                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (vm.CancelAddEditCommand.CanExecute(null))
                    {
                        vm.CancelAddEditCommand.Execute(null);
                        e.Handled = true;
                    }
                }
                else if (e.Key == System.Windows.Input.Key.Enter)
                {
                    // Agar fokus ko'p qatorli Izoh maydonida bo'lsa, foydalanuvchiga qator tashlashga imkon berish
                    if (System.Windows.Input.Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn)
                    {
                        return;
                    }

                    if (vm.SaveProductFormCommand.CanExecute(null))
                    {
                        vm.SaveProductFormCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
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

        private void DataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                if (sender is DependencyObject dep)
                {
                    var sv = FindChildScrollViewer(dep);
                    if (sv != null && sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                    {
                        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                        e.Handled = true;
                    }
                }
            }
        }

        public static string CapitalizeFirstLetter(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]))
                {
                    if (char.IsUpper(input[i])) return input;
                    return input.Substring(0, i) + char.ToUpper(input[i]) + input.Substring(i + 1);
                }
                else if (char.IsDigit(input[i]))
                {
                    // Birinchi belgi raqam bo'lsa (masalan: 2_ombor) o'zgarishsiz qoladi
                    return input;
                }
            }
            return input;
        }

        private void ProductNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            var text = tb.Text;
            if (string.IsNullOrEmpty(text)) return;

            var capitalized = CapitalizeFirstLetter(text);
            if (capitalized != text)
            {
                var caret = tb.CaretIndex;
                tb.Text = capitalized;
                tb.CaretIndex = Math.Min(capitalized.Length, caret);
            }
        }

        private static ScrollViewer? FindChildScrollViewer(DependencyObject? root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                var result = FindChildScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
