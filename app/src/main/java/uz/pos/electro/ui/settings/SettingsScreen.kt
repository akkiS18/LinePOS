package uz.pos.electro.ui.settings

import android.app.TimePickerDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.OpenInNew
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.ContentPaste
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.FileOpen
import androidx.compose.material.icons.filled.Percent
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.filled.SupportAgent
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableDoubleStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import uz.pos.electro.data.backup.BackupPreferences
import uz.pos.electro.data.backup.DatabaseAutoBackupWorker
import uz.pos.electro.data.backup.TelegramBackupService
import uz.pos.electro.data.licensing.DeviceLicensingManager
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.repository.TaxSettingsRepository
import uz.pos.electro.data.sync.LiveSyncStatus
import uz.pos.electro.data.sync.LocalSyncManager
import uz.pos.electro.ui.licensing.ContactDeveloperDialog
import uz.pos.electro.ui.sync.WifiSyncDialog
import uz.pos.electro.ui.theme.LinePrimary
import uz.pos.electro.ui.theme.LineSecondary
import uz.pos.electro.util.DatabaseBackupExporter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

@Composable
fun SettingsScreen(
    taxSettingsRepository: TaxSettingsRepository,
    licensingManager: DeviceLicensingManager,
    syncManager: LocalSyncManager,
    appDatabase: AppDatabase,
    products: List<ProductEntity> = emptyList()
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    var isTaxDialogOpen by remember { mutableStateOf(false) }
    var isContactDialogOpen by remember { mutableStateOf(false) }
    var isWifiSyncOpen by remember { mutableStateOf(false) }

    // Telegram zaxira holatlari
    var telegramChatId by remember { mutableStateOf(BackupPreferences.getChatId(context)) }
    var telegramChatName by remember { mutableStateOf(BackupPreferences.getChatName(context)) }
    var isAutoBackupEnabled by remember { mutableStateOf(BackupPreferences.isAutoBackupEnabled(context)) }
    var backupHour by remember { mutableIntStateOf(BackupPreferences.getBackupHour(context)) }
    var backupMinute by remember { mutableIntStateOf(BackupPreferences.getBackupMinute(context)) }

    var lastBackupStatus by remember { mutableStateOf(BackupPreferences.getLastBackupStatus(context)) }
    var lastBackupTime by remember { mutableLongStateOf(BackupPreferences.getLastBackupTime(context)) }
    var lastBackupFile by remember { mutableStateOf(BackupPreferences.getLastBackupFile(context)) }

    var isBackupLoading by remember { mutableStateOf(false) }
    var backupLoadingText by remember { mutableStateOf("") }
    var isDetectingChat by remember { mutableStateOf(false) }

    // Karta solig'i foizi
    var currentTaxRate by remember { mutableDoubleStateOf(taxSettingsRepository.getCardTaxRate()) }

    // Bazani tiklash dialogi holati
    var pendingRestoreUri by remember { mutableStateOf<Uri?>(null) }
    var showRestoreConfirmDialog by remember { mutableStateOf(false) }

    val dbPickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        if (uri != null) {
            pendingRestoreUri = uri
            showRestoreConfirmDialog = true
        }
    }

    val liveSyncStatus by syncManager.liveSyncStatus.collectAsState()
    val isConnected = telegramChatId.isNotBlank()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // ==================== 1. QURILMA VA PROFIL KARTASI ====================
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    shape = CircleShape,
                    color = LinePrimary.copy(alpha = 0.12f),
                    modifier = Modifier.size(48.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = Icons.Default.Devices,
                            contentDescription = null,
                            tint = LinePrimary,
                            modifier = Modifier.size(26.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.width(14.dp))

                Column(modifier = Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "SMART Kassa",
                            fontSize = 17.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Surface(
                            shape = RoundedCornerShape(6.dp),
                            color = Color(0xFF10B981).copy(alpha = 0.15f)
                        ) {
                            Text(
                                text = "Faol",
                                color = Color(0xFF10B981),
                                fontSize = 11.sp,
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(2.dp))

                    Text(
                        text = "${Build.MANUFACTURER} ${Build.MODEL} • v1.0.0",
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )

                    Text(
                        text = "ID: ${licensingManager.getDeviceId().take(16)}...",
                        fontSize = 11.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
                        fontFamily = FontFamily.Monospace
                    )
                }
            }
        }

        // ==================== 2. TELEGRAM ZAXIRA (AVTOMATLASHTIRILGAN 1-VARIANT) ====================
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                // Sarlavha va Holat belgisi
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = CircleShape,
                            color = Color(0xFF0088CC).copy(alpha = 0.12f),
                            modifier = Modifier.size(40.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.AutoMirrored.Filled.Send,
                                    contentDescription = null,
                                    tint = Color(0xFF0088CC),
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Telegram Baza Zaxirasi",
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = "@SmartKassaBackupBot",
                                fontSize = 11.sp,
                                color = Color(0xFF0088CC),
                                fontWeight = FontWeight.SemiBold
                            )
                        }
                    }

                    // Ulanish holati ko'rsatkichi (Badge)
                    Surface(
                        shape = RoundedCornerShape(10.dp),
                        color = if (isConnected) Color(0xFF10B981).copy(alpha = 0.15f) else Color(0xFFEF4444).copy(alpha = 0.15f)
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Box(
                                modifier = Modifier
                                    .size(7.dp)
                                    .clip(CircleShape)
                                    .background(if (isConnected) Color(0xFF10B981) else Color(0xFFEF4444))
                            )
                            Spacer(modifier = Modifier.width(5.dp))
                            Text(
                                text = if (isConnected) "Ulangan ✅" else "Ulanmagan ⚠️",
                                color = if (isConnected) Color(0xFF10B981) else Color(0xFFEF4444),
                                fontWeight = FontWeight.Bold,
                                fontSize = 11.sp
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // AVTOMATIK ULANISH BLOKI (2 ta oddiy qadam)
                Card(
                    shape = RoundedCornerShape(14.dp),
                    colors = CardDefaults.cardColors(
                        containerColor = if (isConnected)
                            Color(0xFF10B981).copy(alpha = 0.08f)
                        else
                            Color(0xFF0088CC).copy(alpha = 0.08f)
                    ),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(14.dp)) {
                        if (isConnected) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(
                                    imageVector = Icons.Default.CheckCircle,
                                    contentDescription = null,
                                    tint = LineSecondary,
                                    modifier = Modifier.size(20.dp)
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Column {
                                    Text(
                                        text = "Ulanish muvaffaqiyatli o'rnatilgan!",
                                        fontWeight = FontWeight.Bold,
                                        fontSize = 13.sp,
                                        color = LineSecondary
                                    )
                                    val displayName = if (telegramChatName.isNotBlank()) telegramChatName else "Foydalanuvchi"
                                    Text(
                                        text = "Chat: $displayName (ID: $telegramChatId)",
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                }
                            }
                        } else {
                            Text(
                                text = "🚀 Botga tezkor ulanish (2 ta qadam):",
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.sp,
                                color = Color(0xFF0088CC)
                            )
                            Spacer(modifier = Modifier.height(6.dp))
                            Text(
                                text = "1-Qadam: Pastdagi ko'k tugmani bosing va botda 'START' ni bosing.\n2-Qadam: Ilovaga qaytib 'Ulanishni Aniqlash' tugmasini bosing — ilova Chat ID ni o'zi avtomatik topib ulaydi!",
                                fontSize = 11.sp,
                                lineHeight = 16.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                        }

                        Spacer(modifier = Modifier.height(12.dp))

                        // 1-QADAM TUGMASI: Telegram botga o'tish
                        Button(
                            onClick = {
                                try {
                                    val intent = Intent(Intent.ACTION_VIEW, Uri.parse(BackupPreferences.getBotDeepLink()))
                                    context.startActivity(intent)
                                } catch (_: Throwable) {
                                    Toast.makeText(context, "Telegram ilovasi topilmadi", Toast.LENGTH_SHORT).show()
                                }
                            },
                            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0088CC)),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Icon(imageVector = Icons.AutoMirrored.Filled.OpenInNew, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                text = if (isConnected) "Telegram Botni Ochish (@SmartKassaBackupBot)" else "1. Telegram Botga O'tish (START)",
                                fontWeight = FontWeight.Bold,
                                fontSize = 12.sp
                            )
                        }

                        Spacer(modifier = Modifier.height(8.dp))

                        // 2-QADAM TUGMASI: Avtomatik aniqlash va tekshirish
                        Button(
                            onClick = {
                                scope.launch {
                                    isDetectingChat = true
                                    val token = BackupPreferences.getBotToken(context)
                                    val result = TelegramBackupService.fetchLatestChat(token)
                                    isDetectingChat = false

                                    result.onSuccess { info ->
                                        telegramChatId = info.chatId
                                        telegramChatName = info.name
                                        BackupPreferences.setChatId(context, info.chatId)
                                        BackupPreferences.setChatName(context, info.name)
                                        BackupPreferences.setAutoBackupEnabled(context, true)
                                        isAutoBackupEnabled = true
                                        DatabaseAutoBackupWorker.schedule(context)

                                        // Foydalanuvchiga Telegramda xush kelibsiz xabari yuborish
                                        val welcomeMsg = """
                                            🎉 <b>SMART Kassa — Telegramga Muvaffaqiyatli Ulandi!</b>
                                            
                                            Assalomu alaykum, <b>${info.name}</b>!
                                            Kassa qurilmangiz (${Build.MODEL}) ushbu chatga muvaffaqiyatli bog'landi.
                                            
                                            ✅ Har kuni soat ${"%02d:%02d".format(backupHour, backupMinute)} da (Toshkent vaqti) kassa bazasining xavfsiz zaxira nusxasi ushbu chatga avtomatik tarzda kelib turadi.
                                        """.trimIndent()
                                        TelegramBackupService.sendTextMessage(token, info.chatId, welcomeMsg)

                                        Toast.makeText(context, "🎉 Muvaffaqiyatli ulandi: ${info.name}! Botga tasdiq xabari bordi.", Toast.LENGTH_LONG).show()
                                    }.onFailure { err ->
                                        Toast.makeText(context, err.localizedMessage ?: "Chat topilmadi. Botga START bosing.", Toast.LENGTH_LONG).show()
                                    }
                                }
                            },
                            enabled = !isDetectingChat,
                            colors = ButtonDefaults.buttonColors(containerColor = LineSecondary),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            if (isDetectingChat) {
                                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(16.dp), strokeWidth = 2.dp)
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Telegramdan tekshirilmoqda...", fontSize = 12.sp)
                            } else {
                                Icon(imageVector = Icons.Default.Refresh, contentDescription = null, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(
                                    text = if (isConnected) "Ulanishni Qayta Tekshirish" else "2. Ulanishni Tekshirish va Aniqlash",
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 12.sp
                                )
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                // QO'LDA CHAT ID KIRITISH (Optional fallback)
                OutlinedTextField(
                    value = telegramChatId,
                    onValueChange = {
                        val clean = it.trim()
                        telegramChatId = clean
                        BackupPreferences.setChatId(context, clean)
                        if (isAutoBackupEnabled && clean.isNotBlank()) {
                            DatabaseAutoBackupWorker.schedule(context)
                        }
                    },
                    label = { Text("Telegram Chat ID") },
                    placeholder = { Text("Masalan: 123456789") },
                    singleLine = true,
                    trailingIcon = {
                        IconButton(
                            onClick = {
                                val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                val item = clipboard.primaryClip?.getItemAt(0)?.text?.toString()?.trim() ?: ""
                                if (item.isNotBlank()) {
                                    telegramChatId = item
                                    BackupPreferences.setChatId(context, item)
                                    if (isAutoBackupEnabled) {
                                        DatabaseAutoBackupWorker.schedule(context)
                                    }
                                    Toast.makeText(context, "Chat ID qo'yildi: $item", Toast.LENGTH_SHORT).show()
                                } else {
                                    Toast.makeText(context, "Xotirada (Clipboard) matn topilmadi", Toast.LENGTH_SHORT).show()
                                }
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Default.ContentPaste,
                                contentDescription = "Qo'yish",
                                tint = Color(0xFF0088CC),
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                Spacer(modifier = Modifier.height(14.dp))

                // KUNDALIK AVTO-ZAXIRA VA VAQT SOZLASH (Toshkent vaqti)
                Card(
                    shape = RoundedCornerShape(14.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = "Kundalik Avto-zaxira",
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 13.sp,
                                    color = MaterialTheme.colorScheme.onSurface
                                )
                                Text(
                                    text = "Internet ulanganda orqa fonda avtomatik ketadi",
                                    fontSize = 10.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }

                            Switch(
                                checked = isAutoBackupEnabled,
                                onCheckedChange = { checked ->
                                    if (checked && telegramChatId.isBlank()) {
                                        Toast.makeText(context, "Avval yuqoridagi 1-2 qadam orqali botni ulang!", Toast.LENGTH_SHORT).show()
                                        return@Switch
                                    }
                                    isAutoBackupEnabled = checked
                                    BackupPreferences.setAutoBackupEnabled(context, checked)
                                    if (checked) {
                                        DatabaseAutoBackupWorker.schedule(context)
                                        Toast.makeText(context, "Avtomatik zaxiralash yoqildi (%02d:%02d da) ✅".format(backupHour, backupMinute), Toast.LENGTH_SHORT).show()
                                    } else {
                                        DatabaseAutoBackupWorker.cancel(context)
                                        Toast.makeText(context, "Avtomatik zaxiralash to'xtatildi", Toast.LENGTH_SHORT).show()
                                    }
                                }
                            )
                        }

                        Spacer(modifier = Modifier.height(8.dp))

                        // Vaqtni tanlash qatori (Toshkent vaqti UTC+5)
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(10.dp))
                                .background(MaterialTheme.colorScheme.surface)
                                .padding(horizontal = 12.dp, vertical = 8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(imageVector = Icons.Default.AccessTime, contentDescription = null, tint = LinePrimary, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(8.dp))
                                Column {
                                    Text(
                                        text = "Zaxira vaqti (Toshkent UTC+5):",
                                        fontSize = 11.sp,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                    Text(
                                        text = "%02d:%02d".format(backupHour, backupMinute),
                                        fontSize = 16.sp,
                                        fontWeight = FontWeight.ExtraBold,
                                        color = LinePrimary
                                    )
                                }
                            }

                            OutlinedButton(
                                onClick = {
                                    TimePickerDialog(
                                        context,
                                        { _, selectedHour: Int, selectedMinute: Int ->
                                            backupHour = selectedHour
                                            backupMinute = selectedMinute
                                            BackupPreferences.setBackupTime(context, selectedHour, selectedMinute)
                                            if (isAutoBackupEnabled) {
                                                DatabaseAutoBackupWorker.schedule(context)
                                            }
                                            Toast.makeText(context, "Vaqt saqlandi: %02d:%02d (Toshkent vaqti)".format(selectedHour, selectedMinute), Toast.LENGTH_SHORT).show()
                                        },
                                        backupHour,
                                        backupMinute,
                                        true
                                    ).show()
                                },
                                shape = RoundedCornerShape(8.dp),
                                modifier = Modifier.height(34.dp)
                            ) {
                                Text("O'zgartirish", fontSize = 11.sp)
                            }
                        }

                        Spacer(modifier = Modifier.height(8.dp))

                        // Oxirgi zaxira ma'lumoti
                        val lastTimeStr = if (lastBackupTime > 0) {
                            SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()).apply {
                                timeZone = TimeZone.getTimeZone("Asia/Tashkent")
                            }.format(Date(lastBackupTime))
                        } else {
                            "Hali olinmagan"
                        }

                        Text(
                            text = "🕒 Oxirgi zaxira: $lastTimeStr",
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        if (lastBackupFile.isNotBlank()) {
                            Text(
                                text = "📁 Fayl: $lastBackupFile",
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        Text(
                            text = "Holat: $lastBackupStatus",
                            fontSize = 11.sp,
                            fontWeight = FontWeight.Medium,
                            color = if (lastBackupStatus.contains("Muvaffaqiyatli")) LineSecondary else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Jarayon yuklanayotgan bo'lsa
                if (isBackupLoading) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 8.dp),
                        horizontalArrangement = Arrangement.Center,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        CircularProgressIndicator(
                            color = LinePrimary,
                            modifier = Modifier.size(20.dp),
                            strokeWidth = 2.5.dp
                        )
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = backupLoadingText,
                            fontSize = 13.sp,
                            color = LinePrimary,
                            fontWeight = FontWeight.Medium
                        )
                    }
                } else {
                    // ASOSIY TUGMA: Hozir Telegramga Zaxiralash
                    Button(
                        onClick = {
                            if (telegramChatId.isBlank()) {
                                Toast.makeText(context, "Avval botni ulang!", Toast.LENGTH_SHORT).show()
                                return@Button
                            }
                            scope.launch {
                                isBackupLoading = true
                                backupLoadingText = "Baza tayyorlanmoqda va Telegramga yuklanmoqda..."
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
                                    isBackupLoading = false
                                    val now = System.currentTimeMillis()
                                    lastBackupTime = now
                                    lastBackupStatus = "Muvaffaqiyatli saqlandi ✅"
                                    val fileName = "SMART_POS_Baza_${SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date(now))}.db"
                                    lastBackupFile = fileName
                                    BackupPreferences.recordBackupSuccess(context, fileName)
                                    Toast.makeText(context, "Baza Telegramga muvaffaqiyatli yuklandi! 🎉", Toast.LENGTH_LONG).show()
                                }.onFailure { err ->
                                    isBackupLoading = false
                                    lastBackupStatus = "Xatolik: ${err.localizedMessage} ⚠️"
                                    BackupPreferences.recordBackupFailure(context, err.localizedMessage ?: "Noma'lum xatolik")
                                    Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                                }
                            }
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(46.dp)
                    ) {
                        Icon(imageVector = Icons.Default.CloudUpload, contentDescription = null, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Hozir Telegramga Zaxiralash", fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    // QO'SHIMCHA TUGMALAR: Sinov | Ulashish | Tiklash
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        OutlinedButton(
                            onClick = {
                                if (telegramChatId.isBlank()) {
                                    Toast.makeText(context, "Avval botni ulang!", Toast.LENGTH_SHORT).show()
                                    return@OutlinedButton
                                }
                                scope.launch {
                                    isBackupLoading = true
                                    backupLoadingText = "Sinov xabari yuborilmoqda..."
                                    val token = BackupPreferences.getBotToken(context)
                                    val result = TelegramBackupService.sendTestMessage(
                                        botToken = token,
                                        chatId = telegramChatId,
                                        storeName = "SMART Kassa"
                                    )
                                    isBackupLoading = false
                                    result.onSuccess { msg ->
                                        Toast.makeText(context, msg, Toast.LENGTH_LONG).show()
                                    }.onFailure { err ->
                                        Toast.makeText(context, "Xatolik: ${err.localizedMessage}", Toast.LENGTH_LONG).show()
                                    }
                                }
                            },
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Sinov Xabari", fontSize = 11.sp)
                        }

                        OutlinedButton(
                            onClick = {
                                scope.launch {
                                    isBackupLoading = true
                                    backupLoadingText = "Baza fayli tayyorlanmoqda..."
                                    withContext(Dispatchers.IO) {
                                        DatabaseBackupExporter.backupDatabaseFile(context, appDatabase)
                                    }.onSuccess { file ->
                                        isBackupLoading = false
                                        DatabaseBackupExporter.shareBackupFile(
                                            context = context,
                                            file = file,
                                            mimeType = "application/octet-stream",
                                            chooserTitle = "SMART POS Baza (.db) faylini ulashish"
                                        )
                                    }.onFailure { err ->
                                        isBackupLoading = false
                                        Toast.makeText(context, "Xato: ${err.localizedMessage}", Toast.LENGTH_SHORT).show()
                                    }
                                }
                            },
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Fayl Ulashish", fontSize = 11.sp)
                        }

                        OutlinedButton(
                            onClick = { dbPickerLauncher.launch("*/*") },
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Tiklash (.db)", fontSize = 11.sp)
                        }
                    }
                }
            }
        }

        // ==================== 3. KARTA SOLIG'I VA FOIZ SOZLAMALARI ====================
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = CircleShape,
                            color = LineSecondary.copy(alpha = 0.12f),
                            modifier = Modifier.size(38.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.Percent,
                                    contentDescription = null,
                                    tint = LineSecondary,
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Karta Solig'i Foizi",
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = "Karta savdosidan ushlanadigan xarajat foizi",
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    // Foiz ko'rsatkichi
                    Surface(
                        shape = RoundedCornerShape(10.dp),
                        color = LineSecondary.copy(alpha = 0.15f)
                    ) {
                        Text(
                            text = "${currentTaxRate}%",
                            color = LineSecondary,
                            fontWeight = FontWeight.ExtraBold,
                            fontSize = 15.sp,
                            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                OutlinedButton(
                    onClick = { isTaxDialogOpen = true },
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(imageVector = Icons.Default.Edit, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Foizni O'zgartirish", fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                }
            }
        }

        // ==================== 4. LOKAL WI-FI SINXRONIZATSIYA ====================
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = CircleShape,
                            color = Color(0xFF6366F1).copy(alpha = 0.12f),
                            modifier = Modifier.size(38.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.Wifi,
                                    contentDescription = null,
                                    tint = Color(0xFF6366F1),
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "Lokal Wi-Fi Sinxron",
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = "Kassa kompyuteri bilan jonli bog'lanish",
                                fontSize = 11.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    // Holat badge
                    Surface(
                        shape = RoundedCornerShape(10.dp),
                        color = when (liveSyncStatus) {
                            LiveSyncStatus.CONNECTED -> Color(0xFF16A34A).copy(alpha = 0.15f)
                            LiveSyncStatus.CONFLICT, LiveSyncStatus.CONNECTING -> Color(0xFFF59E0B).copy(alpha = 0.15f)
                            LiveSyncStatus.OFFLINE -> MaterialTheme.colorScheme.surfaceVariant
                        }
                    ) {
                        Text(
                            text = when (liveSyncStatus) {
                                LiveSyncStatus.CONNECTED -> "Jonli ✅"
                                LiveSyncStatus.CONFLICT -> "Tahrirni tanlang ⚠️"
                                LiveSyncStatus.CONNECTING -> "Ulanmoqda..."
                                LiveSyncStatus.OFFLINE -> "Oflayn"
                            },
                            color = when (liveSyncStatus) {
                                LiveSyncStatus.CONNECTED -> Color(0xFF16A34A)
                                LiveSyncStatus.CONFLICT, LiveSyncStatus.CONNECTING -> Color(0xFFF59E0B)
                                LiveSyncStatus.OFFLINE -> MaterialTheme.colorScheme.onSurfaceVariant
                            },
                            fontWeight = FontWeight.Bold,
                            fontSize = 11.sp,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                OutlinedButton(
                    onClick = { isWifiSyncOpen = true },
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(imageVector = Icons.Default.Wifi, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Wi-Fi Sinxronni Sozlash", fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                }
            }
        }

        // ==================== 5. BOG'LANISH VA TEXNIK YORDAM ====================
        Card(
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Surface(
                        shape = CircleShape,
                        color = LinePrimary.copy(alpha = 0.12f),
                        modifier = Modifier.size(38.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = Icons.Default.SupportAgent,
                                contentDescription = null,
                                tint = LinePrimary,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }
                    Spacer(modifier = Modifier.width(10.dp))
                    Column {
                        Text(
                            text = "Bog'lanish va Texnik Yordam",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = "Savol, taklif va litsenziya masalalarida",
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                // Telefon qatori
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f))
                        .padding(horizontal = 12.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(imageVector = Icons.Default.Phone, contentDescription = null, tint = LinePrimary, modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(text = "Telefon", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text(text = "+998 95 720 88 33", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        }
                    }

                    Row {
                        IconButton(
                            onClick = {
                                val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                val clip = ClipData.newPlainText("Telefon", "+998957208833")
                                clipboard.setPrimaryClip(clip)
                                Toast.makeText(context, "Telefon nusxalandi!", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(imageVector = Icons.Default.ContentCopy, contentDescription = "Nusxalash", tint = LinePrimary, modifier = Modifier.size(16.dp))
                        }
                        IconButton(
                            onClick = {
                                try {
                                    val intent = Intent(Intent.ACTION_DIAL, Uri.parse("tel:+998957208833"))
                                    context.startActivity(intent)
                                } catch (_: Throwable) {}
                            },
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(imageVector = Icons.AutoMirrored.Filled.OpenInNew, contentDescription = "Qo'ng'iroq", tint = LineSecondary, modifier = Modifier.size(16.dp))
                        }
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                // Telegram qatori
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f))
                        .padding(horizontal = 12.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(imageVector = Icons.AutoMirrored.Filled.Send, contentDescription = null, tint = Color(0xFF0088CC), modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(text = "Telegram", fontSize = 10.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Text(text = "@S18_2003", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = Color(0xFF0088CC))
                        }
                    }

                    Row {
                        IconButton(
                            onClick = {
                                val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                val clip = ClipData.newPlainText("Telegram", "@S18_2003")
                                clipboard.setPrimaryClip(clip)
                                Toast.makeText(context, "Telegram nusxalandi!", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(imageVector = Icons.Default.ContentCopy, contentDescription = "Nusxalash", tint = LinePrimary, modifier = Modifier.size(16.dp))
                        }
                        IconButton(
                            onClick = {
                                try {
                                    val intent = Intent(Intent.ACTION_VIEW, Uri.parse("https://t.me/S18_2003"))
                                    context.startActivity(intent)
                                } catch (_: Throwable) {}
                            },
                            modifier = Modifier.size(32.dp)
                        ) {
                            Icon(imageVector = Icons.AutoMirrored.Filled.OpenInNew, contentDescription = "Ochish", tint = Color(0xFF0088CC), modifier = Modifier.size(16.dp))
                        }
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                OutlinedButton(
                    onClick = { isContactDialogOpen = true },
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Batafsil Ma'lumot", fontSize = 12.sp, fontWeight = FontWeight.Medium)
                }
            }
        }

        Spacer(modifier = Modifier.height(20.dp))
    }

    // Karta solig'i foizini tahrirlash dialogi
    if (isTaxDialogOpen) {
        CardTaxDialog(
            taxSettingsRepository = taxSettingsRepository,
            onDismissRequest = {
                isTaxDialogOpen = false
                currentTaxRate = taxSettingsRepository.getCardTaxRate()
            }
        )
    }

    // Bog'lanish dialogi
    if (isContactDialogOpen) {
        ContactDeveloperDialog(
            onDismiss = { isContactDialogOpen = false }
        )
    }

    // Wi-Fi Sinxronizatsiya dialogi
    if (isWifiSyncOpen) {
        WifiSyncDialog(
            syncManager = syncManager,
            onDismiss = { isWifiSyncOpen = false }
        )
    }

    // Baza faylidan tiklash tasdiqlash dialogi
    if (showRestoreConfirmDialog && pendingRestoreUri != null) {
        val uri = pendingRestoreUri!!
        AlertDialog(
            onDismissRequest = {
                if (!isBackupLoading) {
                    showRestoreConfirmDialog = false
                    pendingRestoreUri = null
                }
            },
            title = {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(imageVector = Icons.Default.Warning, contentDescription = null, tint = Color(0xFFD97706))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Bazani tiklashni tasdiqlang")
                }
            },
            text = {
                Text(
                    "⚠️ DIQQAT! Tanlangan fayldagi baza amaldagi baza o'rniga to'liq o'rnatiladi.\n\n" +
                    "Hozirgi barcha tovarlar va savdolar ushbu zaxira bazasi bilan almashadi. Davom etasizmi?"
                )
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = LinePrimary),
                    onClick = {
                        showRestoreConfirmDialog = false
                        scope.launch {
                            isBackupLoading = true
                            backupLoadingText = "Baza tekshirilmoqda va tiklanmoqda..."
                            withContext(Dispatchers.IO) {
                                DatabaseBackupExporter.restoreDatabaseFromUri(context, uri, appDatabase)
                            }.onSuccess { successMsg ->
                                isBackupLoading = false
                                Toast.makeText(context, successMsg, Toast.LENGTH_LONG).show()
                                DatabaseBackupExporter.restartApp(context)
                            }.onFailure { err ->
                                isBackupLoading = false
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
}
