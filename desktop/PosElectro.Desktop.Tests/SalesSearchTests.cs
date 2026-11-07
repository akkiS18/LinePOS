using System;
using System.IO;
using System.Linq;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>
    /// Hisobotlar → "Cheklar tarixi" qidiruvi: chek raqami, mahsulot nomi va shtrix-kod.
    /// Har test o'zining vaqtinchalik bazasida ishlaydi (mijozning haqiqiy bazasiga tegmaydi).
    /// </summary>
    public sealed class SalesSearchTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-tests-" + Guid.NewGuid().ToString("N"));
        private readonly DatabaseContext _db;
        private readonly Sale _cableSale;
        private readonly Sale _cyrillicSale;
        private readonly Sale _deletedProductSale;
        private readonly Sale _returnSale;

        public SalesSearchTests()
        {
            Directory.CreateDirectory(_dir);
            _db = new DatabaseContext(Path.Combine(_dir, "t.db"));

            var cable = NewProduct("Kabel VVG 3x2.5", "4780001111111");
            var cyr = NewProduct("Лампочка LED 9W", "4780002222222");
            var del = NewProduct("Rozetka ikkilik", "4780003333333");
            _db.SaveProduct(cable); _db.SaveProduct(cyr); _db.SaveProduct(del);

            _cableSale = NewSale(cable, PaymentType.CASH);
            _cyrillicSale = NewSale(cyr, PaymentType.CASH);
            _deletedProductSale = NewSale(del, PaymentType.CASH);
            _returnSale = NewSale(cable, PaymentType.RETURN);

            // Mahsulot keyin o'chirilsa ham (soft delete), tarixdagi chek topilishi kerak
            del.IsDeleted = true;
            _db.SaveProduct(del);
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { /* vaqtinchalik papka */ }
        }

        private static Product NewProduct(string name, string barcode) => new()
        {
            Name = name, Barcode = barcode, CostPrice = 1000, SellingPrice = 1500, StockQuantity = 100,
        };

        private Sale NewSale(Product p, PaymentType type)
        {
            var sale = new Sale
            {
                PaymentType = type, TotalAmount = 1500, TotalCost = 1000, CashAmount = 1500,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Items =
                {
                    new SaleItem { ProductGuid = p.Guid, ProductName = p.Name, Quantity = 1, PriceAtSale = 1500, CostAtSale = 1000, CostCurrency = "UZS" }
                }
            };
            _db.InsertSale(sale, deductStock: false);
            return sale;
        }

        private long[] Search(string q) => _db.SearchSales(q).Select(s => s.Id).OrderBy(i => i).ToArray();

        // ---- Mahsulot nomi ----
        [Theory]
        [InlineData("kabel")]
        [InlineData("KABEL")]
        [InlineData("  kabel  ")]
        [InlineData("vvg")]
        [InlineData("3x2.5")]
        [InlineData("3x2,5")]            // vergul ham nuqta kabi
        [InlineData("2.5 kabel")]        // tartibga bog'liq emas
        public void Finds_receipt_by_product_name(string query)
        {
            var ids = Search(query);
            Assert.Contains(_cableSale.Id, ids);
            Assert.DoesNotContain(_cyrillicSale.Id, ids);
        }

        [Theory]
        [InlineData("лампочка")]
        [InlineData("lampochka")]        // lotin yozuvida kirill nom
        public void Finds_cyrillic_product_by_cyrillic_or_latin_query(string query)
        {
            Assert.Contains(_cyrillicSale.Id, Search(query));
        }

        [Fact]
        public void Return_receipt_of_product_is_found_by_name_too()
        {
            var ids = Search("kabel");
            Assert.Contains(_returnSale.Id, ids);
        }

        // ---- Shtrix-kod ----
        [Theory]
        [InlineData("4780001111111")]    // to'liq
        [InlineData("47800011")]         // qisman
        [InlineData(" 4780001111111 ")]  // bo'shliq bilan (skaner)
        public void Finds_receipt_by_barcode(string query)
        {
            var ids = Search(query);
            Assert.Contains(_cableSale.Id, ids);
            Assert.DoesNotContain(_cyrillicSale.Id, ids);
        }

        [Fact]
        public void Finds_receipt_of_soft_deleted_product_by_barcode_and_name()
        {
            Assert.Contains(_deletedProductSale.Id, Search("4780003333333"));
            Assert.Contains(_deletedProductSale.Id, Search("rozetka"));
        }

        [Fact]
        public void Long_numeric_barcode_is_not_treated_as_sale_id()
        {
            // 4780009999999 hech qaysi mahsulotda yo'q; chek ID/guid bilan tasodifan mos kelmasligi kerak
            Assert.Empty(Search("4780009999999"));
        }

        // ---- Chek raqami (avvalgi xatti-harakat saqlanishi kerak) ----
        [Fact]
        public void Finds_receipt_by_full_receipt_number()
        {
            Assert.Equal(new[] { _cableSale.Id }, Search(_cableSale.ReceiptNumber));
        }

        [Fact]
        public void Finds_receipt_by_receipt_number_without_prefix_lowercase_and_fragment()
        {
            var hex = _cableSale.Guid.Replace("-", "");
            Assert.Contains(_cableSale.Id, Search(hex.ToLowerInvariant()));
            Assert.Contains(_cableSale.Id, Search("LP-" + hex.Substring(0, 8)));
            Assert.Contains(_cableSale.Id, Search("#" + hex.Substring(4, 8)));
        }

        [Fact]
        public void Finds_return_receipt_by_RT_number()
        {
            Assert.Contains(_returnSale.Id, Search(_returnSale.ReceiptNumber));
        }

        [Fact]
        public void Finds_receipt_by_sale_id()
        {
            Assert.Contains(_cableSale.Id, Search("#" + _cableSale.Id));
            Assert.Contains(_cableSale.Id, Search(_cableSale.Id.ToString()));
        }

        // ---- Chegaraviy holatlar ----
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_query_returns_nothing(string query) => Assert.Empty(Search(query));

        [Theory]
        [InlineData("zzzz-yo'q-mahsulot")]
        [InlineData("%")]
        [InlineData("_")]
        [InlineData("'; DROP TABLE sales; --")]
        public void Unknown_or_special_characters_return_nothing_and_do_not_break(string query)
        {
            Assert.Empty(Search(query));
            Assert.Equal(4, _db.GetSales(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)).Count); // jadval joyida
        }

        [Fact]
        public void Word_made_of_hex_letters_does_not_match_random_guid()
        {
            // "cab" — hex harflaridan iborat, lekin chek raqami deb qaralmasligi kerak
            Assert.Empty(Search("cab"));
        }

        [Fact]
        public void Search_does_not_modify_database()
        {
            var before = _db.GetAllProducts(includeDeleted: true).Count;
            Search("kabel"); Search("4780001111111"); Search("#1");
            Assert.Equal(before, _db.GetAllProducts(includeDeleted: true).Count);
            Assert.Equal(4, _db.GetSales(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)).Count);
        }

        [Fact]
        public void Reopening_existing_database_keeps_all_data()
        {
            // Yangi versiya o'rnatilganda dastur mavjud bazani qayta ochadi — ombor va cheklar saqlanib qolishi shart
            var productsBefore = _db.GetAllProducts(includeDeleted: true).Select(p => (p.Guid, p.Name, p.Barcode, p.StockQuantity)).OrderBy(x => x.Guid).ToList();
            var salesBefore = _db.GetSales(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)).Select(s => (s.Guid, s.TotalAmount, s.Items.Count)).OrderBy(x => x.Guid).ToList();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var reopened = new DatabaseContext(_db.DatabaseFilePath);

            Assert.Equal(productsBefore, reopened.GetAllProducts(includeDeleted: true).Select(p => (p.Guid, p.Name, p.Barcode, p.StockQuantity)).OrderBy(x => x.Guid).ToList());
            Assert.Equal(salesBefore, reopened.GetSales(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(2)).Select(s => (s.Guid, s.TotalAmount, s.Items.Count)).OrderBy(x => x.Guid).ToList());
            Assert.Contains(_cableSale.Id, reopened.SearchSales("kabel").Select(s => s.Id));
        }

        [Theory]
        [InlineData("#15", true, "15", true)]
        [InlineData("15", true, "15", true)]
        [InlineData("LP-AB12CD34", true, "AB12CD34", false)]
        [InlineData("4780001111111", true, "4780001111111", false)]
        [InlineData("cab", false, "", false)]
        [InlineData("kabel", false, "", false)]
        [InlineData("kabel 15", false, "", false)]
        public void Receipt_query_detection(string q, bool expected, string pattern, bool matchId)
        {
            Assert.Equal(expected, DatabaseContext.LooksLikeReceiptQuery(q, out var p, out var id));
            Assert.Equal(pattern, p);
            Assert.Equal(matchId, id);
        }
    }
}
