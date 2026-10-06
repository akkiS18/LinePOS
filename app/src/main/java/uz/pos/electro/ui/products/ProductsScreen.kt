package uz.pos.electro.ui.products

import androidx.activity.compose.BackHandler
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
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Category
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.ShoppingCart
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.FloatingActionButtonDefaults
import androidx.compose.material3.Icon

import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Switch
import androidx.compose.material3.TextButton
import uz.pos.electro.data.local.entity.WarehouseEntity
import uz.pos.electro.data.repository.WarehouseWithStats
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.scanner.CameraScannerDialog
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary
import java.text.NumberFormat
import java.util.Locale

@Composable
fun ProductsScreen(
    viewModel: ProductViewModel
) {
    val products by viewModel.products.collectAsState()
    val searchQuery by viewModel.searchQuery.collectAsState()
    val categoryItems by viewModel.categoryItems.collectAsState()
    val currentCategory by viewModel.currentViewCategory.collectAsState()
    val selectedWarehouse by viewModel.selectedWarehouse.collectAsState()
    val warehousesWithStats by viewModel.warehousesWithStats.collectAsState()
    val stocksInWarehouse by viewModel.stocksInSelectedWarehouse.collectAsState()
    val isAddWarehouseOpen by viewModel.isAddWarehouseOpen.collectAsState()
    val isTransferOpen by viewModel.isTransferOpen.collectAsState()
    val selectedLowStockGuids by viewModel.selectedLowStockGuids.collectAsState()
    val isOnlySelectedFilterActive by viewModel.isOnlySelectedFilterActive.collectAsState()
    val selectedLowStockProducts by viewModel.selectedLowStockProducts.collectAsState()
    val quickStockProduct by viewModel.quickStockProduct.collectAsState()
    val quickStockQuantityInput by viewModel.quickStockQuantityInput.collectAsState()
    val quickStockErrorMessage by viewModel.quickStockErrorMessage.collectAsState()

    var isCameraScannerOpen by remember { mutableStateOf(false) }
    var isBackupDialogOpen by remember { mutableStateOf(false) }
    var isReorderSheetOpen by remember { mutableStateOf(false) }
    var productToDelete by remember { mutableStateOf<ProductEntity?>(null) }

    val isSearching = searchQuery.isNotBlank()
    val isInsideCategory = currentCategory != null || isSearching
    val isLowStockCategory = currentCategory == CATEGORY_LOW_STOCK

    // Back handling: 1st back click clears search query
    BackHandler(enabled = searchQuery.isNotBlank()) {
        viewModel.onSearchQueryChanged("")
    }

    // Back handling: 2nd back click exits category to 2-column grid
    BackHandler(enabled = searchQuery.isBlank() && currentCategory != null) {
        viewModel.exitCategory()
    }

    // Back handling: 3rd back click exits warehouse to warehouse cards list
    BackHandler(enabled = searchQuery.isBlank() && currentCategory == null && selectedWarehouse != null) {
        viewModel.exitWarehouse()
    }

    Scaffold(
        contentWindowInsets = WindowInsets(0, 0, 0, 0),
        containerColor = MaterialTheme.colorScheme.background,
        floatingActionButton = {
            if (isLowStockCategory && selectedLowStockGuids.isNotEmpty()) {
                ExtendedFloatingActionButton(
                    onClick = { isReorderSheetOpen = true },
                    icon = {
                        Icon(
                            imageVector = Icons.Default.ShoppingCart,
                            contentDescription = "Buyurtma",
                            modifier = Modifier.size(20.dp)
                        )
                    },
                    text = {
                        Text(
                            text = "Buyurtma (${selectedLowStockGuids.size} ta)",
                            fontWeight = FontWeight.Bold,
                            fontSize = 14.sp
                        )
                    },
                    shape = CircleShape,
                    containerColor = LinePrimary,
                    contentColor = Color.White,
                    elevation = FloatingActionButtonDefaults.elevation(defaultElevation = 6.dp)
                )
            } else {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(start = 32.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Chap tomondagi DB Zaxira (Backup) tugmasi
                    FloatingActionButton(
                        onClick = { isBackupDialogOpen = true },
                        shape = CircleShape,
                        containerColor = MaterialTheme.colorScheme.surface,
                        contentColor = MaterialTheme.colorScheme.primary,
                        elevation = FloatingActionButtonDefaults.elevation(defaultElevation = 4.dp),
                        modifier = Modifier.size(56.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.CloudDownload,
                            contentDescription = "Baza Zaxirasi",
                            modifier = Modifier.size(26.dp)
                        )
                    }

                    // O'ng tomondagi Mahsulot qo'shish (+) tugmasi (Faqat ombor tanlangandan keyin chiqadi)
                    if (selectedWarehouse != null) {
                        FloatingActionButton(
                            onClick = { viewModel.openAddProductDialog() },
                            shape = CircleShape,
                            containerColor = MaterialTheme.colorScheme.primary,
                            contentColor = Color.White,
                            elevation = FloatingActionButtonDefaults.elevation(defaultElevation = 4.dp),
                            modifier = Modifier.size(56.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Add,
                                contentDescription = "Yangi mahsulot",
                                modifier = Modifier.size(28.dp)
                            )
                        }
                    }
                }
            }
        }
    ) { paddingValues ->

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            // 1. Qidiruv va Kamera orqali skanerlash (Apple Capsule Shape)
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

                Surface(
                    onClick = { isCameraScannerOpen = true },
                    shape = CircleShape,
                    color = MaterialTheme.colorScheme.surface,
                    border = androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.3f)),
                    shadowElevation = 2.dp,
                    modifier = Modifier.size(50.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = Icons.Default.QrCodeScanner,
                            contentDescription = "Skaner",
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(24.dp)
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            if (selectedWarehouse == null && !isSearching) {
                // LEVEL 0: OMBORLAR KARTALARI (Boshlang'ich ko'rinish)
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "OMBORLAR (${warehousesWithStats.size})",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Black,
                        color = MaterialTheme.colorScheme.primary
                    )

                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        OutlinedButton(
                            onClick = { viewModel.openAddWarehouseDialog() },
                            shape = CircleShape,
                            contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp)
                        ) {
                            Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Yangi", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }

                        OutlinedButton(
                            onClick = { viewModel.openTransferDialog() },
                            shape = CircleShape,
                            contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp)
                        ) {
                            Icon(Icons.Default.SwapHoriz, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Ko'chirish", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                if (warehousesWithStats.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .weight(1f),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("Omborlar mavjud emas", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier.weight(1f),
                        contentPadding = PaddingValues(bottom = 80.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        items(warehousesWithStats, key = { it.warehouse.guid }) { stats ->
                            WarehouseCard(
                                stats = stats,
                                onSelect = { viewModel.selectWarehouse(stats.warehouse) },
                                onSetPrimary = { viewModel.setPrimaryWarehouse(stats.warehouse.guid) },
                                onDelete = { viewModel.deleteWarehouse(stats.warehouse.guid) }
                            )
                        }
                    }
                }
            } else {
                // LEVEL 1: TANLANGAN OMBOR ICHIDA (yoki qidiruv vaqtida)
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Surface(
                        onClick = { viewModel.exitWarehouse() },
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.6f),
                        modifier = Modifier.size(34.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                                contentDescription = "Omborlar",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(18.dp)
                            )
                        }
                    }

                    Spacer(modifier = Modifier.width(8.dp))

                    Text(
                        text = selectedWarehouse?.name ?: "Barcha omborlar",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Black,
                        color = MaterialTheme.colorScheme.primary
                    )

                    if (selectedWarehouse?.isPrimary == true) {
                        Spacer(modifier = Modifier.width(6.dp))
                        Surface(
                            shape = CircleShape,
                            color = Color(0xFFFEF3C7)
                        ) {
                            Text(
                                text = "⭐ Asosiy",
                                fontSize = 10.sp,
                                fontWeight = FontWeight.Bold,
                                color = Color(0xFF92400E),
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                            )
                        }
                    }
                }

                if (isInsideCategory) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(bottom = 8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Surface(
                            onClick = { viewModel.exitCategory() },
                            shape = CircleShape,
                            color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.6f),
                            modifier = Modifier.size(32.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                                    contentDescription = "Ortga",
                                    tint = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier.size(16.dp)
                                )
                            }
                        }

                        Spacer(modifier = Modifier.width(8.dp))

                        Text(
                            text = if (isSearching) "Qidiruv natijalari" else (currentCategory ?: "Barchasi"),
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )

                        Spacer(modifier = Modifier.width(6.dp))

                        Text(
                            text = "(${products.size} ta tovar)",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    if (isLowStockCategory) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(bottom = 8.dp),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            // Hammasini tanlash
                            OutlinedButton(
                                onClick = { viewModel.selectAllLowStock(products) },
                                shape = CircleShape,
                                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 2.dp),
                                modifier = Modifier.height(32.dp)
                            ) {
                                Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(14.dp))
                                Spacer(modifier = Modifier.width(4.dp))
                                Text("Barchasi", fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                            }

                            // Tozalash
                            if (selectedLowStockGuids.isNotEmpty()) {
                                OutlinedButton(
                                    onClick = { viewModel.clearSelectedLowStock() },
                                    shape = CircleShape,
                                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 2.dp),
                                    modifier = Modifier.height(32.dp)
                                ) {
                                    Icon(Icons.Default.Close, contentDescription = null, modifier = Modifier.size(14.dp))
                                    Spacer(modifier = Modifier.width(4.dp))
                                    Text("Tozalash", fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                                }
                            }

                            // Faqat tanlanganlar filtri
                            Surface(
                                onClick = { viewModel.setOnlySelectedFilter(!isOnlySelectedFilterActive) },
                                shape = CircleShape,
                                color = if (isOnlySelectedFilterActive) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surface,
                                border = if (!isOnlySelectedFilterActive) androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.4f)) else null,
                                modifier = Modifier.height(32.dp)
                            ) {
                                Box(
                                    contentAlignment = Alignment.Center,
                                    modifier = Modifier.padding(horizontal = 10.dp)
                                ) {
                                    Text(
                                        text = if (selectedLowStockGuids.isNotEmpty()) "Tanlanganlar (${selectedLowStockGuids.size})" else "Tanlanganlar",
                                        fontSize = 11.sp,
                                        fontWeight = FontWeight.SemiBold,
                                        color = if (isOnlySelectedFilterActive) Color.White else MaterialTheme.colorScheme.onSurface
                                    )
                                }
                            }

                            Spacer(modifier = Modifier.weight(1f))

                            // Zakaz ro'yxatini ko'rish tugmasi
                            if (selectedLowStockGuids.isNotEmpty()) {
                                Button(
                                    onClick = { isReorderSheetOpen = true },
                                    shape = CircleShape,
                                    colors = ButtonDefaults.buttonColors(containerColor = LineSecondary),
                                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 2.dp),
                                    modifier = Modifier.height(32.dp)
                                ) {
                                    Icon(Icons.Default.ShoppingCart, contentDescription = null, modifier = Modifier.size(14.dp))
                                    Spacer(modifier = Modifier.width(4.dp))
                                    Text("Zakaz (${selectedLowStockGuids.size})", fontSize = 11.sp, fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }

                    // Kategoriyadagi Mahsulotlar Ro'yxati (List)
                    if (products.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxSize()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = if (isSearching) "Mahsulot topilmadi"
                                else if (currentCategory == CATEGORY_LOW_STOCK) "Kam qolgan tovarlar mavjud emas (Ombor to'liq)"
                                else "Ushbu kategoriyada mahsulotlar mavjud emas",
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    } else {
                        LazyColumn(
                            modifier = Modifier.weight(1f),
                            contentPadding = PaddingValues(bottom = 80.dp),
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            items(products, key = { it.id }) { product ->
                                val isSelected = selectedLowStockGuids.contains(product.guid)
                                ProductItemCard(
                                    product = product,
                                    stockOverride = stocksInWarehouse[product.guid],
                                    isSelectable = isLowStockCategory,
                                    isSelected = isSelected,
                                    onToggleSelect = { viewModel.toggleSelectLowStock(product.guid) },
                                    onEditClick = { viewModel.openEditProductDialog(product) },
                                    onQuickStockClick = { viewModel.openQuickStockDialog(product) },
                                    onDeleteClick = { productToDelete = product }
                                )
                            }
                        }
                    }
                } else {
                    // Dastlabki Holat: 2 Ustunli Kategoriya Card lari (Grid Layout)
                    Text(
                        text = "KATEGORIYALAR (${categoryItems.size})",
                        style = MaterialTheme.typography.labelLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary
                    )

                    Spacer(modifier = Modifier.height(10.dp))

                    LazyVerticalGrid(
                        columns = GridCells.Fixed(2),
                        modifier = Modifier.weight(1f),
                        contentPadding = PaddingValues(bottom = 80.dp),
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        items(categoryItems, key = { it.name }) { categoryItem ->
                            CategoryGridCard(
                                categoryItem = categoryItem,
                                onClick = { viewModel.enterCategory(categoryItem.name) }
                            )
                        }
                    }
                }
            }
        }

        // Kamera orqali shtrix-kod o'qish dialogi
        if (isCameraScannerOpen) {
            CameraScannerDialog(
                onDismissRequest = { isCameraScannerOpen = false },
                onBarcodeScanned = { barcode ->
                    isCameraScannerOpen = false
                    viewModel.onSearchQueryChanged(barcode)
                }
            )
        }

        // O'chirishni tasdiqlash modali (Apple Style Confirmation Dialog)
        productToDelete?.let { product ->
            DeleteProductConfirmationDialog(
                product = product,
                onConfirm = {
                    viewModel.deleteProduct(product)
                    productToDelete = null
                },
                onDismiss = { productToDelete = null }
            )
        }

        // Baza Zaxira (Backup) va Qayta Tiklash (Restore) modali
        if (isBackupDialogOpen) {
            DatabaseBackupDialog(
                products = products,
                appDatabase = viewModel.appDatabase,
                productDao = viewModel.productDao,
                productStockDao = viewModel.productStockDao,
                onDismissRequest = { isBackupDialogOpen = false }
            )
        }

        // Yangi ombor qo'shish modali
        if (isAddWarehouseOpen) {
            AddWarehouseDialog(
                onDismiss = { viewModel.closeAddWarehouseDialog() },
                onSave = { name, isPrimary -> viewModel.saveWarehouse(name, isPrimary) }
            )
        }

        // Omborlararo tovar ko'chirish modali
        if (isTransferOpen) {
            TransferStockDialog(
                warehouses = warehousesWithStats.map { it.warehouse },
                products = products,
                onDismiss = { viewModel.closeTransferDialog() },
                onTransfer = { pGuid, fromWh, toWh, qty ->
                    viewModel.transferStock(pGuid, fromWh, toWh, qty)
                }
            )
        }

        // Kam qolgan tovarlar buyurtma ro'yxati (Reorder BottomSheet)
        if (isReorderSheetOpen) {
            ReorderListBottomSheet(
                items = selectedLowStockProducts,
                onDismissRequest = { isReorderSheetOpen = false },
                onRemoveItem = { guid -> viewModel.removeSelectedLowStock(guid) },
                onClearAll = { viewModel.clearSelectedLowStock() }
            )
        }

        // Tezkor qoldiq oshirish (Kirim) modali
        quickStockProduct?.let { product ->
            val whName = selectedWarehouse?.name ?: "Asosiy ombor"
            val currentStock = stocksInWarehouse[product.guid] ?: product.stockQuantity
            QuickStockAddDialog(
                product = product,
                warehouseName = whName,
                currentStock = currentStock,
                quantityInput = quickStockQuantityInput,
                errorMessage = quickStockErrorMessage,
                onQuantityChange = { viewModel.onQuickStockQuantityChanged(it) },
                onDismiss = { viewModel.closeQuickStockDialog() },
                onConfirm = { viewModel.confirmQuickStockAdd() }
            )
        }
    }
}


