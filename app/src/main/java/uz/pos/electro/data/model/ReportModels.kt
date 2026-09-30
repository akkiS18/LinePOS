package uz.pos.electro.data.model

data class SaleReportItem(
    val saleId: Long,
    val timestamp: Long,
    val productId: Long,
    val productName: String,
    val unitType: UnitType,
    val quantity: Double,
    val costPrice: Double,
    val sellingPrice: Double,
    val totalPrice: Double,
    val profit: Double,
    val category: String = "",
    val warehouseName: String = "",
    val warehouseGuid: String = ""
)

data class ReportsSummary(
    val totalRevenue: Double = 0.0,
    val totalCost: Double = 0.0,
    val netProfit: Double = 0.0,
    val netProfitUsd: Double = 0.0,
    val salesCount: Int = 0,
    val totalItemsCount: Double = 0.0,
    val usdRate: Double = 12850.0,
    val totalCashAmount: Double = 0.0,
    val totalCardAmount: Double = 0.0,
    val totalTaxAmount: Double = 0.0
)

enum class TimeRangeFilter(val displayName: String) {
    TODAY("Bugun"),
    YESTERDAY("Kecha"),
    THIS_MONTH("Shu oy"),
    CUSTOM("Oraliq tanlash")
}
