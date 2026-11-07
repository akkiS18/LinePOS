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

