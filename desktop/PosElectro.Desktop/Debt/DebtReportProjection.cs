using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PosElectro.Desktop.Debt;

public sealed record DebtPeriodSummary(
    long RangeStartMs,
    long RangeEndMs,
    long OpeningDebtMinor,
    long OpeningCreditMinor,
    long NewDebtIssuedMinor,
    long DebtPaymentsCashMinor,
    long DebtPaymentsCardMinor,
    long DebtPaymentsFeeMinor,
    long DebtPaymentsTotalMinor,
    long PaymentReversalsCashMinor,
    long PaymentReversalsCardMinor,
    long PaymentReversalsFeeMinor,
    long DebtReturnOffsetMinor,
    long CreditRefundCashMinor,
    long CreditRefundCardMinor,
    long ClosingDebtMinor,
    long ClosingCreditMinor,
    int ActiveDebtorsCount,
    int OverdueDebtorsCount)
{
    public double OpeningDebtUz => OpeningDebtMinor / 100.0;
    public double OpeningCreditUz => OpeningCreditMinor / 100.0;
    public double NewDebtIssuedUz => NewDebtIssuedMinor / 100.0;
    public double DebtPaymentsCashUz => DebtPaymentsCashMinor / 100.0;
    public double DebtPaymentsCardUz => DebtPaymentsCardMinor / 100.0;
    public double DebtPaymentsFeeUz => DebtPaymentsFeeMinor / 100.0;
    public double DebtPaymentsTotalUz => DebtPaymentsTotalMinor / 100.0;
    public double NetDebtCashCollectedUz => (DebtPaymentsCashMinor - PaymentReversalsCashMinor) / 100.0;
    public double NetDebtCardCollectedUz => (DebtPaymentsCardMinor - PaymentReversalsCardMinor) / 100.0;
    public double NetDebtFeeUz => (DebtPaymentsFeeMinor - PaymentReversalsFeeMinor) / 100.0;
    public double NetDebtCollectedTotalUz => NetDebtCashCollectedUz + NetDebtCardCollectedUz;
    public double DebtReturnOffsetUz => DebtReturnOffsetMinor / 100.0;
    public double CreditRefundCashUz => CreditRefundCashMinor / 100.0;
    public double CreditRefundCardUz => CreditRefundCardMinor / 100.0;
    public double ClosingDebtUz => ClosingDebtMinor / 100.0;
    public double ClosingCreditUz => ClosingCreditMinor / 100.0;
}

