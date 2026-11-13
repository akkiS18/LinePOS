using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views
{
    public partial class ReorderListDialog : Window
    {
        private readonly ObservableCollection<Product> _items;
        private readonly Action<Product>? _onRemoveItem;
        private readonly Action? _onClearAll;
        private readonly PrinterService _printerService;
        private readonly DatabaseContext _db;
        private readonly string _warehouseName;

        public ReorderListDialog(
            IEnumerable<Product> items, 
            Action<Product>? onRemoveItem, 
            Action? onClearAll,
            PrinterService printerService,
            DatabaseContext? db = null,
            string warehouseName = "Barcha omborlar")
        {
            InitializeComponent();
            _db = db ?? new DatabaseContext();
            _items = new ObservableCollection<Product>(items);
            _onRemoveItem = onRemoveItem;
            _onClearAll = onClearAll;
            _printerService = printerService;
            _warehouseName = warehouseName;

            DgReorderItems.ItemsSource = _items;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            // Maket 2.10: sarlavha osti va pastki yozuv (faqat ko'rsatish)
            TxtTotalCount.Text = $"{_items.Count} ta tovar · keraksizini ✕ bilan olib tashlang";
            TxtSummary.Text = $"Buyurtma uchun tanlangan tovarlar · {_items.Count} ta · {_warehouseName}";
            TxtEmpty.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        }

        private void BtnChangeImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Product product)
            {
                SelectImageForProduct(product);
            }
        }

        private void MenuSelectImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Product product)
            {
                SelectImageForProduct(product);
            }
        }

        private void MenuRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Product product)
            {
                product.ImagePath = string.Empty;
                _db.UpdateProductImage(product.Guid, string.Empty);
            }
        }

        private void SelectImageForProduct(Product product)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"{product.Name} uchun rasm tanlang",
                    Filter = "Rasm fayllari (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Barcha fayllar (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    var sourcePath = dialog.FileName;
                    if (!File.Exists(sourcePath)) return;

                    var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                    var imagesDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PosElectro",
                        "ProductImages");

                    if (!Directory.Exists(imagesDir))
                    {
                        Directory.CreateDirectory(imagesDir);
                    }

                    var targetPath = Path.Combine(imagesDir, $"{product.Guid}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}");
                    File.Copy(sourcePath, targetPath, overwrite: true);

                    product.ImagePath = targetPath;
                    _db.UpdateProductImage(product.Guid, targetPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rasmni saqlashda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Product p)
            {
                p.IsSelected = false;
                _items.Remove(p);
                _onRemoveItem?.Invoke(p);
                UpdateSummary();
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;

            var result = MessageBox.Show(
                "Buyurtma ro'yxatidagi barcha tovarlarni olib tashlamoqchimisiz?",
                "Tasdiqlash",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in _items.ToList())
                {
                    item.IsSelected = false;
                }
                _items.Clear();
                _onClearAll?.Invoke();
                UpdateSummary();
                Close();
            }
        }

        private void BtnPrintReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Chop etish uchun hech qanday tovar tanlanmagan!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = _printerService.PrintReorderReceipt(_items.ToList());
            if (success)
            {
                MessageBox.Show("Buyurtma ro'yxati chek printeriga muvaffaqiyatli yuborildi!", "Chop etish", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Chek printeri topilmadi yoki ulanmagan. Iltimos, printer ulanishini tekshiring.", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Excelga saqlash uchun hech qanday tovar tanlanmagan!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExcelExportService.ExportReorderList(_items.ToList(), _warehouseName);
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
        }
    }
}
