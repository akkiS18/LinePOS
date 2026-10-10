package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase

data class DebtPeriodSummary(
    val rangeStartMs: Long,
    val rangeEndMs: Long,
    val openingDebtMinor: Long,
    val openingCreditMinor: Long,
    val newDebtIssuedMinor: Long,
    val debtPaymentsCashMinor: Long,
    val debtPaymentsCardMinor: Long,
    val debtPaymentsFeeMinor: Long,
    val debtPaymentsTotalMinor: Long,
    val paymentReversalsCashMinor: Long,
    val paymentReversalsCardMinor: Long,
    val paymentReversalsFeeMinor: Long,
    val debtReturnOffsetMinor: Long,
    val creditRefundCashMinor: Long,
    val creditRefundCardMinor: Long,
    val closingDebtMinor: Long,
    val closingCreditMinor: Long,
    val activeDebtorsCount: Int,
    val overdueDebtorsCount: Int
) {
    val openingDebtUz: Double get() = openingDebtMinor / 100.0
    val openingCreditUz: Double get() = openingCreditMinor / 100.0
    val newDebtIssuedUz: Double get() = newDebtIssuedMinor / 100.0
    val debtPaymentsCashUz: Double get() = debtPaymentsCashMinor / 100.0
    val debtPaymentsCardUz: Double get() = debtPaymentsCardMinor / 100.0
    val debtPaymentsFeeUz: Double get() = debtPaymentsFeeMinor / 100.0
    val debtPaymentsTotalUz: Double get() = debtPaymentsTotalMinor / 100.0
    val netDebtCashCollectedUz: Double get() = (debtPaymentsCashMinor - paymentReversalsCashMinor) / 100.0
    val netDebtCardCollectedUz: Double get() = (debtPaymentsCardMinor - paymentReversalsCardMinor) / 100.0
    val netDebtFeeUz: Double get() = (debtPaymentsFeeMinor - paymentReversalsFeeMinor) / 100.0
    val netDebtCollectedTotalUz: Double get() = netDebtCashCollectedUz + netDebtCardCollectedUz
    val debtReturnOffsetUz: Double get() = debtReturnOffsetMinor / 100.0
    val creditRefundCashUz: Double get() = creditRefundCashMinor / 100.0
    val creditRefundCardUz: Double get() = creditRefundCardMinor / 100.0
    val closingDebtUz: Double get() = closingDebtMinor / 100.0
    val closingCreditUz: Double get() = closingCreditMinor / 100.0
}

object DebtReportProjection {

    fun query(db: SupportSQLiteDatabase, startMs: Long, endMs: Long): DebtPeriodSummary {
        val (openingDebt, openingCredit) = queryBalancesAt(db, startMs)
        val (closingDebt, closingCredit, activeCount, overdueCount) = queryBalancesAtWithOverdue(db, endMs)

        var newDebtIssued = 0L
        db.query(
            """
            SELECT COALESCE(SUM(a.original_debt_minor), 0)
            FROM debt_accounts a
            JOIN debt_events op ON op.guid = a.opening_event_guid
            WHERE op.occurred_at >= ? AND op.occurred_at < ?
            """.trimIndent(),
            arrayOf(startMs, endMs)
        ).use { c ->
            if (c.moveToFirst()) {
                newDebtIssued = c.getLong(0)
            }
        }

        var paymentsCash = 0L
        var paymentsCard = 0L
        var paymentsFee = 0L
        var reversalsCash = 0L
        var reversalsCard = 0L
        var reversalsFee = 0L
        var refundCash = 0L
        var refundCard = 0L

        db.query(
            """
            SELECT kind,
                   COALESCE(SUM(cash_minor), 0),
                   COALESCE(SUM(card_minor), 0),
                   COALESCE(SUM(fee_minor), 0)
            FROM debt_events
            WHERE occurred_at >= ? AND occurred_at < ?
            GROUP BY kind
            """.trimIndent(),
            arrayOf(startMs, endMs)
        ).use { c ->
            while (c.moveToNext()) {
                val kind = c.getString(0)
                val cash = c.getLong(1)
                val card = c.getLong(2)
                val fee = c.getLong(3)
                when (kind) {
                    "payment" -> {
                        paymentsCash += cash
                        paymentsCard += card
                        paymentsFee += fee
                    }
                    "payment_reversal" -> {
                        reversalsCash += cash
                        reversalsCard += card
                        reversalsFee += fee
                    }
                    "credit_refund" -> {
                        refundCash += cash
                        refundCard += card
                    }
                }
            }
        }

        var returnOffset = 0L
        db.query(
            """
            SELECT COALESCE(SUM(-l.debt_delta_minor), 0)
            FROM debt_event_lines l
            JOIN debt_events e ON e.guid = l.event_guid
            WHERE e.kind = 'return_offset' AND e.occurred_at >= ? AND e.occurred_at < ?
            """.trimIndent(),
            arrayOf(startMs, endMs)
        ).use { c ->
            if (c.moveToFirst()) {
                returnOffset = c.getLong(0)
            }
        }

        return DebtPeriodSummary(
            rangeStartMs = startMs,
            rangeEndMs = endMs,
            openingDebtMinor = openingDebt,
            openingCreditMinor = openingCredit,
            newDebtIssuedMinor = newDebtIssued,
            debtPaymentsCashMinor = paymentsCash,
            debtPaymentsCardMinor = paymentsCard,
            debtPaymentsFeeMinor = paymentsFee,
            debtPaymentsTotalMinor = paymentsCash + paymentsCard,
            paymentReversalsCashMinor = reversalsCash,
            paymentReversalsCardMinor = reversalsCard,
            paymentReversalsFeeMinor = reversalsFee,
            debtReturnOffsetMinor = returnOffset,
            creditRefundCashMinor = refundCash,
            creditRefundCardMinor = refundCard,
            closingDebtMinor = closingDebt,
            closingCreditMinor = closingCredit,
            activeDebtorsCount = activeCount,
            overdueDebtorsCount = overdueCount
        )
    }

