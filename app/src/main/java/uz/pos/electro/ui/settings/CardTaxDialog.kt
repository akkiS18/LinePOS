package uz.pos.electro.ui.settings

import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import uz.pos.electro.data.repository.TaxSettingsRepository
import uz.pos.electro.ui.theme.LineSecondary

@Composable
fun CardTaxDialog(
    taxSettingsRepository: TaxSettingsRepository,
    onDismissRequest: () -> Unit
) {
    val context = LocalContext.current
    val currentRate = taxSettingsRepository.getCardTaxRate()
    val initialText = if (currentRate % 1.0 == 0.0) {
        currentRate.toInt().toString()
    } else {
        String.format(java.util.Locale.US, "%.2f", currentRate).trimEnd('0').trimEnd('.')
    }
    var taxRateInput by remember { mutableStateOf(initialText) }
    var errorMessage by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismissRequest,
        shape = RoundedCornerShape(20.dp),
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "✏️ Karta solig'i foizi",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        },
        text = {
            Column(modifier = Modifier.fillMaxWidth()) {
                Text(
                    text = "Karta orqali to'langan summadan soliq xarajati sifatida ushlanadigan foizni kiriting:",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(14.dp))

                OutlinedTextField(
                    value = taxRateInput,
                    onValueChange = {
                        taxRateInput = it
                        errorMessage = ""
                    },
                    label = { Text("Soliq foizi (%)") },
                    placeholder = { Text("1.8") },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp),
                    isError = errorMessage.isNotBlank(),
                    supportingText = if (errorMessage.isNotBlank()) {
                        { Text(errorMessage, color = MaterialTheme.colorScheme.error) }
                    } else {
                        { Text("Standart miqdor: 1.8%") }
                    },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val cleaned = taxRateInput.replace(',', '.').trim()
                    val rate = cleaned.toDoubleOrNull()
                    if (rate != null && rate >= 0.0 && rate <= 100.0) {
                        val rounded = Math.round(rate * 100.0) / 100.0
                        taxSettingsRepository.setCardTaxRate(rounded)
                        Toast.makeText(context, "Karta solig'i foizi saqlandi: $rounded%", Toast.LENGTH_SHORT).show()
                        onDismissRequest()
                    } else {
                        errorMessage = "Iltimos, 0 dan 100 gacha to'g'ri son kiriting"
                    }
                },
                shape = RoundedCornerShape(10.dp),
                colors = ButtonDefaults.buttonColors(containerColor = LineSecondary)
            ) {
                Text("Saqlash", fontWeight = FontWeight.Bold)
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
