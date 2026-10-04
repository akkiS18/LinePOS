using System;

namespace PosElectro.Desktop.Models
{
    public class SaleReportItem
    {
        public string ReceiptNumber { get; set; } = "";
        public string WarehouseGuid { get; set; } = "";
        public double TotalCost { get; set; }
        public double TaxAmount { get; set; }
        public double CashAmount { get; set; }
        public double CardAmount { get; set; }
        public double? ProfitUsd { get; set; }
        public bool IsReturn { get; set; }
        public bool IsBrak { get; set; }
        public long SaleId { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = "Barchasi";
        public string WarehouseName { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public UnitType UnitType { get; set; } = UnitType.DONA;
        public double CostPrice { get; set; } // So'mda (dollar bo'lsa kursga ko'paytirilgan)
        public string CostCurrency { get; set; } = "UZS";
        public double OriginalCost { get; set; } // Asl kiritilgan tan narxi ($ yoki so'm)
        public double SellingPrice { get; set; }
        public double TotalPrice { get; set; }
        public double Profit { get; set; }
        public long Timestamp { get; set; }

        public DateTime DateTime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).LocalDateTime;
        public string UnitDisplay => UnitType switch
        {
            UnitType.DONA => "Dona",
            UnitType.METR => "Metr",
            UnitType.KG => "Kg",
            _ => "Dona"
        };
    }
}
