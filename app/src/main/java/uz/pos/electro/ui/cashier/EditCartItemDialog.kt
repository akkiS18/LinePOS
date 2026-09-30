package uz.pos.electro.ui.cashier

import androidx.compose.foundation.background
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import uz.pos.electro.data.model.CartItemModel
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.ui.components.PosNumpad
import java.text.NumberFormat
import java.util.Locale

enum class EditFieldTarget { QUANTITY, PRICE }

@Composable
fun EditCartItemDialog(
    item: CartItemModel,
    onDismissRequest: () -> Unit,
    onSave: (newQuantity: Double, newPrice: Double) -> Unit,
    onDelete: () -> Unit
) {
    var quantityText by remember {
        val qty = item.quantity
        mutableStateOf(if (qty % 1.0 == 0.0) qty.toLong().toString() else qty.toString())
    }

    var priceText by remember {
        val pr = item.priceAtSale
        mutableStateOf(if (pr % 1.0 == 0.0) pr.toLong().toString() else pr.toString())
    }

    var activeField by remember { mutableStateOf(EditFieldTarget.QUANTITY) }
    val unitLabel = when (item.product.unitType) {
        UnitType.METR -> "Metr"
        UnitType.KG -> "Kg"
        UnitType.DONA -> "Dona"
    }
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }

    fun handleNumberInput(char: String) {
        if (activeField == EditFieldTarget.QUANTITY) {
            if (char == "." && quantityText.contains(".")) return
            if (quantityText == "0" && char != ".") quantityText = char
            else quantityText += char
        } else {
            if (char == "." && priceText.contains(".")) return
            if (priceText == "0" && char != ".") priceText = char
            else priceText += char
        }
    }

    fun handleBackspace() {
        if (activeField == EditFieldTarget.QUANTITY) {
            if (quantityText.isNotEmpty()) {
                quantityText = quantityText.dropLast(1)
                if (quantityText.isEmpty()) quantityText = "0"
            }
        } else {
            if (priceText.isNotEmpty()) {
                priceText = priceText.dropLast(1)
                if (priceText.isEmpty()) priceText = "0"
            }
        }
    }

    fun handleClear() {
        if (activeField == EditFieldTarget.QUANTITY) {
            quantityText = "0"
        } else {
            priceText = "0"
        }
    }

    Dialog(
        onDismissRequest = onDismissRequest,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .padding(vertical = 12.dp),
            shape = RoundedCornerShape(28.dp),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(18.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Sarlavha
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = item.product.name,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Text(
                            text = "Asl narxi: ${numberFormat.format(item.product.sellingPrice)} so'm",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    IconButton(
                        onClick = onDismissRequest,
                        modifier = Modifier.size(36.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Yopish",
                            tint = MaterialTheme.colorScheme.outline
                        )
                    }
                }

                if (item.hasSellingPrice2) {
                    Spacer(modifier = Modifier.height(10.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        val p1 = item.product.sellingPrice
                        val p2 = item.product.sellingPrice2 ?: 0.0
                        val curPrice = priceText.toDoubleOrNull() ?: 0.0

                        Surface(
                            onClick = {
                                priceText = if (p1 % 1.0 == 0.0) p1.toLong().toString() else p1.toString()
                            },
                            shape = RoundedCornerShape(10.dp),
                            color = if (kotlin.math.abs(curPrice - p1) < 0.01) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text(
                                text = "1-narx: ${numberFormat.format(p1)}",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (kotlin.math.abs(curPrice - p1) < 0.01) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 6.dp),
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center
                            )
                        }

                        Surface(
                            onClick = {
                                priceText = if (p2 % 1.0 == 0.0) p2.toLong().toString() else p2.toString()
                            },
                            shape = RoundedCornerShape(10.dp),
                            color = if (kotlin.math.abs(curPrice - p2) < 0.01) Color(0xFF16A34A).copy(alpha = 0.15f) else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text(
                                text = "2-narx: ${numberFormat.format(p2)}",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (kotlin.math.abs(curPrice - p2) < 0.01) Color(0xFF16A34A) else MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 6.dp),
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Tanlash kartalari (Miqdor va Narx)
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    // Miqdor tanlash maydoni
                    Surface(
                        onClick = { activeField = EditFieldTarget.QUANTITY },
                        shape = RoundedCornerShape(18.dp),
                        color = if (activeField == EditFieldTarget.QUANTITY) MaterialTheme.colorScheme.primaryContainer
                        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                        border = androidx.compose.foundation.BorderStroke(
                            width = if (activeField == EditFieldTarget.QUANTITY) 2.dp else 1.dp,
                            color = if (activeField == EditFieldTarget.QUANTITY) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.outline.copy(alpha = 0.2f)
                        ),
                        modifier = Modifier.weight(1f)
                    ) {
                        Column(modifier = Modifier.padding(12.dp)) {
                            Text(
                                text = "Miqdor ($unitLabel)",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (activeField == EditFieldTarget.QUANTITY) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = quantityText.ifEmpty { "0" },
                                fontSize = 18.sp,
                                fontWeight = FontWeight.Black,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                        }
                    }

                    // Narx tanlash maydoni
                    Surface(
                        onClick = { activeField = EditFieldTarget.PRICE },
                        shape = RoundedCornerShape(18.dp),
                        color = if (activeField == EditFieldTarget.PRICE) MaterialTheme.colorScheme.primaryContainer
                        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                        border = androidx.compose.foundation.BorderStroke(
                            width = if (activeField == EditFieldTarget.PRICE) 2.dp else 1.dp,
                            color = if (activeField == EditFieldTarget.PRICE) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.outline.copy(alpha = 0.2f)
                        ),
                        modifier = Modifier.weight(1.3f)
                    ) {
                        Column(modifier = Modifier.padding(12.dp)) {
                            Text(
                                text = "Narx (so'm)",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                color = if (activeField == EditFieldTarget.PRICE) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            val prVal = priceText.toDoubleOrNull() ?: 0.0
                            Text(
                                text = numberFormat.format(prVal),
                                fontSize = 18.sp,
                                fontWeight = FontWeight.Black,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Custom Apple Style POS Numpad (7-8-9 tepada)
                PosNumpad(
                    onNumberClick = { handleNumberInput(it) },
                    onBackspaceClick = { handleBackspace() },
                    onClearClick = { handleClear() },
                    showDecimal = activeField == EditFieldTarget.QUANTITY
                )

                Spacer(modifier = Modifier.height(14.dp))

                // Pastki Amallar (O'chirish va Saqlash)
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Surface(
                        onClick = onDelete,
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.6f),
                        modifier = Modifier.size(48.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Delete,
                                contentDescription = "O'chirish",
                                tint = MaterialTheme.colorScheme.error,
                                modifier = Modifier.size(22.dp)
                            )
                        }
                    }

                    Button(
                        onClick = {
                            val finalQty = quantityText.toDoubleOrNull() ?: item.quantity
                            val finalPrice = priceText.toDoubleOrNull() ?: item.priceAtSale
                            if (finalQty > 0) {
                                onSave(finalQty, finalPrice)
                                onDismissRequest()
                            }
                        },
                        shape = CircleShape,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.primary,
                            contentColor = Color.White
                        ),
                        modifier = Modifier
                            .weight(1f)
                            .height(48.dp)
                    ) {
                        Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Saqlash", fontWeight = FontWeight.Bold, fontSize = 15.sp)
                    }
                }
            }
        }
    }
}
