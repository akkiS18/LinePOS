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
        Title="Qaytarishni bekor qilish"; Width=560; Height=340; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var bgDark = (System.Windows.Media.Brush?)Application.Current?.Resources["BgDark"] ?? System.Windows.Media.Brushes.Navy;
        var textPrimary = (System.Windows.Media.Brush?)Application.Current?.Resources["TextPrimary"] ?? System.Windows.Media.Brushes.White;
        var textSecondary = (System.Windows.Media.Brush?)Application.Current?.Resources["TextSecondary"] ?? System.Windows.Media.Brushes.LightGray;
        var btnStyle = (Style?)Application.Current?.Resources["ModernButton"];
        var txtStyle = (Style?)Application.Current?.Resources["ModernTextBox"];
        var dangerBtnStyle = (Style?)Application.Current?.Resources["DangerButton"];

        Background = bgDark;
        Foreground = textPrimary;

        PreviewKeyDown += (s, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) {
                Close();
                e.Handled = true;
            }
        };

        var panel=new StackPanel { Margin=new Thickness(24) }; Content=panel;
        panel.Children.Add(new TextBlock { Text=receipt.ReceiptNumber, TextWrapping=TextWrapping.Wrap, FontWeight=FontWeights.Bold, FontSize=16, Foreground=textPrimary });
        panel.Children.Add(new TextBlock { Text="Asl yozuv o‘chirilmaydi. Pul va ombor ta’siri qarama-qarshi yozuv bilan tiklanadi. Bankdan avtomatik pul olinmaydi.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,10,0,14), Foreground=textSecondary, FontSize=12 });
        panel.Children.Add(new TextBlock { Text="Bekor qilish sababi:", FontWeight=FontWeights.SemiBold, Foreground=textSecondary, Margin=new Thickness(0,0,0,6) });
        var reason=new TextBox { Height=40, VerticalContentAlignment=VerticalAlignment.Center, Padding=new Thickness(10,0,10,0) };
        if (txtStyle != null) reason.Style = txtStyle;
        panel.Children.Add(reason);

        var btnGrid = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

        var btnClose = new Button { Content = "Yopish (Esc)", Height = 42, IsCancel = true };
        if (btnStyle != null) btnClose.Style = btnStyle;
        btnClose.Click += (_, _) => Close();
        Grid.SetColumn(btnClose, 0);
        btnGrid.Children.Add(btnClose);

        var confirm=new Button { Content="Bekor qilishni tasdiqlash (Enter)", Height=42, IsDefault=true };
        if (dangerBtnStyle != null) confirm.Style = dangerBtnStyle;
        else if (btnStyle != null) confirm.Style = btnStyle;
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
