package uz.pos.electro.ui.cashier

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CallSplit
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.MenuBook
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PersonAdd
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import uz.pos.electro.data.debt.DebtCustomerItemDto
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary
import java.text.NumberFormat
import java.util.Locale

@Composable
fun CheckoutPaymentDialog(
    totalAmount: Double,
    isSubmitting: Boolean = false,
    cardTaxRate: Double,
    activeDebtCustomers: List<DebtCustomerItemDto> = emptyList(),
    selectedDebtCustomer: DebtCustomerItemDto? = null,
    onSelectDebtCustomer: (DebtCustomerItemDto?) -> Unit = {},
    debtDueDate: String? = null,
    onSetDebtDueDate: (String?) -> Unit = {},
    debtCashAdvance: String = "0",
    onSetDebtCashAdvance: (String) -> Unit = {},
    debtCardAdvance: String = "0",
    onSetDebtCardAdvance: (String) -> Unit = {},
    onOpenQuickAddCustomer: () -> Unit = {},
    onDismissRequest: () -> Unit,
    onConfirmSale: (paymentType: PaymentType, cashAmount: Double, cardAmount: Double, taxAmount: Double, taxRate: Double) -> Unit,
    onConfirmDebtSale: (customerGuid: String, dueDate: String?, cashAdvance: Double, cardAdvance: Double) -> Unit = { _, _, _, _ -> }
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    var selectedType by remember {
        mutableStateOf(if (selectedDebtCustomer != null) PaymentType.DEBT else PaymentType.CASH)
    }
    var showCustomerPicker by remember { mutableStateOf(false) }

    var cashInput by remember { mutableStateOf((totalAmount / 2.0).toInt().toString()) }
    var cardInput by remember { mutableStateOf((totalAmount - (totalAmount / 2.0).toInt()).toInt().toString()) }

    val calculatedCardTax: Double = when (selectedType) {
        PaymentType.DEBT -> 0.0
        PaymentType.CASH -> 0.0
        PaymentType.CARD -> totalAmount * (cardTaxRate / 100.0)
        PaymentType.SPLIT -> {
            val card = cardInput.toDoubleOrNull() ?: 0.0
            card * (cardTaxRate / 100.0)
        }
        PaymentType.RETURN, PaymentType.RETURN_REVERSAL, PaymentType.BRAK -> 0.0
    }

    val cashAdvVal = debtCashAdvance.toDoubleOrNull() ?: 0.0
    val cardAdvVal = debtCardAdvance.toDoubleOrNull() ?: 0.0
    val totalAdvance = cashAdvVal + cardAdvVal
    val remainingDebt = (totalAmount - totalAdvance).coerceAtLeast(0.0)
    val isOverAdvance = totalAdvance > totalAmount

    val isDebtValid = selectedDebtCustomer != null && !isOverAdvance

    AlertDialog(
        onDismissRequest = onDismissRequest,
        shape = RoundedCornerShape(22.dp),
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "💳 To'lovni amalga oshirish",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth()) {
                // Jami summa kartasi
                Surface(
                    shape = RoundedCornerShape(14.dp),
                    color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(14.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Jami to'lov:",
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = "${numberFormat.format(totalAmount)} so'm",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Black,
                            color = LineSecondary
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                Text(
                    text = "To'lov turini tanlang:",
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Spacer(modifier = Modifier.height(8.dp))

                // 2x2 To'lov varianti: Naqd, Karta, Aralash, Nasiya
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        PaymentOptionPill(
                            title = "Naqd",
                            icon = Icons.Default.Payments,
                            isSelected = selectedType == PaymentType.CASH,
                            modifier = Modifier.weight(1f),
                            onClick = { selectedType = PaymentType.CASH }
                        )
                        PaymentOptionPill(
                            title = "Karta",
                            icon = Icons.Default.CreditCard,
                            isSelected = selectedType == PaymentType.CARD,
                            modifier = Modifier.weight(1f),
                            onClick = { selectedType = PaymentType.CARD }
                        )
                    }
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        PaymentOptionPill(
                            title = "Aralash",
                            icon = Icons.Default.CallSplit,
                            isSelected = selectedType == PaymentType.SPLIT,
                            modifier = Modifier.weight(1f),
                            onClick = {
                                selectedType = PaymentType.SPLIT
                                cashInput = (totalAmount / 2.0).toInt().toString()
                                cardInput = (totalAmount - (totalAmount / 2.0).toInt()).toInt().toString()
                            }
                        )
                        PaymentOptionPill(
                            title = "Nasiya (Qarz)",
                            icon = Icons.Default.MenuBook,
                            isSelected = selectedType == PaymentType.DEBT,
                            modifier = Modifier.weight(1f),
                            onClick = { selectedType = PaymentType.DEBT }
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                when (selectedType) {
                    PaymentType.CASH, PaymentType.RETURN, PaymentType.RETURN_REVERSAL, PaymentType.BRAK -> {
                        Surface(
                            shape = RoundedCornerShape(10.dp),
                            color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Text(
                                text = "Barcha summa naqd pulda qabul qilinadi. Soliq ushlanmaydi.",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(10.dp)
                            )
                        }
                    }
                    PaymentType.CARD -> {
                        Surface(
                            shape = RoundedCornerShape(10.dp),
                            color = Color(0xFF0284C7).copy(alpha = 0.12f),
                            border = BorderStroke(1.dp, Color(0xFF0284C7).copy(alpha = 0.3f)),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(10.dp),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    text = "Karta solig'i ($cardTaxRate%):",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                Text(
                                    text = "-${numberFormat.format(calculatedCardTax)} so'm",
                                    style = MaterialTheme.typography.bodyMedium,
                                    fontWeight = FontWeight.Bold,
                                    color = Color(0xFF0284C7)
                                )
                            }
                        }
                    }
                    PaymentType.SPLIT -> {
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedTextField(
                                value = cashInput,
                                onValueChange = { newVal ->
                                    val digitsOnly = newVal.filter { it.isDigit() }
                                    cashInput = digitsOnly
                                    val cashVal = digitsOnly.toDoubleOrNull() ?: 0.0
                                    val cardVal = (totalAmount - cashVal).coerceAtLeast(0.0)
                                    cardInput = cardVal.toInt().toString()
                                },
                                label = { Text("Naqd to'lanadigan qismi (so'm)") },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                singleLine = true,
                                shape = RoundedCornerShape(10.dp),
                                modifier = Modifier.fillMaxWidth()
                            )

                            OutlinedTextField(
                                value = cardInput,
                                onValueChange = { newVal ->
                                    val digitsOnly = newVal.filter { it.isDigit() }
                                    cardInput = digitsOnly
                                    val cardVal = digitsOnly.toDoubleOrNull() ?: 0.0
                                    val cashVal = (totalAmount - cardVal).coerceAtLeast(0.0)
                                    cashInput = cashVal.toInt().toString()
                                },
                                label = { Text("Kartadan to'lanadigan qismi (so'm)") },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                singleLine = true,
                                shape = RoundedCornerShape(10.dp),
                                modifier = Modifier.fillMaxWidth()
                            )

                            Surface(
                                shape = RoundedCornerShape(10.dp),
                                color = Color(0xFF0284C7).copy(alpha = 0.12f),
                                border = BorderStroke(1.dp, Color(0xFF0284C7).copy(alpha = 0.3f)),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(10.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Text(
                                        text = "Kartadan soliq ($cardTaxRate%):",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                    Text(
                                        text = "-${numberFormat.format(calculatedCardTax)} so'm",
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontWeight = FontWeight.Bold,
                                        color = Color(0xFF0284C7)
                                    )
                                }
                            }
                        }
                    }
                    PaymentType.DEBT -> {
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            // Mijoz tanlash bloki
                            if (selectedDebtCustomer == null) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    OutlinedButton(
                                        onClick = { showCustomerPicker = true },
                                        modifier = Modifier.weight(1f),
                                        shape = RoundedCornerShape(10.dp)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Person,
                                            contentDescription = null,
                                            modifier = Modifier.size(18.dp)
                                        )
                                        Spacer(modifier = Modifier.width(6.dp))
                                        Text("Mijozni tanlang", fontSize = 13.sp)
                                    }

                                    Button(
                                        onClick = onOpenQuickAddCustomer,
                                        shape = RoundedCornerShape(10.dp),
                                        colors = ButtonDefaults.buttonColors(containerColor = LinePrimary)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.PersonAdd,
                                            contentDescription = null,
                                            modifier = Modifier.size(18.dp)
                                        )
                                        Spacer(modifier = Modifier.width(4.dp))
                                        Text("+ Yangi", fontSize = 13.sp)
                                    }
                                }
                            } else {
                                Card(
                                    shape = RoundedCornerShape(10.dp),
                                    colors = CardDefaults.cardColors(
                                        containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                                    ),
                                    border = BorderStroke(1.dp, LinePrimary.copy(alpha = 0.4f)),
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .padding(10.dp),
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Column(modifier = Modifier.weight(1f)) {
                                            Text(
                                                text = selectedDebtCustomer.name,
                                                fontWeight = FontWeight.Bold,
                                                fontSize = 14.sp
                                            )
                                            if (selectedDebtCustomer.phone.isNotBlank()) {
                                                Text(
                                                    text = selectedDebtCustomer.phone,
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Text(
                                                text = "Joriy qarz: ${numberFormat.format(selectedDebtCustomer.balanceUz)} so'm",
                                                style = MaterialTheme.typography.bodySmall,
                                                fontWeight = FontWeight.SemiBold,
                                                color = if (selectedDebtCustomer.balanceUz > 0) Color(0xFFDC2626) else Color(0xFF16A34A)
                                            )
                                        }

                                        IconButton(onClick = { showCustomerPicker = true }) {
                                            Icon(
                                                imageVector = Icons.Default.Search,
                                                contentDescription = "O'zgartirish",
                                                tint = MaterialTheme.colorScheme.primary
                                            )
                                        }
                                    }
                                }
                            }

                            // Avans (Oldindan to'lov) qatorlari
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                OutlinedTextField(
                                    value = debtCashAdvance,
                                    onValueChange = { newVal ->
                                        onSetDebtCashAdvance(newVal.filter { it.isDigit() })
                                    },
                                    label = { Text("Naqd avans (so'm)") },
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true,
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier.weight(1f)
                                )

                                OutlinedTextField(
                                    value = debtCardAdvance,
                                    onValueChange = { newVal ->
                                        onSetDebtCardAdvance(newVal.filter { it.isDigit() })
                                    },
                                    label = { Text("Karta avans (so'm)") },
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true,
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier.weight(1f)
                                )
                            }

                            // Hisob-kitob qutisi
                            Surface(
                                shape = RoundedCornerShape(10.dp),
                                color = if (isOverAdvance) Color(0xFFFEE2E2) else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                                border = if (isOverAdvance) BorderStroke(1.dp, Color(0xFFEF4444)) else null,
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Column(modifier = Modifier.padding(10.dp)) {
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        horizontalArrangement = Arrangement.SpaceBetween
                                    ) {
                                        Text("Jami avans:", style = MaterialTheme.typography.bodySmall)
                                        Text("${numberFormat.format(totalAdvance)} so'm", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
                                    }
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Row(
                                        modifier = Modifier.fillMaxWidth(),
                                        horizontalArrangement = Arrangement.SpaceBetween
                                    ) {
                                        Text("Nasiyaga qoldiq:", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
                                        Text(
                                            "${numberFormat.format(remainingDebt)} so'm",
                                            fontWeight = FontWeight.ExtraBold,
                                            style = MaterialTheme.typography.bodyMedium,
                                            color = Color(0xFFDC2626)
                                        )
                                    }
                                    if (isOverAdvance) {
                                        Spacer(modifier = Modifier.height(4.dp))
                                        Text(
                                            "⚠️ Avans savdo summasidan katta bo'lishi mumkin emas!",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = Color(0xFFDC2626),
                                            fontWeight = FontWeight.Bold
                                        )
                                    }
                                }
                            }

                            // To'lov muddati (DueDate)
                            OutlinedTextField(
                                value = debtDueDate ?: "",
                                onValueChange = { newVal ->
                                    onSetDebtDueDate(if (newVal.isBlank()) null else newVal)
                                },
                                label = { Text("To'lov muddati (YYYY-MM-DD, ixtiyoriy)") },
                                placeholder = { Text("Masalan: 2026-11-01") },
                                singleLine = true,
                                shape = RoundedCornerShape(10.dp),
                                modifier = Modifier.fillMaxWidth()
                            )
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(
                enabled = !isSubmitting && (selectedType != PaymentType.DEBT || isDebtValid),
                onClick = {
                    if (selectedType == PaymentType.DEBT) {
                        val cust = selectedDebtCustomer ?: return@Button
                        onConfirmDebtSale(
                            cust.guid,
                            debtDueDate,
                            cashAdvVal,
                            cardAdvVal
                        )
                    } else {
                        val (cash, card) = when (selectedType) {
                            PaymentType.DEBT -> Pair(0.0, 0.0)
                            PaymentType.CASH -> Pair(totalAmount, 0.0)
                            PaymentType.CARD -> Pair(0.0, totalAmount)
                            PaymentType.SPLIT -> {
                                var cashVal = cashInput.toDoubleOrNull() ?: 0.0
                                var cardVal = cardInput.toDoubleOrNull() ?: 0.0
                                if (cashVal + cardVal != totalAmount) {
                                    if (cashVal <= totalAmount) {
                                        cardVal = totalAmount - cashVal
                                    } else {
                                        cashVal = totalAmount
                                        cardVal = 0.0
                                    }
                                }
                                Pair(cashVal, cardVal)
                            }
                            PaymentType.RETURN, PaymentType.RETURN_REVERSAL, PaymentType.BRAK -> Pair(0.0, 0.0)
                        }

                        val taxAmount = card * (cardTaxRate / 100.0)
                        onConfirmSale(selectedType, cash, card, taxAmount, cardTaxRate)
                    }
                },
                shape = RoundedCornerShape(10.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = if (selectedType == PaymentType.DEBT) LinePrimary else LineSecondary
                )
            ) {
                Text(
                    text = if (isSubmitting) "Saqlanmoqda…" else if (selectedType == PaymentType.DEBT) "Nasiyani Rasmiylashtirish" else "Tasdiqlash va Sotish",
                    fontWeight = FontWeight.Bold
                )
            }
        },
        dismissButton = {
            OutlinedButton(
                onClick = onDismissRequest,
                shape = RoundedCornerShape(10.dp)
            ) {
                Text("Bekor qilish")
            }
        }
    )

    // Mijoz tanlash dialogi
    if (showCustomerPicker) {
        CustomerPickerDialog(
            customers = activeDebtCustomers,
            onDismissRequest = { showCustomerPicker = false },
            onSelectCustomer = { cust ->
                onSelectDebtCustomer(cust)
                showCustomerPicker = false
            },
            onAddNewCustomer = {
                showCustomerPicker = false
                onOpenQuickAddCustomer()
            }
        )
    }
}

