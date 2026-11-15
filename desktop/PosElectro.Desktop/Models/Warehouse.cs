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
        /// <summary>Qoldiq × sotish narxi (sotuv qiymati). Tan narxi EMAS.</summary>
        public double TotalStockValue { get; set; }
        /// <summary>Qoldiq × tan narxi, so'mda kiritilgan tovarlar bo'yicha.</summary>
        public double StockCostUzs { get; set; }
        /// <summary>Qoldiq × tan narxi, dollarda kiritilgan tovarlar bo'yicha (USD).</summary>
        public double StockCostUsd { get; set; }
        /// <summary>Ombordagi tovarlarning jami tan narxi so'mda (dollarli tovarlar joriy kurs bo'yicha) — Excel hisoboti bilan bir xil hisob.</summary>
        public double TotalStockCost { get; set; }
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
