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

    /// <summary>Modal ochilganda birinchi maydonga fokus (faqat qulaylik).</summary>
    private void FocusWhenShown(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
            Dispatcher.BeginInvoke(new System.Action(() => { tb.Focus(); tb.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>"Boshqa amallar" menyusidan band tanlangach menyu yopiladi.</summary>
    private void MoreMenuItem_Click(object sender, System.Windows.RoutedEventArgs e) => MoreToggle.IsChecked = false;

    private void DebtsView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is DebtsViewModel vm)
        {
            // 0. Bloklash tasdig'i (maket 3.7)
            if (vm.IsBlockConfirmOpen)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CancelToggleArchiveCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && !(e.OriginalSource is Button))
                {
                    e.Handled = true;
                }
                return;
            }

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
