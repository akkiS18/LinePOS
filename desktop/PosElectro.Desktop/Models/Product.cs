using System;

namespace PosElectro.Desktop.Models
{
    public class Product
    {
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
        public string? WarehouseGuid { get; set; }
        public string? WarehouseName { get; set; }
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // UI yordamchi maydonlar
        public bool HasWarehouse => !string.IsNullOrWhiteSpace(WarehouseName);
        public string WarehouseDisplay => string.IsNullOrWhiteSpace(WarehouseName) ? string.Empty : $"🏢 {WarehouseName}";
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
    }
}
