using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PosElectro.Desktop.ViewModels;

namespace PosElectro.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private const int WM_MOUSEHWHEEL = 0x020E;

        public MainWindow()
        {
            InitializeComponent();
            Services.KeyboardLayoutHelper.ForceEnglishLayout();
            _viewModel = new MainViewModel();
            if (Data.DatabaseContext.IsTestEnvironment)
            {
                Title = "[TEST MUHITI] " + Title;
            }
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
        }

        private bool _isUpdatingPin;

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PinInputText))
            {
                if (_isUpdatingPin) return;
                _isUpdatingPin = true;
                string newPin = _viewModel.PinInputText ?? string.Empty;
                if (PinPasswordBox != null && PinPasswordBox.Password != newPin)
                    PinPasswordBox.Password = newPin;
                if (PinTextBox != null && PinTextBox.Text != newPin)
                    PinTextBox.Text = newPin;
                _isUpdatingPin = false;
            }
            else if (e.PropertyName == nameof(MainViewModel.IsPinModalOpen))
            {
                if (_viewModel.IsPinModalOpen)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_viewModel.IsPinVisible)
                        {
                            PinTextBox?.Focus();
                            PinTextBox?.SelectAll();
                        }
                        else
                        {
                            PinPasswordBox?.Focus();
                        }
                    }));
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.IsPinVisible))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_viewModel.IsPinVisible)
                    {
                        PinTextBox?.Focus();
                        if (PinTextBox != null) PinTextBox.CaretIndex = PinTextBox.Text.Length;
                    }
                    else
                    {
                        PinPasswordBox?.Focus();
                    }
                }));
            }
        }

        private void PinPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingPin) return;
            _isUpdatingPin = true;
            if (_viewModel != null)
            {
                _viewModel.PinInputText = PinPasswordBox.Password;
                if (PinTextBox != null && PinTextBox.Text != PinPasswordBox.Password)
                {
                    PinTextBox.Text = PinPasswordBox.Password;
                }
            }
            _isUpdatingPin = false;
        }

        private void PinTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPin) return;
            _isUpdatingPin = true;
            if (_viewModel != null)
            {
                _viewModel.PinInputText = PinTextBox.Text;
                if (PinPasswordBox != null && PinPasswordBox.Password != PinTextBox.Text)
                {
                    PinPasswordBox.Password = PinTextBox.Text;
                }
            }
            _isUpdatingPin = false;
        }

        private void Window_Activated(object? sender, EventArgs e)
        {
            Services.KeyboardLayoutHelper.ForceEnglishLayout();
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Agar foydalanuvchi qidiruv yoki shtrix-kod kiritayotganda ruscha harflar (masalan 'Ф') kelib qolsa,
            // uni darhol inglizcha klaviatura ekvivalentiga (masalan 'A') almashtirib maydonga joylaymiz.
            if (Keyboard.FocusedElement is TextBox tb)
            {
                var original = e.Text;
                var converted = Services.KeyboardLayoutHelper.ConvertRuToEn(original);
                if (converted != original)
                {
                    int caret = tb.CaretIndex;
                    tb.SelectedText = converted;
                    tb.CaretIndex = caret + converted.Length;
                    e.Handled = true;
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                var pos = Mouse.GetPosition(this);
                var hit = InputHitTest(pos) as DependencyObject;
                var sv = FindParentScrollViewer(hit);
                if (sv != null && sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset + delta);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is ScrollViewer sv) return sv;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Skaner apparat savatga qo'shishi FAQAT KASSA oynasida bo'lgandagina ishlasin!
            // Ombor yoki boshqa ekranda bo'lganda klaviatura kabi faol maydonga yoziladi.
            if (_viewModel.CurrentView == _viewModel.CashierVM)
            {
                _viewModel.ScannerService.HandlePreviewKeyDown(e);

                if (!e.Handled && _viewModel.CashierVM != null)
                {
                    var cvm = _viewModel.CashierVM;
                    if (!cvm.IsPaymentModalOpen && !cvm.IsEditModalOpen && !cvm.IsUnrecognizedBarcodeModalOpen && !cvm.IsHoldModalOpen && !cvm.IsBrakModalOpen && cvm.HasCartItems)
                    {
                        var key = e.Key == Key.System ? e.SystemKey : e.Key;
                        if (key == Key.F8)
                        {
                            cvm.OpenPaymentModal(0);
                            e.Handled = true;
                            return;
                        }
                        if (key == Key.F9)
                        {
                            cvm.OpenPaymentModal(1);
                            e.Handled = true;
                            return;
                        }
                        if (key == Key.F10)
                        {
                            cvm.OpenPaymentModal(2);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _viewModel.SyncServer.Stop();
            _viewModel.LicensingService.Dispose();
        }
    }
}
