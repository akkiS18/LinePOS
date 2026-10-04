package uz.pos.electro.ui.reports

import android.content.Context
import android.widget.Toast
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import uz.pos.electro.data.local.entity.WarehouseEntity
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.ReportsSummary
import uz.pos.electro.data.model.SaleReportItem
import uz.pos.electro.data.model.TimeRangeFilter
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.repository.CurrencyRepository
import uz.pos.electro.data.repository.ProductRepository
import uz.pos.electro.data.repository.SaleRepository
import uz.pos.electro.data.repository.WarehouseRepository
import uz.pos.electro.util.ExcelExporter
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale
import javax.inject.Inject

@HiltViewModel
class ReportsViewModel @Inject constructor(
    private val saleRepository: SaleRepository,
    private val productRepository: ProductRepository,
    private val warehouseRepository: WarehouseRepository,
    private val currencyRepository: CurrencyRepository
) : ViewModel() {

    private val _selectedFilter = MutableStateFlow(TimeRangeFilter.TODAY)
    val selectedFilter: StateFlow<TimeRangeFilter> = _selectedFilter.asStateFlow()

    private val _selectedCategory = MutableStateFlow("Barchasi")
    val selectedCategory: StateFlow<String> = _selectedCategory.asStateFlow()

    private val _selectedWarehouseGuid = MutableStateFlow("Barchasi")
    val selectedWarehouseGuid: StateFlow<String> = _selectedWarehouseGuid.asStateFlow()

    val categories: StateFlow<List<String>> = combine(productRepository.getDistinctCategories(), saleRepository.getHistoricalCategories()) { current, historical -> listOf("Barchasi") + (current + historical).distinct().sorted() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), listOf("Barchasi"))

    val warehouses: StateFlow<List<WarehouseEntity>> = warehouseRepository.getAllWarehouses()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    private val _summary = MutableStateFlow(ReportsSummary())
    val summary: StateFlow<ReportsSummary> = _summary.asStateFlow()

    private val _salesList = MutableStateFlow<List<SaleWithItems>>(emptyList())
    val salesList: StateFlow<List<SaleWithItems>> = _salesList.asStateFlow()

    private val _detailedReportItems = MutableStateFlow<List<SaleReportItem>>(emptyList())
    val detailedReportItems: StateFlow<List<SaleReportItem>> = _detailedReportItems.asStateFlow()

    private val _selectedSaleForDetail = MutableStateFlow<SaleWithItems?>(null)
    val selectedSaleForDetail: StateFlow<SaleWithItems?> = _selectedSaleForDetail.asStateFlow()

    private val _customStartDate = MutableStateFlow(System.currentTimeMillis())
    val customStartDate: StateFlow<Long> = _customStartDate.asStateFlow()

    private val _customEndDate = MutableStateFlow(System.currentTimeMillis())
    val customEndDate: StateFlow<Long> = _customEndDate.asStateFlow()

    private val _usdRate = MutableStateFlow(currencyRepository.getCachedUsdRate())
    val usdRate: StateFlow<Double> = _usdRate.asStateFlow()

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing.asStateFlow()

    // --- EXCEL EXPORT MODAL PROPERTIES ---
    private val _isExportModalOpen = MutableStateFlow(false)
    val isExportModalOpen: StateFlow<Boolean> = _isExportModalOpen.asStateFlow()

    private val _exportFilter = MutableStateFlow(TimeRangeFilter.TODAY)
    val exportFilter: StateFlow<TimeRangeFilter> = _exportFilter.asStateFlow()

    private val _exportStartDate = MutableStateFlow(System.currentTimeMillis())
    val exportStartDate: StateFlow<Long> = _exportStartDate.asStateFlow()

    private val _exportEndDate = MutableStateFlow(System.currentTimeMillis())
    val exportEndDate: StateFlow<Long> = _exportEndDate.asStateFlow()

    private val _exportSelectedCategory = MutableStateFlow("Barchasi")
    val exportSelectedCategory: StateFlow<String> = _exportSelectedCategory.asStateFlow()

    private val _exportSelectedWarehouseGuid = MutableStateFlow("Barchasi")
    val exportSelectedWarehouseGuid: StateFlow<String> = _exportSelectedWarehouseGuid.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery = _searchQuery.asStateFlow()
    private val _recordKind = MutableStateFlow("Barchasi")
    val recordKind = _recordKind.asStateFlow()
    private var rawSales: List<SaleWithItems> = emptyList()
    private var rangeStart = 0L
    private var rangeEnd = Long.MAX_VALUE
    fun setSearchQuery(value: String) { _searchQuery.value = value; loadCustomReportData(rangeStart, rangeEnd) }
    fun setRecordKind(value: String) { _recordKind.value = value; applyFilters() }
    private fun kindMatches(item: uz.pos.electro.data.model.SaleReportItem) = when (_recordKind.value) {
        "Savdo" -> !item.isBrak && !item.isReturn; "Brak" -> item.isBrak; "Qaytarish" -> item.isReturn; else -> true
    }
    private fun applyFilters() {
        val query = _searchQuery.value.trim().removePrefix("#").uppercase(java.util.Locale.ROOT)
        val matched = rawSales.filter { query.isBlank() || it.sale.receiptNumber.contains(query) ||
            it.sale.guid.replace("-", "").contains(query.removePrefix("LP-").replace("-", ""), true) || it.sale.id.toString().contains(query) }
        val lines = matched.flatMap { uz.pos.electro.data.model.SaleAccounting.lines(it) }.filter {
            kindMatches(it) && (_selectedCategory.value == "Barchasi" || it.category.equals(_selectedCategory.value, true)) &&
            (_selectedWarehouseGuid.value == "Barchasi" || it.warehouseGuid == _selectedWarehouseGuid.value)
        }
        val ids = lines.map { it.saleId }.toSet()
        _salesList.value = matched.filter { it.sale.id in ids }
        _detailedReportItems.value = lines
        _summary.value = uz.pos.electro.data.model.SaleAccounting.summary(lines, _usdRate.value)
    }

    private var reportCollectionJob: Job? = null

    init {
        loadReportData(TimeRangeFilter.TODAY)
        // Ilova ochilganda orqa fonda dollar kursini yangilab qo'yish
        viewModelScope.launch {
            currencyRepository.fetchLatestUsdRate().onSuccess { newRate ->
                _usdRate.value = newRate
                recalculateCurrentSummary()
            }
        }
    }

    fun setFilter(filter: TimeRangeFilter) {
        _selectedFilter.value = filter
        if (filter != TimeRangeFilter.CUSTOM) {
            loadReportData(filter)
        }
    }

    fun setCustomRange(start: Long, end: Long) {
        _customStartDate.value = start
        _customEndDate.value = end
        _selectedFilter.value = TimeRangeFilter.CUSTOM
        loadCustomReportData(start, end)
    }

    fun selectSaleForDetail(sale: SaleWithItems) {
        _selectedSaleForDetail.value = sale
    }

    fun closeSaleDetail() {
        _selectedSaleForDetail.value = null
    }

    // --- EXCEL MODAL BOSHQARUVI ---
    fun openExportModal() {
        _exportFilter.value = _selectedFilter.value
        _exportStartDate.value = _customStartDate.value
        _exportEndDate.value = _customEndDate.value
        _exportSelectedCategory.value = _selectedCategory.value
        _exportSelectedWarehouseGuid.value = _selectedWarehouseGuid.value
        _isExportModalOpen.value = true
    }

    fun closeExportModal() {
        _isExportModalOpen.value = false
    }

    fun setExportFilter(filter: TimeRangeFilter) {
        _exportFilter.value = filter
    }

    fun setExportCustomRange(start: Long, end: Long) {
        _exportStartDate.value = start
        _exportEndDate.value = end
        _exportFilter.value = TimeRangeFilter.CUSTOM
    }

    fun setExportCategory(category: String) {
        _exportSelectedCategory.value = category
    }

    fun setExportWarehouse(guid: String) {
        _exportSelectedWarehouseGuid.value = guid
    }

    /**
     * Markaziy bankdan kursni va hisobot ma'lumotlarini to'liq dinamik yangilash
     */
    fun refreshReportData(context: Context) {
        viewModelScope.launch {
            _isRefreshing.value = true
            currencyRepository.fetchLatestUsdRate().onSuccess { newRate ->
                _usdRate.value = newRate
            }

            val (start, end) = if (_selectedFilter.value == TimeRangeFilter.CUSTOM) {
                Pair(_customStartDate.value, _customEndDate.value)
            } else {
                calculateTimestamps(_selectedFilter.value)
            }
            loadCustomReportData(start, end)

            _isRefreshing.value = false
            val numFormat = NumberFormat.getNumberInstance(Locale.US)
            Toast.makeText(
                context,
                "✅ Hisobot va CBU kursi yangilandi: 1$ = ${numFormat.format(_usdRate.value)} so'm",
                Toast.LENGTH_SHORT
            ).show()
        }
    }

    fun refreshUsdRate(context: Context) {
        refreshReportData(context)
    }

    private fun loadReportData(filter: TimeRangeFilter) {
        val (start, end) = calculateTimestamps(filter)
        loadCustomReportData(start, end)
    }

    private fun loadCustomReportData(start: Long, end: Long) {
        rangeStart = start; rangeEnd = end
        reportCollectionJob?.cancel()
        reportCollectionJob = viewModelScope.launch {
            val source = if (_searchQuery.value.isBlank()) saleRepository.getSalesBetween(start, end) else saleRepository.getAllSales()
            source.collect { sales -> rawSales = sales; applyFilters() }
        }
    }

    private fun recalculateCurrentSummary() = applyFilters()
    fun setCategoryFilter(category: String) { _selectedCategory.value = category; applyFilters() }
    fun setWarehouseFilter(whGuid: String) { _selectedWarehouseGuid.value = whGuid; applyFilters() }

    fun exportToExcel(context: Context) {
        viewModelScope.launch {
            val (start, end) = if (_exportFilter.value == TimeRangeFilter.CUSTOM) {
                Pair(_exportStartDate.value, _exportEndDate.value)
            } else {
                calculateTimestamps(_exportFilter.value)
            }

            val catFilter = if (_exportSelectedCategory.value == "Barchasi") null else _exportSelectedCategory.value
            val whFilter = if (_exportSelectedWarehouseGuid.value == "Barchasi") null else _exportSelectedWarehouseGuid.value

            val detailedItems = saleRepository.getDetailedReportItems(
                startTimestamp = start,
                endTimestamp = end,
                categoryFilter = catFilter,
                warehouseGuidFilter = whFilter
            ).filter { kindMatches(it) }

            var periodTitle = when (_exportFilter.value) {
                TimeRangeFilter.TODAY -> "Bugun"
                TimeRangeFilter.YESTERDAY -> "Kecha"
                TimeRangeFilter.THIS_MONTH -> "Shu oy"
                TimeRangeFilter.CUSTOM -> {
                    val df = SimpleDateFormat("dd.MM.yyyy", Locale.getDefault())
                    "${df.format(Date(start))} - ${df.format(Date(end))}"
                }
            }

            if (catFilter != null) {
                periodTitle += " | Kat: $catFilter"
            }
            if (whFilter != null) {
                val whName = warehouses.value.find { it.guid == whFilter }?.name ?: "Ombor"
                periodTitle += " | Ombor: $whName"
            }

            periodTitle += " | ${_recordKind.value}"
            val exportSummary = uz.pos.electro.data.model.SaleAccounting.summary(detailedItems, _usdRate.value)

            val result = ExcelExporter.exportAndShareReport(
                context = context,
                periodTitle = periodTitle,
                summary = exportSummary,
                items = detailedItems
            )

            result.onSuccess {
                _isExportModalOpen.value = false
                Toast.makeText(context, "✅ Excel hisoboti tayyorlandi", Toast.LENGTH_SHORT).show()
            }.onFailure { error ->
                Toast.makeText(context, "Eksportda xatolik: ${error.localizedMessage}", Toast.LENGTH_LONG).show()
            }
        }
    }

    private fun calculateTimestamps(filter: TimeRangeFilter): Pair<Long, Long> {
        val calendar = Calendar.getInstance()

        return when (filter) {
            TimeRangeFilter.TODAY -> {
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
                val start = calendar.timeInMillis

                calendar.set(Calendar.HOUR_OF_DAY, 23)
                calendar.set(Calendar.MINUTE, 59)
                calendar.set(Calendar.SECOND, 59)
                calendar.set(Calendar.MILLISECOND, 999)
                val end = calendar.timeInMillis

                Pair(start, end)
            }

            TimeRangeFilter.YESTERDAY -> {
                calendar.add(Calendar.DAY_OF_YEAR, -1)
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
                val start = calendar.timeInMillis

                calendar.set(Calendar.HOUR_OF_DAY, 23)
                calendar.set(Calendar.MINUTE, 59)
                calendar.set(Calendar.SECOND, 59)
                calendar.set(Calendar.MILLISECOND, 999)
                val end = calendar.timeInMillis

                Pair(start, end)
            }

            TimeRangeFilter.THIS_MONTH -> {
                calendar.set(Calendar.DAY_OF_MONTH, 1)
                calendar.set(Calendar.HOUR_OF_DAY, 0)
                calendar.set(Calendar.MINUTE, 0)
                calendar.set(Calendar.SECOND, 0)
                calendar.set(Calendar.MILLISECOND, 0)
                val start = calendar.timeInMillis

                calendar.set(Calendar.DAY_OF_MONTH, calendar.getActualMaximum(Calendar.DAY_OF_MONTH))
                calendar.set(Calendar.HOUR_OF_DAY, 23)
                calendar.set(Calendar.MINUTE, 59)
                calendar.set(Calendar.SECOND, 59)
                calendar.set(Calendar.MILLISECOND, 999)
                val end = calendar.timeInMillis

                Pair(start, end)
            }

            TimeRangeFilter.CUSTOM -> {
                Pair(_customStartDate.value, _customEndDate.value)
            }
        }
    }
}
