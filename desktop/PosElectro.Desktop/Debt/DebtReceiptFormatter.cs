using System;
using System.Text;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Debt;

public static class DebtReceiptFormatter
{
    public static string BuildReceiptText(Sale sale)
    {
        var sb = new StringBuilder();
        sb.AppendLine("          SMART KASSA           ");
        sb.AppendLine("    Elektr jihozlari do'koni    ");
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Chek: #{sale.ReceiptNumber}");
        if (!string.IsNullOrEmpty(sale.OriginalReceiptNumber)) sb.AppendLine($"Asl chek: {sale.OriginalReceiptNumber}");
        sb.AppendLine($"Sana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"To'lov turi: {sale.PaymentTypeDisplay}");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("Tovar              Miqd.    Jami");
        sb.AppendLine("--------------------------------");

        foreach (var item in sale.Items)
        {
            string name = item.ProductName;
            if (name.Length > 30) name = name.Substring(0, 27) + "...";
            sb.AppendLine(name);

            string qtyStr = $"{item.Quantity:0.##}";
            string totalStr = $"{item.TotalPrice:N0}";
            string lineLeft = $"  {item.PriceAtSale:N0} x {qtyStr}";
            string lineRight = totalStr;

            int space = 32 - lineLeft.Length - lineRight.Length;
            if (space < 1) space = 1;
            sb.AppendLine(lineLeft + new string(' ', space) + lineRight);
        }

        sb.AppendLine("--------------------------------");
        string totalTitle = "JAMI:";
        string totalVal = $"{sale.TotalAmount:N0} so'm";
        int totalSpace = 32 - totalTitle.Length - totalVal.Length;
        if (totalSpace < 1) totalSpace = 1;
        sb.AppendLine(totalTitle + new string(' ', totalSpace) + totalVal);

        if (sale.PaymentType == PaymentType.SPLIT)
        {
            sb.AppendLine($"  Naqd:  {sale.CashAmount:N0} so'm");
            sb.AppendLine($"  Karta: {sale.CardAmount:N0} so'm");
        }
        else if (sale.PaymentType == PaymentType.DEBT)
        {
            double debt = Math.Max(0, sale.TotalAmount - sale.CashAmount - sale.CardAmount);
            if (sale.CashAmount > 0) sb.AppendLine($"  Oldindan (Naqd):  {sale.CashAmount:N0} so'm");
            if (sale.CardAmount > 0) sb.AppendLine($"  Oldindan (Karta): {sale.CardAmount:N0} so'm");
            sb.AppendLine($"  Nasiya (Qarz):    {debt:N0} so'm");
        }

        sb.AppendLine("--------------------------------");
        sb.AppendLine("    Rahmat, xaridingiz uchun!   ");
        sb.AppendLine("       Yana tashrif buyuring!   ");
        return sb.ToString();
    }

    public static string BuildCustomerStatementText(DebtCustomerDetailDto customer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("          SMART KASSA           ");
        sb.AppendLine("      MIJOZ QARZ KO'CHIRMASI    ");
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Mijoz: {customer.Customer.Name}");
        if (!string.IsNullOrWhiteSpace(customer.Customer.Phone)) sb.AppendLine($"Tel: {customer.Customer.Phone}");
        sb.AppendLine($"Sana: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("NASIYALAR (HISOB-FAKTURALAR):");
        foreach (var acc in customer.Accounts)
        {
            string status = acc.BalanceMinor <= 0 ? "Yopilgan" : $"{acc.BalanceUz:N0} so'm qoldiq";
            sb.AppendLine($"Chek: {acc.ReceiptNumber} ({acc.CreatedAtDisplay})");
            sb.AppendLine($"  Asl qarz: {acc.OriginalDebtUz:N0} so'm | {status}");
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine("TO'LOVLAR VA AMALLAR TARIXI:");
        foreach (var ev in customer.Events)
        {
            sb.AppendLine($"{ev.OccurredAtDisplay} • {ev.KindDisplay}");
            sb.AppendLine($"  Summa: {ev.AmountUz:N0} so'm (Naqd: {ev.CashUz:N0} / Karta: {ev.CardUz:N0})");
        }
        sb.AppendLine("--------------------------------");
        string balTitle = "JORIY BALANS:";
        double balUz = customer.BalanceMinor / 100.0;
        string balVal = customer.BalanceMinor > 0
            ? $"{balUz:N0} so'm"
            : customer.BalanceMinor < 0
                ? $"+{Math.Abs(balUz):N0} so'm (Haqdor)"
                : "0 so'm";
        int space = 32 - balTitle.Length - balVal.Length;
        if (space < 1) space = 1;
        sb.AppendLine(balTitle + new string(' ', space) + balVal);
        sb.AppendLine("--------------------------------");
        sb.AppendLine("     Hisob-kitob uchun rahmat!   ");
        return sb.ToString();
    }
}
