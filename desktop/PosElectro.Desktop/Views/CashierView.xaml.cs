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

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CashierViewModel vm)
            {
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
                // Agar Savatni kutishga qo'yish (Hold) modali ochiq bo'lsa:
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

                // Agar Brak tasdiqlash modali ochiq bo'lsa:
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

                // Agar foydalanuvchi matn maydonida yozmayotgan bo'lsa va Backspace bosilsa:
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
