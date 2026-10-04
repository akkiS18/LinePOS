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
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CallSplit
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
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
    onDismissRequest: () -> Unit,
    onConfirmSale: (paymentType: PaymentType, cashAmount: Double, cardAmount: Double, taxAmount: Double, taxRate: Double) -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    var selectedType by remember { mutableStateOf(PaymentType.CASH) }

    var cashInput by remember { mutableStateOf((totalAmount / 2.0).toInt().toString()) }
    var cardInput by remember { mutableStateOf((totalAmount - (totalAmount / 2.0).toInt()).toInt().toString()) }

    val calculatedCardTax: Double = when (selectedType) {
        PaymentType.CASH -> 0.0
        PaymentType.CARD -> totalAmount * (cardTaxRate / 100.0)
        PaymentType.SPLIT -> {
            val card = cardInput.toDoubleOrNull() ?: 0.0
            card * (cardTaxRate / 100.0)
        }
        PaymentType.RETURN, PaymentType.BRAK -> 0.0
    }

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

                Spacer(modifier = Modifier.height(16.dp))

                Text(
                    text = "To'lov turini tanlang:",
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Spacer(modifier = Modifier.height(8.dp))

                // 3 ta To'lov varianti
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
                }

                Spacer(modifier = Modifier.height(14.dp))

                when (selectedType) {
                    PaymentType.CASH, PaymentType.RETURN, PaymentType.BRAK -> {
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
                }
            }
        },
        confirmButton = {
            Button(
                enabled = !isSubmitting,
                onClick = {
                    val (cash, card) = when (selectedType) {
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
                        PaymentType.RETURN, PaymentType.BRAK -> Pair(0.0, 0.0)
                    }

                    val taxAmount = card * (cardTaxRate / 100.0)
                    onConfirmSale(selectedType, cash, card, taxAmount, cardTaxRate)
                },
                shape = RoundedCornerShape(10.dp),
                colors = ButtonDefaults.buttonColors(containerColor = LineSecondary)
            ) {
                Text(if (isSubmitting) "Saqlanmoqda…" else "Tasdiqlash va Sotish", fontWeight = FontWeight.Bold)
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
