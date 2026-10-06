using PosElectro.Desktop.Models;

internal static class AccountingTests
{
    public static void Run()
    {
        void Equal(double expected, double actual) { if (Math.Abs(expected - actual) > .000001) throw new Exception($"Expected {expected}, got {actual}"); }
        var sale = new Sale { Id = 7, Guid = "a1b2c3d4-1234-4321-aaaa-0123456789ab", TotalAmount = 150000, TotalCost = 120000, TaxAmount = 2700, CardAmount = 150000, UsdRate = 12000,
            Items = new() { new SaleItem { Id = 1, ProductName = "Old name", CategoryAtSale = "Cable", UnitAtSale = "METR", Quantity = 1, PriceAtSale = 150000, CostAtSale = 10, CostCurrency = "USD", WarehouseGuid = "w" } } };
        var rows = SaleAccounting.Lines(sale);
        Equal(27300, rows.Sum(i => i.Profit)); Equal(2.275, rows.Sum(i => i.ProfitUsd ?? 0));
        if (rows[0].ProductName != "Old name" || rows[0].Category != "Cable") throw new Exception("Historical identity changed");
        var receipt = sale.ReceiptNumber; sale.Id = 981;
        if (sale.ReceiptNumber != receipt) throw new Exception("Receipt depends on local ID");
        sale.UsdRate = 0;
        Equal(2.275, SaleAccounting.Lines(sale)[0].ProfitUsd!.Value);
        sale.Items[0].CostCurrency = "UZS"; sale.Items[0].CostAtSale = 120000;
        if (SaleAccounting.Lines(sale)[0].ProfitUsd != null) throw new Exception("Invented legacy exchange rate");
        sale.Items.Add(new SaleItem { Id = 2, ProductName = "Lamp", CategoryAtSale = "Lamp", Quantity = 2, PriceAtSale = 75000, CostAtSale = 30000, WarehouseGuid = "other" });
        sale.TotalAmount = 300000; sale.TotalCost = 180000; sale.CardAmount = 300000; sale.TaxAmount = 5400;
        rows = SaleAccounting.Lines(sale);
        Equal(300000, rows.Sum(i => i.TotalPrice)); Equal(180000, rows.Sum(i => i.TotalCost)); Equal(5400, rows.Sum(i => i.TaxAmount));
        Equal(150000, rows.Where(i => i.Category == "Cable").Sum(i => i.TotalPrice));
        Equal(27300, rows.Where(i => i.WarehouseGuid == "w").Sum(i => i.Profit));
        sale.PaymentType = PaymentType.BRAK; sale.TotalAmount = 0; sale.CardAmount = 0; sale.TaxAmount = 0;
        rows = SaleAccounting.Lines(sale); Equal(-180000, rows.Sum(i => i.Profit));
        if (rows.Any(i => !i.IsBrak)) throw new Exception("Brak mixed with sales");
        foreach (var total in new[] { .01, .02, .05, 10.01, -10.01 }) {
            var amounts = SaleAccounting.Allocate(total, Enumerable.Repeat(1.0, 100).ToArray());
            Equal(total, amounts.Sum()); if (total >= 0 && amounts.Any(i => i < 0)) throw new Exception("Negative rounding residue");
        }
        Equal(.01, SaleAccounting.Allocate(.01, new[] { 1.0, 1.0, 1.0 })[0]);
        sale.TotalAmount = .02; sale.CashAmount = .01; sale.CardAmount = .01; sale.TaxAmount = 0;
        foreach (var line in SaleAccounting.Lines(sale)) Equal(line.TotalPrice, line.CashAmount + line.CardAmount);
        Console.WriteLine("PASS accounting: immutable UZS/USD, legacy unknown, identity, category/warehouse, tax, brak and rounding");
    }
}
