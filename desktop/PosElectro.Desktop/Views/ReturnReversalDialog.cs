using System;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Returns;
using PosElectro.Desktop.Sync;

namespace PosElectro.Desktop.Views;
public sealed class ReturnReversalDialog : Window
{
    public ReturnReversalDialog(DatabaseContext db, Sale receipt)
    {
        // ---- Maket 4.7: ko'rinish. Bekor qilish mantig'i (pastda) o'zgarmagan. ----
        var box = ModalKit.Setup(this, "Qaytarishni bekor qilish", 500);
        box.Padding = new Thickness(24);

        PreviewKeyDown += (s, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) {
                Close();
                e.Handled = true;
            }
        };

        var panel=new StackPanel(); box.Child=panel;

        var top = new DockPanel();
        var badge = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0),
                                 Child = ModalKit.Icon("M9 14L4 9l5-5 M4 9h11a5 5 0 0 1 0 10h-3", "Warn", 22) };
        ModalKit.Res(badge, Border.BackgroundProperty, "WarnSoft");
        DockPanel.SetDock(badge, Dock.Left); top.Children.Add(badge);
        var topText = new StackPanel();
        topText.Children.Add(ModalKit.Text("Qaytarishni bekor qilasizmi?", 18, "Ink", FontWeights.Bold));
        var lead = ModalKit.Text("Asl yozuv o‘chirilmaydi. Pul va ombor ta’siri qarama-qarshi yozuv bilan tiklanadi. Bankdan avtomatik pul olinmaydi.", 14, "Ink2");
        lead.Margin = new Thickness(0, 4, 0, 0); lead.LineHeight = 20; topText.Children.Add(lead);
        top.Children.Add(topText); panel.Children.Add(top);

        // Qisqa ma'lumot: qaysi qaytarish bekor bo'ladi
        var info = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 14, 0, 0) };
        ModalKit.Res(info, Border.BorderBrushProperty, "Line");
        var infoPanel = new StackPanel(); info.Child = infoPanel;
        void InfoRow(string k, string v, bool last = false)
        {
            var r = new Border { Padding = new Thickness(14, 10, 14, 10), BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1) };
            ModalKit.Res(r, Border.BorderBrushProperty, "LineSoft");
            var d = new DockPanel();
            var key = ModalKit.Text(k, 14, "Ink2"); key.Margin = new Thickness(0, 0, 12, 0); DockPanel.SetDock(key, Dock.Left); d.Children.Add(key);
            var val = ModalKit.Text(v, 14, "Ink", FontWeights.SemiBold); val.TextAlignment = TextAlignment.Right; d.Children.Add(val);
            r.Child = d; infoPanel.Children.Add(r);
        }
        InfoRow("Qaytarish", receipt.ReceiptNumber);
        InfoRow("Tovar", string.Join("\n", System.Linq.Enumerable.Select(receipt.Items, i => $"{i.ProductName} × {Math.Abs(i.Quantity):0.###}")));
        InfoRow("Summa", $"{Math.Abs(receipt.TotalAmount):N0} so‘m", last: true);
        panel.Children.Add(info);

        var reasonLabel = ModalKit.Label(this, "Bekor qilish sababi"); reasonLabel.Margin = new Thickness(0, 14, 0, 6);
        panel.Children.Add(reasonLabel);
        var reason=new TextBox { Style = (Style)FindResource("FieldBox") };
        panel.Children.Add(reason);
        Loaded += (_, _) => reason.Focus();

        var btnGrid = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition());
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var btnClose = ModalKit.Button(this, "Yo‘q, qoldirish", "BtnSecondary"); btnClose.IsCancel = true;
        btnClose.Click += (_, _) => Close();
        btnGrid.Children.Add(btnClose);

        var confirm = ModalKit.Button(this, "Bekor qilish", "BtnPrimary"); confirm.IsDefault = true;
        ModalKit.Res(confirm, Control.BackgroundProperty, "WarnSolid");
        Grid.SetColumn(confirm, 2);
        btnGrid.Children.Add(confirm);
        panel.Children.Add(btnGrid);

        ReturnReversalRequest? submitted=null; var id=Guid.NewGuid().ToString();
        confirm.Click += (_,_) => {
            confirm.IsEnabled=false;
            try {
                submitted ??=new ReturnReversalRequest(id,receipt.Guid,reason.Text.Trim());
                var result=new ReturnStore(db.DatabaseFilePath).Reverse(submitted,"desktop-local",new WifiSyncStore(db.DatabaseFilePath).ServerId);
                try { db.RaiseProductsChanged(); } catch { }
                MessageBox.Show(this,"Saqlandi: RV-"+result.Guid.Replace("-", "").ToUpperInvariant(),"Bekor qilindi"); DialogResult=true;
            } catch(ArgumentException e) { submitted=null; MessageBox.Show(this,e.Message,"Tekshiring"); confirm.IsEnabled=true; }
              catch(Exception e) { MessageBox.Show(this,e.Message,"Shu so‘rovni qayta tekshiring"); confirm.IsEnabled=true; }
        };
    }
}
