package uz.pos.electro.ui.debt

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import uz.pos.electro.data.debt.DebtAccountItemDto
import uz.pos.electro.data.debt.DebtCustomerDetailDto
import uz.pos.electro.data.debt.DebtCustomerItemDto
import uz.pos.electro.data.debt.DebtFilter
import uz.pos.electro.data.debt.DebtPaymentAllocationPreview
import uz.pos.electro.data.debt.DebtService
import uz.pos.electro.data.debt.DebtSummaryDto
import javax.inject.Inject

@HiltViewModel
class DebtsViewModel @Inject constructor(
    private val debtService: DebtService
) : ViewModel() {

    private val _summary = MutableStateFlow(DebtSummaryDto())
    val summary: StateFlow<DebtSummaryDto> = _summary.asStateFlow()

    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    private val _selectedFilter = MutableStateFlow(DebtFilter.ALL)
    val selectedFilter: StateFlow<DebtFilter> = _selectedFilter.asStateFlow()

    private val _customers = MutableStateFlow<List<DebtCustomerItemDto>>(emptyList())
    val customers: StateFlow<List<DebtCustomerItemDto>> = _customers.asStateFlow()

    private val _selectedCustomer = MutableStateFlow<DebtCustomerItemDto?>(null)
    val selectedCustomer: StateFlow<DebtCustomerItemDto?> = _selectedCustomer.asStateFlow()

    private val _selectedCustomerDetails = MutableStateFlow<DebtCustomerDetailDto?>(null)
    val selectedCustomerDetails: StateFlow<DebtCustomerDetailDto?> = _selectedCustomerDetails.asStateFlow()

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    // Mijoz yaratish / tahrirlash dialogi
    private val _isCustomerDialogOpen = MutableStateFlow(false)
    val isCustomerDialogOpen: StateFlow<Boolean> = _isCustomerDialogOpen.asStateFlow()

    private val _editingCustomer = MutableStateFlow<DebtCustomerItemDto?>(null)
    val editingCustomer: StateFlow<DebtCustomerItemDto?> = _editingCustomer.asStateFlow()

    // To'lov olish dialogi
    private val _isPaymentDialogOpen = MutableStateFlow(false)
    val isPaymentDialogOpen: StateFlow<Boolean> = _isPaymentDialogOpen.asStateFlow()

    private val _paymentTargetAccount = MutableStateFlow<DebtAccountItemDto?>(null)
    val paymentTargetAccount: StateFlow<DebtAccountItemDto?> = _paymentTargetAccount.asStateFlow()

    private val _paymentCashAmount = MutableStateFlow("0")
    val paymentCashAmount: StateFlow<String> = _paymentCashAmount.asStateFlow()

    private val _paymentCardAmount = MutableStateFlow("0")
    val paymentCardAmount: StateFlow<String> = _paymentCardAmount.asStateFlow()

    private val _paymentAllocationPreview = MutableStateFlow<DebtPaymentAllocationPreview?>(null)
    val paymentAllocationPreview: StateFlow<DebtPaymentAllocationPreview?> = _paymentAllocationPreview.asStateFlow()

    private val _isSubmittingPayment = MutableStateFlow(false)
    val isSubmittingPayment: StateFlow<Boolean> = _isSubmittingPayment.asStateFlow()

    // Xabarlar
    private val _toastEvent = MutableSharedFlow<String>()
    val toastEvent: SharedFlow<String> = _toastEvent.asSharedFlow()

    init {
        loadData()
    }

    fun loadData() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                refreshSummary()
                refreshCustomers()
                _selectedCustomer.value?.let { current ->
                    loadCustomerDetails(current.guid)
                }
            } catch (e: Throwable) {
                _toastEvent.emit("Ma'lumotlarni yuklashda xatolik: ${e.message}")
            } finally {
                _isLoading.value = false
            }
        }
    }

    private suspend fun refreshSummary() {
        try {
            _summary.value = debtService.getSummary()
        } catch (_: Throwable) { }
    }

    private suspend fun refreshCustomers() {
        try {
            val list = debtService.getCustomerList(
                search = _searchQuery.value,
                filter = _selectedFilter.value
            )
            _customers.value = list
            // Agar tanlangan mijoz yangilangan ro'yxatda bo'lsa, uni ham sinxronlaymiz
            val sel = _selectedCustomer.value
            if (sel != null) {
                _selectedCustomer.value = list.find { it.guid == sel.guid }
            }
        } catch (_: Throwable) { }
    }

    fun onSearchQueryChanged(query: String) {
        _searchQuery.value = query
        viewModelScope.launch {
            refreshCustomers()
        }
    }

    fun onFilterChanged(filter: DebtFilter) {
        _selectedFilter.value = filter
        viewModelScope.launch {
            refreshCustomers()
        }
    }

    fun selectCustomer(customer: DebtCustomerItemDto?) {
        _selectedCustomer.value = customer
        if (customer != null) {
            viewModelScope.launch {
                loadCustomerDetails(customer.guid)
            }
        } else {
            _selectedCustomerDetails.value = null
        }
    }

    private suspend fun loadCustomerDetails(customerGuid: String) {
        try {
            _selectedCustomerDetails.value = debtService.getCustomerDetails(customerGuid)
        } catch (e: Throwable) {
            _toastEvent.emit("Mijoz tafsilotlarini yuklashda xatolik: ${e.message}")
        }
    }

    fun openCreateCustomerDialog() {
        _editingCustomer.value = null
        _isCustomerDialogOpen.value = true
    }

    fun openEditCustomerDialog(customer: DebtCustomerItemDto) {
        _editingCustomer.value = customer
        _isCustomerDialogOpen.value = true
    }

    fun closeCustomerDialog() {
        _isCustomerDialogOpen.value = false
        _editingCustomer.value = null
    }

    fun saveCustomer(name: String, phone: String, note: String) {
        if (name.isBlank()) {
            viewModelScope.launch { _toastEvent.emit("Mijoz ismi kiritilishi shart") }
            return
        }
        viewModelScope.launch {
            try {
                val editing = _editingCustomer.value
                if (editing != null) {
                    debtService.updateCustomer(
                        customerGuid = editing.guid,
                        name = name,
                        phone = phone,
                        note = note,
                        revision = editing.revision
                    )
                    _toastEvent.emit("Mijoz ma'lumotlari yangilandi")
                } else {
                    val newGuid = debtService.createCustomer(name, phone, note)
                    _toastEvent.emit("Yangi mijoz qo'shildi")
                }
                closeCustomerDialog()
                loadData()
            } catch (e: Throwable) {
                _toastEvent.emit("Xatolik: ${e.message}")
            }
        }
    }

    fun toggleArchiveCustomer(customer: DebtCustomerItemDto) {
        viewModelScope.launch {
            try {
                val newArchived = !customer.archived
                debtService.archiveCustomer(customer.guid, newArchived)
                val msg = if (newArchived) "Mijoz arxivlandi" else "Mijoz arxivdan chiqarildi"
                _toastEvent.emit(msg)
                loadData()
            } catch (e: Throwable) {
                _toastEvent.emit("Xatolik: ${e.message}")
            }
        }
    }

    fun openPaymentDialog(customer: DebtCustomerItemDto, targetAccount: DebtAccountItemDto? = null) {
        _selectedCustomer.value = customer
        _paymentTargetAccount.value = targetAccount
        _paymentCashAmount.value = "0"
        _paymentCardAmount.value = "0"
        _paymentAllocationPreview.value = null
        _isPaymentDialogOpen.value = true

        // Agar bitta maqsadli account tanlangan bo'lsa, uning balansini dastlabki summa qilib qo'yishimiz mumkin
        if (targetAccount != null && targetAccount.balanceMinor > 0) {
            val initial = (targetAccount.balanceMinor / 100.0).toLong().toString()
            _paymentCashAmount.value = initial
            recalculatePaymentPreview(initial, "0")
        }
    }

    fun closePaymentDialog() {
        _isPaymentDialogOpen.value = false
        _paymentTargetAccount.value = null
        _paymentCashAmount.value = "0"
        _paymentCardAmount.value = "0"
        _paymentAllocationPreview.value = null
    }

    fun onPaymentCashChanged(cashStr: String) {
        val filtered = cashStr.filter { it.isDigit() }
        _paymentCashAmount.value = filtered
        recalculatePaymentPreview(filtered, _paymentCardAmount.value)
    }

    fun onPaymentCardChanged(cardStr: String) {
        val filtered = cardStr.filter { it.isDigit() }
        _paymentCardAmount.value = filtered
        recalculatePaymentPreview(_paymentCashAmount.value, filtered)
    }

    private fun recalculatePaymentPreview(cashStr: String, cardStr: String) {
        val cust = _selectedCustomer.value ?: return
        val cashVal = cashStr.toLongOrNull() ?: 0L
        val cardVal = cardStr.toLongOrNull() ?: 0L
        val totalPaymentMinor = (cashVal + cardVal) * 100L

        if (totalPaymentMinor <= 0L) {
            _paymentAllocationPreview.value = null
            return
        }

        viewModelScope.launch {
            try {
                val preview = debtService.previewPayment(
                    customerGuid = cust.guid,
                    paymentMinor = totalPaymentMinor,
                    targetAccountGuid = _paymentTargetAccount.value?.accountGuid
                )
                _paymentAllocationPreview.value = preview
            } catch (_: Throwable) {
                _paymentAllocationPreview.value = null
            }
        }
    }

    fun submitPayment() {
        val cust = _selectedCustomer.value ?: return
        val cashVal = _paymentCashAmount.value.toLongOrNull() ?: 0L
        val cardVal = _paymentCardAmount.value.toLongOrNull() ?: 0L
        val cashMinor = cashVal * 100L
        val cardMinor = cardVal * 100L
        val totalPaymentMinor = cashMinor + cardMinor

        if (totalPaymentMinor <= 0L) {
            viewModelScope.launch { _toastEvent.emit("To'lov summasi 0 dan katta bo'lishi kerak") }
            return
        }

        val maxAllowedMinor = _paymentTargetAccount.value?.balanceMinor ?: cust.balanceMinor
        if (totalPaymentMinor > maxAllowedMinor) {
            viewModelScope.launch {
                _toastEvent.emit("Ortiqcha to'lov qabul qilinmaydi! Maksimal qarz: ${(maxAllowedMinor / 100.0).toLong()} so'm")
            }
            return
        }

        if (_isSubmittingPayment.value) return
        _isSubmittingPayment.value = true

        viewModelScope.launch {
            try {
                debtService.recordPayment(
                    customerGuid = cust.guid,
                    cashMinor = cashMinor,
                    cardMinor = cardMinor,
                    targetAccountGuid = _paymentTargetAccount.value?.accountGuid
                )
                _toastEvent.emit("To'lov muvaffaqiyatli qabul qilindi!")
                closePaymentDialog()
                loadData()
            } catch (e: Throwable) {
                _toastEvent.emit("To'lovni saqlashda xatolik: ${e.message}")
            } finally {
                _isSubmittingPayment.value = false
            }
        }
    }
}
