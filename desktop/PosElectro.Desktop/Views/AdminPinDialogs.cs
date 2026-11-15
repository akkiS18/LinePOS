using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PosElectro.Desktop.Services;

namespace PosElectro.Desktop.Views;

/// <summary>Maxfiy ma'lumotni ochish uchun Admin PIN so'rash (maket 4.3: 4 katak + raqamli klaviatura).</summary>
public sealed class AdminPinPromptDialog : Window
{
    private string _pin = string.Empty;

    public AdminPinPromptDialog(string reason)
    {
        Title = "Admin PIN";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontSize = 14;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PosElectro.Desktop;component/Themes/SmartControls.xaml", UriKind.Relative) });

        var root = new Grid();
        root.SetResourceReference(Panel.BackgroundProperty, "Scrim");
        root.SetResourceReference(TextElement.ForegroundProperty, "Ink");
        Content = root;

        var card = new Border { Width = 360, Margin = new Thickness(24), Padding = new Thickness(24), Style = (Style)FindResource("ModalCard") };
        root.Children.Add(card);
        var panel = new StackPanel();
        card.Child = panel;

        // Qulf belgisi
        var lockPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6 11h12a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-6a2 2 0 0 1 2-2z M8 11V7a4 4 0 0 1 8 0v4"),
            StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        lockPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentInk");
        var badge = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(14), HorizontalAlignment = HorizontalAlignment.Center, Child = lockPath };
        badge.SetResourceReference(Border.BackgroundProperty, "AccentSoft");
        panel.Children.Add(badge);

        panel.Children.Add(new TextBlock { Text = "Admin PIN", FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
        var sub = new TextBlock { Text = reason, FontSize = 13, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "Ink2");
        panel.Children.Add(sub);

        // 4 ta katak
        var boxes = new Border[4];
        var dots = new TextBlock[4];
        var boxRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        for (int i = 0; i < 4; i++)
        {
            dots[i] = new TextBlock { FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            boxes[i] = new Border { Width = 48, Height = 56, CornerRadius = new CornerRadius(10), Margin = new Thickness(6, 0, 6, 0), Child = dots[i] };
            boxRow.Children.Add(boxes[i]);
        }
        panel.Children.Add(boxRow);

        var error = new TextBlock { Text = "PIN noto'g'ri", FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Hidden };
        error.SetResourceReference(TextBlock.ForegroundProperty, "Bad");
        panel.Children.Add(error);

        void Render()
        {
            for (int i = 0; i < 4; i++)
            {
                bool filled = i < _pin.Length, active = i == _pin.Length;
                dots[i].Text = filled ? "•" : string.Empty;
                boxes[i].BorderThickness = new Thickness(filled || active ? 1.5 : 1);
                boxes[i].SetResourceReference(Border.BorderBrushProperty, filled || active ? "Accent" : "LineStrong");
                if (active) boxes[i].SetResourceReference(Border.BackgroundProperty, "AccentSoft");
                else boxes[i].SetResourceReference(Border.BackgroundProperty, "Surface");
            }
        }

        void Submit()
        {
            if (_pin.Length != 4) return;
            if (AdminGate.Current.TryUnlock(_pin))
            {
                DialogResult = true;
            }
            else
            {
                error.Visibility = Visibility.Visible;
                _pin = string.Empty;
                Render();
            }
        }

        void Digit(char c)
        {
            if (_pin.Length >= 4) return;
            _pin += c;
            error.Visibility = Visibility.Hidden;
            Render();
            if (_pin.Length == 4) Submit(); // 4-raqam kiritilishi bilan tekshiriladi
        }

        void Back()
        {
            if (_pin.Length == 0) return;
            _pin = _pin[..^1];
            Render();
        }

        // Raqamli klaviatura 3×4
        var pad = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(-4, 8, -4, 0) };
        foreach (var k in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "Esc", "0", "⌫" })
        {
            bool special = k is "Esc" or "⌫";
            var b = new Button
            {
                Content = k, Height = 52, Margin = new Thickness(4), Padding = new Thickness(0), FontSize = special ? 15 : 20,
                Focusable = false, Style = (Style)FindResource("BtnBase"),
                ToolTip = k == "Esc" ? "Bekor qilish" : k == "⌫" ? "Oxirgi raqamni o'chirish" : null
            };
            b.SetResourceReference(Control.BorderBrushProperty, "Line");
            if (special) b.SetResourceReference(Control.BackgroundProperty, "Surface2");
            if (k == "Esc") b.Click += (_, _) => DialogResult = false;
            else if (k == "⌫") b.Click += (_, _) => Back();
            else { char c = k[0]; b.Click += (_, _) => Digit(c); }
            pad.Children.Add(b);
        }
        panel.Children.Add(pad);

        var note = new TextBlock
        {
            Text = "PIN to'g'ri kiritilgach, yashirin raqamlar qo'lda qulflaguncha ochiq turadi",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 14, 0, 0)
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Ink3");
        panel.Children.Add(note);

        // Klaviaturadan kiritish
        PreviewTextInput += (_, e) =>
        {
            foreach (var c in e.Text) if (char.IsDigit(c)) Digit(c);
            e.Handled = true;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            else if (e.Key == Key.Back) { Back(); e.Handled = true; }
        };
        Loaded += (_, _) =>
        {
            // Oyna egasini to'liq qoplaydi (orqa fon xiralashadi, karta markazda)
            if (Owner != null)
            {
                SizeToContent = SizeToContent.Manual;
                if (Owner.WindowState == WindowState.Maximized) { Left = Owner.Left; Top = Owner.Top; WindowState = WindowState.Maximized; }
                else if (Owner.ActualWidth > ActualWidth && Owner.ActualHeight > ActualHeight)
                {
                    Left = Owner.Left; Top = Owner.Top; Width = Owner.ActualWidth; Height = Owner.ActualHeight;
                }
            }
            Focus();
        };
        Focusable = true;
        Render();
    }
}

