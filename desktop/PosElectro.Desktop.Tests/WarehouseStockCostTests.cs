using System;
using System.IO;
using System.Linq;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Services;
using PosElectro.Desktop.ViewModels;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Ombor kartasidagi "Tan narxi" haqiqiy tan narxi bo'lishi kerak (avval sotish narxi bo'yicha qiymat chiqardi).</summary>
    public sealed class WarehouseStockCostTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-whcost-" + Guid.NewGuid().ToString("N"));
        private readonly DatabaseContext _db;

        public WarehouseStockCostTests()
        {
            Directory.CreateDirectory(_dir);
            _db = new DatabaseContext(Path.Combine(_dir, "t.db"));
            _db.SaveProduct(new Product { Name = "Kabel", CostPrice = 1000, SellingPrice = 1500, StockQuantity = 10 });
            _db.SaveProduct(new Product { Name = "Lampa", CostPrice = 500, SellingPrice = 900, StockQuantity = 4 });
            _db.SaveProduct(new Product { Name = "Avtomat", CostPrice = 2, CostCurrency = "USD", SellingPrice = 40000, StockQuantity = 3 });
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void Warehouse_cost_is_quantity_times_cost_price_not_selling_price()
        {
            var whs = _db.GetWarehouses();
            Assert.Equal(10 * 1000 + 4 * 500, whs.Sum(w => w.StockCostUzs));
            Assert.Equal(3 * 2, whs.Sum(w => w.StockCostUsd));
            Assert.Equal(10 * 1500 + 4 * 900 + 3 * 40000, whs.Sum(w => w.TotalStockValue));
        }

        [Fact]
        public void Inventory_screen_total_matches_excel_formula_with_usd_rate()
        {
            var currency = new CurrencyService(_db);
            var vm = new InventoryViewModel(_db, new ProductService(_db), currency);
            vm.Refresh();

            var rate = currency.GetCachedUsdRate();
            var expected = _db.GetAllProducts(includeDeleted: false)
                .Sum(p => p.StockQuantity * (p.CostCurrency == "USD" ? p.CostPrice * rate : p.CostPrice));
            Assert.Equal(expected, vm.Warehouses.Sum(w => w.TotalStockCost), 2);
        }
    }
}
