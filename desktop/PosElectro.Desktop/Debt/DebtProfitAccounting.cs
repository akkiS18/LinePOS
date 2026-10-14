using System;
using System.Collections.Generic;
using System.Linq;

namespace PosElectro.Desktop.Debt;

/// <summary>Read-only cumulative recognition; amounts are integer UZS minor units.</summary>
public static class DebtProfitAccounting
{
    public sealed record Line(long Revenue, long Cost, long ReturnedRevenue = 0,
        long ReturnedCostBasis = 0, long RestoredCost = 0);
    static long Round(decimal n) => checked((long)Math.Round(n, 0, MidpointRounding.AwayFromZero));
    public static List<long> Gross(IReadOnlyList<Line> lines, long balanceMinor)
    {
        if (lines.Count == 0) throw new ArgumentException("Qarz foydasi uchun chek qatorlari kerak");
        var remaining = lines.Select(l => {
            if (l.Revenue < 0 || l.Cost < 0 || l.ReturnedRevenue < 0 || l.ReturnedRevenue > l.Revenue ||
                l.ReturnedCostBasis < 0 || l.ReturnedCostBasis > l.Cost || l.RestoredCost < 0 || l.RestoredCost > l.ReturnedCostBasis)
                throw new ArgumentException("Qaytarish tarixi foyda hisobiga mos emas");
            return checked(l.Revenue - l.ReturnedRevenue);
        }).ToArray();
        var revenue = remaining.Aggregate(0L, (a,b) => checked(a+b));
        var paid = balanceMinor <= 0 ? revenue : checked(revenue - Math.Min(balanceMinor,revenue));
        return lines.Select((l,i) => {
            var margin = checked(remaining[i] - checked(l.Cost - l.ReturnedCostBasis));
            var recognised = revenue == 0 ? 0 : Round(Math.Round((decimal)margin * paid / revenue,16,MidpointRounding.AwayFromZero));
            return checked(recognised - checked(l.ReturnedCostBasis - l.RestoredCost));
        }).ToList();
    }
    public static List<long> Allocate(long amount, IReadOnlyList<long> weights)
    {
        if (weights.Count == 0 || weights.Any(w => w < 0)) throw new ArgumentException("Taqsimlash vaznlari noto‘g‘ri");
        var sum = weights.Aggregate(0L,(a,b)=>checked(a+b));
        decimal cumulative=0; long previous=0;
        return weights.Select((w,i) => {
            cumulative += sum == 0 ? 1 : w;
            var target = i == weights.Count-1 ? amount : Round(Math.Round(amount*cumulative/(sum==0?weights.Count:sum),16,MidpointRounding.AwayFromZero));
            var part=checked(target-previous); previous=target; return part;
        }).ToList();
    }
}
