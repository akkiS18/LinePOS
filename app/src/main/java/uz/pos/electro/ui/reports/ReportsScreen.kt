package uz.pos.electro.ui.reports

import android.app.DatePickerDialog
import android.content.Context
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
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
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.ReceiptLong
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.TrendingUp
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.FloatingActionButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.ui.draw.rotate
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.TimeRangeFilter
import uz.pos.electro.ui.theme.LineSecondary
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale

@Composable
fun ReportsScreen(
    viewModel: ReportsViewModel
) {
    val context = LocalContext.current
    val filter by viewModel.selectedFilter.collectAsState()
    val categories by viewModel.categories.collectAsState()
    val warehouses by viewModel.warehouses.collectAsState()
    val selectedCategory by viewModel.selectedCategory.collectAsState()
    val selectedWarehouseGuid by viewModel.selectedWarehouseGuid.collectAsState()
    val summary by viewModel.summary.collectAsState()
    val salesList by viewModel.salesList.collectAsState()
    val selectedSale by viewModel.selectedSaleForDetail.collectAsState()
    var returning by remember { mutableStateOf(false) }
    var returnHistory by remember { mutableStateOf("") }
    LaunchedEffect(selectedSale?.sale?.guid) {
        returnHistory = selectedSale?.let { viewModel.returnSync.returnHistory(it.sale.guid) } ?: ""
    }
    val customStart by viewModel.customStartDate.collectAsState()
    val customEnd by viewModel.customEndDate.collectAsState()
    val usdRate by viewModel.usdRate.collectAsState()
    val isRefreshing by viewModel.isRefreshing.collectAsState()

    val isExportModalOpen by viewModel.isExportModalOpen.collectAsState()

    // Chek raqami bo'yicha qidiruv
    val searchQuery by viewModel.searchQuery.collectAsState()
    val recordKind by viewModel.recordKind.collectAsState()
    val filteredSales = salesList

    val infiniteTransition = rememberInfiniteTransition(label = "refreshRotation")
    val refreshRotation by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 360f,
        animationSpec = infiniteRepeatable(
            animation = tween(800, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "rotation"
    )

    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val dayFormat = remember { SimpleDateFormat("dd.MM.yyyy", Locale.getDefault()) }

    // Alohida Cheklar Tarixi oynasi (Sub-view) holati
    var isReceiptsViewOpen by remember { mutableStateOf(false) }
    BackHandler(enabled = isReceiptsViewOpen) {
        isReceiptsViewOpen = false
    }

    Scaffold(
        contentWindowInsets = WindowInsets(0, 0, 0, 0),
        containerColor = MaterialTheme.colorScheme.background
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            if (isReceiptsViewOpen) {
                // ==========================================
                // 1. ALOHIDA CHEKLAR TARIXI OYNASI
                // ==========================================
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(14.dp)
                ) {
                    // Top Bar: Orqaga qaytish + Sarlavha + Cheklar soni + Yangilash
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        IconButton(onClick = { isReceiptsViewOpen = false }) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                                contentDescription = "Orqaga",
                                tint = MaterialTheme.colorScheme.primary
                            )
                        }
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = "Cheklar tarixi",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onBackground
                        )
                        Spacer(modifier = Modifier.weight(1f))
                        Surface(
                            shape = CircleShape,
                            color = MaterialTheme.colorScheme.primaryContainer,
                            modifier = Modifier.padding(end = 4.dp)
                        ) {
                            Text(
                                text = "${filteredSales.size} ta chek",
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onPrimaryContainer,
                                modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
                            )
                        }
                        IconButton(onClick = { viewModel.refreshReportData(context) }) {
                            Icon(
                                imageVector = Icons.Default.Refresh,
                                contentDescription = "Yangilash",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier
                                    .size(20.dp)
                                    .rotate(if (isRefreshing) refreshRotation else 0f)
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    // Chek № bo'yicha qidiruv maydoni
                    androidx.compose.material3.OutlinedTextField(
                        value = searchQuery,
                        onValueChange = { viewModel.setSearchQuery(it) },
                        modifier = Modifier.fillMaxWidth(),
                        placeholder = { Text("Chek № bo'yicha qidirish...", fontSize = 14.sp) },
                        leadingIcon = {
                            Icon(
                                imageVector = Icons.Default.ReceiptLong,
                                contentDescription = "Chek qidirish",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(20.dp)
                            )
                        },
                        trailingIcon = if (searchQuery.isNotEmpty()) {
                            {
                                IconButton(onClick = { viewModel.setSearchQuery("") }) {
                                    Icon(Icons.Default.Close, contentDescription = "Tozalash")
                                }
                            }
                        } else null,
                        singleLine = true,
                        shape = RoundedCornerShape(14.dp),
                        keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(
                            keyboardType = androidx.compose.ui.text.input.KeyboardType.Text
                        )
                    )

                    Spacer(modifier = Modifier.height(10.dp))

                    // Turlar filtri (Barchasi, Savdo, Qaytarish, Brak)
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .horizontalScroll(rememberScrollState()),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        listOf("Barchasi", "Savdo", "Qaytarish", "Brak").forEach { kind ->
                            FilterChip(
                                selected = recordKind == kind,
                                onClick = { viewModel.setRecordKind(kind) },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    selectedContainerColor = MaterialTheme.colorScheme.primary,
                                    selectedLabelColor = Color.White
                                ),
                                label = { Text(kind, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) }
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))

                    // Cheklar ro'yxati (To'liq bo'yiga cho'zilgan)
                    if (filteredSales.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = if (searchQuery.isNotBlank()) "Chek #${searchQuery.trim()} topilmadi"
                                else "Ushbu davrda cheklar mavjud emas",
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    } else {
                        LazyColumn(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            items(filteredSales, key = { it.sale.id }) { saleWithItems ->
                                SaleHistoryCard(
                                    saleWithItems = saleWithItems,
                                    onClick = { viewModel.selectSaleForDetail(saleWithItems) }
                                )
                            }
                        }
                    }
                }
            } else {
                // ==========================================
                // 2. ASOSIY HISOBOTLAR DASHBOARDI
                // ==========================================
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(14.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    // Sarlavha va Dollar kursi (Ixcham pill)
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Hisobotlar",
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onBackground
                        )

                        // Ixcham Dollar kursi pill tugmasi
                        Surface(
                            onClick = { viewModel.refreshReportData(context) },
                            shape = CircleShape,
                            color = MaterialTheme.colorScheme.surface,
                            shadowElevation = 2.dp,
                            border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.2f))
                        ) {
                            Row(
                                modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(6.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Refresh,
                                    contentDescription = "Yangilash",
                                    tint = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier
                                        .size(16.dp)
                                        .rotate(if (isRefreshing) refreshRotation else 0f)
                                )
                                val rateFormatted = if (usdRate % 1.0 == 0.0) {
                                    String.format(Locale.US, "%,.0f", usdRate)
                                } else {
                                    String.format(Locale.US, "%,.2f", usdRate)
                                }
                                Text(
                                    text = "1$ = $rateFormatted so'm",
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                            }
                        }
                    }

                    // 1. Vaqt oralig'i filtri (Gorizontal aylanuvchi chipslar)
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .horizontalScroll(rememberScrollState()),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        TimeRangeFilter.values().forEach { timeFilter ->
                            val labelText = if (timeFilter == TimeRangeFilter.CUSTOM && filter == TimeRangeFilter.CUSTOM) {
                                "${dayFormat.format(Date(customStart))} - ${dayFormat.format(Date(customEnd))}"
                            } else {
                                timeFilter.displayName
                            }

                            FilterChip(
                                selected = filter == timeFilter,
                                onClick = {
                                    if (timeFilter == TimeRangeFilter.CUSTOM) {
                                        showSequentialDateRangePicker(context) { start, end ->
                                            viewModel.setCustomRange(start, end)
                                        }
                                    } else {
                                        viewModel.setFilter(timeFilter)
                                    }
                                },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    selectedContainerColor = MaterialTheme.colorScheme.primary,
                                    selectedLabelColor = Color.White
                                ),
                                label = { Text(labelText, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) },
                                leadingIcon = if (timeFilter == TimeRangeFilter.CUSTOM) {
                                    {
                                        Icon(
                                            imageVector = Icons.Default.CalendarMonth,
                                            contentDescription = "Kalendar",
                                            modifier = Modifier.size(14.dp),
                                            tint = if (filter == timeFilter) Color.White else MaterialTheme.colorScheme.primary
                                        )
                                    }
                                } else null
                            )
                        }
                    }

                    // Kategoriya va Ombor filtrlari
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        ReportChoice("Kategoriya", selectedCategory, categories.map { it to it }, viewModel::setCategoryFilter)
                        ReportChoice("Ombor", selectedWarehouseGuid, listOf("Barchasi" to "Barchasi") + warehouses.map { it.guid to it.name }, viewModel::setWarehouseFilter)
                    }

                    // Xulosa ma'lumotlar bloki (Tartibli yorug' blok)
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(12.dp),
                            verticalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Text(
                                text = "Savdo tushumi: ${numberFormat.format(summary.grossSales)} • Qaytarilgan: ${numberFormat.format(summary.refundedAmount)} • Tannarx tiklanishi: ${numberFormat.format(summary.costReversal)} so‘m",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Text(
                                text = "Brak: ${summary.brakCount} ta • Tannarx: ${numberFormat.format(summary.brakCost)} so‘m",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    // 2. Moliyaviy KPI Ko'rsatkichlari (Apple Rounded Cards)
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            KpiCard(
                                title = "JAMI TUSHUM",
                                value = "${numberFormat.format(summary.totalRevenue)} so'm",
                                subtitle = "Naqd: ${numberFormat.format(summary.totalCashAmount)} • Karta: ${numberFormat.format(summary.totalCardAmount)}",
                                icon = Icons.Default.Payments,
                                containerColor = MaterialTheme.colorScheme.primary,
                                contentColor = Color.White,
                                modifier = Modifier.weight(1f)
                            )

                            // SOF FOYDA (So'mda va Dollarda)
                            val formattedUsdProfit = if (summary.usdComplete) String.format(Locale.US, "%.2f", summary.netProfitUsd) else "— (eski kurs yo‘q)"
                            val taxSubtitle = if (summary.totalTaxAmount > 0) "Karta solig'i: -${numberFormat.format(summary.totalTaxAmount)}" else null
                            val fullSubtitle = if (taxSubtitle != null) "($${formattedUsdProfit}) • $taxSubtitle" else "($${formattedUsdProfit})"
                            val profitIsNegative = summary.netProfit < 0
                            val profitValueText = if (profitIsNegative)
                                "${numberFormat.format(summary.netProfit)} so'm"
                            else
                                "+${numberFormat.format(summary.netProfit)} so'm"
                            KpiCard(
                                title = "SOF FOYDA",
                                value = profitValueText,
                                subtitle = fullSubtitle,
                                icon = Icons.Default.TrendingUp,
                                containerColor = if (profitIsNegative) Color(0xFF7F1D1D) else LineSecondary,
                                contentColor = Color.White,
                                modifier = Modifier
                                    .weight(1f)
                                    .clickable { viewModel.refreshUsdRate(context) }
                            )
                        }

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            KpiCard(
                                title = "SAVDOLAR SONI",
                                value = "${summary.salesCount} ta chek",
                                subtitle = "Ko'rish uchun bosing ➔",
                                icon = Icons.Default.ReceiptLong,
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier
                                    .weight(1f)
                                    .clickable { isReceiptsViewOpen = true }
                            )

                            KpiCard(
                                title = "SOTILGAN TOVARLAR",
                                value = "${if (summary.totalItemsCount % 1.0 == 0.0) summary.totalItemsCount.toLong() else summary.totalItemsCount} ta/m",
                                icon = Icons.Default.Description,
                                containerColor = MaterialTheme.colorScheme.surface,
                                contentColor = MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier.weight(1f)
                            )
                        }
                    }

                    // 3. Alohida Cheklar Tarixi Oynasiga O'tish Kartasi
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clickable { isReceiptsViewOpen = true },
                        shape = RoundedCornerShape(20.dp),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.45f)
                        ),
                        border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.primary.copy(alpha = 0.2f))
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                Surface(
                                    shape = CircleShape,
                                    color = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier.size(42.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = Icons.Default.ReceiptLong,
                                            contentDescription = null,
                                            tint = Color.White,
                                            modifier = Modifier.size(22.dp)
                                        )
                                    }
                                }
                                Column {
                                    Text(
                                        text = "Cheklar tarixi",
                                        style = MaterialTheme.typography.titleMedium,
                                        fontWeight = FontWeight.Bold,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                    Text(
                                        text = "${salesList.size} ta chek mavjud • Ro'yxatni ochish",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }
                            Icon(
                                imageVector = Icons.Default.ChevronRight,
                                contentDescription = "Ochish",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(26.dp)
                            )
                        }
                    }

                    // 4. Excel Eksport Tugmasi (Modal ochiladi)
                    Button(
                        onClick = { viewModel.openExportModal() },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(50.dp),
                        shape = RoundedCornerShape(16.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = LineSecondary,
                            contentColor = Color.White
                        )
                    ) {
                        Icon(
                            imageVector = Icons.Default.FileDownload,
                            contentDescription = "Excel",
                            modifier = Modifier.size(20.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = "Excel hisobotni yuklab olish (.xls)",
                            fontWeight = FontWeight.Bold,
                            fontSize = 15.sp
                        )
                    }

                    Spacer(modifier = Modifier.height(16.dp))
                }
            }
        }

        // Chek tafsilotlari dialogi
        selectedSale?.let { saleWithItems ->
            if (returning) ReturnDialog(saleWithItems, viewModel) { returning = false; viewModel.closeSaleDetail() }
            else SaleDetailDialog(
                saleWithItems = saleWithItems,
                onReturn = { returning = true },
                returnHistory = returnHistory,
                onDismissRequest = { viewModel.closeSaleDetail() }
            )
        }

        // Excel Eksport Konfiguratsiyasi Dialogi
        if (isExportModalOpen) {
            ExportReportDialog(
                viewModel = viewModel,
                onDismissRequest = { viewModel.closeExportModal() },
                onExport = { viewModel.exportToExcel(context) }
            )
        }
    }
}