public static class DebtReportProjection
{
    public static DebtPeriodSummary Query(SqliteConnection conn, long startMs, long endMs)
    {
        var (openingDebt, openingCredit) = QueryBalancesAt(conn, startMs);
        var (closingDebt, closingCredit, activeCount, overdueCount) = QueryBalancesAtWithOverdue(conn, endMs);

        // 1. New debt issued from accounts created in [startMs, endMs)
        long newDebtIssued = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(a.original_debt_minor), 0)
                FROM debt_accounts a
                JOIN debt_events op ON op.guid = a.opening_event_guid
                WHERE op.occurred_at >= @start AND op.occurred_at < @end";
            cmd.Parameters.AddWithValue("@start", startMs);
            cmd.Parameters.AddWithValue("@end", endMs);
            newDebtIssued = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }

        // 2. Events in [startMs, endMs)
        long paymentsCash = 0, paymentsCard = 0, paymentsFee = 0;
        long reversalsCash = 0, reversalsCard = 0, reversalsFee = 0;
        long refundCash = 0, refundCard = 0;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT kind,
                       COALESCE(SUM(cash_minor), 0),
                       COALESCE(SUM(card_minor), 0),
                       COALESCE(SUM(fee_minor), 0)
                FROM debt_events
                WHERE occurred_at >= @start AND occurred_at < @end
                GROUP BY kind";
            cmd.Parameters.AddWithValue("@start", startMs);
            cmd.Parameters.AddWithValue("@end", endMs);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var kind = reader.GetString(0);
                var cash = reader.GetInt64(1);
                var card = reader.GetInt64(2);
                var fee = reader.GetInt64(3);

                switch (kind)
                {
                    case "payment":
                        paymentsCash += cash;
                        paymentsCard += card;
                        paymentsFee += fee;
                        break;
                    case "payment_reversal":
                        reversalsCash += cash;
                        reversalsCard += card;
                        reversalsFee += fee;
                        break;
                    case "credit_refund":
                        refundCash += cash;
                        refundCard += card;
                        break;
                }
            }
        }

        // 3. Debt return offsets in [startMs, endMs)
        long returnOffset = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(-l.debt_delta_minor), 0)
                FROM debt_event_lines l
                JOIN debt_events e ON e.guid = l.event_guid
                WHERE e.kind = 'return_offset' AND e.occurred_at >= @start AND e.occurred_at < @end";
            cmd.Parameters.AddWithValue("@start", startMs);
            cmd.Parameters.AddWithValue("@end", endMs);
            returnOffset = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }

        return new DebtPeriodSummary(
            RangeStartMs: startMs,
            RangeEndMs: endMs,
            OpeningDebtMinor: openingDebt,
            OpeningCreditMinor: openingCredit,
            NewDebtIssuedMinor: newDebtIssued,
            DebtPaymentsCashMinor: paymentsCash,
            DebtPaymentsCardMinor: paymentsCard,
            DebtPaymentsFeeMinor: paymentsFee,
            DebtPaymentsTotalMinor: paymentsCash + paymentsCard,
            PaymentReversalsCashMinor: reversalsCash,
            PaymentReversalsCardMinor: reversalsCard,
            PaymentReversalsFeeMinor: reversalsFee,
            DebtReturnOffsetMinor: returnOffset,
            CreditRefundCashMinor: refundCash,
            CreditRefundCardMinor: refundCard,
            ClosingDebtMinor: closingDebt,
            ClosingCreditMinor: closingCredit,
            ActiveDebtorsCount: activeCount,
            OverdueDebtorsCount: overdueCount);
    }

    private static (long Debt, long Credit) QueryBalancesAt(SqliteConnection conn, long timeMs)
    {
        var customerBalances = new Dictionary<string, long>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.customer_guid,
                   a.original_debt_minor + COALESCE((
                       SELECT SUM(l.debt_delta_minor)
                       FROM debt_event_lines l
                       JOIN debt_events e ON e.guid = l.event_guid
                       WHERE l.account_guid = a.guid AND e.occurred_at < @time
                   ), 0) AS bal
            FROM debt_accounts a
            JOIN debt_events op ON op.guid = a.opening_event_guid
            WHERE op.occurred_at < @time";
        cmd.Parameters.AddWithValue("@time", timeMs);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var cust = reader.GetString(0);
            var bal = reader.GetInt64(1);
            if (!customerBalances.TryGetValue(cust, out var cur)) cur = 0;
            customerBalances[cust] = cur + bal;
        }

        long totalDebt = 0;
        long totalCredit = 0;
        foreach (var b in customerBalances.Values)
        {
            if (b > 0) totalDebt += b;
            else if (b < 0) totalCredit += -b;
        }

        return (totalDebt, totalCredit);
    }

    private static (long Debt, long Credit, int ActiveCount, int OverdueCount) QueryBalancesAtWithOverdue(SqliteConnection conn, long timeMs)
    {
        var customerBalances = new Dictionary<string, long>();
        var customerHasOverdue = new Dictionary<string, bool>();

        // Local date string for overdue check at timeMs:
        var dtStr = DateTimeOffset.FromUnixTimeMilliseconds(timeMs).LocalDateTime.ToString("yyyy-MM-dd");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.customer_guid,
                   a.due_date,
                   a.original_debt_minor + COALESCE((
                       SELECT SUM(l.debt_delta_minor)
                       FROM debt_event_lines l
                       JOIN debt_events e ON e.guid = l.event_guid
                       WHERE l.account_guid = a.guid AND e.occurred_at < @time
                   ), 0) AS bal
            FROM debt_accounts a
            JOIN debt_events op ON op.guid = a.opening_event_guid
            WHERE op.occurred_at < @time";
        cmd.Parameters.AddWithValue("@time", timeMs);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var cust = reader.GetString(0);
            var dueDate = reader.IsDBNull(1) ? null : reader.GetString(1);
            var bal = reader.GetInt64(2);

            if (!customerBalances.TryGetValue(cust, out var cur)) cur = 0;
            customerBalances[cust] = cur + bal;

            if (bal > 0 && dueDate != null && string.CompareOrdinal(dueDate, dtStr) < 0)
            {
                customerHasOverdue[cust] = true;
            }
        }

        long totalDebt = 0;
        long totalCredit = 0;
        int activeCount = 0;
        int overdueCount = 0;

        foreach (var kv in customerBalances)
        {
            var b = kv.Value;
            if (b > 0)
            {
                totalDebt += b;
                activeCount++;
                if (customerHasOverdue.TryGetValue(kv.Key, out var isOv) && isOv)
                {
                    overdueCount++;
                }
            }
            else if (b < 0)
            {
                totalCredit += -b;
            }
        }

        return (totalDebt, totalCredit, activeCount, overdueCount);
    }
}
