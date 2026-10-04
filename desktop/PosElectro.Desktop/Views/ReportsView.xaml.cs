using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Views
{
    public partial class ReportsView : UserControl
    {
        public ReportsView()
        {
            InitializeComponent();
        }

        private void BtnViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Sale sale)
            {
                OpenSaleDetail(sale);
            }
        }

        private void GridSales_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridSales.SelectedItem is Sale sale)
            {
                OpenSaleDetail(sale);
            }
        }

        private void OpenSaleDetail(Sale sale)
        {
            try
            {
                var dialog = new SaleDetailDialog(sale, database: (DataContext as PosElectro.Desktop.ViewModels.ReportsViewModel)?.Database)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Tafsilotlarni ko'rsatishda xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
