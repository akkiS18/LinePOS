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
    ReturnQuote quote;
    bool quoteReady = true;
    readonly Dictionary<string, TextBlock> lineLabels = new();
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
        Width = 780; Height = 680; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var bgDark = (System.Windows.Media.Brush?)Application.Current?.Resources["BgDark"] ?? System.Windows.Media.Brushes.Navy;
        var cardDark = (System.Windows.Media.Brush?)Application.Current?.Resources["CardDark"] ?? System.Windows.Media.Brushes.DarkSlateGray;
        var textPrimary = (System.Windows.Media.Brush?)Application.Current?.Resources["TextPrimary"] ?? System.Windows.Media.Brushes.White;
        var textSecondary = (System.Windows.Media.Brush?)Application.Current?.Resources["TextSecondary"] ?? System.Windows.Media.Brushes.LightGray;
        var btnStyle = (Style?)Application.Current?.Resources["ModernButton"];

        Background = bgDark;
        Foreground = textPrimary;

        var panel = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "Asl narx bo‘yicha qaytarish. Nuqsonli tovar sotiladigan qoldiqqa kirmaydi.", Foreground = textSecondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var warehouses = db.GetWarehouses();
        foreach (var line in quote.Lines)
        {
            var label = new TextBlock { Text = LineText(line), Foreground = textPrimary, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap };
            lineLabels.Add(line.Guid, label); panel.Children.Add(label);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var qty = new TextBox { Text = "0", Width = 90, Height = 34, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 12, 0) };
            var wh = new ComboBox { ItemsSource = warehouses, DisplayMemberPath = "Name", SelectedValuePath = "Guid", SelectedValue = line.WarehouseGuid, Width = 260, Height = 34, VerticalContentAlignment = VerticalAlignment.Center };
            if (wh.SelectedIndex < 0) wh.SelectedIndex = 0;
            var damaged = new CheckBox { Content = "Nuqsonli (brak)", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            row.Children.Add(qty); row.Children.Add(wh); row.Children.Add(damaged); panel.Children.Add(row);
            rows.Add((line, qty, wh, damaged)); qty.TextChanged += (_, _) => Preview();
        }
        void Field(string label, TextBox field) {
            panel.Children.Add(new TextBlock { Text = label, Foreground = textSecondary, Margin = new Thickness(0, 12, 0, 4) });
            field.Height = 34; field.VerticalContentAlignment = VerticalAlignment.Center; field.Padding = new Thickness(6, 0, 6, 0);
            panel.Children.Add(field);
        }
        Field("Qaytarish sababi", reason);
        total.Foreground = (System.Windows.Media.Brush?)Application.Current?.Resources["AccentTealLight"] ?? System.Windows.Media.Brushes.Cyan;
        total.FontWeight = FontWeights.Bold;
        panel.Children.Add(total);
        Field("Naqd qaytarish (so‘m)", cash); Field("Karta qaytarish (so‘m)", card);
        Field("Haqiqatan qaytarilgan karta xarajati (odatda 0)", fee);
        panel.Children.Add(new TextBlock { Text = "Tasdiqlash bank orqali avtomatik pul o‘tkazmaydi.", Foreground = textSecondary, Margin = new Thickness(0, 14, 0, 14) });
        if (btnStyle != null) confirm.Style = btnStyle;
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
            // Replays go directly to the authority's idempotency lookup, even if
            // the current balance or returned quantity changed since submission.
            total.Text = "Oldingi so‘rov saqlangan. Natijasi tekshirilmaguncha yangi qaytarish yaratilmaydi.";
        }
    }
    static decimal Number(string text)
    {
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) || n < 0)
            throw new ArgumentException("Miqdor va pul maydonlariga musbat son yoki 0 kiriting.");
        return n;
    }
    static string LineText(ReturnLine line) => $"{line.ProductName} • Sotilgan: {line.Sold} • Qaytgan: {line.Returned} • Qolgan: {line.Sold-line.Returned}";
    void SetEditable(bool editable)
    {
        foreach (var row in rows) row.Quantity.IsEnabled = row.Warehouse.IsEnabled = row.Damaged.IsEnabled = editable;
        reason.IsEnabled = cash.IsEnabled = card.IsEnabled = fee.IsEnabled = editable;
    }
    void RefreshRejectedQuote()
    {
        quoteReady = false;
        var fresh = store.Quote(quote.SaleGuid);
        var updated = rows.Select(row => (Line: fresh.Lines.Single(l => l.Guid == row.Line.Guid),
            row.Quantity, row.Warehouse, row.Damaged)).ToList();
        quote = fresh; rows.Clear(); rows.AddRange(updated);
        foreach (var row in rows) lineLabels[row.Line.Guid].Text = LineText(row.Line);
        quoteReady = true;
        SetEditable(true); Preview();
    }
    void Preview()
    {
        if (submitted != null || !quoteReady) return;
        try {
            decimal sum = 0;
            foreach (var row in rows) { var qty = Number(row.Quantity.Text); if (qty == 0) continue;
                var l = row.Line; sum += ReturnAccounting.Calculate(l.Sold, l.Revenue, l.Cost,
                    new(l.Returned, l.Refunded, l.CostBasis), qty, row.Damaged.IsChecked != true).Refund; }
            if (sum == 0) {
                total.Text = "Qaytariladigan summa: 0 so‘m"; cash.Text = card.Text = "0";
            } else if (quote.DebtAccountGuid != null) {
                var sumMinor = checked((long)Math.Round(sum * 100, MidpointRounding.AwayFromZero));
                var split = DebtAccounting.SplitReturn(sumMinor, quote.AccountBalanceMinor);
                var offset = split.DebtOffsetMinor / 100.0m;
                var refund = split.RefundMinor / 100.0m;
                total.Text = $"Qaytariladigan tovar: {sum:N2} so‘m | Qarzdan chegiriladi: {offset:N2} so‘m | Xaridorga to‘lanadi: {refund:N2} so‘m";
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
            if (submitted == null)
            {
                if (!quoteReady) throw new InvalidOperationException("Ma’lumot yangilanmadi. Oynani qayta oching.");
                // Build before assigning: malformed input must not freeze a draft.
                var request = new ReturnRequest(requestId, quote.SaleGuid, reason.Text.Trim(), Number(cash.Text), Number(card.Text), Number(fee.Text),
                    rows.Where(r => Number(r.Quantity.Text) != 0).Select(r => new ReturnSelection(r.Line.Guid, Number(r.Quantity.Text),
                        (string?)r.Warehouse.SelectedValue ?? "", r.Damaged.IsChecked != true)).ToList());
                submitted = request;
            }
            SetEditable(false);
            confirm.Content = "Oldingi so‘rov natijasini tekshirish";
            var authority = new WifiSyncStore(db.DatabaseFilePath).ServerId;
            // No preview validation here: an existing request must reach replay
            // before current balance/stock validation for a new return.
            var result = store.CommitDesktop(submitted, authority);
            store.FinishDesktopDraft(requestId, result);
            saved = true;
            try { db.RaiseProductsChanged(); } catch { }
            var msg = result.DebtOffset > 0
                ? $"Qaytarish saqlandi: RT-{result.Guid.Replace("-", "").ToUpperInvariant()}\nQarzdan chegirildi: {result.DebtOffset:N2} so‘m\nXaridorga qaytarildi: {result.Refund:N2} so‘m"
                : $"Qaytarish saqlandi: RT-{result.Guid.Replace("-", "").ToUpperInvariant()}\nSumma: {result.Refund:N2} so‘m";
            MessageBox.Show(this, msg, "Qaytarish");
            store.FinishDesktopDraft(requestId, null);
            DialogResult = true;
        } catch (DesktopReturnRejectedException e) {
            // Only the store can prove rejection. A new ID is issued only after
            // the exact uncommitted draft has been removed transactionally.
            submitted = null; requestId = Guid.NewGuid().ToString();
            confirm.Content = "Qaytarishni tasdiqlash";
            try {
                RefreshRejectedQuote();
                MessageBox.Show(this, e.Message + "\n\nQaytarish saqlanmadi. Joriy hisob yangilandi; maydonlarni tekshirib, qayta tasdiqlang.", "Ma’lumotni tekshiring");
            } catch (Exception refreshError) {
                SetEditable(false);
                MessageBox.Show(this, e.Message + "\n\nQaytarish saqlanmadi, ammo ma’lumotni yangilab bo‘lmadi. Oynani yopib qayta oching. " + refreshError.Message, "Qaytarish");
            }
        } catch (ArgumentException e) when (submitted == null) {
            MessageBox.Show(this, e.Message, "Ma’lumotni tekshiring");
        } catch (Exception e) {
            // Unknown outcome or cleanup failure: retain the request/result.
            if (saved) {
                MessageBox.Show(this, "Qaytarish saqlangan. Oynani qayta ochib oldingi natijani tekshiring. " + e.Message, "Qaytarish");
                Close();
            } else {
                MessageBox.Show(this, "Natijani tekshirish uchun aynan shu so‘rovni qayta tasdiqlang. " + e.Message, "Qaytarish");
            }
        } finally { if (!saved) confirm.IsEnabled = submitted != null || quoteReady; }
    }
}
