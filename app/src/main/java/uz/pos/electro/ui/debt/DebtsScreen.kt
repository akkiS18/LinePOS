package uz.pos.electro.ui.debt

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.MenuBook
import androidx.compose.material.icons.filled.Archive
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.EventNote
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PersonAdd
import androidx.compose.material.icons.filled.PointOfSale
import androidx.compose.material.icons.filled.ReceiptLong
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Unarchive
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.flow.collectLatest
import uz.pos.electro.data.debt.DebtAccountItemDto
import uz.pos.electro.data.debt.DebtCustomerDetailDto
import uz.pos.electro.data.debt.DebtCustomerItemDto
import uz.pos.electro.data.debt.DebtEventItemDto
import uz.pos.electro.data.debt.DebtFilter
import uz.pos.electro.data.debt.DebtPaymentAllocationPreview
import uz.pos.electro.data.debt.DebtSummaryDto
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun DebtsScreen(
    viewModel: DebtsViewModel,
    onOpenCustomerInCashier: (DebtCustomerItemDto) -> Unit = {}
) {
    val context = LocalContext.current
    val summary by viewModel.summary.collectAsState()
    val searchQuery by viewModel.searchQuery.collectAsState()
    val selectedFilter by viewModel.selectedFilter.collectAsState()
    val customers by viewModel.customers.collectAsState()
    val selectedCustomer by viewModel.selectedCustomer.collectAsState()
    val selectedCustomerDetails by viewModel.selectedCustomerDetails.collectAsState()
    val isLoading by viewModel.isLoading.collectAsState()

    val isCustomerDialogOpen by viewModel.isCustomerDialogOpen.collectAsState()
    val editingCustomer by viewModel.editingCustomer.collectAsState()

    val isPaymentDialogOpen by viewModel.isPaymentDialogOpen.collectAsState()
    val paymentTargetAccount by viewModel.paymentTargetAccount.collectAsState()
    val paymentCashAmount by viewModel.paymentCashAmount.collectAsState()
    val paymentCardAmount by viewModel.paymentCardAmount.collectAsState()
    val paymentAllocationPreview by viewModel.paymentAllocationPreview.collectAsState()
    val isSubmittingPayment by viewModel.isSubmittingPayment.collectAsState()

    // Toast signallarini tinglash
    LaunchedEffect(Unit) {
        viewModel.toastEvent.collectLatest { msg ->
            Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
        }
    }

    // Agar mijoz tanlangan bo'lsa, Back bosganda mijozlar ro'yxatiga qaytish
    BackHandler(enabled = selectedCustomer != null) {
        viewModel.selectCustomer(null)
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        if (selectedCustomer != null) {
            // Tanlangan mijoz tafsilotlari (Detail View)
            CustomerDetailView(
                customer = selectedCustomer!!,
                details = selectedCustomerDetails,
                onBack = { viewModel.selectCustomer(null) },
                onOpenInCashier = { onOpenCustomerInCashier(selectedCustomer!!) },
                onPayDebt = { targetAccount ->
                    viewModel.openPaymentDialog(selectedCustomer!!, targetAccount)
                },
                onEditCustomer = { viewModel.openEditCustomerDialog(selectedCustomer!!) },
                onToggleArchive = { viewModel.toggleArchiveCustomer(selectedCustomer!!) }
            )
        } else {
            // Asosiy Qarzlar ro'yxati va KPI lari
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 14.dp, vertical = 10.dp)
            ) {
                // 1. KPI kartalari (4 ta ko'rsatkich)
                DebtKpiRow(summary = summary, onRefresh = { viewModel.loadData() })

                Spacer(modifier = Modifier.height(12.dp))

                // 2. Qidiruv va Yangi mijoz tugmasi
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = searchQuery,
                        onValueChange = { viewModel.onSearchQueryChanged(it) },
                        placeholder = { Text("Mijoz ismi yoki telefon...", fontSize = 14.sp) },
                        leadingIcon = {
                            Icon(imageVector = Icons.Default.Search, contentDescription = null, tint = LinePrimary)
                        },
                        trailingIcon = {
                            if (searchQuery.isNotBlank()) {
                                IconButton(onClick = { viewModel.onSearchQueryChanged("") }) {
                                    Icon(imageVector = Icons.Default.Close, contentDescription = "Tozalash")
                                }
                            }
                        },
                        singleLine = true,
                        shape = RoundedCornerShape(14.dp),
                        modifier = Modifier.weight(1f)
                    )

                    Button(
                        onClick = { viewModel.openCreateCustomerDialog() },
                        shape = RoundedCornerShape(14.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                        contentPadding = PaddingValues(horizontal = 14.dp, vertical = 14.dp)
                    ) {
                        Icon(imageVector = Icons.Default.PersonAdd, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("+ Yangi", fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                // 3. Filtr chipslari (5 ta rejim)
                DebtFilterChips(
                    selectedFilter = selectedFilter,
                    onSelectFilter = { viewModel.onFilterChanged(it) }
                )

                Spacer(modifier = Modifier.height(10.dp))

                // 4. Mijozlar ro'yxati
                if (isLoading && customers.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        CircularProgressIndicator(color = LinePrimary)
                    }
                } else if (customers.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.MenuBook,
                                contentDescription = null,
                                modifier = Modifier.size(48.dp),
                                tint = MaterialTheme.colorScheme.outline
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                            Text(
                                text = "Mijozlar topilmadi",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = if (searchQuery.isNotBlank()) "Qidiruv so'zini o'zgartirib ko'ring" else "Yangi mijoz qo'shish uchun '+ Yangi' tugmasini bosing",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                        contentPadding = PaddingValues(bottom = 80.dp)
                    ) {
                        items(customers, key = { it.guid }) { customer ->
                            CustomerListItemCard(
                                customer = customer,
                                onClick = { viewModel.selectCustomer(customer) },
                                onQuickPay = { viewModel.openPaymentDialog(customer) },
                                onOpenInCashier = { onOpenCustomerInCashier(customer) }
                            )
                        }
                    }
                }
            }
        }

        // Mijoz qo'shish / tahrirlash dialogi
        if (isCustomerDialogOpen) {
            CustomerFormDialog(
                editingCustomer = editingCustomer,
                onDismiss = { viewModel.closeCustomerDialog() },
                onSave = { name, phone, note ->
                    viewModel.saveCustomer(name, phone, note)
                }
            )
        }

        // Qarz to'lovini qabul qilish dialogi
        if (isPaymentDialogOpen && selectedCustomer != null) {
            PaymentCollectionDialog(
                customer = selectedCustomer!!,
                targetAccount = paymentTargetAccount,
                cashAmount = paymentCashAmount,
                cardAmount = paymentCardAmount,
                allocationPreview = paymentAllocationPreview,
                isSubmitting = isSubmittingPayment,
                onCashChanged = { viewModel.onPaymentCashChanged(it) },
                onCardChanged = { viewModel.onPaymentCardChanged(it) },
                onDismiss = { viewModel.closePaymentDialog() },
                onConfirm = { viewModel.submitPayment() }
            )
        }
    }
}

