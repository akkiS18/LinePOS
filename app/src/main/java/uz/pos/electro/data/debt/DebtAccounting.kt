package uz.pos.electro.data.debt

import java.math.BigInteger
import java.util.Collections

data class DebtAccount(val accountGuid: String, val saleGuid: String, val customerGuid: String, val createdAt: Long, val balanceMinor: Long)
data class DebtLine(val accountGuid: String, val deltaMinor: Long)
data class DebtAllocation(val accountGuid: String, val amountMinor: Long)
data class DebtTotals(val receivableMinor: Long, val creditMinor: Long)
data class DebtReturnSplit(val debtOffsetMinor: Long, val refundMinor: Long)

// Arithmetic only, not an authorized/persisted event. Identity, ownership, deduplication and
// atomic commits belong to the repository. Never count collection as a second sale.
class DebtEffect internal constructor(lines: List<DebtLine>, val cashMinor: Long, val cardMinor: Long, val feeExpenseMinor: Long) {
    val lines: List<DebtLine> = Collections.unmodifiableList(ArrayList(lines))
    val revenueMinor: Long get() = 0
}

object DebtAccounting {
    private val max = BigInteger.valueOf(Long.MAX_VALUE)
    private fun signed(value: BigInteger): Long {
        if (value < max.negate() || value > max) throw ArithmeticException("Debt amount overflow")
        return value.toLong()
    }
    private fun signed(value: Long): Long = signed(BigInteger.valueOf(value))
    private fun nonNegative(value: Long) { require(value >= 0) }
    private fun id(value: String) { require(value.matches(Regex("[A-Za-z0-9:_-]{1,128}"))) }
    private fun account(a: DebtAccount) {
        id(a.accountGuid); id(a.saleGuid); id(a.customerGuid)
        require(a.createdAt >= 0); signed(a.balanceMinor)
    }
    private fun total(cash: Long, card: Long): Long {
        nonNegative(cash); nonNegative(card)
        return signed(BigInteger.valueOf(cash) + BigInteger.valueOf(card))
    }

    // Canonical ungrouped UZS input; UI removes visual separators. No exponent, signs,
    // whitespace or silent rounding. Symmetric signed range permits reversing every delta.
    fun parseUzs(text: String): Long {
        require(text.length <= 64 && text.matches(Regex("[0-9]+(?:\\.[0-9]{1,2})?")))
        val parts = text.split('.')
        val fraction = if (parts.size == 2) parts[1].padEnd(2, '0').toInt() else 0
        return signed(parts[0].toBigInteger() * BigInteger.valueOf(100) + BigInteger.valueOf(fraction.toLong()))
    }

    fun newDebt(saleMinor: Long, cashMinor: Long, cardMinor: Long): Long {
        nonNegative(saleMinor); val paid = total(cashMinor, cardMinor)
        require(paid <= saleMinor); return saleMinor - paid
    }

    // Caller supplies each persisted event line once. Wide sum makes delivery order irrelevant,
    // including when intermediate signed totals would exceed Long but the final balance fits.
    fun balance(originalDebtMinor: Long, deltas: List<Long>): Long {
        nonNegative(originalDebtMinor)
        var sum = BigInteger.valueOf(originalDebtMinor)
        for (delta in deltas) { signed(delta); sum += BigInteger.valueOf(delta) }
        return signed(sum)
    }

    fun totals(accounts: List<DebtAccount>): DebtTotals {
        var debt = BigInteger.ZERO; var credit = BigInteger.ZERO
        val ids = mutableSetOf<String>()
        for (a in accounts) {
            account(a); require(ids.add(a.accountGuid))
            if (a.balanceMinor > 0) debt += BigInteger.valueOf(a.balanceMinor) else credit -= BigInteger.valueOf(a.balanceMinor)
        }
        return DebtTotals(signed(debt), signed(credit))
    }

