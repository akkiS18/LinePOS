using System;

namespace PosElectro.Desktop.Models;

/// <summary>Calculates incremental refunds from immutable, allocated sale-line amounts.
/// Call under the same write transaction that reads prior returns and records the result.</summary>
public static class ReturnAccounting
{
    public sealed record Prior(decimal Quantity, decimal Refund, decimal CostBasis);
    public sealed record Amounts(decimal Quantity, decimal Refund, decimal CostBasis, decimal CostReversal)
    {
        public decimal ProfitEffect(decimal feeReversal) => -Refund + CostReversal + feeReversal;
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static Amounts Calculate(decimal soldQuantity, decimal lineRevenue, decimal lineCost,
        Prior prior, decimal quantity, bool resellable)
    {
        if (soldQuantity <= 0 || lineRevenue < 0 || lineCost < 0)
            throw new ArgumentException("Asl savdo qatori qaytarish uchun yaroqsiz");
        if (prior.Quantity < 0 || prior.Quantity > soldQuantity || prior.Refund < 0 ||
            prior.Refund > Money(lineRevenue) || prior.CostBasis < 0 || prior.CostBasis > Money(lineCost))
            throw new ArgumentException("Oldingi qaytarishlar tekshirilishi kerak");
        if (quantity <= 0 || quantity > soldQuantity - prior.Quantity)
            throw new ArgumentException("Qaytarish miqdori qolgan miqdordan oshmasligi kerak");

        // Calculate a cumulative target, then subtract previously recorded amounts.
        // The final fragment consumes the exact remainder, independent of fragmentation.
        var cumulativeQuantity = prior.Quantity + quantity;
        var refund = Money(lineRevenue * cumulativeQuantity / soldQuantity) - prior.Refund;
        var basis = Money(lineCost * cumulativeQuantity / soldQuantity) - prior.CostBasis;
        if (refund < 0 || basis < 0)
            throw new ArgumentException("Oldingi qaytarish summalari mos emas");
        return new Amounts(quantity, refund, basis, resellable ? basis : 0);
    }

    public static void ValidatePayment(decimal refund, decimal cash, decimal card,
        decimal feeReversal, decimal originalFee, decimal previouslyReversedFee)
    {
        if (cash < 0 || card < 0 || cash != Money(cash) || card != Money(card) || cash + card != refund)
            throw new ArgumentException("Naqd va karta yig'indisi qaytarish summasiga teng bo'lishi kerak");
        if (feeReversal < 0 || feeReversal != Money(feeReversal) || previouslyReversedFee < 0 ||
            feeReversal + previouslyReversedFee > originalFee)
            throw new ArgumentException("Qaytarilgan komissiya asl komissiyadan oshmasligi kerak");
    }
}