@Composable
private fun DebtKpiRow(
    summary: DebtSummaryDto,
    onRefresh: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            KpiMiniCard(
                title = "Jami Nasiya",
                value = "${numberFormat.format(summary.totalActiveDebtUz)} so'm",
                subtitle = "${summary.debtorsCount} ta mijoz",
                accentColor = Color(0xFFDC2626),
                modifier = Modifier.weight(1f)
            )

            KpiMiniCard(
                title = "Muddati O'tgan",
                value = "${numberFormat.format(summary.overdueDebtUz)} so'm",
                subtitle = "${summary.overdueDebtorsCount} ta qarzdor",
                accentColor = Color(0xFFEA580C),
                modifier = Modifier.weight(1f)
            )
        }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            KpiMiniCard(
                title = "Jami To'langan",
                value = "${numberFormat.format(summary.totalPaidUz)} so'm",
                subtitle = "Tarixiy yig'im",
                accentColor = Color(0xFF16A34A),
                modifier = Modifier.weight(1f)
            )

            KpiMiniCard(
                title = "Haqdorlik (Kredit)",
                value = "${numberFormat.format(summary.totalCreditUz)} so'm",
                subtitle = "${summary.creditCustomersCount} ta mijoz",
                accentColor = Color(0xFF0284C7),
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun KpiMiniCard(
    title: String,
    value: String,
    subtitle: String,
    accentColor: Color,
    modifier: Modifier = Modifier
) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = title,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = FontWeight.SemiBold
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = value,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Black,
                color = accentColor,
                fontSize = 15.sp
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.8f),
                fontSize = 11.sp
            )
        }
    }
}