@Composable
private fun KpiCard(
    title: String,
    value: String,
    subtitle: String? = null,
    icon: ImageVector,
    containerColor: Color,
    contentColor: Color,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(22.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        colors = CardDefaults.cardColors(
            containerColor = containerColor,
            contentColor = contentColor
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.labelSmall,
                    fontWeight = FontWeight.Bold
                )
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    modifier = Modifier.size(20.dp)
                )
            }
            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = value,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Black
            )
            if (subtitle != null) {
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.labelMedium,
                    fontWeight = FontWeight.Bold,
                    color = contentColor.copy(alpha = 0.85f)
                )
            }
        }
    }
}

@Composable
private fun SaleHistoryCard(
    saleWithItems: SaleWithItems,
    onClick: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val dateFormat = remember { SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()) }
    val profit = (saleWithItems.sale.totalAmount - saleWithItems.sale.taxAmount) - saleWithItems.sale.totalCost
    val paymentTypeText = when (saleWithItems.sale.paymentType) {
        uz.pos.electro.data.model.PaymentType.CASH -> "Naqd"
        uz.pos.electro.data.model.PaymentType.CARD -> "Karta"
        uz.pos.electro.data.model.PaymentType.SPLIT -> "Aralash"
        uz.pos.electro.data.model.PaymentType.RETURN_REVERSAL -> "Bekor"
        uz.pos.electro.data.model.PaymentType.RETURN -> "Qaytarish"
        uz.pos.electro.data.model.PaymentType.BRAK -> "⚠️ Brak"
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() },
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = "Chek #${saleWithItems.sale.id}",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Surface(
                        shape = RoundedCornerShape(6.dp),
                        color = when (saleWithItems.sale.paymentType) {
                            uz.pos.electro.data.model.PaymentType.CARD -> Color(0xFF0284C7).copy(alpha = 0.15f)
                            uz.pos.electro.data.model.PaymentType.SPLIT -> Color(0xFF8B5CF6).copy(alpha = 0.15f)
                            uz.pos.electro.data.model.PaymentType.BRAK -> Color(0xFFE11D48).copy(alpha = 0.15f)
                            uz.pos.electro.data.model.PaymentType.RETURN, uz.pos.electro.data.model.PaymentType.RETURN_REVERSAL -> Color(0xFFEA580C).copy(alpha = 0.15f)
                            else -> LineSecondary.copy(alpha = 0.15f)
                        }
                    ) {
                        Text(
                            text = paymentTypeText,
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Bold,
                            color = when (saleWithItems.sale.paymentType) {
                                uz.pos.electro.data.model.PaymentType.CARD -> Color(0xFF0284C7)
                                uz.pos.electro.data.model.PaymentType.SPLIT -> Color(0xFF8B5CF6)
                                uz.pos.electro.data.model.PaymentType.BRAK -> Color(0xFFE11D48)
                                uz.pos.electro.data.model.PaymentType.RETURN, uz.pos.electro.data.model.PaymentType.RETURN_REVERSAL -> Color(0xFFEA580C)
                                else -> LineSecondary
                            },
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    }
                }
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = "${dateFormat.format(Date(saleWithItems.sale.createdAt))}  •  ${saleWithItems.items.size} xil tovar",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            Column(horizontalAlignment = Alignment.End) {
                Text(
                    text = "${numberFormat.format(saleWithItems.sale.totalAmount)} so'm",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.ExtraBold,
                    color = MaterialTheme.colorScheme.primary
                )
                val profitText = if (profit < 0)
                    "Foyda: ${numberFormat.format(profit)} so'm"
                else
                    "Foyda: +${numberFormat.format(profit)} so'm"
                val profitColor = if (profit < 0) Color(0xFFEF4444) else LineSecondary
                Text(
                    text = profitText,
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = FontWeight.Bold,
                    color = profitColor
                )
            }
        }
    }
}

