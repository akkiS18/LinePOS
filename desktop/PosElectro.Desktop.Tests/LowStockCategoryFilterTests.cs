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
    /// <summary>Ombor → "Kam qolgan tovarlar": kategoriya bo'yicha filtr (1.0.1 da tugma bor edi, lekin ishlamas edi).</summary>
    public sealed class LowStockCategoryFilterTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-lowstock-" + Guid.NewGuid().ToString("N"));
        private readonly DatabaseContext _db;
        private readonly InventoryViewModel _vm;

        public LowStockCategoryFilterTests()
        {
            Directory.CreateDirectory(_dir);
            _db = new DatabaseContext(Path.Combine(_dir, "t.db"));
            Add("Kabel VVG 3x2.5", "Kabel", stock: 1);
            Add("Kabel PVS 2x1.5", "Kabel", stock: 2);
            Add("Rozetka ichki", "Rozetka", stock: 0);
            Add("Nomsiz tovar", "", stock: 1);
            Add("Lampa LED", "Lampa", stock: 100);   // kam qolmagan
            Add("Kabel ShVVP", "Kabel", stock: 500); // kam qolmagan

            _vm = new InventoryViewModel(_db, new ProductService(_db), new CurrencyService(_db));
            _vm.Refresh();
            _vm.SelectedCategory = ProductService.CATEGORY_LOW_STOCK;
        }

        private void Add(string name, string category, double stock) =>
            _db.SaveProduct(new Product
            {
                Name = name, Category = category, CostPrice = 1000, SellingPrice = 1500,
                StockQuantity = stock, MinStockAlert = 3
            });

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private LowStockCategoryFilterItem Filter(string name) => _vm.LowStockCategoryFilters.Single(f => f.Name == name);

        [Fact]
        public void Filter_list_contains_only_categories_that_have_low_stock_products()
        {
            Assert.Equal(new[] { "Kabel", "Kategoriyasiz", "Rozetka" }, _vm.LowStockCategoryFilters.Select(f => f.Name).ToArray());
            Assert.Equal(2, Filter("Kabel").Count);
            Assert.Equal(1, Filter("Rozetka").Count);
            Assert.Equal("Kategoriya: barchasi", _vm.SelectedCategoriesFilterText);
            Assert.Equal(4, _vm.Products.Count);
        }

        [Fact]
        public void Checking_a_category_shows_only_its_low_stock_products()
        {
            Filter("Kabel").IsSelected = true;

            Assert.Equal(new[] { "Kabel PVS 2x1.5", "Kabel VVG 3x2.5" }, _vm.Products.Select(p => p.Name).OrderBy(n => n).ToArray());
            Assert.Equal("Kabel", _vm.SelectedCategoriesFilterText);
        }

        [Fact]
        public void Several_categories_can_be_combined_and_cleared()
        {
            Filter("Kabel").IsSelected = true;
            Filter("Rozetka").IsSelected = true;
            Assert.Equal(3, _vm.Products.Count);
            Assert.Equal("Kategoriya: 2 ta", _vm.SelectedCategoriesFilterText);

            _vm.ClearCategoryFiltersCommand.Execute(null);
            Assert.Equal(4, _vm.Products.Count);
            Assert.All(_vm.LowStockCategoryFilters, f => Assert.False(f.IsSelected));
            Assert.Equal("Kategoriya: barchasi", _vm.SelectedCategoriesFilterText);
        }

        [Fact]
        public void Products_without_category_can_be_filtered()
        {
            Filter("Kategoriyasiz").IsSelected = true;
            Assert.Equal("Nomsiz tovar", Assert.Single(_vm.Products).Name);
        }

        [Fact]
        public void Header_checkbox_selects_only_the_filtered_rows()
        {
            Filter("Kabel").IsSelected = true;
            Assert.False(_vm.IsAllLowStockSelected);

            _vm.IsAllLowStockSelected = true;
            Assert.True(_vm.IsAllLowStockSelected);
            Assert.Equal(2, _vm.SelectedLowStockCount);

            _vm.ClearCategoryFiltersCommand.Execute(null);
            Assert.Equal(4, _vm.Products.Count);
            Assert.False(_vm.IsAllLowStockSelected);
            Assert.Equal(2, _vm.SelectedLowStockCount);
        }

        [Fact]
        public void Leaving_low_stock_view_resets_the_filter()
        {
            Filter("Kabel").IsSelected = true;
            _vm.SelectedCategory = ProductService.CATEGORY_ALL;
            Assert.Equal(6, _vm.Products.Count);

            _vm.SelectedCategory = ProductService.CATEGORY_LOW_STOCK;
            Assert.Equal(4, _vm.Products.Count);
        }

        [Fact]
        public void Filter_survives_refresh()
        {
            Filter("Kabel").IsSelected = true;
            _vm.Refresh();
            Assert.True(Filter("Kabel").IsSelected);
            Assert.Equal(2, _vm.Products.Count);
        }
    }
}