@Composable
private fun DebtFilterChips(
    selectedFilter: DebtFilter,
    onSelectFilter: (DebtFilter) -> Unit
) {
    LazyRow(
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        item {
            FilterChipItem(
                label = "Barchasi",
                selected = selectedFilter == DebtFilter.ALL,
                onClick = { onSelectFilter(DebtFilter.ALL) }
            )
        }
        item {
            FilterChipItem(
                label = "Qarzdorlar",
                selected = selectedFilter == DebtFilter.ACTIVE_DEBT,
                onClick = { onSelectFilter(DebtFilter.ACTIVE_DEBT) }
            )
        }
        item {
            FilterChipItem(
                label = "Muddati o'tgan",
                selected = selectedFilter == DebtFilter.OVERDUE,
                onClick = { onSelectFilter(DebtFilter.OVERDUE) }
            )
        }
        item {
            FilterChipItem(
                label = "To'langan",
                selected = selectedFilter == DebtFilter.SETTLED,
                onClick = { onSelectFilter(DebtFilter.SETTLED) }
            )
        }
        item {
            FilterChipItem(
                label = "Arxiv",
                selected = selectedFilter == DebtFilter.CREDIT,
                onClick = { onSelectFilter(DebtFilter.CREDIT) }
            )
        }
    }
}

@Composable
private fun FilterChipItem(
    label: String,
    selected: Boolean,
    onClick: () -> Unit
) {
    FilterChip(
        selected = selected,
        onClick = onClick,
        label = { Text(label, fontSize = 12.sp, fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal) },
        shape = RoundedCornerShape(12.dp),
        colors = FilterChipDefaults.filterChipColors(
            selectedContainerColor = LinePrimary,
            selectedLabelColor = Color.White
        )
    )
}

