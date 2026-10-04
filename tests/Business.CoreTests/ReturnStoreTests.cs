using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Returns;
using Microsoft.Data.Sqlite;

static class ReturnStoreTests
{
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "LinePOS_returns_" + Guid.NewGuid() + ".db");
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        try
        {
            var db = new DatabaseContext(path);
            var product = new Product { Name = "Return test", StockQuantity = 0, SellingPrice = 50000 };
            db.SaveProduct(product);
            var sale = new Sale { TotalAmount = 150000, TotalCost = 120000, TaxAmount = 2700, CardAmount = 150000, UsdRate = 12000,
                Items = new() { new SaleItem { ProductId = product.Id, ProductGuid = product.Guid, ProductName = product.Name,
                    Quantity = 3, PriceAtSale = 50000, CostAtSale = 40000, WarehouseGuid = "main-default-warehouse" } } };
            db.InsertSale(sale);
            var store = new ReturnStore(path); store.Install();
            ReturnRequest Request(decimal qty, bool good = true) => new(Guid.NewGuid().ToString(), sale.Guid, "Test", qty * 50000, 0, 0,
                new() { new(ReturnStore.LineGuid(sale.Guid, 1), qty, "main-default-warehouse", good) });
            var request = Request(1);
            var first = store.Commit(request, "cashier", "desktop");
            var returnReceipt = db.SearchSalesByReceiptNumber("RT-" + first.Guid.Replace("-", "").ToUpperInvariant()).Single();
            Check(returnReceipt.ReceiptNumber.StartsWith("RT-") && SaleAccounting.Lines(returnReceipt).Single().Profit == -10000,
                "Return financial event mismatch");
            Check(first.Refund == 50000 && first.CostReversal == 40000, "Wrong historical refund");
            var retry = new ReturnStore(path).Commit(request, "cashier", "desktop");
            Check(first == retry, "Lost ACK retry created a new return");
            Check(db.GetProductByGuid(product.Guid)!.StockQuantity == -2, "Retry duplicated stock");
            try { store.Commit(request with { Reason = "Changed" }, "cashier", "desktop"); throw new Exception("Changed request accepted"); }
            catch (ArgumentException) { }
            var parallel = new[] { Request(2), Request(2) };
            var results = parallel.Select(r => Task.Run(() => {
                try { new ReturnStore(path).Commit(r, "cashier", "desktop"); return true; }
                catch (ArgumentException) { return false; }
            })).ToArray();
            Task.WaitAll(results);
            Check(results.Count(t => t.Result) == 1, "Parallel returns exceeded sold quantity");
            Check(db.GetProductByGuid(product.Guid)!.StockQuantity == 0, "Return stock mismatch");
            var original = db.SearchSalesByReceiptNumber(sale.ReceiptNumber).Single();
            Check(original.TotalAmount == 150000 && original.Items.Single().Quantity == 3, "Original sale changed");
            using var conn = new SqliteConnection("Data Source=" + path); conn.Open();
            using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM returns";
            Check(Convert.ToInt32(cmd.ExecuteScalar()) == 2, "Duplicate return persisted");
            var fractional = new Sale { TotalAmount = 100, TotalCost = 33.33, CashAmount = 100, UsdRate = 12000,
                Items = new() { new SaleItem { ProductId = product.Id, ProductGuid = product.Guid, ProductName = product.Name,
                    Quantity = .7, PriceAtSale = 100 / .7, CostAtSale = 33.33 / .7, WarehouseGuid = "main-default-warehouse" } } };
            db.InsertSale(fractional);
            var cents = new[] { 14.29m, 14.28m, 14.29m, 14.28m, 14.29m, 14.28m, 14.29m };
            decimal refunded = 0, reversed = 0;
            foreach (var amount in cents) {
                var part = store.Commit(new ReturnRequest(Guid.NewGuid().ToString(), fractional.Guid, "Fractional", amount, 0, 0,
                    new() { new(fractional.Items[0].Guid, .1m, "main-default-warehouse", true) }), "cashier", "desktop");
                refunded += part.Refund; reversed += part.CostReversal;
            }
            Check(refunded == 100 && reversed == 33.33m, "Persisted fractional returns lost cents");
            Check(store.Quote(fractional.Guid).Lines.Single().Returned == .7m, "Persisted fractional quantity drift");
            Console.WriteLine("PASS return transaction: historical amounts, idempotency across restart, payload mismatch, concurrent overreturn protection, inventory, immutable original");
        }
        finally { SqliteConnection.ClearAllPools(); foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
    }
}