    private fun queryBalancesAt(db: SupportSQLiteDatabase, timeMs: Long): Pair<Long, Long> {
        val customerBalances = mutableMapOf<String, Long>()
        db.query(
            """
            SELECT a.customer_guid,
                   a.original_debt_minor + COALESCE((
                       SELECT SUM(l.debt_delta_minor)
                       FROM debt_event_lines l
                       JOIN debt_events e ON e.guid = l.event_guid
                       WHERE l.account_guid = a.guid AND e.occurred_at < ?
                   ), 0) AS bal
            FROM debt_accounts a
            JOIN debt_events op ON op.guid = a.opening_event_guid
            WHERE op.occurred_at < ?
            """.trimIndent(),
            arrayOf(timeMs, timeMs)
        ).use { c ->
            while (c.moveToNext()) {
                val cust = c.getString(0)
                val bal = c.getLong(1)
                customerBalances[cust] = (customerBalances[cust] ?: 0L) + bal
            }
        }

        var totalDebt = 0L
        var totalCredit = 0L
        for (b in customerBalances.values) {
            if (b > 0) totalDebt += b
            else if (b < 0) totalCredit += -b
        }
        return Pair(totalDebt, totalCredit)
    }

    private fun queryBalancesAtWithOverdue(
        db: SupportSQLiteDatabase,
        timeMs: Long
    ): Tuple4<Long, Long, Int, Int> {
        val customerBalances = mutableMapOf<String, Long>()
        val customerHasOverdue = mutableMapOf<String, Boolean>()

        val sdf = java.text.SimpleDateFormat("yyyy-MM-dd", java.util.Locale.US)
        val dtStr = sdf.format(java.util.Date(timeMs))

        db.query(
            """
            SELECT a.customer_guid,
                   a.due_date,
                   a.original_debt_minor + COALESCE((
                       SELECT SUM(l.debt_delta_minor)
                       FROM debt_event_lines l
                       JOIN debt_events e ON e.guid = l.event_guid
                       WHERE l.account_guid = a.guid AND e.occurred_at < ?
                   ), 0) AS bal
            FROM debt_accounts a
            JOIN debt_events op ON op.guid = a.opening_event_guid
            WHERE op.occurred_at < ?
            """.trimIndent(),
            arrayOf(timeMs, timeMs)
        ).use { c ->
            while (c.moveToNext()) {
                val cust = c.getString(0)
                val dueDate = if (c.isNull(1)) null else c.getString(1)
                val bal = c.getLong(2)

                customerBalances[cust] = (customerBalances[cust] ?: 0L) + bal
                if (bal > 0 && dueDate != null && dueDate < dtStr) {
                    customerHasOverdue[cust] = true
                }
            }
        }

        var totalDebt = 0L
        var totalCredit = 0L
        var activeCount = 0
        var overdueCount = 0

        for ((cust, b) in customerBalances) {
            if (b > 0) {
                totalDebt += b
                activeCount++
                if (customerHasOverdue[cust] == true) {
                    overdueCount++
                }
            } else if (b < 0) {
                totalCredit += -b
            }
        }

        return Tuple4(totalDebt, totalCredit, activeCount, overdueCount)
    }

    data class Tuple4<A, B, C, D>(val a: A, val b: B, val c: C, val d: D)
}
