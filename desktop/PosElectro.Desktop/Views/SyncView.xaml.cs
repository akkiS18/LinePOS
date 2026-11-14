using System.Windows.Controls;
using System.Windows.Input;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop.Views
{
    public partial class SyncView : UserControl
    {
        public SyncView()
        {
            InitializeComponent();
        }

        /// <summary>Manzilni (ip:port) almashish xotirasiga nusxalaydi (faqat qulaylik).</summary>
        private void CopyAddress_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not SyncViewModel vm) return;
            try
            {
                System.Windows.Clipboard.SetText($"{vm.ServerIp}:{vm.ServerPort}");
                if (sender is Button b) b.Content = "Nusxalandi";
            }
            catch { }
        }

        private void SyncView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is SyncViewModel vm && vm.IsHelpModalOpen)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    vm.CloseHelpModalCommand?.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}

