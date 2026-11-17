using System;
using System.ComponentModel;
using System.Linq;
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
            Services.WindowStyleHelper.ApplyDarkTheme(this);
            Services.KeyboardLayoutHelper.ForceEnglishLayout();
            _viewModel = new MainViewModel();
            if (Data.DatabaseContext.IsTestEnvironment)
            {
                Title = "[TEST MUHITI] " + Title;
            }
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
            InitBackgroundScanner();
        }

        // ===== 1.0.3: tray + fondagi skaner (USB Serial / COM rejim) =====
        private Services.TrayService? _tray;
        private Services.SerialScannerService? _serialScanner;
        private bool _reallyExit;

        private void InitBackgroundScanner()
        {
            try
            {
                _tray = new Services.TrayService();
                _tray.OpenRequested += () => Dispatcher.BeginInvoke(new Action(ShowFromTray));
                _tray.ExitRequested += () => Dispatcher.BeginInvoke(new Action(ExitApplication));
            }
            catch (Exception ex)
            {
                _tray = null;
                System.Diagnostics.Debug.WriteLine("Tray: " + ex.Message);
            }

            var db = _viewModel.Database;
            _serialScanner = new Services.SerialScannerService(
                () => db.GetSetting(Services.SerialScannerService.SettingKey, ""),
                port => Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { db.SetSetting(Services.SerialScannerService.SettingKey, port); } catch { }
                })));
            _serialScanner.BarcodeScanned += code => Dispatcher.BeginInvoke(new Action(() => OnBackgroundBarcode(code)));
            _serialScanner.StatusChanged += () => Dispatcher.BeginInvoke(new Action(UpdateTrayTooltip));
            _serialScanner.Start();
            UpdateTrayTooltip();

            if (Application.Current != null)
                Application.Current.SessionEnding += (_, _) => ExitApplication();
        }

        private void UpdateTrayTooltip()
        {
            if (_tray == null || _serialScanner == null) return;
            var port = _serialScanner.ActivePort;
            _tray.SetTooltip(!string.IsNullOrEmpty(port)
                ? $"Line kassa · skaner ulangan ({port})"
                : "Line kassa");
        }

        /// <summary>Trayda yoki boshqa oyna ortida turgan kassani oldinga chiqaradi.</summary>
        public void ShowFromTray()
        {
            Services.WindowActivator.BringToFront(this);
        }

        /// <summary>Haqiqiy chiqish (tray menyusi yoki Windows o'chayotganda).</summary>
        public void ExitApplication()
        {
            if (_reallyExit) return;
            _reallyExit = true;
            try { Close(); } catch { }
            Application.Current?.Shutdown();
        }

        /// <summary>
        /// COM skanerdan kod keldi. Qoidalar:
        /// 1) Kassa dasturi faol va kursor matn maydonida (tovar formasidagi shtrix-kod, qidiruv, summa) —
        ///    kod o'sha maydonga yoziladi va Enter beriladi (klaviatura rejimidagi bilan bir xil).
        /// 2) Dasturda boshqa dialog oynasi ochiq — o'sha oyna oldinga chiqadi, savat o'zgartirilmaydi.
        /// 3) Qolgan hamma holat (Telegram faol, dastur trayda, boshqa bo'lim) — kassa oynasi oldinga chiqadi,
        ///    Kassa bo'limiga o'tadi va tovar savatga tushadi (topilmasa — "topilmadi" oynasi).
        /// </summary>
        private void OnBackgroundBarcode(string code)
        {
            if (_viewModel.IsDeviceLocked)
            {
                ShowFromTray();
                return;
            }

            var dialog = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(w => w != this && w.IsVisible);
            bool appActive = IsActive || (dialog?.IsActive ?? false);

            if (appActive && Keyboard.FocusedElement is TextBox tb && tb.IsEnabled && !tb.IsReadOnly)
            {
                TypeIntoTextBox(tb, code);
                return;
            }

            if (dialog != null)
            {
                Services.WindowActivator.BringToFront(dialog);
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            ShowFromTray();
            if (_viewModel.CurrentView != _viewModel.CashierVM)
                _viewModel.NavigateCashierCommand.Execute(null);

            var cvm = _viewModel.CashierVM;
            if (cvm.IsPaymentModalOpen || cvm.IsUnrecognizedBarcodeModalOpen || cvm.IsHoldModalOpen || cvm.IsBrakModalOpen || cvm.IsEditModalOpen || cvm.IsQuickCustomerModalOpen)
            {
                // To'lov yoki boshqa oyna ochiq paytda savat jimgina o'zgarmasin
                System.Media.SystemSounds.Beep.Play();
                return;
            }
            cvm.HandleBarcodeScanned(code);
        }

        private static void TypeIntoTextBox(TextBox tb, string code)
        {
            tb.SelectedText = code;
            tb.CaretIndex = tb.SelectionStart + tb.SelectionLength;
            tb.SelectionLength = 0;

            var source = PresentationSource.FromVisual(tb);
            if (source == null) return;
            var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            tb.RaiseEvent(preview);
            if (preview.Handled) return;
            var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
            tb.RaiseEvent(down);
        }

        // Menyuni ⚙ tugmasining o'ng chetiga tekislaydi (Windows'ning "menyu tekislash" sozlamasidan qat'i nazar)
        private System.Windows.Controls.Primitives.CustomPopupPlacement[] SettingsPopup_Placement(Size popupSize, Size targetSize, Point offset)
        {
            return new[]
            {
                new System.Windows.Controls.Primitives.CustomPopupPlacement(
                    new Point(targetSize.Width - popupSize.Width + 24, targetSize.Height - 4),
                    System.Windows.Controls.Primitives.PopupPrimaryAxis.None)
            };
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SettingsToggle.IsChecked = false;
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

        /// <summary>Karta solig'i oynasidagi tayyor foiz tugmalari: maydonga qiymat yozadi (saqlash — "Saqlash" tugmasida).</summary>
        private void CardTaxPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string v) _viewModel.CardTaxRateInput = v;
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
            // Faqat shtrix-kod maydonida: ruscha harf (masalan 'Ф') kelsa inglizcha ekvivalentiga ('A') almashtiramiz.
            // Qidiruv maydonlari buni SmartSearchHelper orqali o'zi hal qiladi, nom/izoh maydonlarida esa
            // kirill harflari yozilishi kerak — ilgari ular ham buzilib ketardi.
            if (Keyboard.FocusedElement is TextBox tb && Services.KeyboardLayoutHelper.GetIsScannerField(tb))
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
            // 0. Global rasm ko'rish (Lightbox)
            if (_viewModel.IsImagePreviewOpen)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    _viewModel.CloseImagePreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Global modallar (PIN aktivatsiya, Dasturchi bilan aloqa, Karta solig'i)
            if (_viewModel.IsPinModalOpen)
            {
                if (e.Key == Key.Escape)
                {
                    _viewModel.ClosePinModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    _viewModel.SubmitPinCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            if (_viewModel.IsContactModalOpen)
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    _viewModel.CloseContactModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            if (_viewModel.IsCardTaxModalOpen)
            {
                if (e.Key == Key.Escape)
                {
                    _viewModel.CloseCardTaxModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    _viewModel.SaveCardTaxCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Skaner apparat savatga qo'shishi FAQAT KASSA oynasida bo'lgandagina ishlasin!
            // Ombor yoki boshqa ekranda bo'lganda klaviatura kabi faol maydonga yoziladi.
            if (_viewModel.CurrentView == _viewModel.CashierVM)
            {
                var isTextInput = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;

                // Agar foydalanuvchi biror matn maydonida (masalan SearchBox) yozayotgan bo'lsa,
                // skaner xizmati hodisani ushlab qolmasligi va harflar yutilib ketmasligi uchun o'tkazib yuboramiz.
                if (!isTextInput)
                {
                    _viewModel.ScannerService.HandlePreviewKeyDown(e);
                }

                if (!e.Handled && _viewModel.CashierVM != null)
                {
                    var cvm = _viewModel.CashierVM;
                    if (!cvm.IsPaymentModalOpen && !cvm.IsEditModalOpen && !cvm.IsUnrecognizedBarcodeModalOpen && !cvm.IsHoldModalOpen && !cvm.IsBrakModalOpen && cvm.HasCartItems)
                    {
                        var key = e.Key == Key.System ? e.SystemKey : e.Key;
                        
                        // Space = Naqd to'lash (F8 kabi). Kassada qidiruv maydoni doim fokusda turadi, shuning uchun
                        // maydon BO'SH bo'lsa ham Space to'lovni ochadi. Maydonda matn bo'lsa probel odatdagidek yoziladi
                        // ("kabel vvg" kabi ikki so'zli qidiruv buzilmasin).
                        if (key == Key.Space)
                        {
                            if (!Views.CashierView.SpaceShouldPay(Keyboard.FocusedElement))
                            {
                                return;
                            }
                            e.Handled = true;          // ushlab turilgan Space ham maydonga yozilmasin
                            if (!e.IsRepeat) cvm.OpenPaymentModal(0);
                            return;
                        }

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
            // X bosilsa — trayga yashirinadi, skaner tinglanishda davom etadi
            if (!_reallyExit && _tray != null)
            {
                e.Cancel = true;
                Hide();
                _tray.ShowHiddenHintOnce();
                return;
            }

            base.OnClosing(e);
            _viewModel.SyncServer.Stop();
            _viewModel.LicensingService.Dispose();
            _serialScanner?.Dispose();
            _tray?.Dispose();
            _tray = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // Tray bo'lmasa (yaratib bo'lmagan holatda) oddiy yopilish dasturni to'xtatadi
            Application.Current?.Shutdown();
        }
    }
}
