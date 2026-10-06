using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace PosElectro.Desktop.Debt;

public sealed record DebtAccount(string AccountGuid, string SaleGuid, string CustomerGuid, long CreatedAt, long BalanceMinor);
public sealed record DebtLine(string AccountGuid, long DeltaMinor);
public sealed record DebtAllocation(string AccountGuid, long AmountMinor);
public sealed record DebtTotals(long ReceivableMinor, long CreditMinor);
public sealed record DebtReturnSplit(long DebtOffsetMinor, long RefundMinor);

// Pure arithmetic result, NOT a persisted/authorized event. Repository must attach identity,
// deduplicate, validate ownership and commit atomically. Collections are defensive snapshots.
public sealed class DebtEffect
{
    public IReadOnlyList<DebtLine> Lines { get; }
    public long CashMinor { get; }
    public long CardMinor { get; }
    public long FeeExpenseMinor { get; }
    public long RevenueMinor => 0; // Collections, credit transfers/refunds are never new sales.
    internal DebtEffect(IEnumerable<DebtLine> lines, long cash, long card, long fee)
    {
        Lines = Array.AsReadOnly(lines.ToArray()); CashMinor = cash; CardMinor = card; FeeExpenseMinor = fee;
    }
}

public static class DebtAccounting
{
    // Symmetric signed range so every accepted delta can be reversed (exclude Int64.MinValue).
    private static long Signed(BigInteger value)
    {
        if (value < -new BigInteger(long.MaxValue) || value > long.MaxValue) throw new OverflowException("Debt amount overflow");
        return (long)value;
    }
    private static void Require(bool valid) { if (!valid) throw new ArgumentException("Invalid debt accounting input"); }
    private static void NonNegative(long value) => Require(value >= 0);
    private static void Id(string id) => Require(id != null && Regex.IsMatch(id, "\\A[A-Za-z0-9:_-]{1,128}\\z"));
    private static void Account(DebtAccount account)
    {
        Id(account.AccountGuid); Id(account.SaleGuid); Id(account.CustomerGuid);
        Require(account.CreatedAt >= 0); Signed(account.BalanceMinor);
    }
    private static long Total(long cash, long card)
    {
        NonNegative(cash); NonNegative(card); return Signed((BigInteger)cash + card);
    }

