package uz.pos.electro.ui.cashier

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background

import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.isImeVisible

import androidx.compose.material.icons.Icons

import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DeleteSweep
import androidx.compose.material.icons.filled.PauseCircle
import androidx.compose.material.icons.filled.PersonAdd
import androidx.compose.material.icons.filled.PointOfSale
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.ShoppingCart
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
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
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import kotlinx.coroutines.flow.collectLatest
import uz.pos.electro.data.model.CartItemModel
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.scanner.CameraScannerDialog
import uz.pos.electro.scanner.HardwareScannerManager
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary

import java.text.NumberFormat
import java.util.Locale

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun CashierScreen(
    viewModel: CashierViewModel,
    scannerManager: HardwareScannerManager? = null,
    onAddUnknownProduct: (String) -> Unit = {}
) {
    val context = LocalContext.current
    val cartItems by viewModel.cartItems.collectAsState()
    val heldCarts by viewModel.heldCarts.collectAsState()
    val isHoldCartsVisible by viewModel.isHoldCartsDialogVisible.collectAsState()
    val unrecognizedBarcode by viewModel.unrecognizedBarcode.collectAsState()
    val searchQuery by viewModel.searchQuery.collectAsState()
    val searchResults by viewModel.searchResults.collectAsState()
    val selectedItemIndex by viewModel.selectedCartItemIndex.collectAsState()
    val completedSale by viewModel.lastCompletedSale.collectAsState()
    val isCompletingSale by viewModel.isCompletingSale.collectAsState()
    val isCheckoutDialogVisible by viewModel.isCheckoutDialogVisible.collectAsState()
    val cardTaxRate by viewModel.cardTaxRate.collectAsState()

    val activeDebtCustomers by viewModel.activeDebtCustomers.collectAsState()
    val selectedDebtCustomer by viewModel.selectedDebtCustomer.collectAsState()
    val debtDueDate by viewModel.debtDueDate.collectAsState()
    val debtCashAdvance by viewModel.debtCashAdvance.collectAsState()
    val debtCardAdvance by viewModel.debtCardAdvance.collectAsState()
    val isQuickAddCustomerOpen by viewModel.isQuickAddCustomerOpen.collectAsState()

    var isCameraScannerOpen by remember { mutableStateOf(false) }
    var isClearCartDialogVisible by remember { mutableStateOf(false) }
    var isBrakDialogVisible by remember { mutableStateOf(false) }
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val isKeyboardOpen = WindowInsets.isImeVisible

    // Back handling: 1st back click clears cashier search query
    BackHandler(enabled = searchQuery.isNotBlank()) {
        viewModel.onSearchQueryChanged("")
    }




    // Tashqi apparat skaneri signallarini tinglash
    LaunchedEffect(scannerManager) {
        scannerManager?.scanEvents?.collectLatest { barcode ->
            uz.pos.electro.util.SoundFeedbackManager.playBeepAndVibrate(context)
            viewModel.onBarcodeScanned(barcode)
        }
    }


    // Toast xabarlari
    LaunchedEffect(Unit) {
        viewModel.toastEvent.collectLatest { message ->
            Toast.makeText(context, message, Toast.LENGTH_SHORT).show()
        }
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(12.dp)
        ) {
            // 1. Yuqori qism: Qidiruv paneli va Pause (Hold) tugmasi
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                OutlinedTextField(
                    value = searchQuery,
                    onValueChange = { viewModel.onSearchQueryChanged(it) },
                    placeholder = { Text("Qidirish...", color = MaterialTheme.colorScheme.onSurfaceVariant) },
                    leadingIcon = {
                        Icon(
                            imageVector = Icons.Default.Search,
                            contentDescription = "Qidiruv",
                            tint = MaterialTheme.colorScheme.primary
                        )
                    },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    shape = CircleShape,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedContainerColor = MaterialTheme.colorScheme.surface,
                        unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                        focusedBorderColor = MaterialTheme.colorScheme.primary,
                        unfocusedBorderColor = MaterialTheme.colorScheme.outline.copy(alpha = 0.4f)
                    )
                )

                Spacer(modifier = Modifier.width(10.dp))

                // Muzlatilgan savatlar ro'yxati (To'liq yumaloq CircleShape)
                Surface(
                    onClick = { viewModel.showHoldCartsDialog() },
                    shape = CircleShape,
                    color = MaterialTheme.colorScheme.surface,
                    border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.3f)),
                    shadowElevation = 2.dp,
                    modifier = Modifier.size(50.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        BadgedBox(
                            badge = {
                                if (heldCarts.isNotEmpty()) {
                                    Badge(
                                        containerColor = MaterialTheme.colorScheme.error,
                                        contentColor = Color.White
                                    ) { Text(heldCarts.size.toString(), fontWeight = FontWeight.Bold) }
                                }
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Default.PauseCircle,
                                contentDescription = "Muzlatilganlar",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(26.dp)
                            )
                        }
                    }
                }
            }

            // Jonli qidiruv natijalari (Popup / List)
            if (searchResults.isNotEmpty() && searchQuery.isNotBlank()) {
                Spacer(modifier = Modifier.height(6.dp))
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(20.dp),
                    elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height((searchResults.size * 60).coerceAtMost(200).dp)
                    ) {
                        items(searchResults) { product ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { viewModel.addProductToCart(product) }
                                    .padding(horizontal = 18.dp, vertical = 10.dp),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Column {
                                    Text(
                                        text = product.name,
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontWeight = FontWeight.Bold,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                    val unitLabel = when (product.unitType) {
                                        UnitType.METR -> "m"
                                        UnitType.KG -> "kg"
                                        UnitType.DONA -> "dona"
                                    }
                                    Text(
                                        text = "Qoldiq: ${product.stockQuantity} $unitLabel",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                                Text(
                                    text = "${numberFormat.format(product.sellingPrice)} so'm",
                                    style = MaterialTheme.typography.bodyMedium,
                                    fontWeight = FontWeight.ExtraBold,
                                    color = MaterialTheme.colorScheme.primary
                                )
                            }
                            HorizontalDivider(color = MaterialTheme.colorScheme.surfaceVariant)
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            // 2. Savat / Chek ko'rinishi
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                shape = RoundedCornerShape(24.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            ) {
                if (cartItems.isEmpty()) {
                    Box(
                        modifier = Modifier.fillMaxSize(),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Icon(
                                imageVector = Icons.Default.ShoppingCart,
                                contentDescription = "Savat",
                                modifier = Modifier.size(56.dp),
                                tint = MaterialTheme.colorScheme.outline
                            )
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = "Savat bo'sh",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Shtrix-kodni skanerlang yoki qidiruvdan qo'shing",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                } else {
                    Column(modifier = Modifier.fillMaxSize().padding(14.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = "SAVAT (${cartItems.size} xil tovar)",
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.ExtraBold,
                                color = MaterialTheme.colorScheme.primary
                            )

                            Row(
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                // Brak sifatida hisobdan chiqarish (0 so'm) tugmasi (Ixcham Dumaloq)
                                Surface(
                                    onClick = {
                                        if (cartItems.isEmpty()) {
                                            Toast.makeText(context, "Savat bo'sh!", Toast.LENGTH_SHORT).show()
                                        } else {
                                            isBrakDialogVisible = true
                                        }
                                    },
                                    shape = CircleShape,
                                    color = Color(0xFFF43F5E).copy(alpha = 0.15f),
                                    modifier = Modifier.size(36.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = Icons.Default.Warning,
                                            contentDescription = "Brak (0 so'm)",
                                            tint = Color(0xFFF43F5E),
                                            modifier = Modifier.size(18.dp)
                                        )
                                    }
                                }

                                // Savatni bittada tozalash tugmasi (Ixcham Dumaloq)
                                Surface(
                                    onClick = { isClearCartDialogVisible = true },
                                    shape = CircleShape,
                                    color = MaterialTheme.colorScheme.errorContainer,
                                    modifier = Modifier.size(36.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = Icons.Default.DeleteSweep,
                                            contentDescription = "Tozalash",
                                            tint = MaterialTheme.colorScheme.error,
                                            modifier = Modifier.size(18.dp)
                                        )
                                    }
                                }

                                // Savatni muzlatish (Hold) tugmasi (Ixcham Dumaloq)
                                Surface(
                                    onClick = { viewModel.holdCurrentCart() },
                                    shape = CircleShape,
                                    color = MaterialTheme.colorScheme.primaryContainer,
                                    modifier = Modifier.size(36.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = Icons.Default.PauseCircle,
                                            contentDescription = "Hold",
                                            tint = MaterialTheme.colorScheme.primary,
                                            modifier = Modifier.size(18.dp)
                                        )
                                    }
                                }
                            }

                        }
                        selectedDebtCustomer?.let { cust ->
                            Spacer(modifier = Modifier.height(6.dp))
                            Surface(
                                shape = RoundedCornerShape(8.dp),
                                color = Color(0xFFFEF3C7),
                                border = androidx.compose.foundation.BorderStroke(1.dp, Color(0xFFF59E0B)),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Row(
                                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Row(
                                        verticalAlignment = Alignment.CenterVertically,
                                        modifier = Modifier.weight(1f, fill = false)
                                    ) {
                                        Text("📒 Nasiya: ", fontSize = 12.sp, fontWeight = FontWeight.Bold, color = Color(0xFFB45309))
                                        Text(cust.name, fontSize = 12.sp, fontWeight = FontWeight.ExtraBold, color = Color(0xFF92400E))
                                        Text(" (${numberFormat.format(cust.balanceUz)} so'm)", fontSize = 11.sp, color = Color(0xFFB45309))
                                    }
                                    IconButton(
                                        onClick = { viewModel.selectDebtCustomer(null) },
                                        modifier = Modifier.size(22.dp)
                                    ) {
                                        Icon(
                                            imageVector = Icons.Default.Close,
                                            contentDescription = "Bekor qilish",
                                            tint = Color(0xFFB45309),
                                            modifier = Modifier.size(14.dp)
                                        )
                                    }
                                }
                            }
                        }
                        Spacer(modifier = Modifier.height(10.dp))

                        LazyColumn(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            itemsIndexed(cartItems) { index, item ->
                                CartItemRow(
                                    item = item,
                                    onItemClick = { viewModel.selectCartItemForEdit(index) },
                                    onTogglePrice = { viewModel.toggleCartItemPrice(index) },
                                    onIncrease = {
                                        viewModel.updateCartItem(
                                            index,
                                            item.quantity + 1.0,
                                            item.priceAtSale
                                        )
                                    },
                                    onDecrease = {
                                        viewModel.updateCartItem(
                                            index,
                                            item.quantity - 1.0,
                                            item.priceAtSale
                                        )
                                    }
                                )
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            // 3. Jami to'lov, Skaner va Sotish paneli
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(26.dp),
                elevation = CardDefaults.cardElevation(defaultElevation = 6.dp),
                colors = CardDefaults.cardColors(
                    containerColor = LinePrimary
                )
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp)
                ) {
                    // JAMI TO'LOV va O'ng tomonda toza QR Skaner tugmasi
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column {
                            Text(
                                text = "JAMI TO'LOV:",
                                style = MaterialTheme.typography.labelMedium,
                                color = Color.White.copy(alpha = 0.85f),
                                fontWeight = FontWeight.Bold
                            )
                            Text(
                                text = "${numberFormat.format(viewModel.totalAmount)} SO'M",
                                style = MaterialTheme.typography.headlineSmall,
                                fontWeight = FontWeight.Black,
                                color = Color.White
                            )
                        }

                        // Skaner tugmasi (Toza bitta oq doira)
                        Surface(
                            onClick = { isCameraScannerOpen = true },
                            shape = CircleShape,
                            color = Color.White,
                            shadowElevation = 4.dp,
                            modifier = Modifier.size(50.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.QrCodeScanner,
                                    contentDescription = "Skaner",
                                    tint = LinePrimary,
                                    modifier = Modifier.size(26.dp)
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(
                            onClick = { viewModel.openCheckoutDialog() },
                            enabled = cartItems.isNotEmpty(),
                            modifier = Modifier.weight(1f),
                            shape = CircleShape,
                            colors = ButtonDefaults.buttonColors(
                                containerColor = LineSecondary,
                                contentColor = Color.White,
                                disabledContainerColor = Color.White.copy(alpha = 0.25f),
                                disabledContentColor = Color.White.copy(alpha = 0.6f)
                            )
                        ) {
                            Icon(
                                imageVector = Icons.Default.PointOfSale,
                                contentDescription = "Sotish"
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = "SOTISH",
                                fontSize = 17.sp,
                                fontWeight = FontWeight.Black
                            )
                        }
                    }
                }
            }


        }

        // Savatni tozalash tasdiqlash modali
        if (isClearCartDialogVisible) {
            ClearCartConfirmationDialog(
                onConfirm = {
                    viewModel.clearCart()
                    isClearCartDialogVisible = false
                },
                onDismiss = { isClearCartDialogVisible = false }
            )
        }

        // Brak tovarlarni hisobdan chiqarish tasdiqlash dialogi
        if (isBrakDialogVisible) {
            val totalCost = cartItems.sumOf { it.product.costPrice * it.quantity }
            AlertDialog(
                onDismissRequest = { isBrakDialogVisible = false },
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Default.Warning,
                            contentDescription = null,
                            tint = Color(0xFFF43F5E)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Brak Tovarlarni Chiqarish", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                    }
                },
                text = {
                    Column {
                        Text(
                            text = "Savatdagi barcha tovarlarni 0 so'm narx bilan brak (spisanie) sifatida hisobdan chiqarishni tasdiqlaysizmi?",
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Spacer(modifier = Modifier.height(12.dp))
                        Card(
                            shape = RoundedCornerShape(10.dp),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
                        ) {
                            Column(modifier = Modifier.padding(10.dp)) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Text("Chiqarilayotgan:", style = MaterialTheme.typography.bodySmall)
                                    Text("${cartItems.size} xil tovar", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.bodySmall)
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Text("Sotish narxi:", style = MaterialTheme.typography.bodySmall)
                                    Text("0 so'm (Bepul)", fontWeight = FontWeight.Bold, color = LineSecondary, style = MaterialTheme.typography.bodySmall)
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Text("Zarar (tannarx):", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = Color(0xFFF43F5E))
                                    Text("-${numberFormat.format(totalCost)} so'm", fontWeight = FontWeight.Black, color = Color(0xFFF43F5E), style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "ℹ️ Ushbu tovarlar ombor qoldig'idan to'liq ayiriladi, chek chiqarilmaydi va hisobotlarda sof zarar sifatida qayd etiladi.",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                confirmButton = {
                    Button(
                        onClick = {
                            viewModel.writeOffCartAsBrak()
                            isBrakDialogVisible = false
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFE11D48))
                    ) {
                        Text("Ha, chiqarilsin", fontWeight = FontWeight.Bold)
                    }
                },
                dismissButton = {
                    TextButton(onClick = { isBrakDialogVisible = false }) {
                        Text("Yo'q, bekor qilish")
                    }
                }
            )
        }

        // To'lov turini tanlash dialogi (Naqd, Karta, Aralash, Nasiya)
        if (isCheckoutDialogVisible) {
            CheckoutPaymentDialog(
                isSubmitting = isCompletingSale,
                totalAmount = viewModel.totalAmount,
                cardTaxRate = cardTaxRate,
                activeDebtCustomers = activeDebtCustomers,
                selectedDebtCustomer = selectedDebtCustomer,
                onSelectDebtCustomer = { viewModel.selectDebtCustomer(it) },
                debtDueDate = debtDueDate,
                onSetDebtDueDate = { viewModel.setDebtDueDate(it) },
                debtCashAdvance = debtCashAdvance,
                onSetDebtCashAdvance = { viewModel.setDebtCashAdvance(it) },
                debtCardAdvance = debtCardAdvance,
                onSetDebtCardAdvance = { viewModel.setDebtCardAdvance(it) },
                onOpenQuickAddCustomer = { viewModel.openQuickAddCustomer() },
                onDismissRequest = { viewModel.closeCheckoutDialog() },
                onConfirmSale = { paymentType, cash, card, tax, rate ->
                    viewModel.completeSale(paymentType, cash, card, tax, rate)
                },
                onConfirmDebtSale = { customerGuid, dueDate, cashAdv, cardAdv ->
                    viewModel.completeDebtSale(customerGuid, dueDate, cashAdv, cardAdv)
                }
            )
        }

        // Tezkor mijoz qo'shish dialogi
        if (isQuickAddCustomerOpen) {
            QuickAddCustomerDialog(
                onDismiss = { viewModel.closeQuickAddCustomer() },
                onSave = { name, phone, note ->
                    viewModel.saveQuickCustomer(name, phone, note)
                }
            )
        }

        // Muzlatilgan savatlar oynasi (Hold Carts)
        if (isHoldCartsVisible) {
            HoldCartsDialog(
                heldCarts = heldCarts,
                onDismissRequest = { viewModel.dismissHoldCartsDialog() },
                onResumeCart = { cart -> viewModel.resumeHeldCart(cart) },
                onDeleteCart = { id -> viewModel.deleteHeldCart(id) }
            )
        }

        // Tahrirlash modali
        selectedItemIndex?.let { index ->
            if (index in cartItems.indices) {
                val item = cartItems[index]
                EditCartItemDialog(
                    item = item,
                    onDismissRequest = { viewModel.closeEditCartItemDialog() },
                    onSave = { newQty, newPrice ->
                        viewModel.updateCartItem(index, newQty, newPrice)
                    },
                    onDelete = {
                        viewModel.removeCartItem(index)
                        viewModel.closeEditCartItemDialog()
                    }
                )
            }
        }

        // Topilmagan shtrix-kod ogohlantirish va qo'shish taklifi
        unrecognizedBarcode?.let { barcode ->
            UnrecognizedBarcodeDialog(
                barcode = barcode,
                onDismissRequest = { viewModel.clearUnrecognizedBarcode() },
                onAddProduct = { code ->
                    viewModel.clearUnrecognizedBarcode()
                    onAddUnknownProduct(code)
                }
            )
        }

        // Yakunlangan Savdo Cheki
        completedSale?.let { saleState ->
            ReceiptDialog(
                state = saleState,
                onDismissRequest = { viewModel.dismissCompletedSaleDialog() }
            )
        }

        // Kamera Skaneri
        if (isCameraScannerOpen) {
            CameraScannerDialog(
                onDismissRequest = { isCameraScannerOpen = false },
                onBarcodeScanned = { barcode ->
                    viewModel.onBarcodeScanned(barcode)
                }
            )
        }
    }
}

@Composable
private fun ClearCartConfirmationDialog(
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    Dialog(onDismissRequest = onDismiss) {
        Card(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .padding(16.dp),
            shape = RoundedCornerShape(26.dp),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(22.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Surface(
                    shape = CircleShape,
                    color = MaterialTheme.colorScheme.errorContainer,
                    modifier = Modifier.size(56.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = Icons.Default.DeleteSweep,
                            contentDescription = "Tozalash",
                            tint = MaterialTheme.colorScheme.error,
                            modifier = Modifier.size(28.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                Text(
                    text = "Savatni tozalash",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = "Savatdagi barcha tovarlarni o'chirib, savatni bo'shatishni tasdiqlaysizmi?",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )

                Spacer(modifier = Modifier.height(22.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(
                        onClick = onDismiss,
                        shape = CircleShape,
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Yo'q", fontWeight = FontWeight.SemiBold)
                    }

                    Button(
                        onClick = onConfirm,
                        shape = CircleShape,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.error,
                            contentColor = Color.White
                        ),
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Ha, tozalash", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}

@Composable
private fun CartItemRow(
    item: CartItemModel,
    onItemClick: () -> Unit,
    onTogglePrice: () -> Unit = {},
    onIncrease: () -> Unit,
    onDecrease: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val unitLabel = when (item.product.unitType) {
        UnitType.METR -> "m"
        UnitType.KG -> "kg"
        UnitType.DONA -> "ta"
    }
    val qtyText = if (item.quantity % 1.0 == 0.0) item.quantity.toLong().toString() else item.quantity.toString()

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onItemClick() },
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            // 1-qator: Tovar nomi va Jami narxi
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.weight(1f, fill = false)
                ) {
                    if (item.hasSellingPrice2) {
                        Surface(
                            onClick = onTogglePrice,
                            shape = RoundedCornerShape(8.dp),
                            color = if (item.isPrice2Active) Color(0xFF16A34A) else MaterialTheme.colorScheme.primaryContainer,
                            modifier = Modifier.padding(end = 6.dp)
                        ) {
                            Row(
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 3.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Icon(
                                    imageVector = Icons.Default.SwapHoriz,
                                    contentDescription = "Narxni almashtirish",
                                    modifier = Modifier.size(13.dp),
                                    tint = if (item.isPrice2Active) Color.White else MaterialTheme.colorScheme.primary
                                )
                                Spacer(modifier = Modifier.width(3.dp))
                                Text(
                                    text = if (item.isPrice2Active) "2-narx" else "1-narx",
                                    fontSize = 10.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = if (item.isPrice2Active) Color.White else MaterialTheme.colorScheme.primary
                                )
                            }
                        }
                    }

                    Text(
                        text = item.product.name,
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                        overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                }

                Spacer(modifier = Modifier.width(8.dp))

                Text(
                    text = "${numberFormat.format(item.totalPrice)} so'm",
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Black,
                    color = MaterialTheme.colorScheme.primary
                )
            }

            Spacer(modifier = Modifier.height(6.dp))

            // 2-qator: Birlik narxi va [-] / [+] miqdor boshqaruvi
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.weight(1f, fill = false)
                ) {
                    Text(
                        text = "$qtyText $unitLabel x ${numberFormat.format(item.priceAtSale)} so'm",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )

                    if (item.hasWarehouse) {
                        Surface(
                            shape = RoundedCornerShape(4.dp),
                            color = Color(0xFF0F243A),
                            border = androidx.compose.foundation.BorderStroke(0.8.dp, Color(0xFF0284C7)),
                            modifier = Modifier.padding(start = 6.dp)
                        ) {
                            Text(
                                text = "🏢 ${item.warehouseName}",
                                fontSize = 10.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = Color(0xFF38BDF8),
                                modifier = Modifier.padding(horizontal = 4.dp, vertical = 1.dp)
                            )
                        }
                    }
                }

                Row(verticalAlignment = Alignment.CenterVertically) {
                    // Ixcham - tugmasi
                    Surface(
                        onClick = onDecrease,
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.surface,
                        modifier = Modifier.size(28.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Remove,
                                contentDescription = "Kamaytirish",
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.primary
                            )
                        }
                    }

                    Text(
                        text = qtyText,
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.ExtraBold,
                        modifier = Modifier.padding(horizontal = 10.dp),
                        color = MaterialTheme.colorScheme.onSurface
                    )

                    // Ixcham + tugmasi
                    Surface(
                        onClick = onIncrease,
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.surface,
                        modifier = Modifier.size(28.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Add,
                                contentDescription = "Oshirish",
                                modifier = Modifier.size(14.dp),
                                tint = MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun QuickAddCustomerDialog(
    onDismiss: () -> Unit,
    onSave: (name: String, phone: String, note: String) -> Unit
) {
    var name by remember { mutableStateOf("") }
    var phone by remember { mutableStateOf("") }
    var note by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(imageVector = Icons.Default.PersonAdd, contentDescription = null, tint = LinePrimary)
                Spacer(modifier = Modifier.width(8.dp))
                Text("Yangi mijoz qo'shish", fontWeight = FontWeight.Bold, fontSize = 18.sp)
            }
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Mijoz ismi (majburiy)") },
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

