package uz.pos.electro.ui.products

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Category
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Inventory
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import uz.pos.electro.data.model.CurrencyType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.scanner.CameraScannerDialog

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AddEditProductDialog(
    viewModel: ProductViewModel,
    onDismissRequest: () -> Unit
) {
    val barcode by viewModel.barcodeInput.collectAsState()
    val name by viewModel.nameInput.collectAsState()
    val category by viewModel.categoryInput.collectAsState()
    val allCategories by viewModel.categories.collectAsState()
    val costPrice by viewModel.costPriceInput.collectAsState()
    val costCurrency by viewModel.costCurrencyInput.collectAsState()
    val sellingPrice by viewModel.sellingPriceInput.collectAsState()
    val sellingPrice2 by viewModel.sellingPrice2Input.collectAsState()
    val stockQuantity by viewModel.stockQuantityInput.collectAsState()
    val unitType by viewModel.unitTypeInput.collectAsState()
    val minStockAlert by viewModel.minStockAlertInput.collectAsState()
    val note by viewModel.noteInput.collectAsState()
    val errorMessage by viewModel.errorMessage.collectAsState()
    val isEditing = viewModel.editingProductId.collectAsState().value != null

    var isCameraScannerOpen by remember { mutableStateOf(false) }
    var isCategoryDropdownExpanded by remember { mutableStateOf(false) }
    var localValidationMsg by remember { mutableStateOf<String?>(null) }

    val keyboardController = LocalSoftwareKeyboardController.current

    // Filtrlangan kategoriyalar taklifi
    val filteredCategories = remember(category, allCategories) {
        if (category.isBlank()) allCategories
        else allCategories.filter { it.contains(category, ignoreCase = true) }
    }

    fun submitForm() {
        val trimmedName = name.trim()
        if (trimmedName.isBlank()) {
            localValidationMsg = "Mahsulot nomini kiritish shart!"
            return
        }
        val duplicate = viewModel.findDuplicateProduct(trimmedName, viewModel.editingProductId.value)
        if (duplicate != null) {
            localValidationMsg = "\"${duplicate.name}\" nomli tovar omborda allaqachon mavjud!"
            return
        }
        val sellPrice = sellingPrice.trim().toDoubleOrNull()
        if (sellPrice == null || sellPrice <= 0) {
            localValidationMsg = "1-sotish narxini kiritish shart!"
            return
        }
        localValidationMsg = null
        keyboardController?.hide()
        viewModel.saveProduct()
    }

    // Modal dialog o'rniga to'liq sahifa ko'rinishida render qilish
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .statusBarsPadding()
            .imePadding()
    ) {
        Column(
            modifier = Modifier.fillMaxSize()
        ) {
            // --- TOP APP BAR ---
            Surface(
                modifier = Modifier.fillMaxWidth(),
                color = MaterialTheme.colorScheme.surface,
                tonalElevation = 2.dp,
                shadowElevation = 3.dp
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 8.dp, vertical = 10.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    IconButton(onClick = onDismissRequest) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = "Ortga",
                            tint = MaterialTheme.colorScheme.primary
                        )
                    }

                    Spacer(modifier = Modifier.width(4.dp))

                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = if (isEditing) "Mahsulotni tahrirlash" else "Yangi mahsulot kiritish",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        val selectedWh by viewModel.selectedWarehouse.collectAsState()
                        Text(
                            text = "🏢 Ombor: ${selectedWh?.name ?: "Asosiy do'kon"}",
                            style = MaterialTheme.typography.bodySmall,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.primary
                        )
                    }

                    Button(
                        onClick = { submitForm() },
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = Color(0xFF0B6477)
                        )
                    ) {
                        Icon(
                            imageVector = Icons.Default.Check,
                            contentDescription = "Saqlash",
                            modifier = Modifier.size(16.dp),
                            tint = Color.White
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = "Saqlash",
                            fontWeight = FontWeight.Bold,
                            fontSize = 13.sp,
                            color = Color.White
                        )
                    }
                }
            }

            // --- FORMA ASOSIY QISMI (Scroll bo'ladigan bitta sahifa) ---
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                // 1. ASOSIY MA'LUMOTLAR KARTASI
                Card(
                    shape = RoundedCornerShape(20.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Default.Inventory,
                                contentDescription = null,
                                tint = Color(0xFF0B6477),
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = "Asosiy ma'lumotlar",
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.Bold,
                                color = Color(0xFF0B6477)
                            )
                        }

                        // Shtrix-kod va skaner tugmasi
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            OutlinedTextField(
                                value = barcode,
                                onValueChange = { viewModel.onBarcodeChanged(it) },
                                label = { Text("Shtrix-kod (ixtiyoriy)") },
                                placeholder = { Text("Skanerlang yoki kiriting") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(14.dp),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text, imeAction = ImeAction.Next),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = Color(0xFF0B6477),
                                    unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                                )
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            IconButton(
                                onClick = { isCameraScannerOpen = true },
                                modifier = Modifier
                                    .size(52.dp)
                                    .clip(CircleShape)
                                    .background(Color(0xFF0B6477).copy(alpha = 0.1f))
                            ) {
                                Icon(
                                    imageVector = Icons.Default.QrCodeScanner,
                                    contentDescription = "Skanerlash",
                                    tint = Color(0xFF0B6477),
                                    modifier = Modifier.size(26.dp)
                                )
                            }
                        }

                        // Mahsulot Nomi (Majburiy)
                        OutlinedTextField(
                            value = name,
                            onValueChange = {
                                viewModel.onNameChanged(it)
                                localValidationMsg = null
                            },
                            label = { Text("Mahsulot nomi * (majburiy)") },
                            placeholder = { Text("Masalan: Kabel 2x1.5 VVG-P") },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(14.dp),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text, capitalization = KeyboardCapitalization.Sentences, imeAction = ImeAction.Next),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = Color(0xFF0B6477),
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                            )
                        )

                        // Kategoriya (Searchable dropdown)
                        ExposedDropdownMenuBox(
                            expanded = isCategoryDropdownExpanded,
                            onExpandedChange = { isCategoryDropdownExpanded = it }
                        ) {
                            OutlinedTextField(
                                value = category,
                                onValueChange = {
                                    viewModel.onCategoryChanged(it)
                                    isCategoryDropdownExpanded = true
                                },
                                label = { Text("Kategoriya") },
                                leadingIcon = {
                                    Icon(
                                        imageVector = Icons.Default.Category,
                                        contentDescription = null,
                                        tint = Color(0xFF0B6477),
                                        modifier = Modifier.size(18.dp)
                                    )
                                },
                                trailingIcon = {
                                    ExposedDropdownMenuDefaults.TrailingIcon(expanded = isCategoryDropdownExpanded)
                                },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .menuAnchor(MenuAnchorType.PrimaryEditable),
                                singleLine = true,
                                shape = RoundedCornerShape(14.dp),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = Color(0xFF0B6477),
                                    unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                                )
                            )

                            if (filteredCategories.isNotEmpty()) {
                                ExposedDropdownMenu(
                                    expanded = isCategoryDropdownExpanded,
                                    onDismissRequest = { isCategoryDropdownExpanded = false }
                                ) {
                                    filteredCategories.forEach { catOption ->
                                        DropdownMenuItem(
                                            text = { Text(catOption, fontWeight = FontWeight.SemiBold) },
                                            onClick = {
                                                viewModel.onCategoryChanged(catOption)
                                                isCategoryDropdownExpanded = false
                                            }
                                        )
                                    }
                                }
                            }
                        }

                        // O'lchov birligi (Dona / Metr)
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .horizontalScroll(rememberScrollState()),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "Birlik:",
                                style = MaterialTheme.typography.bodyMedium,
                                fontWeight = FontWeight.SemiBold
                            )

                            FilterChip(
                                selected = unitType == UnitType.DONA,
                                onClick = { viewModel.onUnitTypeChanged(UnitType.DONA) },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    selectedContainerColor = Color(0xFF0B6477),
                                    selectedLabelColor = Color.White
                                ),
                                label = { Text("Dona (sht)") }
                            )

                            FilterChip(
                                selected = unitType == UnitType.METR,
                                onClick = { viewModel.onUnitTypeChanged(UnitType.METR) },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    selectedContainerColor = Color(0xFF0B6477),
                                    selectedLabelColor = Color.White
                                ),
                                label = { Text("Metr (m)") }
                            )

                            FilterChip(
                                selected = unitType == UnitType.KG,
                                onClick = { viewModel.onUnitTypeChanged(UnitType.KG) },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    selectedContainerColor = Color(0xFF0B6477),
                                    selectedLabelColor = Color.White
                                ),
                                label = { Text("Kilogram (kg)") }
                            )
                        }
                    }
                }

                // 2. NARXLAR KARTASI
                Card(
                    shape = RoundedCornerShape(20.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Default.Payments,
                                contentDescription = null,
                                tint = Color(0xFF16A34A),
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = "Narxlar",
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.Bold,
                                color = Color(0xFF16A34A)
                            )
                        }

                        // Tan narxi + Valyuta almashuvi
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            OutlinedTextField(
                                value = costPrice,
                                onValueChange = { viewModel.onCostPriceChanged(it) },
                                label = { Text(if (costCurrency == CurrencyType.USD) "Tan narxi ($)" else "Tan narxi (so'm)") },
                                placeholder = { Text("0") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(14.dp),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = Color(0xFF0B6477),
                                    unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                                )
                            )

                            Spacer(modifier = Modifier.width(8.dp))

                            // $ / UZS almashuv tugmasi
                            Surface(
                                onClick = { viewModel.onCostCurrencyToggle() },
                                shape = RoundedCornerShape(12.dp),
                                color = if (costCurrency == CurrencyType.USD) Color(0xFF0B6477) else MaterialTheme.colorScheme.surfaceVariant,
                                modifier = Modifier
                                    .height(52.dp)
                                    .width(60.dp)
                            ) {
                                Box(contentAlignment = Alignment.Center) {
                                    Text(
                                        text = if (costCurrency == CurrencyType.USD) "$" else "UZS",
                                        fontWeight = FontWeight.Black,
                                        fontSize = 13.sp,
                                        color = if (costCurrency == CurrencyType.USD) Color.White else Color(0xFF0B6477)
                                    )
                                }
                            }
                        }

                        // 1-Sotish narxi (Asosiy / Chakana narx) *
                        OutlinedTextField(
                            value = sellingPrice,
                            onValueChange = {
                                viewModel.onSellingPriceChanged(it)
                                localValidationMsg = null
                            },
                            label = { Text("1-Sotish narxi (so'm) *") },
                            placeholder = { Text("Chakana narx") },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(14.dp),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = Color(0xFF16A34A),
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                            )
                        )

                        // 2-Sotish narxi (Usta / doimiy mijoz narxi) [Ixtiyoriy]
                        OutlinedTextField(
                            value = sellingPrice2,
                            onValueChange = { viewModel.onSellingPrice2Changed(it) },
                            label = { Text("2-Sotish narxi (usta narxi) [Ixtiyoriy]") },
                            placeholder = { Text("Bo'sh qoldirsa ham bo'ladi") },
                            supportingText = { Text("Ustalarga arzonlashtirilgan yoki doimiy narx") },
                            modifier = Modifier.fillMaxWidth(),
                            singleLine = true,
                            shape = RoundedCornerShape(14.dp),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = Color(0xFF16A34A),
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                            )
                        )
                    }
                }

                // 3. OMBOR VA OGOHLANTIRISH KARTASI
                Card(
                    shape = RoundedCornerShape(20.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            // Ombordagi qoldiq
                            OutlinedTextField(
                                value = stockQuantity,
                                onValueChange = { viewModel.onStockQuantityChanged(it) },
                                label = {
                                    Text(
                                        when (unitType) {
                                            UnitType.METR -> "Qoldiq (metr)"
                                            UnitType.KG -> "Qoldiq (kg)"
                                            UnitType.DONA -> "Qoldiq (dona)"
                                        }
                                    )
                                },
                                placeholder = { Text("0") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(14.dp),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = Color(0xFF0B6477),
                                    unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                                )
                            )

                            // Minimal ogohlantirish qoldig'i
                            OutlinedTextField(
                                value = minStockAlert,
                                onValueChange = { viewModel.onMinStockAlertChanged(it) },
                                label = { Text("Min. ogoh") },
                                placeholder = { Text("3") },
                                modifier = Modifier.weight(1f),
                                singleLine = true,
                                shape = RoundedCornerShape(14.dp),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = Color(0xFF0B6477),
                                    unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                                )
                            )
                        }

                        // Izoh / Eslatma
                        OutlinedTextField(
                            value = note,
                            onValueChange = { viewModel.onNoteChanged(it) },
                            label = { Text("Izoh / Eslatma (ixtiyoriy)") },
                            placeholder = { Text("Yetkazib beruvchi yoki tovar xususiyati...") },
                            modifier = Modifier.fillMaxWidth(),
                            minLines = 2,
                            maxLines = 3,
                            shape = RoundedCornerShape(14.dp),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text, imeAction = ImeAction.Done),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = Color(0xFF0B6477),
                                unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.35f)
                            )
                        )
                    }
                }

                // Xatolik xabari
                val activeError = localValidationMsg ?: errorMessage
                if (activeError != null) {
                    Surface(
                        shape = RoundedCornerShape(12.dp),
                        color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.7f),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            text = activeError,
                            color = MaterialTheme.colorScheme.onErrorContainer,
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(12.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(4.dp))

                // --- PASTKI HARAKAT TUGMALARI ---
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 24.dp),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(
                        onClick = onDismissRequest,
                        modifier = Modifier
                            .weight(1f)
                            .height(50.dp),
                        shape = RoundedCornerShape(14.dp)
                    ) {
                        Text("Bekor qilish", fontWeight = FontWeight.SemiBold)
                    }

                    Button(
                        onClick = { submitForm() },
                        modifier = Modifier
                            .weight(1.3f)
                            .height(50.dp),
                        shape = RoundedCornerShape(14.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = Color(0xFF0B6477)
                        )
                    ) {
                        Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = if (isEditing) "Saqlash" else "Qo'shish",
                            fontWeight = FontWeight.Bold,
                            fontSize = 15.sp
                        )
                    }
                }
            }
        }
    }

    if (isCameraScannerOpen) {
        CameraScannerDialog(
            onDismissRequest = { isCameraScannerOpen = false },
            onBarcodeScanned = { code ->
                viewModel.onBarcodeChanged(code)
            }
        )
    }
}
