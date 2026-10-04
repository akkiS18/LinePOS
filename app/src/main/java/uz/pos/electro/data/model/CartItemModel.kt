package uz.pos.electro.data.model

import uz.pos.electro.data.local.entity.ProductEntity

data class CartItemModel(
    val product: ProductEntity,
    val quantity: Double,
    val priceAtSale: Double,
    val warehouseGuid: String? = null,
    val warehouseName: String? = null
) {
    val totalPrice: Double
        get() = SaleAccounting.money(quantity * priceAtSale)

    val hasSellingPrice2: Boolean
        get() = product.sellingPrice2 != null && product.sellingPrice2 > 0

    val isPrice2Active: Boolean
        get() = hasSellingPrice2 && kotlin.math.abs(priceAtSale - (product.sellingPrice2 ?: 0.0)) < 0.01

    val hasWarehouse: Boolean
        get() = !warehouseName.isNullOrBlank()
}

