using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        // ---- Maket 4.5: ko'rinish. Maydonlar (rows, reason, cash, card, fee, total, confirm) va mantiq o'zgarmagan. ----
        var cardBox = ModalKit.Setup(this, "Tovarni qaytarish — " + sale.ReceiptNumber, 760);
        cardBox.MaxHeight = 680;

        PreviewKeyDown += (s, e) => {
            if (e.Key == System.Windows.Input.Key.Escape) {
                Close();
                e.Handled = true;
            }
        };

        var layout = new DockPanel(); cardBox.Child = layout;

        // Sarlavha
        var head = new Border { Padding = new Thickness(22, 16, 22, 16), BorderThickness = new Thickness(0, 0, 0, 1) };
        ModalKit.Res(head, Border.BorderBrushProperty, "LineSoft");
        var headPanel = new StackPanel();
        headPanel.Children.Add(ModalKit.Text("Qaytarish · chek " + sale.ReceiptNumber, 18, "Ink", FontWeights.Bold, wrap: false));
        var headSub = ModalKit.Text($"{sale.CreatedDateTime:dd.MM.yyyy · HH:mm} · {(sale.PaymentType == PaymentType.SPLIT ? "Aralash" : sale.PaymentTypeDisplay)}", 13, "Ink3");
        headSub.Margin = new Thickness(0, 2, 0, 0); headPanel.Children.Add(headSub);
        head.Child = headPanel; DockPanel.SetDock(head, Dock.Top); layout.Children.Add(head);

        // Pastki qism: jami va tugmalar
        var foot = new Border { Padding = new Thickness(22, 14, 22, 14), BorderThickness = new Thickness(0, 1, 0, 0) };
        ModalKit.Res(foot, Border.BorderBrushProperty, "LineSoft");
        var footPanel = new DockPanel();
        var btnCancel = ModalKit.Button(this, "Bekor", "BtnSecondary");
        btnCancel.IsCancel = true; btnCancel.Padding = new Thickness(18, 0, 18, 0); btnCancel.Margin = new Thickness(12, 0, 10, 0);
        btnCancel.VerticalAlignment = VerticalAlignment.Bottom;
        btnCancel.Click += (_, _) => Close();
        confirm.Content = "Qaytarishni tasdiqlash";
        confirm.Style = (Style)FindResource("BtnPrimary");
        confirm.Height = 48; confirm.Padding = new Thickness(22, 0, 22, 0);
        confirm.VerticalAlignment = VerticalAlignment.Bottom;
        confirm.IsDefault = true;
        confirm.Click += Confirm;
        DockPanel.SetDock(confirm, Dock.Right); footPanel.Children.Add(confirm);
        DockPanel.SetDock(btnCancel, Dock.Right); footPanel.Children.Add(btnCancel);
        total.Margin = new Thickness(0); total.FontSize = 15; total.FontWeight = FontWeights.Bold;
        total.TextWrapping = TextWrapping.Wrap; total.VerticalAlignment = VerticalAlignment.Center;
        ModalKit.Res(total, TextBlock.ForegroundProperty, "Ink");
        footPanel.Children.Add(total);
        foot.Child = footPanel; DockPanel.SetDock(foot, Dock.Bottom); layout.Children.Add(foot);

        var panel = new StackPanel { Margin = new Thickness(22, 0, 22, 16) };
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        Grid RowGrid()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            return g;
        }
        var cols = RowGrid(); cols.Height = 38;
        void Head(string t, int c, HorizontalAlignment a)
        {
            var h = ModalKit.Text(t, 12, "Ink3", FontWeights.SemiBold, wrap: false);
            h.VerticalAlignment = VerticalAlignment.Center; h.HorizontalAlignment = a;
            Grid.SetColumn(h, c); cols.Children.Add(h);
        }
        Head("TOVAR", 0, HorizontalAlignment.Left); Head("QAYTARILADI", 1, HorizontalAlignment.Center);
        Head("QAYSI OMBORGA", 2, HorizontalAlignment.Left); Head("BRAK", 3, HorizontalAlignment.Center);
        panel.Children.Add(cols);

        var warehouses = db.GetWarehouses();
        foreach (var line in quote.Lines)
        {
            var rowBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 8), MinHeight = 56 };
            ModalKit.Res(rowBorder, Border.BorderBrushProperty, "LineSoft");
            var row = RowGrid(); rowBorder.Child = row;

            var label = new TextBlock { Text = LineText(line), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
                                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            lineLabels.Add(line.Guid, label); row.Children.Add(label);

            // Miqdor: − [son] +
            var qty = new TextBox { Text = "0", Width = 54, Height = 40, BorderThickness = new Thickness(0), FontSize = 15, FontWeight = FontWeights.Bold,
                                    TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0),
                                    Background = Brushes.Transparent };
            ModalKit.Res(qty, Control.ForegroundProperty, "Ink");
            var stepper = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Center,
                                       VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            ModalKit.Res(stepper, Border.BorderBrushProperty, "LineStrong");
            var stepPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Button Step(string sign, decimal delta)
            {
                var b = new Button { Content = sign, Width = 40, Height = 40, FontSize = 18, Focusable = false, BorderThickness = new Thickness(0),
                                     Cursor = System.Windows.Input.Cursors.Hand, Padding = new Thickness(0) };
                ModalKit.Res(b, Control.BackgroundProperty, "Surface2");
                ModalKit.Res(b, Control.ForegroundProperty, "Ink");
                b.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding(nameof(IsEnabled)) { Source = qty });
                b.Click += (_, _) =>
                {
                    // Faqat qulaylik: maydondagi sonni 1 ga o'zgartiradi (0 dan kam emas, qolgandan ko'p emas)
                    decimal.TryParse(qty.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var cur);
                    var current = rows.Find(r => ReferenceEquals(r.Quantity, qty)).Line ?? line;
                    var next = Math.Max(0, Math.Min(cur + delta, current.Sold - current.Returned));
                    qty.Text = next.ToString(CultureInfo.InvariantCulture);
                };
                return b;
            }
            stepPanel.Children.Add(Step("−", -1)); stepPanel.Children.Add(qty); stepPanel.Children.Add(Step("+", 1));
            stepper.Child = stepPanel; Grid.SetColumn(stepper, 1); row.Children.Add(stepper);

            var wh = new ComboBox { ItemsSource = warehouses, DisplayMemberPath = "Name", SelectedValuePath = "Guid", SelectedValue = line.WarehouseGuid,
                                    Style = (Style)FindResource("FieldCombo"), Height = 40, VerticalAlignment = VerticalAlignment.Center };
            if (wh.SelectedIndex < 0) wh.SelectedIndex = 0;
            Grid.SetColumn(wh, 2); row.Children.Add(wh);

            var damaged = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                                         ToolTip = "Nuqsonli (brak): tovar sotiladigan qoldiqqa qaytmaydi" };
            Grid.SetColumn(damaged, 3); row.Children.Add(damaged);

            panel.Children.Add(rowBorder);
            rows.Add((line, qty, wh, damaged)); qty.TextChanged += (_, _) => Preview();
        }

        // Brak izohi (maketdagi qizil karta)
        var brak = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 6, 0, 0) };
        ModalKit.Res(brak, Border.BorderBrushProperty, "BadLine"); ModalKit.Res(brak, Border.BackgroundProperty, "BadSoft");
        var brakPanel = new StackPanel();
        brakPanel.Children.Add(ModalKit.Text("Brak — omborga qaytmaydi", 14, "Bad", FontWeights.SemiBold));
        var brakSub = ModalKit.Text("Belgilanmasa, tovar tanlangan omborga qaytib, qoldiq oshadi. Qaytarish asl sotuv narxi bo‘yicha hisoblanadi.", 13, "Ink2");
        brakSub.Margin = new Thickness(0, 2, 0, 0); brakPanel.Children.Add(brakSub);
        brak.Child = brakPanel; panel.Children.Add(brak);

        FrameworkElement Field(string label, TextBox field, string? suffix = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            sp.Children.Add(ModalKit.Label(this, label));
            field.Style = (Style)FindResource("FieldBox");
            if (suffix != null) field.Tag = suffix;
            sp.Children.Add(field);
            return sp;
        }
        panel.Children.Add(Field("Qaytarish sababi", reason));
        var money = new Grid();
        money.ColumnDefinitions.Add(new ColumnDefinition()); money.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); money.ColumnDefinitions.Add(new ColumnDefinition());
        money.Children.Add(Field("Naqd qaytariladi", cash, "so‘m"));
        var cardField = Field("Kartaga qaytariladi", card, "so‘m"); Grid.SetColumn(cardField, 2); money.Children.Add(cardField);
        panel.Children.Add(money);
        panel.Children.Add(Field("Haqiqatan qaytarilgan karta xarajati (odatda 0)", fee, "so‘m"));
        var bankNote = ModalKit.Text("Tasdiqlash bank orqali avtomatik pul o‘tkazmaydi.", 13, "Ink3");
        bankNote.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(bankNote);
        Preview();
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
    static string LineText(ReturnLine line) => $"{line.ProductName}\nSotilgan: {line.Sold:0.###} · Qaytgan: {line.Returned:0.###} · Qolgan: {line.Sold-line.Returned:0.###}";
    void SetEditable(bool editable)
    {
        foreach (var row in rows) row.Quantity.IsEnabled = row.Warehouse.IsEnabled = row.Damaged.IsEnabled = editable;
        reason.IsEnabled = cash.IsEnabled = card.IsEnabled = fee.IsEnabled = editable;
    }
    void RefreshRejectedQuote()
    {
        quoteReady = false;
        ApplyQuote(store.Quote(quote.SaleGuid));
        SetEditable(true); Preview();
    }
    void ApplyQuote(ReturnQuote fresh)
    {
        var updated = rows.Select(row => (Line: fresh.Lines.Single(l => l.Guid == row.Line.Guid),
            row.Quantity, row.Warehouse, row.Damaged)).ToList();
        quote = fresh; rows.Clear(); rows.AddRange(updated);
        foreach (var row in rows) lineLabels[row.Line.Guid].Text = LineText(row.Line);
        quoteReady = true;
    }
    static bool SameCalculation(ReturnQuote a, ReturnQuote b) =>
        a.SaleGuid == b.SaleGuid && a.DebtAccountGuid == b.DebtAccountGuid &&
        a.CustomerGuid == b.CustomerGuid && a.AccountBalanceMinor == b.AccountBalanceMinor &&
        a.RemainingFee == b.RemainingFee &&
        a.Lines.OrderBy(l => l.Guid).SequenceEqual(b.Lines.OrderBy(l => l.Guid));
    void Preview(bool updatePayments = true)
    {
        if (submitted != null || !quoteReady) return;
        try {
            decimal sum = 0;
            foreach (var row in rows) { var qty = Number(row.Quantity.Text); if (qty == 0) continue;
                var l = row.Line; sum += ReturnAccounting.Calculate(l.Sold, l.Revenue, l.Cost,
                    new(l.Returned, l.Refunded, l.CostBasis), qty, row.Damaged.IsChecked != true).Refund; }
            if (sum == 0) {
                total.Text = "Qaytariladigan summa: 0 so‘m"; if (updatePayments) cash.Text = card.Text = "0";
            } else if (quote.DebtAccountGuid != null) {
                var sumMinor = checked((long)Math.Round(sum * 100, MidpointRounding.AwayFromZero));
                var split = DebtAccounting.SplitReturn(sumMinor, quote.AccountBalanceMinor);
                var offset = split.DebtOffsetMinor / 100.0m;
                var refund = split.RefundMinor / 100.0m;
                total.Text = $"Qaytariladigan tovar: {sum:N2} so‘m | Qarzdan chegiriladi: {offset:N2} so‘m | Xaridorga to‘lanadi: {refund:N2} so‘m";
                if (updatePayments) { cash.Text = refund.ToString(CultureInfo.InvariantCulture); card.Text = "0"; }
            } else {
                total.Text = $"Qaytariladigan summa: {sum:N2} so‘m";
                if (updatePayments) { cash.Text = sum.ToString(CultureInfo.InvariantCulture); card.Text = "0"; }
            }
        } catch (Exception e) { total.Text = e.Message; }
    }
    bool ConfirmAmounts(string text)
    {
        // Maket uslubidagi tasdiq oynasi (faqat ko'rinish)
        var dialog = new Window { Owner = this };
        var box = ModalKit.Setup(dialog, "Qaytarish", 440, keepScrim: true);
        box.Padding = new Thickness(24);
        var panel = new StackPanel(); box.Child = panel;
        panel.Children.Add(ModalKit.Text("Qaytarishni tasdiqlaysizmi?", 18, "Ink", FontWeights.Bold));
        var body = ModalKit.Text(text, 15, "Ink2"); body.Margin = new Thickness(0, 8, 0, 20); body.LineHeight = 22;
        panel.Children.Add(body);
        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var no = ModalKit.Button(dialog, "Yo‘q", "BtnSecondary"); no.IsCancel = true;
        var yes = ModalKit.Button(dialog, "Ha, qaytarish", "BtnPrimary"); yes.IsDefault = true;
        no.Click += (_,_) => dialog.DialogResult=false;
        yes.Click += (_,_) => dialog.DialogResult=true;
        Grid.SetColumn(yes, 2); buttons.Children.Add(no); buttons.Children.Add(yes); panel.Children.Add(buttons);
        return dialog.ShowDialog()==true;
    }
    void Confirm(object sender, RoutedEventArgs args)
    {
        if (saved || !confirm.IsEnabled) return;
        confirm.IsEnabled = false;
        try {
            if (submitted == null)
            {
                if (!quoteReady) throw new InvalidOperationException("Ma’lumot yangilanmadi. Oynani qayta oching.");
                var fresh = store.Quote(quote.SaleGuid);
                var changed = !SameCalculation(quote, fresh);
                ApplyQuote(fresh);
                if (changed) Preview(updatePayments: false);
                // Build before assigning: malformed input must not freeze a draft.
                var request = new ReturnRequest(requestId, quote.SaleGuid, reason.Text.Trim(), Number(cash.Text), Number(card.Text), Number(fee.Text),
                    rows.Where(r => Number(r.Quantity.Text) != 0).Select(r => new ReturnSelection(r.Line.Guid, Number(r.Quantity.Text),
                        (string?)r.Warehouse.SelectedValue ?? "", r.Damaged.IsChecked != true)).ToList());
                decimal returned = 0;
                foreach (var item in request.Items) {
                    var line = quote.Lines.Single(l => l.Guid == item.SaleItemGuid);
                    returned += ReturnAccounting.Calculate(line.Sold,line.Revenue,line.Cost,
                        new(line.Returned,line.Refunded,line.CostBasis),item.Quantity,item.Resellable).Refund;
                }
                if (returned <= 0) throw new ArgumentException("Qaytariladigan miqdorni kiriting.");
                var offset = quote.DebtAccountGuid == null ? 0m :
                    DebtAccounting.SplitReturn(checked((long)(returned * 100)),quote.AccountBalanceMinor).DebtOffsetMinor / 100m;
                var refund = returned - offset;
                if (request.CashRefund + request.CardRefund != refund) {
                    if (!changed) throw new ArgumentException("Naqd va karta yig‘indisi mos emas.");
                    var keptCard = Math.Min(request.CardRefund,refund);
                    request = request with { CashRefund = refund-keptCard, CardRefund = keptCard };
                    cash.Text=request.CashRefund.ToString(CultureInfo.InvariantCulture);
                    card.Text=request.CardRefund.ToString(CultureInfo.InvariantCulture);
                }
                var text = $"Mijozga qaytariladi: {refund:N2} so‘m";
                if (offset > 0) text += $"\nQarzdan chegiriladi: {offset:N2} so‘m";
                if (request.CardRefund > 0) text += $"\nNaqd: {request.CashRefund:N2} · Karta: {request.CardRefund:N2}";
                if (!ConfirmAmounts(text)) return;
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
            } else if (submitted == null) {
                MessageBox.Show(this, "Joriy hisobni tekshirib bo‘lmadi. Qaytarish yuborilmadi; qayta urinib ko‘ring. " + e.Message, "Qaytarish");
            } else {
                MessageBox.Show(this, "Natijani tekshirish uchun aynan shu so‘rovni qayta tasdiqlang. " + e.Message, "Qaytarish");
            }
        } finally { if (!saved) confirm.IsEnabled = submitted != null || quoteReady; }
    }
}
