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
    private val warehouseRepository: uz.pos.electro.data.repository.WarehouseRepository,
    private val debtService: uz.pos.electro.data.debt.DebtService,
    private val currencyRepository: uz.pos.electro.data.repository.CurrencyRepository
) : ViewModel() {

    // Savatdagi tovarlar
    private val checkoutGate = java.util.concurrent.atomic.AtomicBoolean(false)
    private val _isCompletingSale = MutableStateFlow(false)
    val isCompletingSale = _isCompletingSale.asStateFlow()

    // Nasiya (Debt) holatlari
    private val _activeDebtCustomers = MutableStateFlow<List<uz.pos.electro.data.debt.DebtCustomerItemDto>>(emptyList())
    val activeDebtCustomers: StateFlow<List<uz.pos.electro.data.debt.DebtCustomerItemDto>> = _activeDebtCustomers.asStateFlow()

    private val _selectedDebtCustomer = MutableStateFlow<uz.pos.electro.data.debt.DebtCustomerItemDto?>(null)
    val selectedDebtCustomer: StateFlow<uz.pos.electro.data.debt.DebtCustomerItemDto?> = _selectedDebtCustomer.asStateFlow()

    private val _debtDueDate = MutableStateFlow<String?>(null)
    val debtDueDate: StateFlow<String?> = _debtDueDate.asStateFlow()

    private val _debtCashAdvance = MutableStateFlow("0")
    val debtCashAdvance: StateFlow<String> = _debtCashAdvance.asStateFlow()

    private val _debtCardAdvance = MutableStateFlow("0")
    val debtCardAdvance: StateFlow<String> = _debtCardAdvance.asStateFlow()

    private val _isQuickAddCustomerOpen = MutableStateFlow(false)
    val isQuickAddCustomerOpen: StateFlow<Boolean> = _isQuickAddCustomerOpen.asStateFlow()

    init {
        refreshActiveDebtCustomers()
    }

    fun refreshActiveDebtCustomers() {
        viewModelScope.launch {
            try {
                _activeDebtCustomers.value = debtService.getActiveCustomers()
            } catch (_: Throwable) { }
        }
    }

    fun selectDebtCustomer(customer: uz.pos.electro.data.debt.DebtCustomerItemDto?) {
        _selectedDebtCustomer.value = customer
    }

    fun setDebtCustomerByGuid(guid: String) {
        viewModelScope.launch {
            refreshActiveDebtCustomers()
            _selectedDebtCustomer.value = _activeDebtCustomers.value.find { it.guid == guid }
        }
    }

    fun setDebtDueDate(dateStr: String?) {
        _debtDueDate.value = dateStr
    }

    fun setDebtCashAdvance(value: String) {
        _debtCashAdvance.value = value
    }

    fun setDebtCardAdvance(value: String) {
        _debtCardAdvance.value = value
    }

    fun openQuickAddCustomer() {
        _isQuickAddCustomerOpen.value = true
    }

    fun closeQuickAddCustomer() {
        _isQuickAddCustomerOpen.value = false
    }

    fun saveQuickCustomer(name: String, phone: String, note: String) {
        if (name.isBlank()) return
        viewModelScope.launch {
            try {
                val newGuid = debtService.createCustomer(name, phone, note)
                refreshActiveDebtCustomers()
                _selectedDebtCustomer.value = _activeDebtCustomers.value.find { it.guid == newGuid }
                _isQuickAddCustomerOpen.value = false
                _toastEvent.emit("Yangi mijoz qo'shildi: $name")
            } catch (e: Throwable) {
                _toastEvent.emit("Mijoz qo'shishda xatolik: ${e.message}")
            }
        }
    }

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
        get() = uz.pos.electro.data.model.SaleAccounting.money(_cartItems.value.sumOf { it.totalPrice })

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
        if (checkoutGate.get()) return

        val currentList = _cartItems.value.toMutableList()
        val existingIndex = currentList.indexOfFirst { it.product.id == product.id }

        if (existingIndex != -1) {
            val existingItem = currentList[existingIndex]
            val newQuantity = existingItem.quantity + quantity



            currentList[existingIndex] = existingItem.copy(quantity = newQuantity)
            _cartItems.value = currentList
            _searchQuery.value = ""
        } else {


            viewModelScope.launch {
                val primaryWh = warehouseRepository.getPrimaryWarehouse()
                val whName = primaryWh?.name ?: "Do'kondagi ombor"
                val whGuid = primaryWh?.guid ?: "main-default-warehouse"

                if (checkoutGate.get()) return@launch
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
            items = currentItems,
            selectedPaymentType = if (_selectedDebtCustomer.value != null) PaymentType.DEBT else PaymentType.CASH,
            customerGuid = _selectedDebtCustomer.value?.guid,
            customerName = _selectedDebtCustomer.value?.name,
            dueDate = _debtDueDate.value,
            debtCashAdvance = _debtCashAdvance.value,
            debtCardAdvance = _debtCardAdvance.value
        )

        _heldCarts.value = _heldCarts.value + heldCart
        _cartItems.value = emptyList()
        _selectedDebtCustomer.value = null
        _debtDueDate.value = null
        _debtCashAdvance.value = "0"
        _debtCardAdvance.value = "0"

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
                items = currentActiveItems,
                selectedPaymentType = if (_selectedDebtCustomer.value != null) PaymentType.DEBT else PaymentType.CASH,
                customerGuid = _selectedDebtCustomer.value?.guid,
                customerName = _selectedDebtCustomer.value?.name,
                dueDate = _debtDueDate.value,
                debtCashAdvance = _debtCashAdvance.value,
                debtCardAdvance = _debtCardAdvance.value
            )
            _heldCarts.value = (_heldCarts.value.filterNot { it.id == heldCart.id }) + autoHold
        } else {
            _heldCarts.value = _heldCarts.value.filterNot { it.id == heldCart.id }
        }

        _cartItems.value = heldCart.items
        if (!heldCart.customerGuid.isNullOrBlank()) {
            setDebtCustomerByGuid(heldCart.customerGuid)
        } else {
            _selectedDebtCustomer.value = null
        }
        _debtDueDate.value = heldCart.dueDate
        _debtCashAdvance.value = heldCart.debtCashAdvance ?: "0"
        _debtCardAdvance.value = heldCart.debtCardAdvance ?: "0"
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
     * Nasiya (Qarz) savdosini yakunlash
     */
    fun completeDebtSale(
        customerGuid: String,
        dueDate: String? = null,
        cashAdvance: Double = 0.0,
        cardAdvance: Double = 0.0
    ) {
        val items = _cartItems.value
        if (items.isEmpty() || !checkoutGate.compareAndSet(false, true)) return
        _isCompletingSale.value = true
        viewModelScope.launch {
            try {
                val totalAmount = uz.pos.electro.data.model.SaleAccounting.money(items.sumOf { it.totalPrice })
                val cashMinor = Math.round(cashAdvance * 100).toLong()
                val cardMinor = Math.round(cardAdvance * 100).toLong()
                val usdRate = currencyRepository.getCachedUsdRate()
                val effectiveTaxRate = cardTaxRate.value

                val saleGuid = java.util.UUID.randomUUID().toString()
                val requestGuid = java.util.UUID.randomUUID().toString()

                val snapshot = uz.pos.electro.data.debt.DebtService.buildSaleSnapshot(
                    saleGuid = saleGuid,
                    occurredAt = System.currentTimeMillis(),
                    cartItems = items,
                    cashMinor = cashMinor,
                    cardMinor = cardMinor,
                    usdRate = usdRate,
                    cardTaxRate = effectiveTaxRate
                )

                debtService.openDebtSale(
                    requestGuid = requestGuid,
                    customerGuid = customerGuid,
                    saleSnapshot = snapshot,
                    dueDate = dueDate,
                    userId = 1L
                )

                _lastCompletedSale.value = CompletedSaleState(
                    saleId = 0L,
                    receiptNumber = "LP-" + saleGuid.replace("-", "").uppercase(java.util.Locale.ROOT),
                    items = items,
                    totalAmount = totalAmount,
                    paymentType = PaymentType.DEBT,
                    timestamp = snapshot.occurredAt
                )

                _isCheckoutDialogVisible.value = false
                _cartItems.value = emptyList()
                _selectedDebtCustomer.value = null
                _debtDueDate.value = null
                _debtCashAdvance.value = "0"
                _debtCardAdvance.value = "0"
                _toastEvent.emit("Nasiya savdo muvaffaqiyatli saqlandi!")
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
