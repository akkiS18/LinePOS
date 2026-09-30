using System;

namespace PosElectro.Desktop.Models
{
    public class Warehouse
    {
        public long Id { get; set; }
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsPrimary { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // UI yordamchi maydonlar
        public int TotalProductCount { get; set; }
        public double TotalStockQuantity { get; set; }
        public double TotalStockValue { get; set; }
        public string PrimaryBadge => IsPrimary ? "ASOSIY OMBOR" : string.Empty;
        public string SummaryDisplay => $"{TotalProductCount} xil tovar | Qoldiq: {TotalStockQuantity:N0}";
        public bool CanDelete => !IsPrimary;
        public bool CanSetPrimary => !IsPrimary;
    }

    public class ProductStock
    {
        public long Id { get; set; }
        public string ProductGuid { get; set; } = string.Empty;
        public string WarehouseGuid { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public class StockTransferEvent
    {
        public string ProductGuid { get; set; } = string.Empty;
        public string FromWarehouseGuid { get; set; } = string.Empty;
        public string ToWarehouseGuid { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