    // Canonical ungrouped UZS input. UI is responsible for removing its own visual grouping.
    // Reject exponent, signs, comma, whitespace and >2 decimals rather than silently rounding.
    public static long ParseUzs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Require(text.Length <= 64 && Regex.IsMatch(text, "\\A[0-9]+(?:\\.[0-9]{1,2})?\\z"));
        var parts = text.Split('.');
        var whole = BigInteger.Parse(parts[0], CultureInfo.InvariantCulture);
        var fraction = parts.Length == 2 ? int.Parse(parts[1].PadRight(2, '0'), CultureInfo.InvariantCulture) : 0;
        return Signed(whole * 100 + fraction);
    }

    public static long NewDebt(long saleMinor, long cashMinor, long cardMinor)
    {
        NonNegative(saleMinor); var paid = Total(cashMinor, cardMinor);
        Require(paid <= saleMinor); return saleMinor - paid;
    }

    // Input must contain each persisted event line once. BigInteger avoids order-dependent
    // intermediate overflow when signed offline events arrive in different orders.
    public static long Balance(long originalDebtMinor, IEnumerable<long> deltas)
    {
        NonNegative(originalDebtMinor); BigInteger sum = originalDebtMinor;
        foreach (var delta in deltas) { Signed(delta); sum += delta; }
        return Signed(sum);
    }

    public static DebtTotals Totals(IEnumerable<DebtAccount> accounts)
    {
        BigInteger debt = 0, credit = 0; var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            Account(account); Require(ids.Add(account.AccountGuid));
            if (account.BalanceMinor > 0) debt += account.BalanceMinor; else credit -= account.BalanceMinor;
        }
        return new(Signed(debt), Signed(credit));
    }

    public static IReadOnlyList<DebtAllocation> Allocate(long paymentMinor, string customerGuid,
        IEnumerable<DebtAccount> accounts, string? targetAccountGuid = null)
    {
        Require(paymentMinor > 0); Id(customerGuid);
        if (targetAccountGuid != null) Id(targetAccountGuid);
        var all = accounts.ToArray(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var sales = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in all) { Account(a); Require(a.CustomerGuid == customerGuid && ids.Add(a.AccountGuid) && sales.Add(a.SaleGuid)); }
        if (targetAccountGuid != null) Require(all.Any(a => a.AccountGuid == targetAccountGuid));
        var ordered = all.Where(a => a.BalanceMinor > 0 && (targetAccountGuid == null || a.AccountGuid == targetAccountGuid))
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.SaleGuid, StringComparer.Ordinal);
        var result = new List<DebtAllocation>(); long remaining = paymentMinor;
        foreach (var a in ordered)
        {
            if (remaining == 0) break;
            var amount = Math.Min(remaining, a.BalanceMinor); result.Add(new(a.AccountGuid, amount)); remaining -= amount;
        }
        Require(remaining == 0); // Reject known excess; never clamp or silently distribute it.
        return result.AsReadOnly();
    }

    public static DebtEffect Payment(long cashMinor, long cardMinor, long feeMinor, IEnumerable<DebtAllocation> allocations)
    {
        var total = Total(cashMinor, cardMinor); Require(total > 0); NonNegative(feeMinor); Require(feeMinor <= cardMinor);
        var lines = new List<DebtLine>(); var ids = new HashSet<string>(StringComparer.Ordinal); BigInteger assigned = 0;
        foreach (var a in allocations)
        {
            Id(a.AccountGuid); Require(a.AmountMinor > 0 && ids.Add(a.AccountGuid));
            assigned += a.AmountMinor; lines.Add(new(a.AccountGuid, -a.AmountMinor));
        }
        Require(assigned == total); return new(lines, cashMinor, cardMinor, feeMinor);
    }

    public static DebtEffect ReversePayment(DebtEffect original, long refundedFeeMinor)
    {
        // Validate the supplied effect is a payment, not a refund, transfer or reversal.
        Payment(original.CashMinor, original.CardMinor, original.FeeExpenseMinor,
            original.Lines.Select(l => new DebtAllocation(l.AccountGuid, Signed(-(BigInteger)l.DeltaMinor))));
        NonNegative(refundedFeeMinor); Require(refundedFeeMinor <= original.FeeExpenseMinor);
        return new(original.Lines.Select(l => new DebtLine(l.AccountGuid, -l.DeltaMinor)),
            -original.CashMinor, -original.CardMinor, -refundedFeeMinor);
    }

    // Caller validates remaining returnable goods/value and commits through LAN authority.
    public static DebtReturnSplit SplitReturn(long returnedValueMinor, long currentBalanceMinor)
    {
        Require(returnedValueMinor > 0); Signed(currentBalanceMinor);
        var offset = Math.Min(returnedValueMinor, Math.Max(0, currentBalanceMinor));
        return new(offset, returnedValueMinor - offset);
    }

    public static DebtEffect TransferCredit(DebtAccount source, DebtAccount target, long amountMinor)
    {
        Account(source); Account(target); Require(amountMinor > 0);
        Require(source.CustomerGuid == target.CustomerGuid && source.AccountGuid != target.AccountGuid && source.SaleGuid != target.SaleGuid);
        Require(source.BalanceMinor < 0 && amountMinor <= -source.BalanceMinor && amountMinor <= target.BalanceMinor);
        return new(new[] { new DebtLine(source.AccountGuid, amountMinor), new DebtLine(target.AccountGuid, -amountMinor) }, 0, 0, 0);
    }

    public static DebtEffect RefundCredit(DebtAccount account, long cashMinor, long cardMinor)
    {
        Account(account); var total = Total(cashMinor, cardMinor);
        Require(total > 0 && account.BalanceMinor < 0 && total <= -account.BalanceMinor);
        return new(new[] { new DebtLine(account.AccountGuid, total) }, -cashMinor, -cardMinor, 0);
    }
}
