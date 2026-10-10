using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Returns;
using PosElectro.Desktop.Sync;

namespace PosElectro.Desktop.Views;

public sealed class ReturnDialog : Window
{
    readonly ReturnStore store;
    readonly DatabaseContext db;
    readonly ReturnQuote quote;
    readonly List<(ReturnLine Line, TextBox Quantity, ComboBox Warehouse, CheckBox Damaged)> rows = new();
    readonly TextBox reason = new(), cash = new(), card = new(), fee = new() { Text = "0" };
    readonly TextBlock total = new() { Margin = new Thickness(0, 12, 0, 12) };
    readonly Button confirm = new() { Content = "Qaytarishni tasdiqlash", Height = 40 };
    string requestId = Guid.NewGuid().ToString();
    ReturnRequest? submitted;
    bool saved;

    public ReturnDialog(DatabaseContext database, Sale sale)
    {
        db = database; store = new ReturnStore(db.DatabaseFilePath); store.Install(); quote = store.Quote(sale.Guid);
        Title = "Mahsulot qaytarish — " + sale.ReceiptNumber;
        Width = 760; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20) }; Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "Asl narx bo‘yicha qaytarish. Nuqsonli tovar sotiladigan qoldiqqa kirmaydi.", TextWrapping = TextWrapping.Wrap });
        var warehouses = db.GetWarehouses();
        foreach (var line in quote.Lines)
        {
            panel.Children.Add(new TextBlock { Text = $"{line.ProductName} • Sotilgan: {line.Sold} • Qaytgan: {line.Returned} • Qolgan: {line.Sold-line.Returned}", Margin = new Thickness(0, 12, 0, 4), TextWrapping = TextWrapping.Wrap });
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var qty = new TextBox { Text = "0", Width = 100, Margin = new Thickness(0, 0, 12, 0) };
            var wh = new ComboBox { ItemsSource = warehouses, DisplayMemberPath = "Name", SelectedValuePath = "Guid", SelectedValue = line.WarehouseGuid, Width = 250 };
            if (wh.SelectedIndex < 0) wh.SelectedIndex = 0;
            var damaged = new CheckBox { Content = "Nuqsonli", Margin = new Thickness(12, 0, 0, 0) };
            row.Children.Add(qty); row.Children.Add(wh); row.Children.Add(damaged); panel.Children.Add(row);
            rows.Add((line, qty, wh, damaged)); qty.TextChanged += (_, _) => Preview();
        }
        void Field(string label, TextBox field) { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 4) }); panel.Children.Add(field); }
        Field("Qaytarish sababi", reason); panel.Children.Add(total);
        Field("Naqd qaytarish (so‘m)", cash); Field("Karta qaytarish (so‘m)", card);
        Field("Haqiqatan qaytarilgan karta xarajati (odatda 0)", fee);
        panel.Children.Add(new TextBlock { Text = "Tasdiqlash bank orqali avtomatik pul o‘tkazmaydi.", Margin = new Thickness(0, 12, 0, 12) });
        panel.Children.Add(confirm); confirm.Click += Confirm; Preview();
        var draft=store.DesktopDraft(sale.Guid);
        if(draft.Request != null) {
            submitted=draft.Request; requestId=submitted.RequestGuid;
            reason.Text=submitted.Reason; fee.Text=submitted.FeeReversal.ToString(CultureInfo.InvariantCulture);
            foreach(var row in rows) {
                var selected=submitted.Items.Find(i=>i.SaleItemGuid==row.Line.Guid);
                if(selected!=null) { row.Quantity.Text=selected.Quantity.ToString(CultureInfo.InvariantCulture); row.Warehouse.SelectedValue=selected.WarehouseGuid; row.Damaged.IsChecked=!selected.Resellable; }
                row.Quantity.IsEnabled=false; row.Warehouse.IsEnabled=false; row.Damaged.IsEnabled=false;
            }
            cash.Text=submitted.CashRefund.ToString(CultureInfo.InvariantCulture); card.Text=submitted.CardRefund.ToString(CultureInfo.InvariantCulture);
            reason.IsEnabled=cash.IsEnabled=card.IsEnabled=fee.IsEnabled=false; confirm.Content="Oldingi so‘rov natijasini tekshirish";
            if(draft.Result != null) Loaded += (_,_) => {
                saved=true; MessageBox.Show(this,"Avval saqlangan: RT-"+draft.Result.Guid.Replace("-", "").ToUpperInvariant(),"Qaytarish");
                store.FinishDesktopDraft(requestId,null); DialogResult=true;
            };
        }
    }
    static decimal Number(string text) => decimal.Parse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    void Preview()
    {
        try {
            decimal sum = 0;
            foreach (var row in rows) { var qty = Number(row.Quantity.Text); if (qty == 0) continue;
                var l = row.Line; sum += ReturnAccounting.Calculate(l.Sold, l.Revenue, l.Cost,
                    new(l.Returned, l.Refunded, l.CostBasis), qty, row.Damaged.IsChecked != true).Refund; }
            if (quote.DebtAccountGuid != null) {
                var sumMinor = checked((long)Math.Round(sum * 100, MidpointRounding.AwayFromZero));
                var split = DebtAccounting.SplitReturn(sumMinor, quote.AccountBalanceMinor);
                var offset = split.DebtOffsetMinor / 100.0m;
                var refund = split.RefundMinor / 100.0m;
                total.Text = $"Qaytariladigan tovar: {sum:N2} so‘m | Qarzdan chegiriladi: {offset:N2} so‘m | To‘lanadi: {refund:N2} so‘m";
                cash.Text = refund.ToString(CultureInfo.InvariantCulture); card.Text = "0";
            } else {
                total.Text = $"Qaytariladigan summa: {sum:N2} so‘m";
                cash.Text = sum.ToString(CultureInfo.InvariantCulture); card.Text = "0";
            }
        } catch (Exception e) { total.Text = e.Message; }
    }
    void Confirm(object sender, RoutedEventArgs args)
    {
        if (saved || !confirm.IsEnabled) return;
        confirm.IsEnabled = false;
        try {
            submitted ??= new ReturnRequest(requestId, quote.SaleGuid, reason.Text.Trim(), Number(cash.Text), Number(card.Text), Number(fee.Text),
                rows.Where(r => Number(r.Quantity.Text) != 0).Select(r => new ReturnSelection(r.Line.Guid, Number(r.Quantity.Text),
                    (string?)r.Warehouse.SelectedValue ?? "", r.Damaged.IsChecked != true)).ToList());
            var authority=new WifiSyncStore(db.DatabaseFilePath).ServerId;
            store.SaveDesktopDraft(submitted,authority);
            var result = store.Commit(submitted, "desktop-local", authority);
            store.FinishDesktopDraft(requestId,result);
            saved = true;
            try { db.RaiseProductsChanged(); } catch { }
            var msg = result.DebtOffset > 0
                ? $"Qaytarish saqlandi: RT-{result.Guid.Replace("-", "").ToUpperInvariant()}\nQarzdan chegirildi: {result.DebtOffset:N2} so‘m\nXaridorga qaytarildi: {result.Refund:N2} so‘m"
                : $"Qaytarish saqlandi: RT-{result.Guid.Replace("-", "").ToUpperInvariant()}\nSumma: {result.Refund:N2} so‘m";
            MessageBox.Show(this, msg, "Qaytarish");
            store.FinishDesktopDraft(requestId,null);
            DialogResult = true;
        } catch (ArgumentException e) {
            store.FinishDesktopDraft(requestId,null); submitted = null; MessageBox.Show(this, e.Message, "Ma’lumotni tekshiring");
        } catch (Exception e) {
            MessageBox.Show(this, "Natijani tekshirish uchun aynan shu so‘rovni qayta tasdiqlang. " + e.Message, "Qaytarish");
        } finally { if (!saved) confirm.IsEnabled = true; }
    }
}
