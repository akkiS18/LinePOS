package uz.pos.electro.data.debt

import java.math.BigDecimal
import java.math.RoundingMode

/** Read-only cumulative recognition; amounts are integer UZS minor units. */
object DebtProfitAccounting {
    data class Line(val revenue: Long, val cost: Long, val returnedRevenue: Long = 0,
        val returnedCostBasis: Long = 0, val restoredCost: Long = 0)
    private fun round(n: BigDecimal) = n.setScale(0, RoundingMode.HALF_UP).longValueExact()
    fun gross(lines: List<Line>, balanceMinor: Long): List<Long> {
        require(lines.isNotEmpty())
        val remaining = lines.map {
            require(it.revenue >= 0 && it.cost >= 0 && it.returnedRevenue in 0..it.revenue &&
                it.returnedCostBasis in 0..it.cost && it.restoredCost in 0..it.returnedCostBasis)
            Math.subtractExact(it.revenue, it.returnedRevenue)
        }
        val revenue = remaining.fold(0L, Math::addExact)
        val paid = if (balanceMinor <= 0) revenue else Math.subtractExact(revenue, minOf(balanceMinor, revenue))
        return lines.mapIndexed { i, l ->
            val margin = Math.subtractExact(remaining[i], Math.subtractExact(l.cost, l.returnedCostBasis))
            val recognised = if (revenue == 0L) 0L else round(BigDecimal.valueOf(margin).multiply(BigDecimal.valueOf(paid))
                .divide(BigDecimal.valueOf(revenue), 16, RoundingMode.HALF_UP))
            // Damaged goods are an actual cost loss, even if the customer never paid.
            Math.subtractExact(recognised, Math.subtractExact(l.returnedCostBasis, l.restoredCost))
        }
    }
    /** Cumulative rounding conserves the exact signed amount across allocations. */
    fun allocate(amount: Long, weights: List<Long>): List<Long> {
        require(weights.isNotEmpty() && weights.all { it >= 0 })
        val sum = weights.fold(0L, Math::addExact)
        var cumulative = BigDecimal.ZERO; var previous = 0L
        return weights.mapIndexed { i, w ->
            cumulative += BigDecimal.valueOf(if (sum == 0L) 1 else w)
            val target = if (i == weights.lastIndex) amount else round(BigDecimal.valueOf(amount).multiply(cumulative)
                .divide(BigDecimal.valueOf(if (sum == 0L) weights.size.toLong() else sum), 16, RoundingMode.HALF_UP))
            Math.subtractExact(target, previous).also { previous = target }
        }
    }
}
