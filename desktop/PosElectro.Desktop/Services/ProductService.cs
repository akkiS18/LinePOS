using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Services
{
    public class ProductService
    {
        public const string CATEGORY_ALL = "Barchasi";
        public const string CATEGORY_LOW_STOCK = "Kam qolgan tovarlar";

        private readonly DatabaseContext _db;

        public ProductService(DatabaseContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Aqlli nom normallashtirish (kichik harflar, bo'shliqlarni bittaga keltirish, apostroflarni standartlash)
        /// Android ProductViewModel dagi aqlli tekshiruv bilan 100% bir xil ishlaydi.
        /// </summary>
        public static string NormalizeProductName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input.Trim().ToLowerInvariant();
            // Barcha turdagi apostroflarni (' ' ` ʹ ’ ‘ ʻ ʼ) bitta standart ' ga keltirish
            s = Regex.Replace(s, @"[''`ʹʻʼ’‘]", "'");
            // Ketma-ket kelgan bo'shliqlarni bitta bo'shliqqa aylantirish
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        public Product? FindDuplicateProduct(string name, long currentProductId = 0)
        {
            var normalizedTarget = NormalizeProductName(name);
            if (string.IsNullOrEmpty(normalizedTarget)) return null;

            var allProducts = _db.GetAllProducts(includeDeleted: false);
            return allProducts.FirstOrDefault(p => 
                p.Id != currentProductId && 
                NormalizeProductName(p.Name) == normalizedTarget
            );
        }

        public List<Product> GetProductsByCategory(string category)
        {
            var all = _db.GetAllProducts(includeDeleted: false);

            if (category == CATEGORY_LOW_STOCK)
            {
                return all.Where(p => p.IsLowStock).ToList();
            }
            if (category == CATEGORY_ALL || string.IsNullOrWhiteSpace(category))
            {
                return all;
            }

            return all.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<string> GetCategories()
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                CATEGORY_ALL,
                CATEGORY_LOW_STOCK
            };

            var dbCategories = _db.GetAllProducts()
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c) && 
                            c != CATEGORY_ALL && 
                            c != CATEGORY_LOW_STOCK)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var c in dbCategories)
            {
                categories.Add(c);
            }

            return categories.ToList();
        }

        public void SaveProduct(Product product)
        {
            var duplicate = FindDuplicateProduct(product.Name, product.Id);
            if (duplicate != null)
            {
                throw new InvalidOperationException($"'{duplicate.Name}' nomli mahsulot omborda allaqachon mavjud!");
            }

            _db.SaveProduct(product);
        }

        public void DeleteProduct(long id)
        {
            var product = _db.GetAllProducts().FirstOrDefault(p => p.Id == id);
            if (product != null)
            {
                product.IsDeleted = true;
                _db.SaveProduct(product);
            }
        }

        public DatabaseContext Database => _db;

        public string GenerateUniqueBarcode() => BarcodeGeneratorHelper.GenerateUniqueEan13(_db);
    }
}