    fun allocate(paymentMinor: Long, customerGuid: String, accounts: List<DebtAccount>, targetAccountGuid: String? = null): List<DebtAllocation> {
        require(paymentMinor > 0); id(customerGuid)
        if (targetAccountGuid != null) id(targetAccountGuid)
        val ids = mutableSetOf<String>(); val sales = mutableSetOf<String>()
        for (a in accounts) {
            account(a); require(a.customerGuid == customerGuid && ids.add(a.accountGuid) && sales.add(a.saleGuid))
        }
        if (targetAccountGuid != null) require(accounts.any { it.accountGuid == targetAccountGuid })
        val ordered = accounts.filter { it.balanceMinor > 0 && (targetAccountGuid == null || it.accountGuid == targetAccountGuid) }
            .sortedWith(compareBy<DebtAccount> { it.createdAt }.thenBy { it.saleGuid })
        val result = mutableListOf<DebtAllocation>(); var remaining = paymentMinor
        for (a in ordered) {
            if (remaining == 0L) break
            val amount = minOf(remaining, a.balanceMinor); result.add(DebtAllocation(a.accountGuid, amount)); remaining -= amount
        }
        require(remaining == 0L) // Reject known excess; delayed offline payments instead form credit in balance().
        return Collections.unmodifiableList(result)
    }

    fun payment(cashMinor: Long, cardMinor: Long, feeMinor: Long, allocations: List<DebtAllocation>): DebtEffect {
        val total = total(cashMinor, cardMinor); require(total > 0); nonNegative(feeMinor); require(feeMinor <= cardMinor)
        val ids = mutableSetOf<String>(); val lines = mutableListOf<DebtLine>(); var assigned = BigInteger.ZERO
        for (a in allocations) {
            id(a.accountGuid); require(a.amountMinor > 0 && ids.add(a.accountGuid))
            assigned += BigInteger.valueOf(a.amountMinor); lines.add(DebtLine(a.accountGuid, -a.amountMinor))
        }
        require(assigned == BigInteger.valueOf(total)); return DebtEffect(lines, cashMinor, cardMinor, feeMinor)
    }

    fun reversePayment(original: DebtEffect, refundedFeeMinor: Long): DebtEffect {
        payment(original.cashMinor, original.cardMinor, original.feeExpenseMinor,
            original.lines.map { DebtAllocation(it.accountGuid, signed(BigInteger.valueOf(it.deltaMinor).negate())) })
        nonNegative(refundedFeeMinor); require(refundedFeeMinor <= original.feeExpenseMinor)
        return DebtEffect(original.lines.map { DebtLine(it.accountGuid, -it.deltaMinor) },
            -original.cashMinor, -original.cardMinor, -refundedFeeMinor)
    }

    // Remaining returnable goods/value validation and authority commit are outside this core.
    fun splitReturn(returnedValueMinor: Long, currentBalanceMinor: Long): DebtReturnSplit {
        require(returnedValueMinor > 0); signed(currentBalanceMinor)
        val offset = minOf(returnedValueMinor, maxOf(0, currentBalanceMinor))
        return DebtReturnSplit(offset, returnedValueMinor - offset)
    }

    fun transferCredit(source: DebtAccount, target: DebtAccount, amountMinor: Long): DebtEffect {
        account(source); account(target); require(amountMinor > 0)
        require(source.customerGuid == target.customerGuid && source.accountGuid != target.accountGuid && source.saleGuid != target.saleGuid)
        require(source.balanceMinor < 0 && amountMinor <= -source.balanceMinor && amountMinor <= target.balanceMinor)
        return DebtEffect(listOf(DebtLine(source.accountGuid, amountMinor), DebtLine(target.accountGuid, -amountMinor)), 0, 0, 0)
    }

    fun refundCredit(a: DebtAccount, cashMinor: Long, cardMinor: Long): DebtEffect {
        account(a); val total = total(cashMinor, cardMinor)
        require(total > 0 && a.balanceMinor < 0 && total <= -a.balanceMinor)
        return DebtEffect(listOf(DebtLine(a.accountGuid, total)), -cashMinor, -cardMinor, 0)
    }
}
