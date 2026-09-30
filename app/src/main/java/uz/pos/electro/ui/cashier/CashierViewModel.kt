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

    // Jonli qidiruv
    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    val searchResults: StateFlow<List<ProductEntity>> = _searchQuery
        .flatMapLatest { query ->
            if (query.length < 2) {
                flowOf(emptyList())
            } else {
                productRepository.searchProducts(query.trim())
            }
        }
        .stateIn(
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
        get() = _cartItems.value.sumOf { it.totalPrice }

    fun onSearchQueryChanged(query: String) {
        _searchQuery.value = query
    }

    /**
     * Skanerdan kelgan shtrix-kodni qabul qilib to'g'ridan-to'g'ri savatga qo'shish.
     */
    fun onBarcodeScanned(barcode: String) {
        val cleanBarcode = barcode.trim()
        if (cleanBarcode.isBlank()) return

        viewModelScope.launch {
            val product = productRepository.getProductByBarcode(cleanBarcode)
            if (product != null) {
                if (product.stockQuantity <= 0) {
                    _toastEvent.emit("${product.name} omborda qolmagan (0 qoldiq)!")
                    return@launch
                }
                addProductToCart(product)
                _toastEvent.emit("${product.name} savatga qo'shildi")
            } else {
                _unrecognizedBarcode.value = cleanBarcode
            }
        }
    }

    fun clearUnrecognizedBarcode() {
        _unrecognizedBarcode.value = null
    }

    /**
     * Mahsulotni savatga qo'shish (Ombor qoldig'i tekshiruvi bilan)
     */
    fun addProductToCart(product: ProductEntity, quantity: Double = 1.0) {
        if (product.stockQuantity <= 0) {
            viewModelScope.launch {
                _toastEvent.emit("${product.name} omborda qolmagan!")
            }
            return
        }

        val currentList = _cartItems.value.toMutableList()
        val existingIndex = currentList.indexOfFirst { it.product.id == product.id }

        if (existingIndex != -1) {
            val existingItem = currentList[existingIndex]
            val newQuantity = existingItem.quantity + quantity

            if (newQuantity > product.stockQuantity) {
                viewModelScope.launch {
                    val unitLabel = when (product.unitType) {
                        UnitType.METR -> "m"
                        UnitType.KG -> "kg"
                        UnitType.DONA -> "dona"
                    }
                    _toastEvent.emit("Omborda yetarli qoldiq yo'q! (Mavjud: ${product.stockQuantity} $unitLabel)")
                }
                return
            }

            currentList[existingIndex] = existingItem.copy(quantity = newQuantity)
            _cartItems.value = currentList
            _searchQuery.value = ""
        } else {
            if (quantity > product.stockQuantity) {
                viewModelScope.launch {
                    val unitLabel = when (product.unitType) {
                        UnitType.METR -> "m"
                        UnitType.KG -> "kg"
                        UnitType.DONA -> "dona"
                    }
                    _toastEvent.emit("Omborda yetarli qoldiq yo'q! (Mavjud: ${product.stockQuantity} $unitLabel)")
                }
                return
            }

            viewModelScope.launch {
                val primaryWh = warehouseRepository.getPrimaryWarehouse()
                val whName = primaryWh?.name ?: "Do'kondagi ombor"
                val whGuid = primaryWh?.guid ?: "main-default-warehouse"

                val updatedList = _cartItems.value.toMutableList()
                val idx = updatedList.indexOfFirst { it.product.id == product.id }
                if (idx != -1) {
                    val ex = updatedList[idx]
                    updatedList[idx] = ex.copy(quantity = ex.quantity + quantity)
                } else {
                    updatedList.add(
                        CartItemModel(
                            product = product,
                            quantity = quantity,
                            priceAtSale = product.sellingPrice,
                            warehouseGuid = whGuid,
                            warehouseName = whName
                        )
                    )
                }
                _cartItems.value = updatedList
                _searchQuery.value = ""
            }
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
        val currentList = _cartItems.value.toMutableList()
        if (index in currentList.indices) {
            val item = currentList[index]
            if (newQuantity <= 0) {
                currentList.removeAt(index)
            } else {
                if (newQuantity > item.product.stockQuantity) {
                    viewModelScope.launch {
                        val unitLabel = when (item.product.unitType) {
                            UnitType.METR -> "m"
                            UnitType.KG -> "kg"
                            UnitType.DONA -> "dona"
                        }
                        _toastEvent.emit("Omborda yetarli qoldiq yo'q! (Mavjud: ${item.product.stockQuantity} $unitLabel)")
                    }
                    return
                }

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
        if (items.isEmpty()) return

        viewModelScope.launch {
            try {
                val total = totalAmount
                val saleId = saleRepository.completeSale(
                    items = items,
                    userId = 1L,
                    paymentType = paymentType,
                    cashAmount = cashAmount,
                    cardAmount = cardAmount,
                    taxAmount = taxAmount,
                    taxRate = taxRate
                )

                _lastCompletedSale.value = CompletedSaleState(
                    saleId = saleId,
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
            }
        }
    }

    /**
     * Savatdagi tovarlarni 0 so'mga (Brak/Spisanie) hisobdan chiqarish
     */
    fun writeOffCartAsBrak() {
        val items = _cartItems.value
        if (items.isEmpty()) return

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
            }
        }
    }
}

data class CompletedSaleState(
    val saleId: Long,
    val items: List<CartItemModel>,
    val totalAmount: Double,
    val paymentType: PaymentType = PaymentType.CASH,
    val timestamp: Long
)