@Composable
private fun PaymentOptionPill(
    title: String,
    icon: ImageVector,
    isSelected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    val bg = if (isSelected) LinePrimary else MaterialTheme.colorScheme.surface
    val border = if (isSelected) LinePrimary else MaterialTheme.colorScheme.outlineVariant
    val contentColor = if (isSelected) Color.White else MaterialTheme.colorScheme.onSurface

    Box(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(bg)
            .border(1.dp, border, RoundedCornerShape(12.dp))
            .clickable { onClick() }
            .padding(vertical = 12.dp, horizontal = 6.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(
                imageVector = icon,
                contentDescription = title,
                tint = contentColor,
                modifier = Modifier.size(20.dp)
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = title,
                fontSize = 12.sp,
                fontWeight = if (isSelected) FontWeight.Bold else FontWeight.SemiBold,
                color = contentColor
            )
        }
    }
}

@Composable
fun CustomerPickerDialog(
    customers: List<DebtCustomerItemDto>,
    onDismissRequest: () -> Unit,
    onSelectCustomer: (DebtCustomerItemDto) -> Unit,
    onAddNewCustomer: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    var search by remember { mutableStateOf("") }

    val filtered = remember(customers, search) {
        val s = search.trim().lowercase(Locale.ROOT)
        if (s.isEmpty()) customers
        else customers.filter {
            it.name.lowercase(Locale.ROOT).contains(s) || it.phone.lowercase(Locale.ROOT).contains(s)
        }
    }

    Dialog(onDismissRequest = onDismissRequest) {
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            modifier = Modifier
                .fillMaxWidth(0.95f)
                .heightIn(max = 550.dp)
                .padding(8.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Mijozni tanlang",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )
                    IconButton(onClick = onDismissRequest) {
                        Icon(imageVector = Icons.Default.Close, contentDescription = "Yopish")
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                OutlinedTextField(
                    value = search,
                    onValueChange = { search = it },
                    placeholder = { Text("Ism yoki telefon bo'yicha qidirish...") },
                    leadingIcon = {
                        Icon(imageVector = Icons.Default.Search, contentDescription = null)
                    },
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(modifier = Modifier.height(10.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "Mijozlar (${filtered.size})",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    TextButton(onClick = onAddNewCustomer) {
                        Icon(imageVector = Icons.Default.PersonAdd, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("+ Yangi mijoz", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                    }
                }

                HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))

                if (filtered.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = if (search.isBlank()) "Mijozlar ro'yxati bo'sh" else "Mijoz topilmadi",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        items(filtered, key = { it.guid }) { cust ->
                            Surface(
                                shape = RoundedCornerShape(10.dp),
                                color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                                onClick = { onSelectCustomer(cust) },
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(12.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            text = cust.name,
                                            fontWeight = FontWeight.Bold,
                                            fontSize = 14.sp
                                        )
                                        if (cust.phone.isNotBlank()) {
                                            Text(
                                                text = cust.phone,
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                    }

                                    Column(horizontalAlignment = Alignment.End) {
                                        Text(
                                            text = "${numberFormat.format(cust.balanceUz)} so'm",
                                            fontWeight = FontWeight.Bold,
                                            fontSize = 13.sp,
                                            color = if (cust.balanceUz > 0) Color(0xFFDC2626) else if (cust.balanceUz < 0) Color(0xFF16A34A) else MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        if (cust.isOverdue) {
                                            Text(
                                                text = "Muddati o'tgan",
                                                fontSize = 10.sp,
                                                fontWeight = FontWeight.Bold,
                                                color = Color(0xFFDC2626)
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
