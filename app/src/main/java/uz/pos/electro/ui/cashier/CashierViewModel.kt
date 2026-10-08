package uz.pos.electro.ui.cashier

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.model.CartItemModel
import uz.pos.electro.data.model.HeldCart
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.repository.ProductRepository
import uz.pos.electro.data.repository.SaleRepository
import uz.pos.electro.util.SmartSearchHelper

import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import javax.inject.Inject

@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class CashierViewModel @Inject constructor(
    private val productRepository: ProductRepository,
    private val saleRepository: SaleRepository,
    private val taxSettingsRepository: uz.pos.electro.data.repository.TaxSettingsRepository,
    private val warehouseRepository: uz.pos.electro.data.repository.WarehouseRepository
) : ViewModel() {

    // Savatdagi tovarlar
    private val checkoutGate = java.util.concurrent.atomic.AtomicBoolean(false)
    private val _isCompletingSale = MutableStateFlow(false)
    val isCompletingSale = _isCompletingSale.asStateFlow()

    private val _cartItems = MutableStateFlow<List<CartItemModel>>(emptyList())
    val cartItems: StateFlow<List<CartItemModel>> = _cartItems.asStateFlow()

    // To'lov dialogi holati va karta soliq stavkasi
    private val _isCheckoutDialogVisible = MutableStateFlow(false)
    val isCheckoutDialogVisible: StateFlow<Boolean> = _isCheckoutDialogVisible.asStateFlow()
    val cardTaxRate: StateFlow<Double> = taxSettingsRepository.cardTaxRate

    // Hold Carts (Muzlatilgan savatlar ro'yxati)
    private val _heldCarts = MutableStateFlow<List<HeldCart>>(emptyList())
    val heldCarts: StateFlow<List<HeldCart>> = _heldCarts.asStateFlow()

    private val _isHoldCartsDialogVisible = MutableStateFlow(false)
    val isHoldCartsDialogVisible: StateFlow<Boolean> = _isHoldCartsDialogVisible.asStateFlow()

    // Topilmagan shtrix-kod holati
    private val _unrecognizedBarcode = MutableStateFlow<String?>(null)
    val unrecognizedBarcode: StateFlow<String?> = _unrecognizedBarcode.asStateFlow()

data class CashierSearchResult(
    val product: ProductEntity,
    val warehouseGuid: String,
    val warehouseName: String,
    val warehouseIndex: Int,
    val stockQuantity: Double
)

    // Jonli qidiruv
    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    val searchResults: StateFlow<List<CashierSearchResult>> = combine(
        _searchQuery,
        productRepository.getAllProducts(),
        warehouseRepository.getAllWarehouses(),
        warehouseRepository.getAllStocks()
    ) { query, allProducts, warehouses, stocks ->
        if (query.trim().length < 2) {
            emptyList()
        } else {
            val matched = SmartSearchHelper.filterAndRank(
                source = allProducts.filter { !it.isDeleted },
                query = query.trim(),
                nameSelector = { it.name },
                barcodeSelector = { it.barcode },
                noteSelector = { it.note }
            )

            val primaryWh = warehouses.firstOrNull { it.isPrimary } ?: warehouses.firstOrNull()
            val resultList = mutableListOf<CashierSearchResult>()

            for (prod in matched) {
                if (warehouses.isEmpty() || warehouses.size <= 1) {
                    val wh = warehouses.firstOrNull() ?: primaryWh
                    val whGuid = wh?.guid ?: "main-default-warehouse"
                    val whName = wh?.name ?: "Do'kondagi ombor"
                    val stockQty = stocks.find { it.productGuid == prod.guid && it.warehouseGuid == whGuid }?.quantity ?: prod.stockQuantity
                    resultList.add(
                        CashierSearchResult(
                            product = prod,
                            warehouseGuid = whGuid,
                            warehouseName = whName,
                            warehouseIndex = 1,
                            stockQuantity = stockQty
                        )
                    )
                } else {
                    // Ko'p omborli rejim:
                    // 1-o'rinda: Har doim Asosiy ombor (1)
                    val pGuid = primaryWh?.guid ?: "main-default-warehouse"
                    val pName = primaryWh?.name ?: "Do'kondagi ombor"
                    val pStock = stocks.find { it.productGuid == prod.guid && it.warehouseGuid == pGuid }?.quantity ?: 0.0
                    resultList.add(
                        CashierSearchResult(
                            product = prod,
                            warehouseGuid = pGuid,
                            warehouseName = pName,
                            warehouseIndex = 1,
                            stockQuantity = pStock
                        )
                    )

                    // Keyingi o'rinlarda: Qolgan faol omborlar (2, 3...) - faqat qoldig'i 0 dan katta bo'lsa
                    for (i in warehouses.indices) {
                        val wh = warehouses[i]
                        if (wh.guid == pGuid) continue
                        val secStock = stocks.find { it.productGuid == prod.guid && it.warehouseGuid == wh.guid }?.quantity ?: 0.0
                        if (secStock > 0) {
                            resultList.add(
                                CashierSearchResult(
                                    product = prod,
                                    warehouseGuid = wh.guid,
                                    warehouseName = wh.name,
                                    warehouseIndex = i + 1,
                                    stockQuantity = secStock
                                )
                            )
                        }
                    }
                }
            }
            resultList
        }
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5000),
        initialValue = emptyList()
    )

    // Xabarnomalar (Toast uchun)
    private val _toastEvent = MutableSharedFlow<String>()
    val toastEvent: SharedFlow<String> = _toastEvent.asSharedFlow()

    // Tahrirlanayotgan savat elementi
    private val _selectedCartItemIndex = MutableStateFlow<Int?>(null)
    val selectedCartItemIndex: StateFlow<Int?> = _selectedCartItemIndex.asStateFlow()

    // Yakunlangan savdo holati (Chek dialogi)
    private val _lastCompletedSale = MutableStateFlow<CompletedSaleState?>(null)
    val lastCompletedSale: StateFlow<CompletedSaleState?> = _lastCompletedSale.asStateFlow()

    val totalAmount: Double
        get() = uz.pos.electro.data.model.SaleAccounting.money(_cartItems.value.sumOf { it.totalPrice })

    fun onSearchQueryChanged(query: String) {
        _searchQuery.value = query
    }

    /**
     * Skanerdan kelgan shtrix-kodni qabul qilib to'g'ridan-to'g'ri savatga qo'shish (Ustuvorlik zanjiri bilan).
     */
    fun onBarcodeScanned(barcode: String) {
        val cleanBarcode = barcode.trim()
        if (cleanBarcode.isBlank()) return

        viewModelScope.launch {
            val product = productRepository.getProductByBarcode(cleanBarcode)
            if (product != null) {
                val activeWarehouses = warehouseRepository.getAllWarehousesList()
                // Ustuvorlik tartibi: Asosiy ombor (stock > 0) -> 2-ombor (stock > 0) -> ... -> Fallback Asosiy ombor
                var chosenWh = activeWarehouses.firstOrNull { it.isPrimary } ?: activeWarehouses.firstOrNull()
                for (wh in activeWarehouses) {
                    val stock = warehouseRepository.getProductStockInWarehouse(product.guid, wh.guid)
                    if (stock > 0) {
                        chosenWh = wh
                        break
                    }
                }
                val finalGuid = chosenWh?.guid ?: "main-default-warehouse"
                val finalName = chosenWh?.name ?: "Do'kondagi ombor"
                addProductToCart(product, 1.0, finalGuid, finalName)
                _toastEvent.emit("${product.name} ($finalName) savatga qo'shildi")
            } else {
                _unrecognizedBarcode.value = cleanBarcode
            }
        }
    }

    fun clearUnrecognizedBarcode() {
        _unrecognizedBarcode.value = null
    }

    /**
     * Mahsulotni savatga qo'shish (Ombor ko'rsatilgan holda)
     */
    fun addProductToCart(
        product: ProductEntity, 
        quantity: Double = 1.0,
        warehouseGuid: String? = null,
        warehouseName: String? = null
    ) {
        if (checkoutGate.get()) return

        viewModelScope.launch {
            val primaryWh = warehouseRepository.getPrimaryWarehouse()
            val finalWhGuid = warehouseGuid ?: primaryWh?.guid ?: "main-default-warehouse"
            val finalWhName = warehouseName ?: primaryWh?.name ?: "Do'kondagi ombor"

            val currentList = _cartItems.value.toMutableList()
            val existingIndex = currentList.indexOfFirst { 
                it.product.id == product.id && it.warehouseGuid == finalWhGuid 
            }

            if (existingIndex != -1) {
                val existingItem = currentList[existingIndex]
                val newQuantity = existingItem.quantity + quantity
                currentList[existingIndex] = existingItem.copy(quantity = newQuantity)
            } else {
                currentList.add(
                    CartItemModel(
                        product = product,
                        quantity = quantity,
                        priceAtSale = product.sellingPrice,
                        warehouseGuid = finalWhGuid,
                        warehouseName = finalWhName
                    )
                )
            }
            _cartItems.value = currentList
            _searchQuery.value = ""
        }
    }

    fun selectCartItemForEdit(index: Int) {
        _selectedCartItemIndex.value = index
    }

    fun closeEditCartItemDialog() {
        _selectedCartItemIndex.value = null
    }

    /**
     * Savatdagi tovar miqdori va narxini yangilash
     */
    fun updateCartItem(index: Int, newQuantity: Double, newPrice: Double) {
        if (checkoutGate.get()) return
        val currentList = _cartItems.value.toMutableList()
        if (index in currentList.indices) {
            val item = currentList[index]
            if (newQuantity <= 0) {
                currentList.removeAt(index)
            } else {


                currentList[index] = item.copy(
                    quantity = newQuantity,
                    priceAtSale = newPrice
                )
            }
            _cartItems.value = currentList
        }
        closeEditCartItemDialog()
    }

    fun removeCartItem(index: Int) {
        if (checkoutGate.get()) return
        val currentList = _cartItems.value.toMutableList()
        if (index in currentList.indices) {
            currentList.removeAt(index)
            _cartItems.value = currentList
        }
    }

    /**
     * Savatdagi tovar narxini 1-narx va 2-narx (usta narxi) o'rtasida almashtirish
     */
    fun toggleCartItemPrice(index: Int) {
        if (checkoutGate.get()) return
        val currentList = _cartItems.value.toMutableList()
        if (index in currentList.indices) {
            val item = currentList[index]
            val p2 = item.product.sellingPrice2
            if (p2 != null && p2 > 0) {
                val isCurrentlyP2 = kotlin.math.abs(item.priceAtSale - p2) < 0.01
                val targetPrice = if (isCurrentlyP2) item.product.sellingPrice else p2
                currentList[index] = item.copy(priceAtSale = targetPrice)
                _cartItems.value = currentList
            }
        }
    }

    fun clearCart() {
        if (checkoutGate.get()) return
        _cartItems.value = emptyList()
    }

    // --- HOLD CART LOGIKASI ---

    fun showHoldCartsDialog() {
        _isHoldCartsDialogVisible.value = true
    }

    fun dismissHoldCartsDialog() {
        _isHoldCartsDialogVisible.value = false
    }

    fun holdCurrentCart(customName: String? = null) {
        if (checkoutGate.get()) return
        val currentItems = _cartItems.value
        if (currentItems.isEmpty()) return

        val timeFormat = SimpleDateFormat("HH:mm", Locale.getDefault())
        val defaultName = "Mijoz #${_heldCarts.value.size + 1} (${timeFormat.format(Date())})"
        val cartName = if (!customName.isNullOrBlank()) customName else defaultName

        val heldCart = HeldCart(
            name = cartName,
            items = currentItems
        )

        _heldCarts.value = _heldCarts.value + heldCart
        _cartItems.value = emptyList()

        viewModelScope.launch {
            _toastEvent.emit("Savat muzlatildi: $cartName")
        }
    }

    fun resumeHeldCart(heldCart: HeldCart) {
        if (checkoutGate.get()) return
        val currentActiveItems = _cartItems.value

        if (currentActiveItems.isNotEmpty()) {
            val timeFormat = SimpleDateFormat("HH:mm", Locale.getDefault())
            val autoHold = HeldCart(
                name = "Mijoz (Avto) ${timeFormat.format(Date())}",
                items = currentActiveItems
            )
            _heldCarts.value = (_heldCarts.value.filterNot { it.id == heldCart.id }) + autoHold
        } else {
            _heldCarts.value = _heldCarts.value.filterNot { it.id == heldCart.id }
        }

        _cartItems.value = heldCart.items
        _isHoldCartsDialogVisible.value = false

        viewModelScope.launch {
            _toastEvent.emit("${heldCart.name} savati ochildi")
        }
    }

    fun deleteHeldCart(heldCartId: String) {
        _heldCarts.value = _heldCarts.value.filterNot { it.id == heldCartId }
    }

    fun dismissCompletedSaleDialog() {
        _lastCompletedSale.value = null
    }

    fun openCheckoutDialog() {
        if (_cartItems.value.isNotEmpty()) {
            _isCheckoutDialogVisible.value = true
        }
    }

    fun closeCheckoutDialog() {
        if (checkoutGate.get()) return
        _isCheckoutDialogVisible.value = false
    }

    /**
     * Savdoni yakunlash (Sotish)
     */
    fun completeSale(
        paymentType: PaymentType = PaymentType.CASH,
        cashAmount: Double = 0.0,
        cardAmount: Double = 0.0,
        taxAmount: Double = 0.0,
        taxRate: Double = 0.0
    ) {
        val items = _cartItems.value
        if (items.isEmpty() || !checkoutGate.compareAndSet(false, true)) return
        _isCompletingSale.value = true
        viewModelScope.launch {
            try {
                val total = uz.pos.electro.data.model.SaleAccounting.money(items.sumOf { it.totalPrice })
                val saleGuid = java.util.UUID.randomUUID().toString()
                val saleId = saleRepository.completeSale(
                    items = items,
                    userId = 1L,
                    paymentType = paymentType,
                    cashAmount = cashAmount,
                    cardAmount = cardAmount,
                    taxAmount = taxAmount,
                    taxRate = taxRate,
                    saleGuid = saleGuid
                )

                _lastCompletedSale.value = CompletedSaleState(
                    saleId = saleId,
                    receiptNumber = "LP-" + saleGuid.replace("-", "").uppercase(java.util.Locale.ROOT),
                    items = items,
                    totalAmount = total,
                    paymentType = paymentType,
                    timestamp = System.currentTimeMillis()
                )

                _isCheckoutDialogVisible.value = false
                _cartItems.value = emptyList()
                _toastEvent.emit("Savdo muvaffaqiyatli amalga oshirildi!")
            } catch (e: Exception) {
                _toastEvent.emit("Xatolik: ${e.localizedMessage}")
            } finally {
                _isCompletingSale.value = false
                checkoutGate.set(false)
            }
        }
    }

    /**
     * Savatdagi tovarlarni 0 so'mga (Brak/Spisanie) hisobdan chiqarish
     */
    fun writeOffCartAsBrak() {
        val items = _cartItems.value
        if (items.isEmpty() || !checkoutGate.compareAndSet(false, true)) return
        _isCompletingSale.value = true
        viewModelScope.launch {
            try {
                val zeroPriceItems = items.map { it.copy(priceAtSale = 0.0) }

                saleRepository.completeSale(
                    items = zeroPriceItems,
                    userId = 1L,
                    paymentType = PaymentType.BRAK,
                    cashAmount = 0.0,
                    cardAmount = 0.0,
                    taxAmount = 0.0,
                    taxRate = 0.0
                )

                _cartItems.value = emptyList()
                _toastEvent.emit("⚠️ Tovar(lar) brak sifatida hisobdan chiqarildi (0 so'm)")
            } catch (e: Exception) {
                _toastEvent.emit("Xatolik: ${e.localizedMessage}")
            } finally {
                _isCompletingSale.value = false
                checkoutGate.set(false)
            }
        }
    }
}

data class CompletedSaleState(
    val saleId: Long,
    val receiptNumber: String,
    val items: List<CartItemModel>,
    val totalAmount: Double,
    val paymentType: PaymentType = PaymentType.CASH,
    val timestamp: Long
)
