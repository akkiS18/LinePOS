using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PosElectro.Desktop.Views;

/// <summary>
/// Bazani zaxiradan tiklashni tasdiqlash: nima yo'qolishini ko'rsatadi va "TIKLASH" deb yozishni talab qiladi.
/// Oddiy Ha/Yo'q oynasini tasodifan Enter bilan tasdiqlab yuborish xavfini yo'qotadi.
/// Ko'rinishi maket 6.4 bo'yicha (ModalKit); tasdiqlash sharti o'zgarmagan.
/// </summary>
public sealed class RestoreConfirmDialog : Window
{
    private const string Keyword = "TIKLASH";

    public RestoreConfirmDialog(string backupPath, DateTime? backupLastSale, int lostSales, double lostAmount)
    {
        var card = ModalKit.Setup(this, "Bazani zaxiradan tiklash", 540);
        card.ClipToBounds = true;
        var outer = new StackPanel(); card.Child = outer;

        // Qizil chiziq + sarlavha
        card.BorderThickness = new Thickness(0, 4, 0, 0);
        ModalKit.Res(card, Border.BorderBrushProperty, "Bad");

        var head = new DockPanel { Margin = new Thickness(24, 18, 24, 16) };
        var badge = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0),
                                 Child = ModalKit.Icon("M12 3l9 16H3z M12 10v4 M12 17v0.5", "Bad", 22) };
        ModalKit.Res(badge, Border.BackgroundProperty, "BadSoft");
        DockPanel.SetDock(badge, Dock.Left); head.Children.Add(badge);
        var headText = new StackPanel();
        headText.Children.Add(ModalKit.Text("Bazani zaxiradan tiklash", 18, "Ink", FontWeights.Bold));
        var lead = ModalKit.Text("Hozirgi barcha ma'lumotlar tanlangan zaxira holatiga qaytariladi.", 14, "Ink2");
        lead.Margin = new Thickness(0, 4, 0, 0); lead.LineHeight = 20; headText.Children.Add(lead);
        head.Children.Add(headText);
        outer.Children.Add(head);

        var panel = new StackPanel { Margin = new Thickness(24, 0, 24, 0) };
        outer.Children.Add(panel);

        // Zaxira fayli haqida
        var fi = new FileInfo(backupPath);
        var info = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 12) };
        ModalKit.Res(info, Border.BorderBrushProperty, "LineStrong");
        var infoRow = new DockPanel();
        var dbIcon = ModalKit.Icon("M4 6a8 3 0 1 0 16 0a8 3 0 1 0-16 0 M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6 M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3", "AccentInk", 22);
        dbIcon.Margin = new Thickness(0, 0, 12, 0); dbIcon.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(dbIcon, Dock.Left); infoRow.Children.Add(dbIcon);
        var infoStack = new StackPanel();
        var fileName = ModalKit.Text(fi.Name, 14, "Ink", FontWeights.SemiBold, wrap: false);
        fileName.ToolTip = backupPath; infoStack.Children.Add(fileName);
        var fileMeta = ModalKit.Text(
            $"Fayl sanasi: {fi.LastWriteTime:dd.MM.yyyy HH:mm} · Zaxiradagi oxirgi savdo: " +
            (backupLastSale.HasValue ? backupLastSale.Value.ToString("dd.MM.yyyy HH:mm") : "yo'q"), 12, "Ink3");
        fileMeta.Margin = new Thickness(0, 2, 0, 0); infoStack.Children.Add(fileMeta);
        infoRow.Children.Add(infoStack);
        info.Child = infoRow;
        panel.Children.Add(info);

        // Nima yo'qoladi
        bool losing = lostSales > 0;
        var loss = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 12, 0, 0) };
        ModalKit.Res(loss, Border.BackgroundProperty, losing ? "BadSoft" : "WarnSoft");
        ModalKit.Res(loss, Border.BorderBrushProperty, losing ? "BadLine" : "WarnLine");
        var lossText = ModalKit.Text(
            losing
                ? $"Yo'qoladi: zaxiradan keyin qilingan {lostSales} ta savdo ({lostAmount:N0} so'm). Shuningdek, shu vaqtdan keyingi ombor, qarz va boshqa o'zgarishlar ham yo'qoladi."
                : "Zaxiradan keyin yangi savdo qilinmagan. Lekin shu vaqtdan keyingi ombor va qarz o'zgarishlari yo'qolishi mumkin.",
            13, losing ? "Bad" : "Warn", FontWeights.SemiBold);
        lossText.LineHeight = 20;
        loss.Child = lossText;
        panel.Children.Add(loss);

        // Avtomatik zaxira haqida
        var safe = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 12, 0, 0) };
        ModalKit.Res(safe, Border.BackgroundProperty, "OkSoft");
        var safeRow = new DockPanel();
        var check = ModalKit.Icon("M5 12l5 5l9-10", "Ok", 16);
        check.Margin = new Thickness(0, 0, 10, 0); check.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(check, Dock.Left); safeRow.Children.Add(check);
        safeRow.Children.Add(ModalKit.Text("Tiklashdan oldin hozirgi baza avtomatik zaxiralanadi", 13, "Ok"));
        safe.Child = safeRow;
        panel.Children.Add(safe);

        var label = ModalKit.Label(this, $"Tasdiqlash uchun {Keyword} deb yozing");
        label.Margin = new Thickness(0, 12, 0, 6);
        panel.Children.Add(label);
        var input = new TextBox { Style = (Style)FindResource("FieldBox"), FontFamily = new System.Windows.Media.FontFamily("Consolas"), Height = 46 };
        panel.Children.Add(input);

        var buttons = new Grid { Margin = new Thickness(24, 18, 24, 20) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());

        var cancel = ModalKit.Button(this, "Bekor", "BtnSecondary");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => { DialogResult = false; };
        buttons.Children.Add(cancel);

        var confirm = ModalKit.Button(this, "Tiklash", "BtnDanger");
        confirm.IsEnabled = false;
        Grid.SetColumn(confirm, 2);
        confirm.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(confirm);
        outer.Children.Add(buttons);

        input.TextChanged += (_, _) =>
            confirm.IsEnabled = string.Equals(input.Text.Trim(), Keyword, StringComparison.OrdinalIgnoreCase);

        Loaded += (_, _) => input.Focus();
    }
}
