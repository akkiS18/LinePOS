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
    /// <summary>Hisobotlar oynasi: qidiruv va "X" (tozalash) bosilgandagi ro'yxat holati va tartibi.</summary>
    public sealed class ReportsViewModelSearchTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-vmtests-" + Guid.NewGuid().ToString("N"));
        private readonly DatabaseContext _db;
        private readonly Product _cable;
        private readonly long _now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public ReportsViewModelSearchTests()
        {
            Directory.CreateDirectory(_dir);
            _db = new DatabaseContext(Path.Combine(_dir, "t.db"));
            _cable = new Product { Name = "Kabel VVG 3x2.5", Barcode = "4780001111111", CostPrice = 1000, SellingPrice = 1500, StockQuantity = 100 };
            _db.SaveProduct(_cable);
            _db.SaveProduct(new Product { Name = "Lampa LED", Barcode = "4780002222222", CostPrice = 500, SellingPrice = 800, StockQuantity = 100 });
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private Sale AddSale(long createdAt, Product p)
        {
            var s = new Sale
            {
                PaymentType = PaymentType.CASH, TotalAmount = 1500, TotalCost = 1000, CashAmount = 1500, CreatedAt = createdAt,
                Items = { new SaleItem { ProductGuid = p.Guid, ProductName = p.Name, Quantity = 1, PriceAtSale = 1500, CostAtSale = 1000, CostCurrency = "UZS" } }
            };
            _db.InsertSale(s, deductStock: false);
            return s;
        }

        [Fact]
        public void Search_lists_newest_first_across_history_and_clearing_restores_period_list()
        {
            // Times stay inside today even when the test runs just after midnight
            long dayStart = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();
            var todayMorning = AddSale(Math.Max(_now - 3 * 3_600_000, dayStart + 1_000), _cable);   // bugun, 3 soat oldin
            var todayLatest = AddSale(Math.Max(_now - 60_000, dayStart + 2_000), _cable);           // bugun, eng oxirgi
            var lastWeek = AddSale(_now - 7L * 24 * 3_600_000, _cable); // 7 kun oldin (id eng kichik emas, keyin kiritilgan)
            var lamp = AddSale(Math.Max(_now - 30_000, dayStart + 3_000), _db.GetAllProducts().First(p => p.Name.StartsWith("Lampa")));

            var vm = new ReportsViewModel(_db, new CurrencyService(_db));   // standart davr: Bugun
            Assert.Equal(new[] { lamp.Id, todayLatest.Id, todayMorning.Id }, vm.Sales.Select(s => s.Id).ToArray());

            vm.SearchReceiptNumber = "kabel";
            vm.LoadData();   // debounce taymeri UI oqimida ishlaydi; testda natijani to'g'ridan-to'g'ri chaqiramiz
            var searched = vm.Sales.Select(s => s.Id).ToArray();
            Assert.Equal(new[] { todayLatest.Id, todayMorning.Id, lastWeek.Id }, searched);   // butun tarix, vaqt bo'yicha kamayish
            Assert.DoesNotContain(lamp.Id, searched);
            Assert.True(vm.HasSearchInfo);

            vm.ClearReceiptSearchCommand.Execute(null);   // "X" tugmasi
            Assert.Equal(string.Empty, vm.SearchReceiptNumber);
            Assert.False(vm.HasSearchReceiptNumber);
            Assert.Equal(new[] { lamp.Id, todayLatest.Id, todayMorning.Id }, vm.Sales.Select(s => s.Id).ToArray());   // faqat bugungi davr qaytdi
            Assert.False(vm.HasSearchInfo);
        }
    }
}
