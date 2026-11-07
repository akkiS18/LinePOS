using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PosElectro.Desktop.Models
{
    public class Product : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public long Id { get; set; }
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Barchasi";
        public double CostPrice { get; set; }
        public string CostCurrency { get; set; } = "UZS";
        public double SellingPrice { get; set; }
        public double? SellingPrice2 { get; set; }
        public double StockQuantity { get; set; }
        public UnitType UnitType { get; set; } = UnitType.DONA;
        public double MinStockAlert { get; set; } = 3.0;
        public bool IsDeleted { get; set; } = false;
        public string Note { get; set; } = string.Empty;
        private string? _imagePath;
        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                if (_imagePath != value)
                {
                    _imagePath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasImage));
                }
            }
        }
        public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath) && System.IO.File.Exists(ImagePath);
        public string? WarehouseGuid { get; set; }
        public string? WarehouseName { get; set; }
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private string? _normalizedName;
        public string NormalizedName => _normalizedName ??= NormalizeSearchText(Name);

        private string? _normalizedBarcode;
        public string NormalizedBarcode => _normalizedBarcode ??= (Barcode?.Trim().ToLowerInvariant() ?? string.Empty);

        private string? _normalizedNote;
        public string NormalizedNote => _normalizedNote ??= (Note != null ? NormalizeSearchText(Note) : string.Empty);

        private static string NormalizeSearchText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim().ToLowerInvariant();
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[''`ʹʻʼ’‘]", "'");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<=\d),(?=\d)", ".");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
            return s;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public int WarehouseOrderIndex { get; set; } = 1;
        public string WarehouseBadgeDisplay => !string.IsNullOrWhiteSpace(WarehouseName)
            ? (WarehouseOrderIndex > 0 ? $"{WarehouseOrderIndex}. {WarehouseName}" : WarehouseName)
            : string.Empty;

        // UI yordamchi maydonlar
        public bool HasWarehouse => !string.IsNullOrWhiteSpace(WarehouseName);
        public string WarehouseDisplay => string.IsNullOrWhiteSpace(WarehouseName) ? string.Empty : $"🏢 {WarehouseBadgeDisplay}";
        public bool HasSellingPrice2 => SellingPrice2.HasValue && SellingPrice2.Value > 0;
        public string SellingPrice2Display => HasSellingPrice2 ? $"{SellingPrice2!.Value:N0} so'm" : "—";
        public bool IsLowStock => StockQuantity <= MinStockAlert;
        public bool IsNegativeStock => StockQuantity < 0;
        public string StockDisplay => IsNegativeStock ? $"⚠️ {StockQuantity}" : StockQuantity.ToString();
        public string CostPriceDisplay => CostCurrency == "USD" ? $"${CostPrice:N2}" : $"{CostPrice:N0} so'm";
        public string UnitDisplay => UnitType switch
        {
            UnitType.DONA => "dona",
            UnitType.METR => "metr",
            UnitType.KG => "kg",
            _ => "dona"
        };

        public Product CloneForWarehouse(string whGuid, string whName, int orderIndex, double stock)
        {
            return new Product
            {
                Id = this.Id,
                Guid = this.Guid,
                Barcode = this.Barcode,
                Name = this.Name,
                Category = this.Category,
                CostPrice = this.CostPrice,
                CostCurrency = this.CostCurrency,
                SellingPrice = this.SellingPrice,
                SellingPrice2 = this.SellingPrice2,
                StockQuantity = stock,
                UnitType = this.UnitType,
                MinStockAlert = this.MinStockAlert,
                IsDeleted = this.IsDeleted,
                Note = this.Note,
                WarehouseGuid = whGuid,
                WarehouseName = whName,
                WarehouseOrderIndex = orderIndex,
                UpdatedAt = this.UpdatedAt
            };
        }
    }
}
