using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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
        private readonly string _warehouseName;

        public ReorderListDialog(
            IEnumerable<Product> items, 
            Action<Product>? onRemoveItem, 
            Action? onClearAll,
            PrinterService printerService,
            string warehouseName = "Barcha omborlar")
        {
            InitializeComponent();
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
            TxtTotalCount.Text = $"Jami: {_items.Count} ta tovar";
            TxtSummary.Text = _items.Count > 0 
                ? $"Ro'yxatda {_items.Count} ta mahsulot mavjud. Adashib qo'shilganlarini o'chirishingiz yoki chek/Excelga chiqarishingiz mumkin."
                : "Buyurtma uchun hech qanday tovar qolmadi.";
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
                // Agar chek printeri topilmasa yoki xatolik bo'lsa
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
    }
}
