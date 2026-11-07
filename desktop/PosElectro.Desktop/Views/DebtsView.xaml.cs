using System.Windows.Controls;
using System.Windows.Input;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Views;

public partial class DebtsView : UserControl
{
    public DebtsView()
    {
        InitializeComponent();
    }

    private void DebtsView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is DebtsViewModel vm)
        {
            // 1. Mijoz haqida batafsil ma'lumot modali
            if (vm.IsCustomerInfoModalOpen)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    vm.CloseCustomerInfoModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // 2. Qarz to'lovini qabul qilish modali
            if (vm.IsPaymentModalOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.ClosePaymentModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    if (vm.ConfirmPaymentCommand.CanExecute(null))
                    {
                        vm.ConfirmPaymentCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }
            }

            // 3. Ortiqcha to'lovni qaytarish (Haqdorlik) modali
            if (vm.IsRefundCreditModalOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CloseRefundCreditModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    if (vm.ConfirmRefundCreditCommand.CanExecute(null))
                    {
                        vm.ConfirmRefundCreditCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }
            }

            // 4. Mijoz yaratish / tahrirlash modali
            if (vm.IsCustomerModalOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CloseCustomerModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    // Agar fokus ko'p qatorli Izoh maydonida bo'lsa, yangi qatorga ruxsat
                    if (Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn)
                    {
                        return;
                    }

                    if (vm.SaveCustomerModalCommand.CanExecute(null))
                    {
                        vm.SaveCustomerModalCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }
            }
        }
    }
}