/**
 * 2 Ustunli Kategoriya Card (Apple HIG Squircle Card)
 */
@Composable
private fun CategoryGridCard(
    categoryItem: CategoryItem,
    onClick: () -> Unit
) {
    val isAll = categoryItem.name == "Barchasi"
    val isLowStock = categoryItem.name == CATEGORY_LOW_STOCK
    val hasLowStock = isLowStock && categoryItem.productCount > 0

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .height(125.dp)
            .clickable { onClick() },
        shape = RoundedCornerShape(24.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = if (hasLowStock) androidx.compose.foundation.BorderStroke(
            1.5.dp,
            MaterialTheme.colorScheme.error.copy(alpha = 0.5f)
        ) else null,
        colors = CardDefaults.cardColors(
            containerColor = if (isAll) MaterialTheme.colorScheme.primary
            else MaterialTheme.colorScheme.surface
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(14.dp),
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    shape = CircleShape,
                    color = when {
                        isAll -> Color.White.copy(alpha = 0.2f)
                        hasLowStock -> MaterialTheme.colorScheme.error.copy(alpha = 0.12f)
                        else -> MaterialTheme.colorScheme.primaryContainer
                    },
                    modifier = Modifier.size(36.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = when {
                                isAll -> Icons.Default.Inventory2
                                isLowStock -> Icons.Default.Warning
                                else -> Icons.Default.Folder
                            },
                            contentDescription = null,
                            tint = when {
                                isAll -> Color.White
                                hasLowStock -> MaterialTheme.colorScheme.error
                                else -> MaterialTheme.colorScheme.primary
                            },
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }

                Icon(
                    imageVector = Icons.Default.ChevronRight,
                    contentDescription = null,
                    tint = if (isAll) Color.White.copy(alpha = 0.7f) else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f),
                    modifier = Modifier.size(18.dp)
                )
            }

            Column {
                Text(
                    text = categoryItem.name,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = when {
                        isAll -> Color.White
                        hasLowStock -> MaterialTheme.colorScheme.error
                        else -> MaterialTheme.colorScheme.onSurface
                    },
                    maxLines = 1
                )
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = if (isLowStock && hasLowStock) "${categoryItem.productCount} ta kam qolgan" else "${categoryItem.productCount} ta mahsulot",
                    style = MaterialTheme.typography.bodySmall,
                    fontWeight = if (hasLowStock) FontWeight.SemiBold else FontWeight.Normal,
                    color = when {
                        isAll -> Color.White.copy(alpha = 0.8f)
                        hasLowStock -> MaterialTheme.colorScheme.error
                        else -> MaterialTheme.colorScheme.onSurfaceVariant
                    }
                )
            }
        }
    }
}

