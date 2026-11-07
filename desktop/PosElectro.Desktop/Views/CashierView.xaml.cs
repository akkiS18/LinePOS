using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Views
{
    public partial class CashierView : UserControl
    {
        public CashierView()
        {
            InitializeComponent();
        }

        private void ProductList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange > 0 && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 120)
            {
                if (DataContext is CashierViewModel vm)
                {
                    vm.LoadNextBatch();
                }
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm)
            {
                if (vm.IsQuickCustomerModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.SaveQuickCustomerModalCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CloseQuickCustomerModalCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    return;
                }

                if (vm.IsPaymentModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.ConfirmSale();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.IsPaymentModalOpen = false;
                        e.Handled = true;
                        return;
                    }
                }

                if (vm.IsUnrecognizedBarcodeModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.ConfirmAddUnrecognizedBarcode();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CloseUnrecognizedBarcode();
                        e.Handled = true;
                        return;
                    }
                }

                var searchKey = e.Key == Key.System ? e.SystemKey : e.Key;
                if (!vm.IsPaymentModalOpen && !vm.IsEditModalOpen && !vm.IsUnrecognizedBarcodeModalOpen && !vm.IsHoldModalOpen && !vm.IsBrakModalOpen && vm.HasCartItems)
                {
                    if (searchKey == Key.F8)
                    {
                        vm.OpenPaymentModal(0);
                        e.Handled = true;
                        return;
                    }
                    else if (searchKey == Key.F9)
                    {
                        vm.OpenPaymentModal(1);
                        e.Handled = true;
                        return;
                    }
                    else if (searchKey == Key.F10)
                    {
                        vm.OpenPaymentModal(2);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.Key == Key.Enter)
                {
                    vm.HandleSearchQueryEnter();
                    e.Handled = true;
                }
            }
        }

        private void UnrecognizedBarcodeModal_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border && border.Visibility == Visibility.Visible)
            {
                border.Focus();
                AddUnrecognizedBarcodeBtn?.Focus();
            }
        }

        private void UnrecognizedBarcodeModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm && vm.IsUnrecognizedBarcodeModalOpen)
            {
                if (e.Key == Key.Enter)
                {
                    vm.ConfirmAddUnrecognizedBarcode();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.CloseUnrecognizedBarcode();
                    e.Handled = true;
                }
            }
        }

        private void EditModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm && vm.IsEditModalOpen)
            {
                if (e.Key == Key.Enter)
                {
                    vm.SaveEditModal();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.CancelEditModal();
                    e.Handled = true;
                }
            }
        }

        private void PaymentModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm && vm.IsPaymentModalOpen)
            {
                // Agar ustidagi Tezkor mijoz qo'shish modali ochiq bo'lsa, to'lov modali tugmalarni qabul qilmasin!
                if (vm.IsQuickCustomerModalOpen) return;

                if (e.Key == Key.Enter)
                {
                    vm.ConfirmSale();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.IsPaymentModalOpen = false;
                    e.Handled = true;
                }
            }
        }

        private void PaymentModal_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border)
            {
                if (border.Visibility == Visibility.Visible)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PaymentConfirmBtn?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SearchBox?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private void BrakModal_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border)
            {
                if (border.Visibility == Visibility.Visible)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        BrakConfirmBtn?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SearchBox?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private void HoldModal_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border && border.Visibility == Visibility.Visible)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TxtHoldCartName?.Focus();
                    TxtHoldCartName?.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void HoldModal_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm && vm.IsHoldModalOpen)
            {
                if (e.Key == Key.Enter)
                {
                    vm.ConfirmHoldCart();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.CloseHoldModal();
                    e.Handled = true;
                }
            }
        }

        private void HeldCartsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void HeldCartsScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            HeldCartsScrollViewer.ScrollToHorizontalOffset(HeldCartsScrollViewer.HorizontalOffset - 120);
        }

        private void HeldCartsScrollRight_Click(object sender, RoutedEventArgs e)
        {
            HeldCartsScrollViewer.ScrollToHorizontalOffset(HeldCartsScrollViewer.HorizontalOffset + 120);
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm)
            {
                // 1. Agar Tezkor mijoz qo'shish modali ochiq bo'lsa (To'lov ustida turgan bo'lsa ham eng birinchi bajariladi!):
                if (vm.IsQuickCustomerModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.SaveQuickCustomerModalCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CloseQuickCustomerModalCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                // 2. Agar To'lov tasdiqlash modali ochiq bo'lsa:
                if (vm.IsPaymentModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.ConfirmSale();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.IsPaymentModalOpen = false;
                        e.Handled = true;
                        return;
                    }
                }

                // 3. Agar Savatni kutishga qo'yish (Hold) modali ochiq bo'lsa:
                if (vm.IsHoldModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.ConfirmHoldCart();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CloseHoldModal();
                        e.Handled = true;
                        return;
                    }
                }

                // 4. Agar Brak tasdiqlash modali ochiq bo'lsa:
                if (vm.IsBrakModalOpen)
                {
                    if (e.Key == Key.Enter || e.Key == Key.Y)
                    {
                        vm.ConfirmBrakWriteOff();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape || e.Key == Key.N)
                    {
                        vm.IsBrakModalOpen = false;
                        e.Handled = true;
                        return;
                    }
                }

                // 3c. Agar Tovar tahrirlash modali ochiq bo'lsa:
                if (vm.IsEditModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.SaveEditModal();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CancelEditModal();
                        e.Handled = true;
                        return;
                    }
                }

                // 3d. Agar Noma'lum shtrix-kod modali ochiq bo'lsa:
                if (vm.IsUnrecognizedBarcodeModalOpen)
                {
                    if (e.Key == Key.Enter)
                    {
                        vm.ConfirmAddUnrecognizedBarcode();
                        e.Handled = true;
                        return;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        vm.CloseUnrecognizedBarcode();
                        e.Handled = true;
                        return;
                    }
                }

                // 4. Asosiy kassa ekranida tezkor to'lov tugmalari (Space / F8: Naqd, F9: Karta, F10: Aralash):
                if (!vm.IsPaymentModalOpen && !vm.IsEditModalOpen && !vm.IsUnrecognizedBarcodeModalOpen && !vm.IsHoldModalOpen && !vm.IsBrakModalOpen && vm.HasCartItems)
                {
                    var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
                    var isInput = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;

                    // Space: faqat biror matn maydonida bo'lmagandagina naqd to'lov
                    if (actualKey == Key.Space)
                    {
                        if (!isInput)
                        {
                            vm.OpenPaymentModal(0);
                            e.Handled = true;
                            return;
                        }
                    }
                    else if (actualKey == Key.F8)
                    {
                        vm.OpenPaymentModal(0);
                        e.Handled = true;
                        return;
                    }
                    else if (actualKey == Key.F9)
                    {
                        vm.OpenPaymentModal(1);
                        e.Handled = true;
                        return;
                    }
                    else if (actualKey == Key.F10)
                    {
                        vm.OpenPaymentModal(2);
                        e.Handled = true;
                        return;
                    }
                }

                // 5. Agar foydalanuvchi matn maydonida yozmayotgan bo'lsa va Backspace bosilsa:
                if (e.Key == Key.Back && !(e.OriginalSource is TextBox) && !(e.OriginalSource is PasswordBox))
                {
                    if (!vm.IsPaymentModalOpen && !vm.IsEditModalOpen && !vm.IsUnrecognizedBarcodeModalOpen && !vm.IsHoldModalOpen)
                    {
                        vm.OpenBrakModal();
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
