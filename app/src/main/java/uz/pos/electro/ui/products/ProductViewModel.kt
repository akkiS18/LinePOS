package uz.pos.electro.ui.products

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.local.entity.WarehouseEntity
import uz.pos.electro.data.model.CurrencyType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.repository.ProductRepository
import uz.pos.electro.data.repository.WarehouseRepository
import uz.pos.electro.data.repository.WarehouseWithStats
import java.util.Locale
import javax.inject.Inject

const val CATEGORY_LOW_STOCK = "Kam qolgan tovarlar"

data class CategoryItem(
    val name: String,
    val productCount: Int
)

@OptIn(ExperimentalCoroutinesApi::class)
@HiltViewModel
class ProductViewModel @Inject constructor(
    private val productRepository: ProductRepository,
    private val warehouseRepository: WarehouseRepository,
    val appDatabase: AppDatabase,
    val productDao: ProductDao,
    val productStockDao: ProductStockDao
) : ViewModel() {

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    // Hozir tanlangan ombor (null bo'lsa -> Omborlar Kartochkalari ko'rinadi)
    private val _selectedWarehouse = MutableStateFlow<WarehouseEntity?>(null)
    val selectedWarehouse: StateFlow<WarehouseEntity?> = _selectedWarehouse.asStateFlow()

    // Hozir ichiga kirilgan kategoriya (null bo'lsa -> Kategoriyalar Gridi ko'rinadi)
    private val _currentViewCategory = MutableStateFlow<String?>(null)
    val currentViewCategory: StateFlow<String?> = _currentViewCategory.asStateFlow()

    // Omborlar statistikasi bilan (Cardlar uchun)
    val warehousesWithStats: StateFlow<List<WarehouseWithStats>> = warehouseRepository.getWarehousesWithStats()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    // Tanlangan ombordagi tovarlar qoldiqlari xaritasi (productGuid -> quantity)
    val stocksInSelectedWarehouse: StateFlow<Map<String, Double>> = _selectedWarehouse.flatMapLatest { wh ->
        if (wh == null) {
            MutableStateFlow(emptyMap())
        } else {
            warehouseRepository.getStocksForWarehouse(wh.guid).map { list ->
                list.associate { it.productGuid to it.quantity }
            }
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyMap())

    // Yangi ombor ochish modal dialogi
    private val _isAddWarehouseOpen = MutableStateFlow(false)
    val isAddWarehouseOpen: StateFlow<Boolean> = _isAddWarehouseOpen.asStateFlow()

    // Omborlararo ko'chirish (transfer) modal dialogi
    private val _isTransferOpen = MutableStateFlow(false)
    val isTransferOpen: StateFlow<Boolean> = _isTransferOpen.asStateFlow()

    // Ro'yxatni majburiy yangilash triggeri
    private val _refreshTrigger = MutableStateFlow(0L)

    // Barcha mahsulotlar oqimi
    private val allProductsFlow = _refreshTrigger.flatMapLatest { productRepository.getAllProducts() }

    // Faol mahsulotlar ro'yxati (dublikat tekshirish va kesh uchun)
    private val allProductsState = allProductsFlow.stateIn(
        scope = viewModelScope,
        started = SharingStarted.Eagerly,
        initialValue = emptyList()
    )

    // Kategoriyalar va ulardagi mahsulotlar soni (tanlangan omborga moslangan)
    val categoryItems: StateFlow<List<CategoryItem>> = combine(
        allProductsFlow,
        _selectedWarehouse,
        stocksInSelectedWarehouse
    ) { products, selectedWh, stocks ->
        val activeProducts = products.filter { !it.isDeleted }
        val whProducts = if (selectedWh != null) {
            activeProducts.filter { stocks.containsKey(it.guid) }
                .map { it.copy(stockQuantity = stocks[it.guid] ?: 0.0) }
        } else {
            activeProducts
        }

        val totalCount = whProducts.size
        val lowStockCount = whProducts.count { it.stockQuantity <= it.minStockAlert }

        val list = mutableListOf<CategoryItem>()
        list.add(CategoryItem("Barchasi", totalCount))
        list.add(CategoryItem(CATEGORY_LOW_STOCK, lowStockCount))

        val grouped = whProducts.groupBy { it.category.ifBlank { "Barchasi" } }
        grouped.forEach { (catName, items) ->
            if (catName != "Barchasi" && catName != CATEGORY_LOW_STOCK) {
                list.add(CategoryItem(catName, items.size))
            }
        }
        list
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5000),
        initialValue = listOf(
            CategoryItem("Barchasi", 0),
            CategoryItem(CATEGORY_LOW_STOCK, 0)
        )
    )

    // Mavjud kategoriyalar nomlari ro'yxati (takliflar uchun)
    val categories: StateFlow<List<String>> = categoryItems.map { list ->
        list.map { it.name }.filter { it != CATEGORY_LOW_STOCK }
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5000),
        initialValue = listOf("Barchasi")
    )

    // Tanlangan kategoriya yoki qidiruv bo'yicha mahsulotlar (tanlangan omborga moslangan)
    val products: StateFlow<List<ProductEntity>> = combine(
        _searchQuery.flatMapLatest { query ->
            if (query.isBlank()) {
                productRepository.getAllProducts()
            } else {
                productRepository.searchProducts(query.trim())
            }
        },
        _currentViewCategory,
        _selectedWarehouse,
        stocksInSelectedWarehouse
    ) { productList, category, selectedWh, stocks ->
        val active = productList.filter { !it.isDeleted }
        val scopedList = if (selectedWh != null) {
            active.filter { stocks.containsKey(it.guid) }
                .map { it.copy(stockQuantity = stocks[it.guid] ?: 0.0) }
        } else {
            active
        }

        when {
            category == null || category == "Barchasi" -> scopedList
            category == CATEGORY_LOW_STOCK -> scopedList.filter { it.stockQuantity <= it.minStockAlert }
            else -> scopedList.filter { it.category.equals(category, ignoreCase = true) }
        }
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5000),
        initialValue = emptyList()
    )

    // Mahsulot qo'shish / tahrirlash formasi holati
    private val _isAddEditOpen = MutableStateFlow(false)
    val isAddEditOpen: StateFlow<Boolean> = _isAddEditOpen.asStateFlow()

    private val _editingProductId = MutableStateFlow<Long?>(null)
    val editingProductId: StateFlow<Long?> = _editingProductId.asStateFlow()

    var barcodeInput = MutableStateFlow("")
        private set
    var nameInput = MutableStateFlow("")
        private set
    var categoryInput = MutableStateFlow("Barchasi")
        private set
    var costPriceInput = MutableStateFlow("")
        private set
    var costCurrencyInput = MutableStateFlow(CurrencyType.UZS)
        private set
    var sellingPriceInput = MutableStateFlow("")
        private set
    var sellingPrice2Input = MutableStateFlow("")
        private set
    var stockQuantityInput = MutableStateFlow("")
        private set
    var unitTypeInput = MutableStateFlow(UnitType.DONA)
        private set
    var minStockAlertInput = MutableStateFlow("3.0")
        private set
    var noteInput = MutableStateFlow("")
        private set

    private val _errorMessage = MutableStateFlow<String?>(null)
    val errorMessage: StateFlow<String?> = _errorMessage.asStateFlow()

    companion object {
        fun normalizeProductName(name: String): String {
            return name.trim()
                .replace(Regex("['`’ʻ‘]"), "'")
                .lowercase(Locale.ROOT)
                .replace(Regex("\\s+"), " ")
        }

        fun capitalizeFirstLetter(input: String): String {
            if (input.isEmpty()) return input
            for (i in input.indices) {
                val c = input[i]
                if (c.isLetter()) {
                    if (c.isUpperCase()) return input
                    return input.substring(0, i) + c.uppercaseChar() + input.substring(i + 1)
                } else if (c.isDigit()) {
                    return input
                }
            }
            return input
        }
    }

    /**
     * Aqlli qidiruv: katta-kichik harf, boshidagi/oxiridagi va so'zlar orasidagi ortiqcha probellar,
     * tutuq belgilari inobatga olingan holda mavjud tovarlar ichidan tekshiradi.
     */
    fun findDuplicateProduct(name: String, excludeId: Long? = null): ProductEntity? {
        val normalized = normalizeProductName(name)
        if (normalized.isBlank()) return null
        return allProductsState.value.firstOrNull { product ->
            !product.isDeleted &&
            (excludeId == null || product.id != excludeId) &&
            normalizeProductName(product.name) == normalized
        }
    }

    fun onSearchQueryChanged(newQuery: String) {
        _searchQuery.value = newQuery
    }

    fun enterCategory(categoryName: String) {
        _currentViewCategory.value = categoryName
    }

    fun exitCategory() {
        _currentViewCategory.value = null
        _searchQuery.value = ""
    }

    fun openAddProductDialog(scannedBarcode: String? = null) {
        if (_selectedWarehouse.value == null) {
            viewModelScope.launch {
                val warehouses = warehouseRepository.getAllWarehousesList()
                val primaryWh = warehouses.firstOrNull { it.isPrimary } ?: warehouses.firstOrNull()
                _selectedWarehouse.value = primaryWh
            }
        }
        _editingProductId.value = null
        barcodeInput.value = scannedBarcode ?: ""
        nameInput.value = ""
        val currentCat = _currentViewCategory.value
        categoryInput.value = if (!currentCat.isNullOrBlank() && currentCat != "Barchasi" && currentCat != CATEGORY_LOW_STOCK) currentCat else "Barchasi"
        costPriceInput.value = ""
        costCurrencyInput.value = CurrencyType.UZS
        sellingPriceInput.value = ""
        sellingPrice2Input.value = ""
        stockQuantityInput.value = ""
        unitTypeInput.value = UnitType.DONA
        minStockAlertInput.value = "3.0"
        noteInput.value = ""
        _errorMessage.value = null
        _isAddEditOpen.value = true
    }

    fun openEditProductDialog(product: ProductEntity) {
        _editingProductId.value = product.id
        barcodeInput.value = product.barcode ?: ""
        nameInput.value = product.name
        categoryInput.value = if (product.category.isBlank()) "Barchasi" else product.category
        costPriceInput.value = if (product.costPrice % 1.0 == 0.0) product.costPrice.toLong().toString() else product.costPrice.toString()
        costCurrencyInput.value = if (product.costCurrency == "USD") CurrencyType.USD else CurrencyType.UZS
        sellingPriceInput.value = if (product.sellingPrice % 1.0 == 0.0) product.sellingPrice.toLong().toString() else product.sellingPrice.toString()
        sellingPrice2Input.value = product.sellingPrice2?.let { if (it % 1.0 == 0.0) it.toLong().toString() else it.toString() } ?: ""
        stockQuantityInput.value = if (product.stockQuantity % 1.0 == 0.0) product.stockQuantity.toLong().toString() else product.stockQuantity.toString()
        unitTypeInput.value = product.unitType
        minStockAlertInput.value = if (product.minStockAlert % 1.0 == 0.0) product.minStockAlert.toLong().toString() else product.minStockAlert.toString()
        noteInput.value = product.note
        _errorMessage.value = null
        _isAddEditOpen.value = true
    }

    fun closeDialog() {
        _isAddEditOpen.value = false
        _editingProductId.value = null
        _errorMessage.value = null
    }

    fun onBarcodeChanged(value: String) {
        barcodeInput.value = value
    }

    fun onNameChanged(value: String) {
        nameInput.value = capitalizeFirstLetter(value)
    }

    fun onCategoryChanged(value: String) {
        categoryInput.value = value
    }

    fun onCostPriceChanged(value: String) {
        costPriceInput.value = value
    }

    fun onCostCurrencyToggle() {
        costCurrencyInput.value = if (costCurrencyInput.value == CurrencyType.UZS) CurrencyType.USD else CurrencyType.UZS
    }

    fun onSellingPriceChanged(value: String) {
        sellingPriceInput.value = value
    }

    fun onSellingPrice2Changed(value: String) {
        sellingPrice2Input.value = value
    }

    fun onStockQuantityChanged(value: String) {
        stockQuantityInput.value = value
    }

    fun onUnitTypeChanged(value: UnitType) {
        unitTypeInput.value = value
    }

    fun onMinStockAlertChanged(value: String) {
        minStockAlertInput.value = value
    }

    fun onNoteChanged(value: String) {
        noteInput.value = value
    }

    fun saveProduct(onSuccess: () -> Unit = {}) {
        val rawName = capitalizeFirstLetter(nameInput.value)
        val name = rawName.trim().replace(Regex("\\s+"), " ")
        if (name.isBlank()) {
            _errorMessage.value = "Mahsulot nomini kiritish shart!"
            return
        }

        // Aqlli tekshirish: Bu nomdagi tovar oldin qo'shilganmi?
        val duplicate = findDuplicateProduct(name, _editingProductId.value)
        if (duplicate != null) {
            _errorMessage.value = "\"${duplicate.name}\" nomli mahsulot omborda allaqachon mavjud! Boshqa nom kiriting yoki qoldiqni yangilang."
            return
        }

        val costPrice = costPriceInput.value.trim().toDoubleOrNull()
        if (costPrice == null || costPrice < 0) {
            _errorMessage.value = "Tan narxini to'g'ri kiriting!"
            return
        }

        val sellingPrice = sellingPriceInput.value.trim().toDoubleOrNull()
        if (sellingPrice == null || sellingPrice < 0) {
            _errorMessage.value = "Sotish narxini to'g'ri kiriting!"
            return
        }

        val stock = stockQuantityInput.value.trim().toDoubleOrNull() ?: 0.0
        val minAlert = minStockAlertInput.value.trim().toDoubleOrNull() ?: 3.0
        val barcode = barcodeInput.value.trim().ifBlank { null }
        if (!barcode.isNullOrBlank()) {
            val duplicateBarcode = allProductsState.value.firstOrNull { 
                !it.isDeleted && (_editingProductId.value == null || it.id != _editingProductId.value) && it.barcode == barcode 
            }
            if (duplicateBarcode != null) {
                _errorMessage.value = "\"${duplicateBarcode.name}\" mahsuloti allaqachon ushbu shtrix-kod bilan mavjud!"
                return
            }
        }
        val category = categoryInput.value.trim().ifBlank { "Barchasi" }
        val note = noteInput.value.trim()

        viewModelScope.launch {
            try {
                val existing = _editingProductId.value?.let { id -> allProductsState.value.firstOrNull { it.id == id } }
                val product = ProductEntity(
                    id = _editingProductId.value ?: 0L,
                    guid = existing?.guid ?: java.util.UUID.randomUUID().toString(),
                    barcode = barcode,
                    name = name,
                    category = category,
                    costPrice = costPrice,
                    costCurrency = costCurrencyInput.value.name,
                    sellingPrice = sellingPrice,
                    sellingPrice2 = sellingPrice2Input.value.trim().toDoubleOrNull(),
                    stockQuantity = stock,
                    unitType = unitTypeInput.value,
                    minStockAlert = minAlert,
                    note = note,
                    updatedAt = System.currentTimeMillis()
                )
                // Tanlangan omborga yoki asosiy omborga qoldiqni kiritish
                var currentWh = _selectedWarehouse.value
                if (currentWh == null) {
                    val warehouses = warehouseRepository.getAllWarehousesList()
                    currentWh = warehouses.firstOrNull { it.isPrimary } ?: warehouses.firstOrNull()
                    _selectedWarehouse.value = currentWh
                }
                val whGuid = currentWh?.guid ?: "main-default-warehouse"
                productRepository.saveProduct(product, whGuid)

                _refreshTrigger.value = System.currentTimeMillis()
                _searchQuery.value = ""
                _currentViewCategory.value = category
                closeDialog()
                onSuccess()
            } catch (e: Exception) {
                _errorMessage.value = "Xatolik: Bu shtrix-kod boshqa mahsulotda mavjud bo'lishi mumkin."
            }
        }
    }

    fun deleteProduct(product: ProductEntity) {
        viewModelScope.launch {
            productRepository.deleteProduct(product)
            _refreshTrigger.value = System.currentTimeMillis()
        }
    }

    fun selectWarehouse(warehouse: WarehouseEntity?) {
        _selectedWarehouse.value = warehouse
        _currentViewCategory.value = null
        _searchQuery.value = ""
    }

    fun exitWarehouse() {
        _selectedWarehouse.value = null
        _currentViewCategory.value = null
        _searchQuery.value = ""
    }

    fun openAddWarehouseDialog() {
        _isAddWarehouseOpen.value = true
    }

    fun closeAddWarehouseDialog() {
        _isAddWarehouseOpen.value = false
    }

    fun saveWarehouse(name: String, isPrimary: Boolean) {
        viewModelScope.launch {
            warehouseRepository.saveWarehouse(name, isPrimary)
            closeAddWarehouseDialog()
        }
    }

    fun setPrimaryWarehouse(guid: String) {
        viewModelScope.launch {
            warehouseRepository.setPrimaryWarehouse(guid)
        }
    }

    fun deleteWarehouse(guid: String) {
        viewModelScope.launch {
            warehouseRepository.deleteWarehouse(guid)
            if (_selectedWarehouse.value?.guid == guid) {
                _selectedWarehouse.value = null
            }
        }
    }

    fun openTransferDialog() {
        _isTransferOpen.value = true
    }

    fun closeTransferDialog() {
        _isTransferOpen.value = false
    }

    fun transferStock(productGuid: String, fromWh: String, toWh: String, quantity: Double) {
        viewModelScope.launch {
            warehouseRepository.transferStock(productGuid, fromWh, toWh, quantity)
            _refreshTrigger.value = System.currentTimeMillis()
            closeTransferDialog()
        }
    }
}