@Composable
private fun CustomerListItemCard(
    customer: DebtCustomerItemDto,
    onClick: () -> Unit,
    onQuickPay: () -> Unit,
    onOpenInCashier: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }

    Card(
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(
            1.dp,
            if (customer.isOverdue) Color(0xFFEF4444).copy(alpha = 0.5f)
            else MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)
        ),
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() }
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = customer.name,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    if (customer.archived) {
                        Spacer(modifier = Modifier.width(6.dp))
                        Surface(
                            shape = RoundedCornerShape(6.dp),
                            color = MaterialTheme.colorScheme.surfaceVariant
                        ) {
                            Text(
                                text = "Arxiv",
                                fontSize = 10.sp,
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                            )
                        }
                    }
                }

                if (customer.phone.isNotBlank()) {
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = customer.phone,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                if (customer.isOverdue) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Default.Warning,
                            contentDescription = null,
                            tint = Color(0xFFDC2626),
                            modifier = Modifier.size(14.dp)
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = "Muddati o'tgan (${customer.earliestDueDate ?: ""})",
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFFDC2626)
                        )
                    }
                }
            }

            Column(horizontalAlignment = Alignment.End) {
                Text(
                    text = "${numberFormat.format(customer.balanceUz)} so'm",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Black,
                    color = when {
                        customer.balanceUz > 0 -> Color(0xFFDC2626)
                        customer.balanceUz < 0 -> Color(0xFF16A34A)
                        else -> MaterialTheme.colorScheme.onSurfaceVariant
                    }
                )

                Spacer(modifier = Modifier.height(6.dp))

                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    if (customer.balanceUz > 0) {
                        Surface(
                            onClick = onQuickPay,
                            shape = RoundedCornerShape(8.dp),
                            color = LineSecondary.copy(alpha = 0.15f)
                        ) {
                            Text(
                                text = "To'lov",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = LineSecondary,
                                modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                            )
                        }
                    }

                    Surface(
                        onClick = onOpenInCashier,
                        shape = RoundedCornerShape(8.dp),
                        color = LinePrimary.copy(alpha = 0.15f)
                    ) {
                        Text(
                            text = "Kassada",
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = LinePrimary,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun CustomerDetailView(
    customer: DebtCustomerItemDto,
    details: DebtCustomerDetailDto?,
    onBack: () -> Unit,
    onOpenInCashier: () -> Unit,
    onPayDebt: (DebtAccountItemDto?) -> Unit,
    onEditCustomer: () -> Unit,
    onToggleArchive: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    var selectedTab by remember { mutableIntStateOf(0) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(14.dp)
    ) {
        // Yuqori orqaga qaytish paneli
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                IconButton(onClick = onBack) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                        contentDescription = "Orqaga"
                    )
                }
                Text(
                    text = customer.name,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Black
                )
            }

            Row {
                IconButton(onClick = onEditCustomer) {
                    Icon(imageVector = Icons.Default.Edit, contentDescription = "Tahrirlash")
                }
                IconButton(onClick = onToggleArchive) {
                    Icon(
                        imageVector = if (customer.archived) Icons.Default.Unarchive else Icons.Default.Archive,
                        contentDescription = "Arxiv"
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(10.dp))

        // Mijoz Balansi va ma'lumotlari kartasi
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column {
                        Text(
                            text = "Joriy qarz balansi",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = "${numberFormat.format(customer.balanceUz)} so'm",
                            style = MaterialTheme.typography.headlineMedium,
                            fontWeight = FontWeight.Black,
                            color = if (customer.balanceUz > 0) Color(0xFFDC2626) else Color(0xFF16A34A)
                        )
                    }

                    if (customer.phone.isNotBlank()) {
                        Text(
                            text = customer.phone,
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold,
                            color = LinePrimary
                        )
                    }
                }

                if (customer.note.isNotBlank()) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Izoh: ${customer.note}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Amallar tugmalari
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Button(
                        onClick = { onPayDebt(null) },
                        enabled = customer.balanceUz > 0,
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = LineSecondary),
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(imageVector = Icons.Default.Payments, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("To'lov olish", fontWeight = FontWeight.Bold)
                    }

                    OutlinedButton(
                        onClick = onOpenInCashier,
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(imageVector = Icons.Default.PointOfSale, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Kassada ochish", fontWeight = FontWeight.Bold)
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                val context = androidx.compose.ui.platform.LocalContext.current
                OutlinedButton(
                    onClick = {
                        if (details != null) {
                            val text = uz.pos.electro.data.debt.DebtReceiptFormatter.buildCustomerStatementText(details)
                            val sendIntent = android.content.Intent().apply {
                                action = android.content.Intent.ACTION_SEND
                                putExtra(android.content.Intent.EXTRA_TEXT, text)
                                type = "text/plain"
                            }
                            context.startActivity(android.content.Intent.createChooser(sendIntent, "Hisob ko'chirmasini ulashish"))
                        }
                    },
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(imageVector = Icons.Default.ReceiptLong, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("Ko'chirmani ulashish (Sverka)", fontWeight = FontWeight.Bold)
                }
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        // Tablar: Nasiyalar (Accounts) va Tarix (Events)
        TabRow(
            selectedTabIndex = selectedTab,
            containerColor = MaterialTheme.colorScheme.surface,
            contentColor = LinePrimary
        ) {
            Tab(
                selected = selectedTab == 0,
                onClick = { selectedTab = 0 },
                text = { Text("Nasiyalar (${details?.accounts?.size ?: 0})", fontWeight = FontWeight.Bold) }
            )
            Tab(
                selected = selectedTab == 1,
                onClick = { selectedTab = 1 },
                text = { Text("Tarix (${details?.events?.size ?: 0})", fontWeight = FontWeight.Bold) }
            )
        }

        Spacer(modifier = Modifier.height(10.dp))

        if (details == null) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator(color = LinePrimary)
            }
        } else {
            when (selectedTab) {
                0 -> {
                    // Accounts ro'yxati
                    if (details.accounts.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text("Nasiyalar mavjud emas", color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    } else {
                        LazyColumn(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            items(details.accounts, key = { it.accountGuid }) { account ->
                                AccountItemCard(
                                    account = account,
                                    onPayAccount = { onPayDebt(account) }
                                )
                            }
                        }
                    }
                }
                1 -> {
                    // Events tarixi
                    if (details.events.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text("Tarix mavjud emas", color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    } else {
                        LazyColumn(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            items(details.events, key = { it.eventGuid }) { event ->
                                EventItemCard(event = event)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun AccountItemCard(
    account: DebtAccountItemDto,
    onPayAccount: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val dateFormat = remember { SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()) }

    Card(
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(
            1.dp,
            if (account.isOverdue) Color(0xFFEF4444).copy(alpha = 0.5f)
            else MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f)
        ),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.ReceiptLong,
                        contentDescription = null,
                        tint = LinePrimary,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = account.receiptNumber,
                        fontWeight = FontWeight.Bold,
                        style = MaterialTheme.typography.titleSmall
                    )
                }

                Text(
                    text = "${numberFormat.format(account.balanceUz)} so'm",
                    fontWeight = FontWeight.Black,
                    style = MaterialTheme.typography.titleMedium,
                    color = if (account.balanceMinor > 0) Color(0xFFDC2626) else Color(0xFF16A34A)
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = "Asl qarz: ${numberFormat.format(account.originalDebtUz)} so'm  •  To'langan: ${numberFormat.format(account.totalPaidUz)} so'm",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Sana: ${dateFormat.format(Date(account.createdAt))}" + (account.dueDate?.let { " • Muddat: $it" } ?: ""),
                    style = MaterialTheme.typography.bodySmall,
                    color = if (account.isOverdue) Color(0xFFDC2626) else MaterialTheme.colorScheme.onSurfaceVariant,
                    fontWeight = if (account.isOverdue) FontWeight.Bold else FontWeight.Normal
                )

                if (account.balanceMinor > 0) {
                    Surface(
                        onClick = onPayAccount,
                        shape = RoundedCornerShape(8.dp),
                        color = LineSecondary.copy(alpha = 0.15f)
                    ) {
                        Text(
                            text = "To'lash",
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = LineSecondary,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun EventItemCard(event: DebtEventItemDto) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val dateFormat = remember { SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()) }

    Card(
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.4f)),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = when (event.kind) {
                            "sale_open" -> Icons.Default.PointOfSale
                            "payment" -> Icons.Default.Payments
                            else -> Icons.Default.History
                        },
                        contentDescription = null,
                        tint = when (event.kind) {
                            "sale_open" -> Color(0xFFDC2626)
                            "payment" -> Color(0xFF16A34A)
                            else -> LinePrimary
                        },
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = event.kindDisplay,
                        fontWeight = FontWeight.Bold,
                        style = MaterialTheme.typography.titleSmall
                    )
                }

                Text(
                    text = "${numberFormat.format(event.totalPaymentUz)} so'm",
                    fontWeight = FontWeight.Black,
                    style = MaterialTheme.typography.titleSmall,
                    color = when (event.kind) {
                        "sale_open" -> Color(0xFFDC2626)
                        "payment" -> Color(0xFF16A34A)
                        else -> MaterialTheme.colorScheme.onSurface
                    }
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = dateFormat.format(Date(event.occurredAt)) + " • Naqd: ${numberFormat.format(event.cashMinor / 100.0)} so'm, Karta: ${numberFormat.format(event.cardMinor / 100.0)} so'm",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            if (event.lines.isNotEmpty()) {
                Spacer(modifier = Modifier.height(4.dp))
                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    event.lines.forEach { line ->
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "Hisob: ...${line.accountGuid.takeLast(8)}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                fontSize = 11.sp
                            )
                            Text(
                                text = "${if (line.debtDeltaMinor > 0) "+" else ""}${numberFormat.format(line.deltaUz)} so'm",
                                style = MaterialTheme.typography.bodySmall,
                                fontWeight = FontWeight.Bold,
                                color = if (line.debtDeltaMinor < 0) Color(0xFF16A34A) else Color(0xFFDC2626),
                                fontSize = 11.sp
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun CustomerFormDialog(
    editingCustomer: DebtCustomerItemDto?,
    onDismiss: () -> Unit,
    onSave: (name: String, phone: String, note: String) -> Unit
) {
    var name by remember { mutableStateOf(editingCustomer?.name ?: "") }
    var phone by remember { mutableStateOf(editingCustomer?.phone ?: "") }
    var note by remember { mutableStateOf(editingCustomer?.note ?: "") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = if (editingCustomer != null) Icons.Default.Edit else Icons.Default.PersonAdd,
                    contentDescription = null,
                    tint = LinePrimary
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = if (editingCustomer != null) "Mijozni tahrirlash" else "Yangi mijoz qo'shish",
                    fontWeight = FontWeight.Bold,
                    fontSize = 18.sp
                )
            }
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Ism (majburiy)") },
                    singleLine = true,
                    shape = RoundedCornerShape(10.dp),
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = phone,
                    onValueChange = { phone = it },
                    label = { Text("Telefon raqami") },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
                    singleLine = true,
                    shape = RoundedCornerShape(10.dp),
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = note,
                    onValueChange = { note = it },
                    label = { Text("Izoh (ixtiyoriy)") },
                    singleLine = true,
                    shape = RoundedCornerShape(10.dp),
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                enabled = name.isNotBlank(),
                onClick = { onSave(name, phone, note) },
                colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                shape = RoundedCornerShape(10.dp)
            ) {
                Text("Saqlash", fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            OutlinedButton(
                onClick = onDismiss,
                shape = RoundedCornerShape(10.dp)
            ) {
                Text("Bekor qilish")
            }
        }
    )
}

@Composable
private fun PaymentCollectionDialog(
    customer: DebtCustomerItemDto,
    targetAccount: DebtAccountItemDto?,
    cashAmount: String,
    cardAmount: String,
    allocationPreview: DebtPaymentAllocationPreview?,
    isSubmitting: Boolean,
    onCashChanged: (String) -> Unit,
    onCardChanged: (String) -> Unit,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val cashVal = cashAmount.toLongOrNull() ?: 0L
    val cardVal = cardAmount.toLongOrNull() ?: 0L
    val totalEntered = cashVal + cardVal
    val maxAllowed = (targetAccount?.balanceMinor ?: customer.balanceMinor) / 100.0
    val isOverpayment = totalEntered > maxAllowed
    val isValid = totalEntered > 0 && !isOverpayment

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(imageVector = Icons.Default.Payments, contentDescription = null, tint = LineSecondary)
                Spacer(modifier = Modifier.width(8.dp))
                Text("Qarz to'lovini qabul qilish", fontWeight = FontWeight.Bold, fontSize = 18.sp)
            }
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Surface(
                    shape = RoundedCornerShape(12.dp),
                    color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text("Mijoz:", style = MaterialTheme.typography.bodySmall)
                            Text(customer.name, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
                        }
                        if (targetAccount != null) {
                            Spacer(modifier = Modifier.height(4.dp))
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text("Nasiya cheki:", style = MaterialTheme.typography.bodySmall)
                                Text(targetAccount.receiptNumber, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
                            }
                        }
                        Spacer(modifier = Modifier.height(4.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text("To'lanishi kerak:", style = MaterialTheme.typography.bodySmall)
                            Text(
                                "${numberFormat.format(maxAllowed)} so'm",
                                fontWeight = FontWeight.Black,
                                color = Color(0xFFDC2626),
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    }
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = cashAmount,
                        onValueChange = onCashChanged,
                        label = { Text("Naqd (so'm)") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        shape = RoundedCornerShape(10.dp),
                        modifier = Modifier.weight(1f)
                    )

                    OutlinedTextField(
                        value = cardAmount,
                        onValueChange = onCardChanged,
                        label = { Text("Karta (so'm)") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        shape = RoundedCornerShape(10.dp),
                        modifier = Modifier.weight(1f)
                    )
                }

                if (isOverpayment) {
                    Text(
                        text = "⚠️ Kiritilgan summa qarzdorlikdan (${numberFormat.format(maxAllowed)} so'm) ko'p!",
                        style = MaterialTheme.typography.bodySmall,
                        color = Color(0xFFDC2626),
                        fontWeight = FontWeight.Bold
                    )
                }

                // Taxtimot ko'rinishi (Live Preview)
                allocationPreview?.let { prev ->
                    Surface(
                        shape = RoundedCornerShape(12.dp),
                        color = Color(0xFFF0FDF4),
                        border = BorderStroke(1.dp, Color(0xFF86EFAC)),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(10.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text("Qoldiq qarz:", style = MaterialTheme.typography.bodySmall)
                                Text(
                                    "${numberFormat.format(prev.remainingBalanceMinor / 100.0)} so'm",
                                    fontWeight = FontWeight.Bold,
                                    color = Color(0xFF16A34A),
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                            if (prev.lines.isNotEmpty()) {
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    "Yopilayotgan cheklar soni: ${prev.lines.size} ta",
                                    fontSize = 11.sp,
                                    color = Color(0xFF15803D)
                                )
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                enabled = isValid && !isSubmitting,
                onClick = onConfirm,
                colors = ButtonDefaults.buttonColors(containerColor = LineSecondary),
                shape = RoundedCornerShape(10.dp)
            ) {
                Text(if (isSubmitting) "Saqlanmoqda…" else "To'lovni tasdiqlash", fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            OutlinedButton(
                onClick = onDismiss,
                shape = RoundedCornerShape(10.dp)
            ) {
                Text("Bekor qilish")
            }
        }
    )
}
