using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using Microsoft.Data.Sqlite;

ReturnAccountingTests.Run();
ReturnStoreTests.Run();

var path = Path.Combine(Path.GetTempPath(), "LinePOS_business_" + Guid.NewGuid() + ".db");
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
try {
    var db = new DatabaseContext(path);
    var product = new Product { Guid = Guid.NewGuid().ToString(), Name = "Kabel", Category = "Cable", CostPrice = 10, CostCurrency = "USD", SellingPrice = 150000, StockQuantity = 0 };
    db.SaveProduct(product);
    var sale = new Sale { Guid = Guid.NewGuid().ToString(), CreatedAt = DateTimeOffset.Now.AddDays(-10).ToUnixTimeMilliseconds(), TotalAmount = 150000, TotalCost = 120000, CardAmount = 150000, TaxAmount = 2700, UsdRate = 12000,
        Items = new() { new SaleItem { ProductId = product.Id, ProductGuid = product.Guid, ProductName = "Original Kabel", CategoryAtSale = "Cable", UnitAtSale = "METR", CostAtSale = 10, CostCurrency = "USD", Quantity = 1, PriceAtSale = 150000, WarehouseGuid = "main-default-warehouse" } } };
    db.InsertSale(sale);
    Check(db.GetProductByGuid(product.Guid)!.StockQuantity == -1, "Negative stock policy changed");
    var saved = db.GetSales(DateTime.Today.AddDays(-11), DateTime.Today).Single();
    Check(saved.Items.Single().Guid == sale.Guid + ":1", "Stable line GUID missing");
    Check(saved.UsdRate == 12000 && saved.Items.Single().CategoryAtSale == "Cable", "Snapshot not stored");
    Check(db.SearchSalesByReceiptNumber(sale.ReceiptNumber).Single().Guid == sale.Guid, "Global receipt search failed");
    Check(db.GetSales(DateTime.Today, DateTime.Today.AddDays(1)).Count == 0, "Date range ignored");
    product = db.GetProductByGuid(product.Guid)!;
    product.Name = "Renamed"; product.Category = "Other"; db.SaveProduct(product);
    var rows = db.GetDetailedReportItems(DateTime.Today.AddDays(-11), DateTime.Today, 99999, "Cable", "main-default-warehouse");
    Check(rows.Single().ProductName == "Original Kabel" && rows.Single().Profit == 27300, "Current metadata/rate changed historical report");
    Check(db.GetDetailedReportItems(DateTime.Today.AddDays(-11), DateTime.Today, 99999, null, "wrong").Count == 0, "Warehouse filter leaked");
    Check(db.GetDetailedReportItems(DateTime.Today.AddDays(-11), DateTime.Today, 99999, "Other").Count == 0, "Category filter leaked");
    db.InsertSale(sale);
    Check(db.GetTotalSalesCount() == 1 && db.GetProductByGuid(product.Guid)!.StockQuantity == -1, "Duplicate GUID changed inventory");
    using (var conn = new SqliteConnection("Data Source=" + path)) { conn.Open(); using var cmd = conn.CreateCommand(); cmd.CommandText = "ALTER TABLE sales DROP COLUMN usd_rate; DROP INDEX index_sale_items_guid; ALTER TABLE sale_items DROP COLUMN guid; ALTER TABLE sale_items DROP COLUMN category_at_sale; ALTER TABLE sale_items DROP COLUMN unit_at_sale;"; cmd.ExecuteNonQuery(); }
    db = new DatabaseContext(path);
    saved = db.GetSales(DateTime.Today.AddDays(-11), DateTime.Today).Single();
    Check(saved.Items.Single().Guid == sale.Guid + ":1", "Migrated receipt line identity changed");
    Check(saved.Guid == sale.Guid && saved.TotalCost == 120000 && saved.UsdRate == 0, "Legacy migration changed money/identity");
    Check(SaleAccounting.Lines(saved).Single().Profit == 27300, "Legacy profit lost");
    Console.WriteLine("PASS real database: migrations, historic snapshots, receipt search, date/category/warehouse filters, negative stock, duplicate receipt");
} finally { SqliteConnection.ClearAllPools(); foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