@Composable
private fun SaleDetailDialog(
    saleWithItems: SaleWithItems,
    onReturn: () -> Unit,
    returnHistory: String,
    onDismissRequest: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val dateFormat = remember { SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()) }
    // Tax ajratilgan holda hisoblash (SaleHistoryCard bilan bir xil)
    val profit = (saleWithItems.sale.totalAmount - saleWithItems.sale.taxAmount) - saleWithItems.sale.totalCost

    Dialog(onDismissRequest = onDismissRequest) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            shape = RoundedCornerShape(26.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(20.dp)
            ) {
                Text(
                    text = "Chek #${saleWithItems.sale.id} Tafsilotlari",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )

                Text(
                    text = "Hujjat: ${saleWithItems.sale.receiptNumber} • ${dateFormat.format(Date(saleWithItems.sale.createdAt))}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                Spacer(modifier = Modifier.height(12.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.surfaceVariant)
                Spacer(modifier = Modifier.height(10.dp))

                LazyColumn(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(180.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(saleWithItems.items) { item ->
                        val costLabel = if (item.costCurrency == "USD") "$${item.costAtSale}" else "${numberFormat.format(item.costAtSale)} so'm"
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = item.productName.ifBlank { "Mahsulot #${item.productId}" },
                                    style = MaterialTheme.typography.bodyMedium,
                                    fontWeight = FontWeight.Bold
                                )
                                Text(
                                    text = "${item.quantity} x ${numberFormat.format(item.priceAtSale)} so'm (Tan: $costLabel)",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Text(
                                text = "${numberFormat.format(item.priceAtSale * item.quantity)} so'm",
                                style = MaterialTheme.typography.bodyMedium,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.surfaceVariant)
                Spacer(modifier = Modifier.height(12.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text("JAMI SUMMA:", fontWeight = FontWeight.Bold)
                    Text(
                        "${numberFormat.format(saleWithItems.sale.totalAmount)} SO'M",
                        fontWeight = FontWeight.ExtraBold,
                        color = MaterialTheme.colorScheme.primary
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    val profitLabelColor = if (profit < 0) Color(0xFFEF4444) else LineSecondary
                    val profitValueText = if (profit < 0)
                        "${numberFormat.format(profit)} SO'M"
                    else
                        "+${numberFormat.format(profit)} SO'M"
                    Text("SOF FOYDA:", fontWeight = FontWeight.Bold, color = profitLabelColor)
                    Text(
                        profitValueText,
                        fontWeight = FontWeight.Bold,
                        color = profitLabelColor
                    )
                }

                Spacer(modifier = Modifier.height(18.dp))

                if (returnHistory.isNotBlank()) Text(returnHistory, style = MaterialTheme.typography.bodySmall)
                if (saleWithItems.sale.paymentType != uz.pos.electro.data.model.PaymentType.BRAK &&
                    saleWithItems.sale.paymentType != uz.pos.electro.data.model.PaymentType.RETURN_REVERSAL) {
                    Button(onClick = onReturn, modifier = Modifier.fillMaxWidth()) { Text(if (saleWithItems.sale.paymentType == uz.pos.electro.data.model.PaymentType.RETURN) "Qaytarishni bekor qilish" else "Qaytarish") }
                }
                Button(
                    onClick = onDismissRequest,
                    modifier = Modifier.fillMaxWidth(),
                    shape = CircleShape
                ) {
                    Text("Yopish", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

private fun showSequentialDateRangePicker(
    context: Context,
    onRangeSelected: (Long, Long) -> Unit
) {
    val cal = Calendar.getInstance()

    val startDatePicker = DatePickerDialog(
        context,
        { _, startYear, startMonth, startDay ->
            val startCal = Calendar.getInstance().apply {
                set(startYear, startMonth, startDay, 0, 0, 0)
                set(Calendar.MILLISECOND, 0)
            }

            val endDatePicker = DatePickerDialog(
                context,
                { _, endYear, endMonth, endDay ->
                    val endCal = Calendar.getInstance().apply {
                        set(endYear, endMonth, endDay, 23, 59, 59)
                        set(Calendar.MILLISECOND, 999)
                    }
                    onRangeSelected(startCal.timeInMillis, endCal.timeInMillis)
                },
                startYear,
                startMonth,
                startDay
            )
            endDatePicker.setTitle("Tugash sanasini tanlang")
            endDatePicker.show()
        },
        cal.get(Calendar.YEAR),
        cal.get(Calendar.MONTH),
        cal.get(Calendar.DAY_OF_MONTH)
    )
    startDatePicker.setTitle("Boshlang'ich sanani tanlang")
    startDatePicker.show()
}

@Composable
fun ExportReportDialog(
    viewModel: ReportsViewModel,
    onDismissRequest: () -> Unit,
    onExport: () -> Unit
) {
    val context = LocalContext.current
    val exportFilter by viewModel.exportFilter.collectAsState()
    val exportStartDate by viewModel.exportStartDate.collectAsState()
    val exportEndDate by viewModel.exportEndDate.collectAsState()
    val exportCategory by viewModel.exportSelectedCategory.collectAsState()
    val exportWarehouseGuid by viewModel.exportSelectedWarehouseGuid.collectAsState()
    val categories by viewModel.categories.collectAsState()
    val warehouses by viewModel.warehouses.collectAsState()

    val dayFormat = remember { SimpleDateFormat("dd.MM.yyyy", Locale.getDefault()) }

    Dialog(onDismissRequest = onDismissRequest) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 6.dp,
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 16.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(20.dp)
                    .verticalScroll(rememberScrollState())
            ) {
                // Sarlavha
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Surface(
                            shape = RoundedCornerShape(10.dp),
                            color = LineSecondary.copy(alpha = 0.15f),
                            modifier = Modifier.size(38.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.FileDownload,
                                    contentDescription = "Excel",
                                    tint = LineSecondary,
                                    modifier = Modifier.size(22.dp)
                                )
                            }
                        }
                        Column {
                            Text(
                                text = "Excel hisoboti",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = "Eksport parametrlarini sozlang",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    IconButton(
                        onClick = onDismissRequest,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Yopish",
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outline.copy(alpha = 0.2f))
                Spacer(modifier = Modifier.height(14.dp))

                // 1. Vaqt oralig'i
                Text(
                    text = "Vaqt oralig'i:",
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    TimeRangeFilter.values().forEach { timeFilter ->
                        val isSelected = exportFilter == timeFilter
                        val labelText = if (timeFilter == TimeRangeFilter.CUSTOM && isSelected) {
                            "${dayFormat.format(Date(exportStartDate))} - ${dayFormat.format(Date(exportEndDate))}"
                        } else {
                            timeFilter.displayName
                        }

                        FilterChip(
                            selected = isSelected,
                            onClick = {
                                if (timeFilter == TimeRangeFilter.CUSTOM) {
                                    showSequentialDateRangePicker(context) { start, end ->
                                        viewModel.setExportCustomRange(start, end)
                                    }
                                } else {
                                    viewModel.setExportFilter(timeFilter)
                                }
                            },
                            shape = CircleShape,
                            colors = FilterChipDefaults.filterChipColors(
                                selectedContainerColor = LineSecondary,
                                selectedLabelColor = Color.White
                            ),
                            label = { Text(labelText, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) },
                            leadingIcon = if (timeFilter == TimeRangeFilter.CUSTOM) {
                                {
                                    Icon(
                                        imageVector = Icons.Default.CalendarMonth,
                                        contentDescription = "Kalendar",
                                        modifier = Modifier.size(14.dp),
                                        tint = if (isSelected) Color.White else MaterialTheme.colorScheme.primary
                                    )
                                }
                            } else null
                        )
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                // 2. Ombor filtri
                Text(
                    text = "🏢 Ombor:",
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    FilterChip(
                        selected = exportWarehouseGuid == "Barchasi",
                        onClick = { viewModel.setExportWarehouse("Barchasi") },
                        shape = CircleShape,
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = MaterialTheme.colorScheme.primary,
                            selectedLabelColor = Color.White
                        ),
                        label = { Text("Barcha omborlar", fontSize = 12.sp, fontWeight = FontWeight.Medium) }
                    )
                    warehouses.forEach { wh ->
                        FilterChip(
                            selected = exportWarehouseGuid == wh.guid,
                            onClick = { viewModel.setExportWarehouse(wh.guid) },
                            shape = CircleShape,
                            colors = FilterChipDefaults.filterChipColors(
                                selectedContainerColor = MaterialTheme.colorScheme.primary,
                                selectedLabelColor = Color.White
                            ),
                            label = { Text(wh.name, fontSize = 12.sp, fontWeight = FontWeight.Medium) }
                        )
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                // 3. Kategoriya filtri
                Text(
                    text = "🏷️ Kategoriya:",
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(8.dp))
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    categories.forEach { cat ->
                        FilterChip(
                            selected = exportCategory == cat,
                            onClick = { viewModel.setExportCategory(cat) },
                            shape = CircleShape,
                            colors = FilterChipDefaults.filterChipColors(
                                selectedContainerColor = MaterialTheme.colorScheme.secondary,
                                selectedLabelColor = Color.White
                            ),
                            label = { Text(cat, fontSize = 12.sp, fontWeight = FontWeight.Medium) }
                        )
                    }
                }

                Spacer(modifier = Modifier.height(20.dp))
                HorizontalDivider(color = MaterialTheme.colorScheme.outline.copy(alpha = 0.2f))
                Spacer(modifier = Modifier.height(16.dp))

                // Tugmalar
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(
                        onClick = onDismissRequest,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Text("Bekor qilish", fontWeight = FontWeight.SemiBold)
                    }

                    Button(
                        onClick = onExport,
                        modifier = Modifier.weight(1.5f),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = LineSecondary,
                            contentColor = Color.White
                        )
                    ) {
                        Icon(
                            imageVector = Icons.Default.FileDownload,
                            contentDescription = "Yuklab olish",
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Yuklab olish (.xls)", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}

@Composable
private fun ReportChoice(label: String, selected: String, choices: List<Pair<String, String>>, onSelect: (String) -> Unit) {
    var expanded by remember { androidx.compose.runtime.mutableStateOf(false) }
    Box {
        androidx.compose.material3.TextButton(onClick = { expanded = true }) {
            Text("$label: ${choices.firstOrNull { it.first == selected }?.second ?: selected}", maxLines = 1)
        }
        androidx.compose.material3.DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            choices.forEach { (value, title) ->
                androidx.compose.material3.DropdownMenuItem(text = { Text(title) }, onClick = { expanded = false; onSelect(value) })
            }
        }
    }
}