@Composable
fun ProductItemCard(
    product: ProductEntity,
    stockOverride: Double? = null,
    isSelectable: Boolean = false,
    isSelected: Boolean = false,
    onToggleSelect: (() -> Unit)? = null,
    onEditClick: () -> Unit,
    onQuickStockClick: (() -> Unit)? = null,
    onDeleteClick: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val currentStock = stockOverride ?: product.stockQuantity
    val isLowStock = currentStock <= product.minStockAlert

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable {
                if (isSelectable && onToggleSelect != null) {
                    onToggleSelect()
                } else {
                    onEditClick()
                }
            },
        shape = RoundedCornerShape(20.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = when {
            isSelected -> androidx.compose.foundation.BorderStroke(2.dp, MaterialTheme.colorScheme.primary)
            isLowStock -> androidx.compose.foundation.BorderStroke(1.dp, MaterialTheme.colorScheme.error.copy(alpha = 0.45f))
            else -> null
        },
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.25f)
            else MaterialTheme.colorScheme.surface
        )
    ) {

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            if (isSelectable) {
                Surface(
                    onClick = { onToggleSelect?.invoke() },
                    shape = CircleShape,
                    color = if (isSelected) MaterialTheme.colorScheme.primary else Color.Transparent,
                    border = androidx.compose.foundation.BorderStroke(
                        1.5.dp,
                        if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline.copy(alpha = 0.6f)
                    ),
                    modifier = Modifier.size(24.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        if (isSelected) {
                            Icon(
                                imageVector = Icons.Default.Check,
                                contentDescription = "Tanlangan",
                                tint = Color.White,
                                modifier = Modifier.size(16.dp)
                            )
                        }
                    }
                }
                Spacer(modifier = Modifier.width(12.dp))
            }

            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = product.name,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    if (isLowStock) {
                        Spacer(modifier = Modifier.width(6.dp))
                        Icon(
                            imageVector = Icons.Default.Warning,
                            contentDescription = "Kam qolgan",
                            tint = MaterialTheme.colorScheme.error,
                            modifier = Modifier.size(16.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(2.dp))

                val costPriceText = if (product.costCurrency == "USD") "$${product.costPrice}" else "${numberFormat.format(product.costPrice)} so'm"
                Text(
                    text = "Sotish: ${numberFormat.format(product.sellingPrice)} so'm  |  Tan: $costPriceText",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                if (product.barcode != null) {
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = "Barkod: ${product.barcode}",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.primary.copy(alpha = 0.8f)
                    )
                }

                if (!product.note.isNullOrBlank()) {
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = "📝 ${product.note}",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.85f),
                        maxLines = 1,
                        overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                }
            }

            Spacer(modifier = Modifier.width(8.dp))

            Column(
                horizontalAlignment = Alignment.End,
                verticalArrangement = Arrangement.Center
            ) {
                val unitLabel = when (product.unitType) {
                    UnitType.METR -> "m"
                    UnitType.KG -> "kg"
                    UnitType.DONA -> "ta"
                }
                val stockText = if (currentStock % 1.0 == 0.0) currentStock.toLong().toString() else currentStock.toString()

                val isNegativeStock = currentStock < 0

                Surface(
                    onClick = { onQuickStockClick?.invoke() },
                    shape = CircleShape,
                    color = when {
                        isNegativeStock -> MaterialTheme.colorScheme.error.copy(alpha = 0.22f)
                        isLowStock -> MaterialTheme.colorScheme.error.copy(alpha = 0.12f)
                        else -> MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
                    },
                    modifier = Modifier.padding(bottom = 6.dp)
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
                    ) {
                        Text(
                            text = if (isNegativeStock) "⚠️ $stockText $unitLabel" else "$stockText $unitLabel",
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = if (isNegativeStock || isLowStock) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                        )
                        Spacer(modifier = Modifier.width(3.dp))
                        Text(
                            text = "➕",
                            fontSize = 9.sp,
                            color = if (isNegativeStock || isLowStock) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                        )
                    }
                }

                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Surface(
                        onClick = onEditClick,
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.primary.copy(alpha = 0.12f),
                        modifier = Modifier.size(34.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Edit,
                                contentDescription = "Tahrirlash",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(16.dp)
                            )
                        }
                    }

                    Surface(
                        onClick = onDeleteClick,
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.error.copy(alpha = 0.12f),
                        modifier = Modifier.size(34.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Delete,
                                contentDescription = "O'chirish",
                                tint = MaterialTheme.colorScheme.error,
                                modifier = Modifier.size(16.dp)
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun DeleteProductConfirmationDialog(
    product: ProductEntity,
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
                    modifier = Modifier.size(54.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = Icons.Default.Delete,
                            contentDescription = "O'chirish",
                            tint = MaterialTheme.colorScheme.error,
                            modifier = Modifier.size(28.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                Text(
                    text = "Mahsulotni o'chirish",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = "\"${product.name}\" mahsulotini ombordan o'chirishni xohlaysizmi?",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )

                Spacer(modifier = Modifier.height(20.dp))

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
                        Text("Ha, o'chirish", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}

/**
 * Ombor Card (Apple Rounded Card - Warehouse with Stats & Actions)
 */
@Composable
private fun WarehouseCard(
    stats: WarehouseWithStats,
    onSelect: () -> Unit,
    onSetPrimary: () -> Unit,
    onDelete: () -> Unit
) {
    val numberFormat = remember { NumberFormat.getNumberInstance(Locale.US) }
    val isPrimary = stats.warehouse.isPrimary

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onSelect() },
        shape = RoundedCornerShape(22.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = if (isPrimary) androidx.compose.foundation.BorderStroke(1.5.dp, MaterialTheme.colorScheme.primary) else null,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Sarlavha qismi
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.weight(1f)) {
                    Surface(
                        shape = CircleShape,
                        color = if (isPrimary) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.6f),
                        modifier = Modifier.size(40.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.Inventory2,
                                contentDescription = null,
                                tint = if (isPrimary) Color.White else MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }

                    Spacer(modifier = Modifier.width(10.dp))

                    Column {
                        Text(
                            text = stats.warehouse.name,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        if (isPrimary) {
                            Text(
                                text = "⭐ Asosiy ombor (sotuv uchun)",
                                fontSize = 11.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = MaterialTheme.colorScheme.primary
                            )
                        } else {
                            Text(
                                text = "Qo'shimcha ombor",
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                if (!isPrimary) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        TextButton(
                            onClick = onSetPrimary,
                            contentPadding = PaddingValues(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Text("⭐ Asosiy qilish", fontSize = 11.sp, fontWeight = FontWeight.Bold)
                        }
                        IconButton(onClick = onDelete, modifier = Modifier.size(32.dp)) {
                            Icon(
                                imageVector = Icons.Default.Delete,
                                contentDescription = "O'chirish",
                                tint = MaterialTheme.colorScheme.error,
                                modifier = Modifier.size(18.dp)
                            )
                        }
                    }
                }
            }

            // Statistika 3 ta ko'rsatkich
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f), RoundedCornerShape(14.dp))
                    .padding(10.dp),
                horizontalArrangement = Arrangement.SpaceAround
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("Tovar turlari", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text("${stats.productCount} ta", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
                }
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("Jami qoldiq", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    val qtyStr = if (stats.totalQuantity % 1.0 == 0.0) stats.totalQuantity.toLong().toString() else stats.totalQuantity.toString()
                    Text("$qtyStr ta", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary)
                }
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("Jami qiymati", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text("${numberFormat.format(stats.totalStockValue)} so'm", fontSize = 12.sp, fontWeight = FontWeight.Bold, color = Color(0xFF15803D))
                }
            }

            // Kirish tugmasi
            Button(
                onClick = onSelect,
                modifier = Modifier.fillMaxWidth(),
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = if (isPrimary) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.secondary
                )
            ) {
                Text("${stats.warehouse.name}ga kirish", fontWeight = FontWeight.Bold, fontSize = 13.sp)
                Spacer(modifier = Modifier.width(6.dp))
                Icon(Icons.Default.ChevronRight, contentDescription = null, modifier = Modifier.size(16.dp))
            }
        }
    }
}

/**
 * Yangi Ombor Yaratish Dialogi
 */
@Composable
fun AddWarehouseDialog(
    onDismiss: () -> Unit,
    onSave: (name: String, isPrimary: Boolean) -> Unit
) {
    var name by remember { mutableStateOf("") }
    var isPrimary by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }

    Dialog(onDismissRequest = onDismiss) {
        Card(
            shape = RoundedCornerShape(24.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier.padding(20.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                Text(
                    text = "Yangi Ombor Qo'shish",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )

                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it; error = null },
                    label = { Text("Ombor nomi *") },
                    placeholder = { Text("Masalan: Uydagi ombor, Filial 2") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(14.dp),
                    singleLine = true
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text("Asosiy ombor qilish", fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                        Text("Savdolar birinchi navbatda shu ombordan ayriladi", fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Switch(checked = isPrimary, onCheckedChange = { isPrimary = it })
                }

                if (error != null) {
                    Text(error!!, color = MaterialTheme.colorScheme.error, fontSize = 12.sp)
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(onClick = onDismiss, modifier = Modifier.weight(1f), shape = CircleShape) {
                        Text("Bekor qilish")
                    }
                    Button(
                        onClick = {
                            if (name.trim().isBlank()) {
                                error = "Ombor nomini kiriting!"
                            } else {
                                onSave(name.trim(), isPrimary)
                            }
                        },
                        modifier = Modifier.weight(1f),
                        shape = CircleShape
                    ) {
                        Text("Saqlash", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}

/**
 * Omborlararo tovar ko'chirish (Transfer) dialogi
 */
@Composable
fun TransferStockDialog(
    warehouses: List<WarehouseEntity>,
    products: List<ProductEntity>,
    onDismiss: () -> Unit,
    onTransfer: (productGuid: String, fromWh: String, toWh: String, quantity: Double) -> Unit
) {
    if (warehouses.size < 2) {
        Dialog(onDismissRequest = onDismiss) {
            Card(
                shape = RoundedCornerShape(20.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                modifier = Modifier.padding(16.dp)
            ) {
                Column(modifier = Modifier.padding(20.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("Ko'chirish uchun kamida 2 ta ombor kerak!", fontWeight = FontWeight.Bold, textAlign = androidx.compose.ui.text.style.TextAlign.Center)
                    Spacer(modifier = Modifier.height(14.dp))
                    Button(onClick = onDismiss, shape = CircleShape) { Text("Tushundim") }
                }
            }
        }
        return
    }

    var fromWh by remember { mutableStateOf(warehouses.first().guid) }
    var toWh by remember { mutableStateOf(warehouses.getOrNull(1)?.guid ?: warehouses.first().guid) }
    var selectedProductGuid by remember { mutableStateOf(products.firstOrNull()?.guid ?: "") }
    var quantityText by remember { mutableStateOf("") }
    var errorMsg by remember { mutableStateOf<String?>(null) }

    var fromExpanded by remember { mutableStateOf(false) }
    var toExpanded by remember { mutableStateOf(false) }
    var prodExpanded by remember { mutableStateOf(false) }

    val fromWhName = warehouses.find { it.guid == fromWh }?.name ?: ""
    val toWhName = warehouses.find { it.guid == toWh }?.name ?: ""
    val selectedProd = products.find { it.guid == selectedProductGuid }

    Dialog(onDismissRequest = onDismiss) {
        Card(
            shape = RoundedCornerShape(24.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier
                    .padding(20.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = "Omborlararo tovar ko'chirish",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )

                // Chiqish ombori
                Column {
                    Text("Qaysi ombordan:", fontSize = 12.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(modifier = Modifier.height(4.dp))
                    Box {
                        OutlinedButton(
                            onClick = { fromExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Text(fromWhName.ifBlank { "Omborni tanlang" }, modifier = Modifier.weight(1f))
                            Icon(Icons.Default.ChevronRight, contentDescription = null)
                        }
                        DropdownMenu(expanded = fromExpanded, onDismissRequest = { fromExpanded = false }) {
                            warehouses.forEach { wh ->
                                DropdownMenuItem(
                                    text = { Text(wh.name) },
                                    onClick = { fromWh = wh.guid; fromExpanded = false }
                                )
                            }
                        }
                    }
                }

                // Qabul qiluvchi ombor
                Column {
                    Text("Qaysi omborga:", fontSize = 12.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(modifier = Modifier.height(4.dp))
                    Box {
                        OutlinedButton(
                            onClick = { toExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Text(toWhName.ifBlank { "Omborni tanlang" }, modifier = Modifier.weight(1f))
                            Icon(Icons.Default.ChevronRight, contentDescription = null)
                        }
                        DropdownMenu(expanded = toExpanded, onDismissRequest = { toExpanded = false }) {
                            warehouses.forEach { wh ->
                                DropdownMenuItem(
                                    text = { Text(wh.name) },
                                    onClick = { toWh = wh.guid; toExpanded = false }
                                )
                            }
                        }
                    }
                }

                // Tovar tanlash
                Column {
                    Text("Ko'chiriladigan tovar:", fontSize = 12.sp, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(modifier = Modifier.height(4.dp))
                    Box {
                        OutlinedButton(
                            onClick = { prodExpanded = true },
                            modifier = Modifier.fillMaxWidth(),
                            shape = RoundedCornerShape(12.dp)
                        ) {
                            Text(selectedProd?.name ?: "Tovarni tanlang", modifier = Modifier.weight(1f), maxLines = 1)
                            Icon(Icons.Default.ChevronRight, contentDescription = null)
                        }
                        DropdownMenu(expanded = prodExpanded, onDismissRequest = { prodExpanded = false }) {
                            products.forEach { prod ->
                                val qtyStr = if (prod.stockQuantity % 1.0 == 0.0) prod.stockQuantity.toLong().toString() else prod.stockQuantity.toString()
                                DropdownMenuItem(
                                    text = { Text("${prod.name} ($qtyStr)") },
                                    onClick = { selectedProductGuid = prod.guid; prodExpanded = false }
                                )
                            }
                        }
                    }
                }

                // Miqdor
                OutlinedTextField(
                    value = quantityText,
                    onValueChange = { quantityText = it; errorMsg = null },
                    label = { Text("Ko'chirish miqdori *") },
                    placeholder = { Text("Masalan: 10") },
                    keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = androidx.compose.ui.text.input.KeyboardType.Decimal),
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    singleLine = true
                )

                if (errorMsg != null) {
                    Text(errorMsg!!, color = MaterialTheme.colorScheme.error, fontSize = 12.sp)
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    OutlinedButton(onClick = onDismiss, modifier = Modifier.weight(1f), shape = CircleShape) {
                        Text("Bekor qilish")
                    }
                    Button(
                        onClick = {
                            val qty = quantityText.toDoubleOrNull()
                            if (fromWh == toWh) {
                                errorMsg = "Chiqish va qabul qiluvchi ombor bir xil bo'lmasligi kerak!"
                            } else if (selectedProductGuid.isBlank()) {
                                errorMsg = "Tovarni tanlang!"
                            } else if (qty == null || qty <= 0.0) {
                                errorMsg = "Miqdorni to'g'ri kiriting!"
                            } else {
                                onTransfer(selectedProductGuid, fromWh, toWh, qty)
                            }
                        },
                        modifier = Modifier.weight(1f),
                        shape = CircleShape
                    ) {
                        Text("Ko'chirish", fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }
}
