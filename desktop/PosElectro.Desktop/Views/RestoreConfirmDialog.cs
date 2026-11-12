using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PosElectro.Desktop.Views;

/// <summary>
/// Bazani zaxiradan tiklashni tasdiqlash: nima yo'qolishini ko'rsatadi va "TIKLASH" deb yozishni talab qiladi.
/// Oddiy Ha/Yo'q oynasini tasodifan Enter bilan tasdiqlab yuborish xavfini yo'qotadi.
/// </summary>
public sealed class RestoreConfirmDialog : Window
{
    private const string Keyword = "TIKLASH";

    public RestoreConfirmDialog(string backupPath, DateTime? backupLastSale, int lostSales, double lostAmount)
    {
        Brush R(string key, Brush fallback) => (Application.Current?.TryFindResource(key) as Brush) ?? fallback;
        var surface = R("Surface", Brushes.Black);
        var surface2 = R("Surface2", Brushes.Black);
        var line = R("Line", Brushes.Gray);
        var ink = R("Ink", Brushes.White);
        var ink2 = R("Ink2", Brushes.LightGray);
        var bad = R("Bad", Brushes.IndianRed);
        var badSoft = R("BadSoft", Brushes.Maroon);
        var badLine = R("BadLine", Brushes.DarkRed);
        var okSoft = R("OkSoft", Brushes.DarkGreen);
        var ok = R("Ok", Brushes.LightGreen);

        Title = "Bazani zaxiradan tiklash";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = surface;
        Foreground = ink;
        FontSize = 14;

        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = panel;

        panel.Children.Add(new TextBlock { Text = "Bazani zaxiradan tiklash", FontSize = 20, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = "Hozirgi ma'lumotlar tanlangan zaxira holatiga qaytariladi.",
            Foreground = ink2, Margin = new Thickness(0, 4, 0, 16), TextWrapping = TextWrapping.Wrap
        });

        // Zaxira fayli haqida
        var fi = new FileInfo(backupPath);
        var info = new Border
        {
            Background = surface2, BorderBrush = line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 10)
        };
        var infoStack = new StackPanel();
        infoStack.Children.Add(new TextBlock { Text = fi.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"Fayl sanasi: {fi.LastWriteTime:dd.MM.yyyy HH:mm}  ·  Zaxiradagi oxirgi savdo: " +
                   (backupLastSale.HasValue ? backupLastSale.Value.ToString("dd.MM.yyyy HH:mm") : "yo'q"),
            Foreground = ink2, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        info.Child = infoStack;
        panel.Children.Add(info);

        // Nima yo'qoladi
        var loss = new Border
        {
            Background = lostSales > 0 ? badSoft : okSoft,
            BorderBrush = lostSales > 0 ? badLine : ok,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 12, 0, 0)
        };
        loss.Child = new TextBlock
        {
            Text = lostSales > 0
                ? $"Zaxiradan keyin qilingan {lostSales} ta savdo ({lostAmount:N0} so'm) o'chib ketadi. Shuningdek, shu vaqtdan keyingi ombor, qarz va boshqa o'zgarishlar ham yo'qoladi."
                : "Zaxiradan keyin yangi savdo qilinmagan. Lekin shu vaqtdan keyingi ombor va qarz o'zgarishlari yo'qolishi mumkin.",
            Foreground = lostSales > 0 ? bad : ok, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(loss);

        panel.Children.Add(new TextBlock
        {
            Text = "Tiklashdan oldin hozirgi bazaning nusxasi avtomatik saqlanadi.",
            Foreground = ink2, FontSize = 12, Margin = new Thickness(0, 10, 0, 16), TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock { Text = $"Tasdiqlash uchun {Keyword} deb yozing:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var input = new TextBox { Height = 44, FontSize = 16, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0, 10, 0) };
        if (Application.Current?.TryFindResource("ModernTextBox") is Style ts) input.Style = ts;
        panel.Children.Add(input);

        var buttons = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        var cancel = new Button { Content = "Bekor qilish", Height = 46, IsCancel = true };
        if (Application.Current?.TryFindResource("ModernButton") is Style bs)
        {
            cancel.Style = bs;
            cancel.Background = line;
        }
        cancel.Click += (_, _) => { DialogResult = false; };
        buttons.Children.Add(cancel);

        var confirm = new Button { Content = "Tiklash", Height = 46, IsEnabled = false };
        if (Application.Current?.TryFindResource("DangerButton") is Style ds) confirm.Style = ds;
        Grid.SetColumn(confirm, 2);
        confirm.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);

        input.TextChanged += (_, _) =>
            confirm.IsEnabled = string.Equals(input.Text.Trim(), Keyword, StringComparison.OrdinalIgnoreCase);

        Loaded += (_, _) => input.Focus();
    }
}
