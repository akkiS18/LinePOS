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
        Title="Qaytarishni bekor qilish"; Width=560; Height=320; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel { Margin=new Thickness(20) }; Content=panel;
        panel.Children.Add(new TextBlock { Text=receipt.ReceiptNumber, TextWrapping=TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text="Asl yozuv o‘chirilmaydi. Pul va ombor ta’siri qarama-qarshi yozuv bilan tiklanadi. Bankdan avtomatik pul olinmaydi.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,12,0,12) });
        panel.Children.Add(new TextBlock { Text="Bekor qilish sababi" });
        var reason=new TextBox(); panel.Children.Add(reason);
        var confirm=new Button { Content="Bekor qilishni tasdiqlash", Height=40, Margin=new Thickness(0,12,0,0) }; panel.Children.Add(confirm);
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
