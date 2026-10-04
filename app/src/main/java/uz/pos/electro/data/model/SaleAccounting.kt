package uz.pos.electro.data.model

import uz.pos.electro.data.local.relation.SaleWithItems
import java.math.BigDecimal
import java.math.RoundingMode

/** Immutable receipt totals are authoritative. Never revalue old sales at today's rate. */
object SaleAccounting {
    fun money(value: Double): Double = BigDecimal.valueOf(value).setScale(2, RoundingMode.HALF_UP).toDouble()
    fun allocate(total: Double, weights: List<Double>): List<Double> {
        if (weights.isEmpty()) return emptyList()
        val positive = weights.map { BigDecimal.valueOf(kotlin.math.abs(it)) }
        val sum = positive.fold(BigDecimal.ZERO, BigDecimal::add)
        val cents = BigDecimal.valueOf(total).movePointRight(2).setScale(0, RoundingMode.HALF_UP).longValueExact()
        val raw = positive.map { if (sum.signum() == 0) BigDecimal.valueOf(kotlin.math.abs(cents)).divide(BigDecimal(weights.size), 16, RoundingMode.DOWN)
            else BigDecimal.valueOf(kotlin.math.abs(cents)).multiply(it).divide(sum, 16, RoundingMode.DOWN) }
        val parts = raw.map { it.setScale(0, RoundingMode.DOWN).longValueExact() }.toMutableList()
        val order = raw.indices.sortedWith(compareByDescending<Int> { raw[it].subtract(BigDecimal.valueOf(parts[it])) }.thenBy { it })
        val remainder = kotlin.math.abs(cents) - parts.sum()
        repeat(remainder.toInt()) { parts[order[it % order.size]]++ }
        return parts.map { (if (cents < 0) -it else it) / 100.0 }
    }

    fun lines(receipt: SaleWithItems): List<SaleReportItem> {
        val sale = receipt.sale
        val items = receipt.items.sortedBy { it.id }
        val usd = items.filter { it.costCurrency == "USD" }.sumOf { it.costAtSale * it.quantity }
        val uzs = items.filter { it.costCurrency != "USD" }.sumOf { it.costAtSale * it.quantity }
        val inferred = if (usd > 0) (sale.totalCost - uzs) / usd else 0.0
        val rate = sale.usdRate.takeIf { it > 0 && it.isFinite() }
            ?: inferred.takeIf { it > 0 && it.isFinite() }
        val weights = items.map { it.priceAtSale * it.quantity }
        val revenue = allocate(sale.totalAmount, weights)
        val tax = allocate(sale.taxAmount, weights)
        val cash = allocate(sale.cashAmount, revenue)
        val card = if (money(sale.cashAmount + sale.cardAmount) == money(sale.totalAmount))
            revenue.indices.map { money(revenue[it] - cash[it]) } else allocate(sale.cardAmount, revenue)
        val cost = allocate(sale.totalCost, items.map {
            it.costAtSale * it.quantity * (if (it.costCurrency == "USD") rate ?: 1.0 else 1.0)
        })
        return items.mapIndexed { index, item ->
            val profit = BigDecimal.valueOf(revenue[index]).subtract(BigDecimal.valueOf(cost[index]))
                .subtract(BigDecimal.valueOf(tax[index])).toDouble()
            SaleReportItem(saleId = sale.id, timestamp = sale.createdAt, productId = item.productId,
                productName = item.productName.ifBlank { "Mahsulot #${item.productId}" },
                unitType = runCatching { UnitType.valueOf(item.unitAtSale) }.getOrDefault(UnitType.DONA),
                quantity = item.quantity, costPrice = if (item.quantity != 0.0) cost[index] / item.quantity else 0.0,
                sellingPrice = item.priceAtSale, totalPrice = revenue[index], profit = profit,
                category = item.categoryAtSale.ifBlank { "Tarixiy kategoriya noma’lum" },
                warehouseGuid = item.warehouseGuid, warehouseName = item.warehouseName.ifBlank { "Ombor noma’lum" },
                receiptNumber = sale.receiptNumber, totalCost = cost[index], taxAmount = tax[index],
                cashAmount = cash[index], cardAmount = card[index], profitUsd = rate?.let { profit / it },
                isReturn = sale.paymentType == PaymentType.RETURN,
                isBrak = sale.paymentType == PaymentType.BRAK)
        }
    }

    fun summary(lines: List<SaleReportItem>, displayRate: Double): ReportsSummary = ReportsSummary(
        totalRevenue = lines.sumOf { it.totalPrice }, totalCost = lines.sumOf { it.totalCost },
        netProfit = lines.sumOf { it.profit }, netProfitUsd = lines.sumOf { it.profitUsd ?: 0.0 },
        usdComplete = lines.all { it.profitUsd != null },
        salesCount = lines.filterNot { it.isBrak || it.isReturn }.map { it.saleId }.distinct().size,
        brakCount = lines.filter { it.isBrak }.map { it.saleId }.distinct().size,
        brakCost = lines.filter { it.isBrak }.sumOf { it.totalCost },
        totalItemsCount = lines.filterNot { it.isBrak || it.isReturn }.sumOf { it.quantity }, usdRate = displayRate,
        totalCashAmount = lines.sumOf { it.cashAmount }, totalCardAmount = lines.sumOf { it.cardAmount },
        totalTaxAmount = lines.sumOf { it.taxAmount })
}
