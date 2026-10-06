package uz.pos.electro

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.isImeVisible
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.Inventory
import androidx.compose.material.icons.filled.PointOfSale
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import androidx.hilt.navigation.compose.hiltViewModel
import uz.pos.electro.data.licensing.DeviceLicensingManager
import uz.pos.electro.data.sync.LiveSyncStatus
import uz.pos.electro.scanner.HardwareScannerManager
import uz.pos.electro.ui.cashier.CashierScreen
import uz.pos.electro.ui.cashier.CashierViewModel
import uz.pos.electro.ui.licensing.ActivationScreen
import uz.pos.electro.ui.products.AddEditProductDialog
import uz.pos.electro.ui.products.ProductViewModel
import uz.pos.electro.ui.products.ProductsScreen
import uz.pos.electro.ui.reports.ReportsScreen
import uz.pos.electro.ui.reports.ReportsViewModel
import uz.pos.electro.ui.settings.SettingsScreen

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    scannerManager: HardwareScannerManager? = null,
    licensingManager: DeviceLicensingManager,
    syncManager: uz.pos.electro.data.sync.LocalSyncManager,
    taxSettingsRepository: uz.pos.electro.data.repository.TaxSettingsRepository,
    cashierViewModel: CashierViewModel = hiltViewModel(),
    productViewModel: ProductViewModel = hiltViewModel(),
    reportsViewModel: ReportsViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val isActivated by licensingManager.isActivated.collectAsState()

    var selectedTab by remember { mutableIntStateOf(0) }
    val isAddEditOpen by productViewModel.isAddEditOpen.collectAsState()
    val haptic = LocalHapticFeedback.current

    // Wi-Fi lokal sinxronizatsiya dialogi holati
    var isWifiSyncOpen by remember { mutableStateOf(false) }

    // 1. Agar qurilma hali faollashtirilmagan bo'lsa -> Faqat Aktivatsiya ekrani ko'rsatiladi
    if (!isActivated) {
        ActivationScreen(
            licensingManager = licensingManager,
            onActivated = {}
        )
        return
    }

    // BackHandler: Agar foydalanuvchi boshqa tabda bo'lsa, Back bosganda Kassaga qaytadi
    BackHandler(enabled = selectedTab != 0 && !isAddEditOpen) {
        selectedTab = 0
    }

    // 2. Agar qurilma faollashtirilgan bo'lsa -> To'liq SMART POS ilovasi ishlaydi
    Scaffold(
        contentWindowInsets = WindowInsets(0, 0, 0, 0),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            Surface(
                modifier = Modifier
                    .shadow(elevation = 6.dp, shape = RoundedCornerShape(bottomStart = 24.dp, bottomEnd = 24.dp))
                    .clip(RoundedCornerShape(bottomStart = 24.dp, bottomEnd = 24.dp)),
                color = MaterialTheme.colorScheme.surface
            ) {
                TopAppBar(
                    title = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            // Logo
                            Box(
                                modifier = Modifier
                                    .size(38.dp)
                                    .clip(CircleShape),
                                contentAlignment = Alignment.Center
                            ) {
                                Image(
                                    painter = painterResource(id = R.drawable.app_logo),
                                    contentDescription = "Line Logo",
                                    modifier = Modifier
                                        .size(34.dp)
                                        .clip(CircleShape)
                                )
                            }

                            Spacer(modifier = Modifier.width(12.dp))

                            Text(
                                text = when (selectedTab) {
                                    0 -> "SMART — Kassa"
                                    1 -> "SMART — Ombor"
                                    2 -> "SMART — Hisobotlar"
                                    else -> "SMART — Sozlamalar"
                                },
                                fontWeight = FontWeight.Bold,
                                fontSize = 20.sp,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                    },
                    actions = {
                        // Jonli Wi-Fi sinxron holati ko'rsatkichi (Pill)
                        val liveSyncStatus by syncManager.liveSyncStatus.collectAsState()
                        Surface(
                            onClick = { isWifiSyncOpen = true },
                            shape = CircleShape,
                            color = when (liveSyncStatus) {
                                LiveSyncStatus.CONNECTED -> Color(0xFF16A34A).copy(alpha = 0.15f)
                                LiveSyncStatus.CONFLICT, LiveSyncStatus.CONNECTING -> Color(0xFFF59E0B).copy(alpha = 0.15f)
                                LiveSyncStatus.OFFLINE -> MaterialTheme.colorScheme.surfaceVariant
                            },
                            modifier = Modifier.padding(end = 8.dp)
                        ) {
                            Row(
                                modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(8.dp)
                                        .clip(CircleShape)
                                        .background(
                                            when (liveSyncStatus) {
                                                LiveSyncStatus.CONNECTED -> Color(0xFF16A34A)
                                                LiveSyncStatus.CONFLICT, LiveSyncStatus.CONNECTING -> Color(0xFFF59E0B)
                                                LiveSyncStatus.OFFLINE -> Color.Gray
                                            }
                                        )
                                    )
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(
                                    text = when (liveSyncStatus) {
                                        LiveSyncStatus.CONNECTED -> "Jonli"
                                        LiveSyncStatus.CONNECTING -> "Ulanmoqda"
                                        LiveSyncStatus.CONFLICT -> "Tahrirni tanlang"
                                        LiveSyncStatus.OFFLINE -> "Oflayn"
                                    },
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = when (liveSyncStatus) {
                                        LiveSyncStatus.CONNECTED -> Color(0xFF16A34A)
                                        LiveSyncStatus.CONFLICT, LiveSyncStatus.CONNECTING -> Color(0xFFF59E0B)
                                        LiveSyncStatus.OFFLINE -> MaterialTheme.colorScheme.onSurfaceVariant
                                    }
                                )
                            }
                        }
                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = Color.Transparent,
                        titleContentColor = MaterialTheme.colorScheme.primary
                    )
                )
            }
        },
        bottomBar = {
            if (!isAddEditOpen) {
                // Tezkor, Kechikishsiz va Hover-Free Apple Style Bottom Dock (4 ta tab)
                Surface(
                    modifier = Modifier
                        .shadow(elevation = 10.dp, shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))
                        .clip(RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp)),
                    color = MaterialTheme.colorScheme.surface
                ) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .navigationBarsPadding()
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(64.dp)
                                .padding(horizontal = 16.dp),
                            horizontalArrangement = Arrangement.SpaceAround,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                        AppleDockTabItem(
                            icon = Icons.Default.PointOfSale,
                            isSelected = selectedTab == 0,
                            onClick = {
                                if (selectedTab != 0) {
                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                    selectedTab = 0
                                }
                            }
                        )

                        AppleDockTabItem(
                            icon = Icons.Default.Inventory,
                            isSelected = selectedTab == 1,
                            onClick = {
                                if (selectedTab != 1) {
                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                    selectedTab = 1
                                }
                            }
                        )

                        AppleDockTabItem(
                            icon = Icons.Default.Assessment,
                            isSelected = selectedTab == 2,
                            onClick = {
                                if (selectedTab != 2) {
                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                    selectedTab = 2
                                }
                            }
                        )

                        AppleDockTabItem(
                            icon = Icons.Default.Settings,
                            isSelected = selectedTab == 3,
                            onClick = {
                                if (selectedTab != 3) {
                                    haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                                    selectedTab = 3
                                }
                            }
                        )
                    }
                    }
                }
            }
        }
    ) { paddingValues ->

        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            // Tezkor va 0ms kechikishsiz bir lahzali almashuv
            when (selectedTab) {
                0 -> CashierScreen(
                    viewModel = cashierViewModel,
                    scannerManager = scannerManager,
                    onAddUnknownProduct = { barcode ->
                        productViewModel.openAddProductDialog(barcode)
                    }
                )
                1 -> ProductsScreen(
                    viewModel = productViewModel
                )
                2 -> ReportsScreen(
                    viewModel = reportsViewModel
                )
                3 -> {
                    val productList by productViewModel.products.collectAsState()
                    SettingsScreen(
                        taxSettingsRepository = taxSettingsRepository,
                        licensingManager = licensingManager,
                        syncManager = syncManager,
                        appDatabase = productViewModel.appDatabase,
                        products = productList
                    )
                }
            }
        }

        // Global Mahsulot qo'shish / tahrirlash sahifasi (to'liq ekran)
        if (isAddEditOpen) {
            BackHandler {
                productViewModel.closeDialog()
            }
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .zIndex(100f)
            ) {
                AddEditProductDialog(
                    viewModel = productViewModel,
                    onDismissRequest = { productViewModel.closeDialog() }
                )
            }
        }

        // Wi-Fi Lokal Sinxronizatsiya Dialogi (Navbar pill orqali ochilganda)
        if (isWifiSyncOpen) {
            uz.pos.electro.ui.sync.WifiSyncDialog(
                syncManager = syncManager,
                onDismiss = { isWifiSyncOpen = false }
            )
        }
    }
}

@Composable
private fun AppleDockTabItem(
    icon: ImageVector,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    val interactionSource = remember { MutableInteractionSource() }

    Box(
        modifier = Modifier
            .size(54.dp)
            .clip(CircleShape)
            .clickable(
                interactionSource = interactionSource,
                indication = null,
                onClick = onClick
            ),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = if (isSelected) MaterialTheme.colorScheme.primary
            else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.35f),
            modifier = Modifier.size(28.dp)
        )
    }
}
