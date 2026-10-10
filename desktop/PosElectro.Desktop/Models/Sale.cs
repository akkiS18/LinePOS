using System;
using System.Collections.Generic;

namespace PosElectro.Desktop.Models
{
    public class Sale
    {
        public long Id { get; set; }
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string ReceiptNumber => (PaymentType == PaymentType.RETURN ? "RT-" : PaymentType == PaymentType.RETURN_REVERSAL ? "RV-" : "LP-") + Guid.Replace("-", "").ToUpperInvariant();
        public string OriginalReceiptNumber { get; set; } = string.Empty;
        public double UsdRate { get; set; }
        public double TotalAmount { get; set; }
        public double TotalCost { get; set; }
        public PaymentType PaymentType { get; set; } = PaymentType.CASH;
        public double CashAmount { get; set; }
        public double CardAmount { get; set; }
        public double TaxAmount { get; set; }
        public double TaxRate { get; set; } = 1.8;
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long UserId { get; set; } = 1L;
        public bool IsSynced { get; set; } = false;

        public List<SaleItem> Items { get; set; } = new();

        public DateTime CreatedDateTime => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).LocalDateTime;
        public double NetProfit => (TotalAmount - TaxAmount) - TotalCost;
        public double Profit => NetProfit;
        public bool IsProfitNegative => Profit < 0;
        public string ProfitDisplay => Profit < 0
            ? $"{Profit:N0} so'm"
            : $"+{Profit:N0} so'm";
        public string PaymentTypeDisplay => PaymentType switch
        {
            PaymentType.CASH => "Naqd",
            PaymentType.CARD => "Karta",
            PaymentType.SPLIT => $"Aralash (N: {CashAmount:N0} / K: {CardAmount:N0})",
            PaymentType.DEBT => "Nasiya",
            PaymentType.RETURN => "Qaytarish",
            PaymentType.RETURN_REVERSAL => "Qaytarishni bekor qilish",
            PaymentType.BRAK => "⚠️ Brak (Spisanie)",
            _ => PaymentType.ToString()
        };
    }

    public class SaleItem
    {
        public string Guid { get; set; } = string.Empty;
        public long Id { get; set; }
        public long SaleId { get; set; }
        public string SaleGuid { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public string ProductGuid { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryAtSale { get; set; } = string.Empty;
        public string UnitAtSale { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double PriceAtSale { get; set; }
        public double CostAtSale { get; set; }
        public string CostCurrency { get; set; } = "UZS";
        public string WarehouseGuid { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;

        public double TotalPrice => Quantity * PriceAtSale;
        public string CostDisplay => CostCurrency == "USD" ? $"${CostAtSale:N2}" : $"{CostAtSale:N0} so'm";
    }
}