/// <summary>⚙ → "Admin PIN · yashirin ma'lumotlar": himoyani yoqish/o'chirish va PIN o'rnatish (maket 6.7).</summary>
public sealed class AdminPinSettingsDialog : Window
{
    private static PasswordBox PinBox(Window w)
    {
        var pb = new PasswordBox
        {
            MaxLength = 4, HorizontalContentAlignment = HorizontalAlignment.Center, PasswordChar = '●',
            Style = (Style)w.FindResource("FieldPassword")
        };
        // Faqat raqamlar
        pb.PreviewTextInput += (_, e) => { if (!e.Text.All(char.IsDigit)) e.Handled = true; };
        DataObject.AddPastingHandler(pb, (_, e) => e.CancelCommand());
        return pb;
    }

    public AdminPinSettingsDialog()
    {
        var card = ModalKit.Setup(this, "Admin PIN", 520);
        var gate = AdminGate.Current;
        var outer = new StackPanel(); card.Child = outer;

        // Sarlavha
        var head = new Border { Padding = new Thickness(24, 20, 24, 14), BorderThickness = new Thickness(0, 0, 0, 1) };
        ModalKit.Res(head, Border.BorderBrushProperty, "LineSoft");
        var headPanel = new StackPanel();
        headPanel.Children.Add(ModalKit.Text("Admin PIN · yashirin ma'lumotlar", 18, "Ink", FontWeights.Bold));
        var headSub = ModalKit.Text("Dasturni ochish paroli emas — faqat maxfiy raqamlarni kassirdan yashiradi", 13, "Ink3");
        headSub.Margin = new Thickness(0, 3, 0, 0); headPanel.Children.Add(headSub);
        head.Child = headPanel; outer.Children.Add(head);

        var panel = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
        outer.Children.Add(panel);

        // Yoqish/o'chirish
        var toggleCard = new Border { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12), Padding = new Thickness(14) };
        ModalKit.Res(toggleCard, Border.BorderBrushProperty, "Accent");
        ModalKit.Res(toggleCard, Border.BackgroundProperty, "AccentSoft");
        var tg = new DockPanel();
        var toggle = new CheckBox { IsChecked = gate.IsEnabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0),
                                    Style = (Style)FindResource("Switch"), ToolTip = "Himoyani yoqish yoki o'chirish" };
        DockPanel.SetDock(toggle, Dock.Right); tg.Children.Add(toggle);
        var tgText = new StackPanel();
        tgText.Children.Add(ModalKit.Text("Yashirin ma'lumotlarni himoyalash", 14, "Ink", FontWeights.Bold));
        var stateText = ModalKit.Text("", 13, "Ink2");
        stateText.Margin = new Thickness(0, 2, 0, 0); tgText.Children.Add(stateText);
        tg.Children.Add(tgText);
        toggleCard.Child = tg;
        panel.Children.Add(toggleCard);

        var chipsLabel = ModalKit.Label(this, "Yoqilganda PIN so'raladi:"); chipsLabel.Margin = new Thickness(0, 16, 0, 6);
        panel.Children.Add(chipsLabel);
        var chips = new WrapPanel();
        foreach (var t in new[] { "Foyda (hisobotlar)", "Tan narx (ombor)", "Karta foizi", "Excel hisobotlar", "Bazani tiklash" })
        {
            var chip = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 6, 6),
                                    Child = ModalKit.Text(t, 13, "Ink") };
            ModalKit.Res(chip, Border.BackgroundProperty, "Surface2");
            chips.Children.Add(chip);
        }
        panel.Children.Add(chips);

        // PIN maydonlari
        var pinArea = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        pinArea.ColumnDefinitions.Add(new ColumnDefinition());
        pinArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        pinArea.ColumnDefinitions.Add(new ColumnDefinition());
        var p1 = PinBox(this);
        var p2 = PinBox(this);
        var c1 = new StackPanel();
        c1.Children.Add(ModalKit.Label(this, gate.IsEnabled ? "Yangi PIN (4 raqam)" : "PIN (4 raqam)"));
        c1.Children.Add(p1);
        var c2 = new StackPanel();
        c2.Children.Add(ModalKit.Label(this, "Takrorlang"));
        c2.Children.Add(p2);
        Grid.SetColumn(c2, 2);
        pinArea.Children.Add(c1);
        pinArea.Children.Add(c2);
        panel.Children.Add(pinArea);

        var hint = ModalKit.Text("", 12, "Ink3");
        hint.Margin = new Thickness(0, 12, 0, 0); hint.LineHeight = 18;
        panel.Children.Add(hint);
        var error = ModalKit.Text("", 13, "Bad", FontWeights.SemiBold);
        error.Margin = new Thickness(0, 6, 0, 0); error.Visibility = Visibility.Collapsed;
        panel.Children.Add(error);

        var buttons = new Grid { Margin = new Thickness(24, 0, 24, 20) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var cancel = ModalKit.Button(this, "Bekor", "BtnSecondary");
        cancel.IsCancel = true;
        var save = ModalKit.Button(this, "Saqlash", "BtnPrimary");
        Grid.SetColumn(save, 2);
        buttons.Children.Add(cancel); buttons.Children.Add(save);
        outer.Children.Add(buttons);

        void Refresh()
        {
            bool on = toggle.IsChecked == true;
            stateText.Text = on ? "Yoqilgan — maxfiy raqamlar PIN bilan ochiladi" : "O'chiq — hamma narsa ochiq ko'rinadi";
            pinArea.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = on
                ? (gate.IsEnabled ? "PIN-ni almashtirmoqchi bo'lsangiz yangisini kiriting, aks holda bo'sh qoldiring." : "Birinchi yoqishda PIN o'rnatiladi.")
                : (gate.IsEnabled ? "Saqlasangiz himoya o'chiriladi va PIN o'chib ketadi." : "Himoya o'chiq.");
            save.Content = on && !gate.IsEnabled ? "Yoqish va saqlash" : "Saqlash";
            error.Visibility = Visibility.Collapsed;
        }
        toggle.Checked += (_, _) => Refresh();
        toggle.Unchecked += (_, _) => Refresh();
        Refresh();

        void Fail(string msg) { error.Text = msg; error.Visibility = Visibility.Visible; }

        save.Click += (_, _) =>
        {
            bool on = toggle.IsChecked == true;
            if (!on)
            {
                if (gate.IsEnabled) gate.Disable();
                DialogResult = true;
                return;
            }
            bool changing = p1.Password.Length > 0 || p2.Password.Length > 0;
            if (gate.IsEnabled && !changing) { DialogResult = true; return; }
            if (p1.Password.Length != 4) { Fail("PIN 4 ta raqamdan iborat bo'lishi kerak"); p1.Focus(); return; }
            if (p1.Password != p2.Password) { Fail("PIN-lar bir xil emas"); p2.Clear(); p2.Focus(); return; }
            gate.SetPin(p1.Password);
            DialogResult = true;
        };
        cancel.Click += (_, _) => DialogResult = false;
        p1.PasswordChanged += (_, _) => { if (p1.Password.Length == 4) p2.Focus(); };
    }
}
