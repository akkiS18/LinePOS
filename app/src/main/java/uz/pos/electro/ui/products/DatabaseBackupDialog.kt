package uz.pos.electro.ui.products

import android.content.Context
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Code
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.FileOpen
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Send
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import uz.pos.electro.data.backup.BackupPreferences
import uz.pos.electro.data.backup.DatabaseAutoBackupWorker
import uz.pos.electro.data.backup.TelegramBackupService
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary
import uz.pos.electro.util.DatabaseBackupExporter

@Composable
fun DatabaseBackupDialog(
    products: List<ProductEntity>,
    appDatabase: AppDatabase? = null,
    productDao: ProductDao? = null,
    productStockDao: ProductStockDao? = null,
    onDismissRequest: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var selectedTab by remember { mutableIntStateOf(0) } // 0: Eksport (Zaxira), 1: Tiklash (Import)
    var isLoading by remember { mutableStateOf(false) }
    var loadingMessage by remember { mutableStateOf("Jarayon bajarilmoqda...") }

    // Tiklash tasdiqlash modali
    var pendingRestoreUri by remember { mutableStateOf<Uri?>(null) }
    var showRestoreConfirmDialog by remember { mutableStateOf(false) }

    // Kompyuterdan tiklash modali
    var showDesktopRestoreDialog by remember { mutableStateOf(false) }
    val sharedPrefs = remember { context.getSharedPreferences("pos_local_sync_prefs", Context.MODE_PRIVATE) }
    var desktopUrlInput by remember {
        mutableStateOf(sharedPrefs.getString("local_desktop_url", "http://192.168.1.100:8080") ?: "http://192.168.1.100:8080")
    }

    // Telegram Avto-Zaxira holatlari
    var telegramChatId by remember { mutableStateOf(BackupPreferences.getChatId(context)) }
    var isAutoBackupEnabled by remember { mutableStateOf(BackupPreferences.isAutoBackupEnabled(context)) }
    var customBotToken by remember { mutableStateOf(BackupPreferences.getCustomBotToken(context)) }
    var showCustomTokenField by remember { mutableStateOf(customBotToken.isNotBlank()) }
    var lastBackupStatus by remember { mutableStateOf(BackupPreferences.getLastBackupStatus(context)) }
    var lastBackupTime by remember { mutableStateOf(BackupPreferences.getLastBackupTime(context)) }
    var lastBackupFile by remember { mutableStateOf(BackupPreferences.getLastBackupFile(context)) }

    // Tizim fayl tanlagichi (.db tiklash uchun)
    val dbPickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        if (uri != null) {
            pendingRestoreUri = uri
            showRestoreConfirmDialog = true
        }
    }

    // JSON tanlagichi (tovarlar importi uchun)
    val jsonPickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        if (uri != null && productDao != null && productStockDao != null) {
            scope.launch {
                isLoading = true
                loadingMessage = "JSON fayldagi tovarlar yuklanmoqda..."
                withContext(Dispatchers.IO) {
                    DatabaseBackupExporter.importInventoryJson(
                        context = context,
                        jsonUri = uri,
                        productDao = productDao,
                        productStockDao = productStockDao
                    )
                }.onSuccess { count ->
                    isLoading = false
                    Toast.makeText(context, "$count ta tovar muvaffaqiyatli import qilindi!", Toast.LENGTH_LONG).show()
                    onDismissRequest()
                }.onFailure { err ->
                    isLoading = false
                    Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    Dialog(
        onDismissRequest = { if (!isLoading) onDismissRequest() },
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth(0.95f)
                .padding(vertical = 16.dp),
            shape = RoundedCornerShape(28.dp),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Sarlavha va [ X ] Yopish
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = CircleShape,
                            color = MaterialTheme.colorScheme.primaryContainer,
                            modifier = Modifier.size(42.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.Storage,
                                    contentDescription = "Baza markazi",
                                    tint = MaterialTheme.colorScheme.primary,
                                    modifier = Modifier.size(24.dp)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Baza va Xavfsizlik Markazi",
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.primary
                            )
                            Text(
                                text = "Eksport, zaxiralash va qayta tiklash",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    IconButton(
                        onClick = onDismissRequest,
                        enabled = !isLoading,
                        modifier = Modifier.size(32.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = "Yopish",
                            tint = MaterialTheme.colorScheme.outline
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Tablar: 📤 Zaxira olish | 📥 Qayta tiklash | 🤖 Telegram
                TabRow(
                    selectedTabIndex = selectedTab,
                    containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                    contentColor = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.clip(RoundedCornerShape(14.dp))
                ) {
                    Tab(
                        selected = selectedTab == 0,
                        onClick = { selectedTab = 0 },
                        text = { Text("📤 Zaxira", fontWeight = FontWeight.Bold, fontSize = 12.sp) }
                    )
                    Tab(
                        selected = selectedTab == 1,
                        onClick = { selectedTab = 1 },
                        text = { Text("📥 Tiklash", fontWeight = FontWeight.Bold, fontSize = 12.sp) }
                    )
                    Tab(
                        selected = selectedTab == 2,
                        onClick = { selectedTab = 2 },
                        text = { Text("🤖 Telegram", fontWeight = FontWeight.Bold, fontSize = 12.sp) }
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                if (isLoading) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(200.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                            Spacer(modifier = Modifier.height(14.dp))
                            Text(
                                text = loadingMessage,
                                style = MaterialTheme.typography.bodyMedium,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }
                } else {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .verticalScroll(rememberScrollState()),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        if (selectedTab == 0) {
                            // ==================== 📤 ZAXIRA OLISH (EKSPORT) ====================
                            // 1. To'liq SQLite Baza Fayli (.db) - WAL checkpoint bilan
                            BackupOptionCard(
                                title = "To'liq Baza Nusxasi (.db)",
                                description = "Barcha tovarlar, omborlar va savdo tarixi (WAL Checkpoint xavfsizligi)",
                                icon = Icons.Default.Storage,
                                iconColor = LinePrimary,
                                badgeText = "Xavfsiz",
                                onClick = {
                                    scope.launch {
                                        isLoading = true
                                        loadingMessage = "Baza tayyorlanmoqda (WAL checkpoint)..."
                                        withContext(Dispatchers.IO) {
                                            DatabaseBackupExporter.backupDatabaseFile(context, appDatabase)
                                        }.onSuccess { file ->
                                            isLoading = false
                                            DatabaseBackupExporter.shareBackupFile(
                                                context = context,
                                                file = file,
                                                mimeType = "application/octet-stream",
                                                chooserTitle = "Line POS Baza (.db) faylini yuborish (Telegram)"
                                            )
                                        }.onFailure { error ->
                                            isLoading = false
                                            Toast.makeText(context, "Xatolik: ${error.localizedMessage}", Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                }
                            )

                            // 2. Excel Jadvali (.xls)
                            BackupOptionCard(
                                title = "Excel Jadvali (.xls)",
                                description = "Ombordagi tovarlar, tan va sotish narxlari, qoldiqlar hisoboti",
                                icon = Icons.Default.Description,
                                iconColor = LineSecondary,
                                onClick = {
                                    scope.launch {
                                        isLoading = true
                                        loadingMessage = "Excel jadvali tayyorlanmoqda..."
                                        withContext(Dispatchers.IO) {
                                            DatabaseBackupExporter.exportInventoryExcel(context, products)
                                        }.onSuccess { file ->
                                            isLoading = false
                                            DatabaseBackupExporter.shareBackupFile(
                                                context = context,
                                                file = file,
                                                mimeType = "application/vnd.ms-excel",
                                                chooserTitle = "Ombor Excel jadvalini yuborish"
                                            )
                                        }.onFailure { error ->
                                            isLoading = false
                                            Toast.makeText(context, "Xatolik: ${error.localizedMessage}", Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                }
                            )

                            // 3. JSON Nusxasi (.json)
                            BackupOptionCard(
                                title = "JSON Nusxasi (.json)",
                                description = "Tovarlarning universal strukturasi (boshqa tizimlar uchun)",
                                icon = Icons.Default.Code,
                                iconColor = Color(0xFFE65100),
                                onClick = {
                                    scope.launch {
                                        isLoading = true
                                        loadingMessage = "JSON eksport qilinmoqda..."
                                        withContext(Dispatchers.IO) {
                                            DatabaseBackupExporter.exportInventoryJson(context, products)
                                        }.onSuccess { file ->
                                            isLoading = false
                                            DatabaseBackupExporter.shareBackupFile(
                                                context = context,
                                                file = file,
                                                mimeType = "application/json",
                                                chooserTitle = "JSON faylini yuborish"
                                            )
                                        }.onFailure { error ->
                                            isLoading = false
                                            Toast.makeText(context, "Xatolik: ${error.localizedMessage}", Toast.LENGTH_SHORT).show()
                                        }
                                    }
                                }
                            )
                        } else if (selectedTab == 1) {
                            // ==================== 📥 QAYTA TIKLASH (IMPORT) ====================
                            // Ma'lumot kartochkasi
                            Surface(
                                shape = RoundedCornerShape(12.dp),
                                color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Row(
                                    modifier = Modifier.padding(10.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Warning,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.primary,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text(
                                        text = "Yangi telefon olinganda bazani Telegramdan yuklab olib fayldan yoki kassa kompyuteridan tiklashingiz mumkin.",
                                        style = MaterialTheme.typography.bodySmall,
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                }
                            }

                            // 1. Zaxira faylidan (.db) tiklash
                            BackupOptionCard(
                                title = "📁 Baza faylidan tiklash (.db)",
                                description = "Telegram / Drive dan olingan .db bazasini to'liq tiklash",
                                icon = Icons.Default.FileOpen,
                                iconColor = LinePrimary,
                                badgeText = "Asosiy",
                                onClick = {
                                    dbPickerLauncher.launch("*/*")
                                }
                            )

                            // 2. Kompyuterdan (Wi-Fi) to'liq bazani tortib olish
                            BackupOptionCard(
                                title = "🌐 Kompyuterdan tiklash (Wi-Fi)",
                                description = "Kassa kompyuteridagi oxirgi to'liq bazani 1 tugma bilan yuklab olish",
                                icon = Icons.Default.CloudDownload,
                                iconColor = LineSecondary,
                                badgeText = "Wi-Fi",
                                onClick = {
                                    showDesktopRestoreDialog = true
                                }
                            )

                            // 3. JSON fayldan tovarlarni tiklash
                            BackupOptionCard(
                                title = "📑 JSON tovarlarni tiklash",
                                description = "Eski tovarlar va qoldiqlarni joriy bazaga qo'shish/birlashtirish",
                                icon = Icons.Default.Code,
                                iconColor = Color(0xFFE65100),
                                onClick = {
                                    jsonPickerLauncher.launch("*/*")
                                }
                            )
                        } else {
                            // ==================== 🤖 TELEGRAM AVTO-ZAXIRA ====================
                            // 1. Holat kartochkasi (Status Card)
                            Card(
                                shape = RoundedCornerShape(16.dp),
                                colors = CardDefaults.cardColors(
                                    containerColor = if (isAutoBackupEnabled && telegramChatId.isNotBlank())
                                        MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.35f)
                                    else
                                        MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                                ),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Column(modifier = Modifier.padding(14.dp)) {
                                    Row(
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        modifier = Modifier.fillMaxWidth()
                                    ) {
                                        Row(
                                            verticalAlignment = Alignment.CenterVertically,
                                            modifier = Modifier.weight(1f)
                                        ) {
                                            Icon(
                                                imageVector = if (isAutoBackupEnabled && telegramChatId.isNotBlank())
                                                    Icons.Default.CheckCircle
                                                else
                                                    Icons.Default.Warning,
                                                contentDescription = null,
                                                tint = if (isAutoBackupEnabled && telegramChatId.isNotBlank())
                                                    Color(0xFF10B981)
                                                else
                                                    Color(0xFFF59E0B),
                                                modifier = Modifier.size(20.dp)
                                            )
                                            Spacer(modifier = Modifier.width(8.dp))
                                            Text(
                                                text = if (isAutoBackupEnabled && telegramChatId.isNotBlank())
                                                    "Avto-zaxira: Faol (23:00 da)"
                                                else
                                                    "Avto-zaxira: O'chiq",
                                                fontWeight = FontWeight.Bold,
                                                fontSize = 13.sp,
                                                color = MaterialTheme.colorScheme.onSurface
                                            )
                                        }

                                        Switch(
                                            checked = isAutoBackupEnabled,
                                            onCheckedChange = { checked ->
                                                if (checked && telegramChatId.isBlank()) {
                                                    Toast.makeText(context, "Avval Telegram Chat ID ni kiriting!", Toast.LENGTH_SHORT).show()
                                                    return@Switch
                                                }
                                                isAutoBackupEnabled = checked
                                                BackupPreferences.setAutoBackupEnabled(context, checked)
                                                if (checked) {
                                                    DatabaseAutoBackupWorker.schedule(context)
                                                    Toast.makeText(context, "Avtomatik zaxira har kuni 23:00 ga yoqildi ✅", Toast.LENGTH_SHORT).show()
                                                } else {
                                                    DatabaseAutoBackupWorker.cancel(context)
                                                    Toast.makeText(context, "Avtomatik zaxira o'chirildi", Toast.LENGTH_SHORT).show()
                                                }
                                            }
                                        )
                                    }

                                    Spacer(modifier = Modifier.height(6.dp))

                                    val lastTimeStr = if (lastBackupTime > 0) {
                                        SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()).format(Date(lastBackupTime))
                                    } else {
                                        "Hali zaxira olinmagan"
                                    }

                                    Text(
                                        text = "🕒 Oxirgi zaxira: $lastTimeStr",
                                        style = MaterialTheme.typography.bodySmall,
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                    if (lastBackupFile.isNotBlank()) {
                                        Text(
                                            text = "📁 Fayl: $lastBackupFile",
                                            style = MaterialTheme.typography.bodySmall,
                                            fontSize = 11.sp,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                    Text(
                                        text = "Holat: $lastBackupStatus",
                                        style = MaterialTheme.typography.bodySmall,
                                        fontSize = 11.sp,
                                        fontWeight = FontWeight.Medium,
                                        color = if (lastBackupStatus.contains("Muvaffaqiyatli")) Color(0xFF10B981) else MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }

                            Spacer(modifier = Modifier.height(4.dp))

                            // 2. Chat ID kiritish maydoni
                            OutlinedTextField(
                                value = telegramChatId,
                                onValueChange = {
                                    telegramChatId = it
                                    BackupPreferences.setChatId(context, it)
                                    if (isAutoBackupEnabled) {
                                        DatabaseAutoBackupWorker.schedule(context)
                                    }
                                },
                                label = { Text("Telegram Chat ID (yoki Kanal ID)") },
                                placeholder = { Text("Masalan: 123456789 yoki -100...") },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp)
                            )

                            // Qo'llanma matni
                            Text(
                                text = "💡 O'z Chat ID raqamingizni bilish uchun Telegramda @userinfobot ga kiring yoki botga /start bosing.",
                                style = MaterialTheme.typography.bodySmall,
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )

                            Spacer(modifier = Modifier.height(4.dp))

                            // 3. Shaxsiy bot tokeni (Accordion / Toggle)
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.SpaceBetween,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { showCustomTokenField = !showCustomTokenField }
                                    .padding(vertical = 4.dp)
                            ) {
                                Text(
                                    text = "⚙️ Shaxsiy Bot Token kiritish (ixtiyoriy)",
                                    style = MaterialTheme.typography.bodySmall,
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Medium,
                                    color = MaterialTheme.colorScheme.primary
                                )
                                Text(
                                    text = if (showCustomTokenField) "▲" else "▼",
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.primary
                                )
                            }

                            if (showCustomTokenField) {
                                OutlinedTextField(
                                    value = customBotToken,
                                    onValueChange = {
                                        customBotToken = it
                                        BackupPreferences.setCustomBotToken(context, it)
                                    },
                                    label = { Text("Shaxsiy Bot Token (@BotFather)") },
                                    placeholder = { Text("123456789:ABCdefGhIjk...") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(12.dp)
                                )
                            }

                            Spacer(modifier = Modifier.height(6.dp))

                            // 4. Tugmalar: Sinov xabari va Darhol zaxiralash
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                OutlinedButton(
                                    onClick = {
                                        if (telegramChatId.isBlank()) {
                                            Toast.makeText(context, "Iltimos, Telegram Chat ID ni kiriting!", Toast.LENGTH_SHORT).show()
                                            return@OutlinedButton
                                        }
                                        scope.launch {
                                            isLoading = true
                                            loadingMessage = "Telegramga sinov xabari yuborilmoqda..."
                                            val token = BackupPreferences.getBotToken(context)
                                            val result = TelegramBackupService.sendTestMessage(
                                                botToken = token,
                                                chatId = telegramChatId,
                                                storeName = "SMART Kassa"
                                            )
                                            isLoading = false
                                            result.onSuccess { msg ->
                                                Toast.makeText(context, msg, Toast.LENGTH_LONG).show()
                                            }.onFailure { err ->
                                                Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                                            }
                                        }
                                    },
                                    modifier = Modifier.weight(1f),
                                    shape = RoundedCornerShape(12.dp)
                                ) {
                                    Icon(Icons.Default.Send, contentDescription = null, modifier = Modifier.size(16.dp))
                                    Spacer(modifier = Modifier.width(6.dp))
                                    Text("Sinov", fontSize = 12.sp)
                                }

                                Button(
                                    onClick = {
                                        if (telegramChatId.isBlank()) {
                                            Toast.makeText(context, "Iltimos, Telegram Chat ID ni kiriting!", Toast.LENGTH_SHORT).show()
                                            return@Button
                                        }
                                        scope.launch {
                                            isLoading = true
                                            loadingMessage = "Baza tayyorlanmoqda va Telegramga yuklanmoqda..."
                                            withContext(Dispatchers.IO) {
                                                val fileResult = DatabaseBackupExporter.backupDatabaseFile(context, appDatabase)
                                                val file = fileResult.getOrThrow()
                                                val token = BackupPreferences.getBotToken(context)
                                                val sendResult = TelegramBackupService.sendDatabaseBackup(
                                                    botToken = token,
                                                    chatId = telegramChatId,
                                                    databaseFile = file,
                                                    storeName = "SMART Kassa",
                                                    extraInfo = "Joriy tovarlar: ${products.size} ta"
                                                )
                                                try { file.delete() } catch (_: Throwable) {}
                                                sendResult
                                            }.onSuccess {
                                                isLoading = false
                                                val now = System.currentTimeMillis()
                                                lastBackupTime = now
                                                lastBackupStatus = "Muvaffaqiyatli saqlandi ✅"
                                                val fileName = "SMART_POS_Baza_${SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date(now))}.db"
                                                lastBackupFile = fileName
                                                BackupPreferences.recordBackupSuccess(context, fileName)
                                                Toast.makeText(context, "Baza Telegramga muvaffaqiyatli yuborildi! 🎉", Toast.LENGTH_LONG).show()
                                            }.onFailure { err ->
                                                isLoading = false
                                                lastBackupStatus = "Xatolik: ${err.localizedMessage} ⚠️"
                                                BackupPreferences.recordBackupFailure(context, err.localizedMessage ?: "Noma'lum xatolik")
                                                Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                                            }
                                        }
                                    },
                                    modifier = Modifier.weight(1.3f),
                                    colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                                    shape = RoundedCornerShape(12.dp)
                                ) {
                                    Icon(Icons.Default.CloudDownload, contentDescription = null, modifier = Modifier.size(16.dp))
                                    Spacer(modifier = Modifier.width(6.dp))
                                    Text("Hozir zaxiralash", fontWeight = FontWeight.Bold, fontSize = 12.sp)
                                }
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                Button(
                    onClick = onDismissRequest,
                    enabled = !isLoading,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(46.dp),
                    shape = CircleShape
                ) {
                    Text("Yopish", fontWeight = FontWeight.Bold)
                }
            }
        }
    }

    // 1. Fayldan tiklashni tasdiqlash modali
    if (showRestoreConfirmDialog && pendingRestoreUri != null) {
        val uri = pendingRestoreUri!!
        AlertDialog(
            onDismissRequest = {
                if (!isLoading) {
                    showRestoreConfirmDialog = false
                    pendingRestoreUri = null
                }
            },
            title = {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.Warning,
                        contentDescription = null,
                        tint = Color(0xFFD97706)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Bazani tiklashni tasdiqlang")
                }
            },
            text = {
                Text(
                    "⚠️ DIQQAT! Tanlangan fayldagi baza amaldagi baza o'rniga to'liq o'rnatiladi.\n\n" +
                    "Hozirgi barcha tovarlar va savdolar tanlangan zaxira bazasi bilan almashadi. Davom etasizmi?"
                )
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                    onClick = {
                        showRestoreConfirmDialog = false
                        scope.launch {
                            isLoading = true
                            loadingMessage = "Baza tekshirilmoqda va tiklanmoqda..."
                            withContext(Dispatchers.IO) {
                                DatabaseBackupExporter.restoreDatabaseFromUri(context, uri, appDatabase)
                            }.onSuccess { successMsg ->
                                isLoading = false
                                Toast.makeText(context, successMsg, Toast.LENGTH_LONG).show()
                                // V2 imports into the existing Room database; no file replacement or restart.
                            }.onFailure { err ->
                                isLoading = false
                                Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                            }
                        }
                    }
                ) {
                    Text("Ha, tiklansin", fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = {
                    showRestoreConfirmDialog = false
                    pendingRestoreUri = null
                }) {
                    Text("Bekor qilish")
                }
            }
        )
    }

    // 2. Kompyuterdan tiklash modali (URL kiritish va yuklab olish)
    if (showDesktopRestoreDialog) {
        AlertDialog(
            onDismissRequest = { if (!isLoading) showDesktopRestoreDialog = false },
            title = {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(imageVector = Icons.Default.Wifi, contentDescription = null, tint = LineSecondary)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Kompyuterdan tiklash")
                }
            },
            text = {
                Column {
                    Text(
                        "Avval Wi-Fi sinxron oynasida kompyuter QR kodini skanerlang. So'ng uning IP manzilini kiriting. Mavjud savdolar saqlanadi:",
                        style = MaterialTheme.typography.bodySmall
                    )
                    Spacer(modifier = Modifier.height(10.dp))
                    OutlinedTextField(
                        value = desktopUrlInput,
                        onValueChange = { desktopUrlInput = it },
                        label = { Text("Kompyuter IP manzili") },
                        placeholder = { Text("http://192.168.1.100:8080") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = LineSecondary),
                    onClick = {
                        showDesktopRestoreDialog = false
                        // Pairing is performed only in the Wi-Fi dialog; never overwrite its server identity here.
                        scope.launch {
                            isLoading = true
                            loadingMessage = "Kompyuterdan baza yuklab olinmoqda..."
                            withContext(Dispatchers.IO) {
                                DatabaseBackupExporter.restoreDatabaseFromDesktop(context, desktopUrlInput, appDatabase)
                            }.onSuccess { successMsg ->
                                isLoading = false
                                Toast.makeText(context, successMsg, Toast.LENGTH_LONG).show()
                                // V2 imports into the existing Room database; no file replacement or restart.
                            }.onFailure { err ->
                                isLoading = false
                                Toast.makeText(context, "Yuklab bo'lmadi: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                            }
                        }
                    }
                ) {
                    Text("Yuklab tiklash", fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = { showDesktopRestoreDialog = false }) {
                    Text("Bekor qilish")
                }
            }
        )
    }
}

@Composable
private fun BackupOptionCard(
    title: String,
    description: String,
    icon: ImageVector,
    iconColor: Color,
    badgeText: String? = null,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() },
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.45f)
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.weight(1f)) {
                Surface(
                    shape = CircleShape,
                    color = iconColor.copy(alpha = 0.15f),
                    modifier = Modifier.size(42.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = icon,
                            contentDescription = null,
                            tint = iconColor,
                            modifier = Modifier.size(22.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.width(12.dp))

                Column {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = title,
                            style = MaterialTheme.typography.bodyMedium,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        if (!badgeText.isNullOrEmpty()) {
                            Spacer(modifier = Modifier.width(6.dp))
                            Surface(
                                shape = RoundedCornerShape(6.dp),
                                color = iconColor.copy(alpha = 0.15f)
                            ) {
                                Text(
                                    text = badgeText,
                                    color = iconColor,
                                    fontSize = 10.sp,
                                    fontWeight = FontWeight.Bold,
                                    modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                                )
                            }
                        }
                    }
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = description,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        fontSize = 11.sp,
                        lineHeight = 15.sp
                    )
                }
            }

            Surface(
                shape = CircleShape,
                color = MaterialTheme.colorScheme.surface,
                modifier = Modifier.size(32.dp)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Icon(
                        imageVector = Icons.Default.Send,
                        contentDescription = "Harakat",
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(14.dp)
                    )
                }
            }
        }
    }
}
