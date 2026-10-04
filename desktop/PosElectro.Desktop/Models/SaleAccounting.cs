using System;
using System.Collections.Generic;
using System.Linq;
namespace PosElectro.Desktop.Models;

public static class SaleAccounting
{
    public static double Money(double value) => (double)Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
    public static double[] Allocate(double total, double[] weights)
    {
        if (weights.Length == 0) return Array.Empty<double>();
        var positive = weights.Select(w => (decimal)Math.Max(0, w)).ToArray();
        var sum = positive.Sum();
        var cents = decimal.ToInt64(Math.Round((decimal)total * 100, 0, MidpointRounding.AwayFromZero));
        var raw = positive.Select(w => sum == 0 ? (decimal)Math.Abs(cents) / weights.Length : Math.Abs(cents) * w / sum).ToArray();
        var parts = raw.Select(v => decimal.ToInt64(decimal.Floor(v))).ToArray();
        var order = Enumerable.Range(0, weights.Length).OrderByDescending(i => raw[i] - parts[i]).ThenBy(i => i).ToArray();
        var remainder = Math.Abs(cents) - parts.Sum();
        for (var i = 0; i < remainder; i++) parts[order[i % order.Length]]++;
        return parts.Select(v => (cents < 0 ? -v : v) / 100.0).ToArray();
    }
    public static List<SaleReportItem> Lines(Sale sale)
    {
        var items = sale.Items.OrderBy(i => i.Id).ToArray();
        var usd = items.Where(i => i.CostCurrency == "USD").Sum(i => i.CostAtSale * i.Quantity);
        var uzs = items.Where(i => i.CostCurrency != "USD").Sum(i => i.CostAtSale * i.Quantity);
        var inferred = usd > 0 ? (sale.TotalCost - uzs) / usd : 0;
        double? rate = sale.UsdRate > 0 && double.IsFinite(sale.UsdRate) ? sale.UsdRate :
            inferred > 0 && double.IsFinite(inferred) ? inferred : null;
        var weights = items.Select(i => i.PriceAtSale * i.Quantity).ToArray();
        var revenue = Allocate(sale.TotalAmount, weights);
        var tax = Allocate(sale.TaxAmount, weights);
        var cash = Allocate(sale.CashAmount, revenue);
        var card = Money(sale.CashAmount + sale.CardAmount) == Money(sale.TotalAmount)
            ? revenue.Select((v, i) => Money(v - cash[i])).ToArray() : Allocate(sale.CardAmount, revenue);
        var cost = Allocate(sale.TotalCost, items.Select(i => i.CostAtSale * i.Quantity *
            (i.CostCurrency == "USD" ? rate ?? 1 : 1)).ToArray());
        return items.Select((item, index) => {
            var profit = (double)((decimal)revenue[index] - (decimal)cost[index] - (decimal)tax[index]);
            return new SaleReportItem {
                SaleId = sale.Id, ReceiptNumber = sale.ReceiptNumber, ProductId = item.ProductId,
                ProductName = string.IsNullOrWhiteSpace(item.ProductName) ? $"Mahsulot #{item.ProductId}" : item.ProductName,
                Category = string.IsNullOrWhiteSpace(item.CategoryAtSale) ? "Tarixiy kategoriya noma’lum" : item.CategoryAtSale,
                UnitType = Enum.TryParse<UnitType>(item.UnitAtSale, out var unit) ? unit : UnitType.DONA,
                WarehouseGuid = item.WarehouseGuid, WarehouseName = item.WarehouseName,
                Quantity = item.Quantity, CostPrice = item.Quantity != 0 ? cost[index] / item.Quantity : 0,
                CostCurrency = item.CostCurrency, OriginalCost = item.CostAtSale,
                SellingPrice = item.PriceAtSale, TotalPrice = revenue[index], TotalCost = cost[index],
                TaxAmount = tax[index], CashAmount = cash[index], CardAmount = card[index], Profit = profit,
                ProfitUsd = rate.HasValue ? profit / rate.Value : null, Timestamp = sale.CreatedAt,
                IsBrak = sale.PaymentType == PaymentType.BRAK };
        }).ToList();
    }
}
