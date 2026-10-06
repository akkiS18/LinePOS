package uz.pos.electro.ui.sync

import android.widget.Toast
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.LinkOff
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.launch
import uz.pos.electro.data.sync.LiveSyncStatus
import uz.pos.electro.data.sync.LocalSyncManager
import uz.pos.electro.data.sync.ServerChangedException
import uz.pos.electro.scanner.CameraScannerDialog

@Composable
fun WifiSyncDialog(
    syncManager: LocalSyncManager,
    onDismiss: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()

    var serverUrl by remember { mutableStateOf(syncManager.getServerUrl() ?: "") }
    var pairingCode by remember { mutableStateOf("") }
    val syncMessage by syncManager.syncMessage.collectAsState()
    val pendingCount by syncManager.pendingCount.collectAsState()
    val hasConflict by syncManager.hasConflict.collectAsState()
    var isLoading by remember { mutableStateOf(false) }
    var statusText by remember { mutableStateOf("") }
    var isConnected by remember { mutableStateOf(false) }
    var isScannerOpen by remember { mutableStateOf(false) }
    var pendingRePair by remember { mutableStateOf<Pair<String, String>?>(null) }
    var isUnlinkDialogOpen by remember { mutableStateOf(false) }

    val liveStatus by syncManager.liveSyncStatus.collectAsState()

    fun testAndConnect(urlToTest: String) {
        if (urlToTest.isBlank()) {
            statusText = "Server IP manzilini kiriting"
            return
        }
        val normalized = runCatching { LocalSyncManager.normalizeUrl(urlToTest) }.getOrElse {
            statusText = it.message ?: "IP noto‘g‘ri"
            return
        }
        serverUrl = normalized

        scope.launch {
            isLoading = true
            statusText = "Ulanish tekshirilmoqda..."
            val res = if (urlToTest.trim().startsWith("{") || pairingCode.isNotBlank())
                syncManager.pairDesktop(urlToTest, pairingCode) else syncManager.pingDesktop(normalized)
            isLoading = false
            if (res.isSuccess) {
                isConnected = true
                pairingCode = ""
                statusText = "Bog'landi: ${res.getOrNull() ?: ""}"
                syncManager.restartLiveSyncEngine()
                Toast.makeText(context, "🟢 Kompyuter bilan jonli aloqa o'rnatildi!", Toast.LENGTH_SHORT).show()
            } else {
                isConnected = false
                val err = res.exceptionOrNull()
                if (err is ServerChangedException) {
                    pendingRePair = Pair(urlToTest, pairingCode)
                    statusText = "Boshqa kompyuter aniqlandi. Tasdiqlash kutilmoqda."
                } else {
                    val rawMsg = err?.message ?: "Qurilmalar bir xil Wi-Fi tarmog'ida bo'lishi kerak"
                    statusText = if (rawMsg.contains("Kompyuter bazasi almashgan")) {
                        "Kompyuter bazasi yangilangan. Kompyuterdagi QR kodni skanerlang."
                    } else {
                        "Ulanib bo'lmadi: $rawMsg"
                    }
                }
            }
        }
    }

    if (isScannerOpen) {
        CameraScannerDialog(
            onDismissRequest = { isScannerOpen = false },
            onBarcodeScanned = { scannedValue ->
                isScannerOpen = false
                testAndConnect(scannedValue)
            }
        )
    }

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(
            usePlatformDefaultWidth = false,
            dismissOnBackPress = true
        )
    ) {
        Scaffold(
            containerColor = MaterialTheme.colorScheme.background,
            topBar = {
                Surface(
                    modifier = Modifier
                        .fillMaxWidth()
                        .shadow(elevation = 4.dp),
                    color = MaterialTheme.colorScheme.surface
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .statusBarsPadding()
                            .padding(horizontal = 8.dp, vertical = 12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        IconButton(onClick = onDismiss) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                                contentDescription = "Orqaga",
                                tint = MaterialTheme.colorScheme.onSurface
                            )
                        }
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = "Kompyuterga Ulanish",
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                }
            }
        ) { innerPadding ->
            val isCurrentlyLive = liveStatus == LiveSyncStatus.CONNECTED || isConnected

            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding)
                    .navigationBarsPadding()
                    .imePadding()
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // 1. HOLAT KARTASI (Minimal & Modern)
                Surface(
                    shape = RoundedCornerShape(16.dp),
                    color = if (isCurrentlyLive) Color(0xFF10B981).copy(alpha = 0.12f)
                    else if (liveStatus == LiveSyncStatus.CONNECTING || liveStatus == LiveSyncStatus.CONFLICT || isLoading) Color(0xFFF59E0B).copy(alpha = 0.12f)
                    else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                    border = BorderStroke(
                        width = 1.dp,
                        color = if (isCurrentlyLive) Color(0xFF10B981).copy(alpha = 0.4f)
                        else if (liveStatus == LiveSyncStatus.CONNECTING || liveStatus == LiveSyncStatus.CONFLICT || isLoading) Color(0xFFF59E0B).copy(alpha = 0.4f)
                        else MaterialTheme.colorScheme.outline.copy(alpha = 0.2f)
                    ),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Surface(
                            shape = CircleShape,
                            color = if (isCurrentlyLive) Color(0xFF10B981)
                            else if (liveStatus == LiveSyncStatus.CONNECTING || liveStatus == LiveSyncStatus.CONFLICT || isLoading) Color(0xFFF59E0B)
                            else Color(0xFF94A3B8),
                            modifier = Modifier.size(46.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                if (liveStatus == LiveSyncStatus.CONNECTING || isLoading) {
                                    CircularProgressIndicator(strokeWidth = 2.dp, color = Color.White, modifier = Modifier.size(22.dp))
                                } else if (isCurrentlyLive) {
                                    Icon(Icons.Default.CheckCircle, contentDescription = null, tint = Color.White, modifier = Modifier.size(26.dp))
                                } else {
                                    Icon(Icons.Default.Wifi, contentDescription = null, tint = Color.White, modifier = Modifier.size(26.dp))
                                }
                            }
                        }

                        Spacer(modifier = Modifier.width(14.dp))

                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = if (isCurrentlyLive) "Ulangan"
                                else if (liveStatus == LiveSyncStatus.CONFLICT) "Ziddiyat aniqlandi"
                                else if (liveStatus == LiveSyncStatus.CONNECTING || isLoading) "Ulanmoqda..."
                                else "Aloqa yo'q",
                                fontWeight = FontWeight.Bold,
                                fontSize = 16.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )

                            if (serverUrl.isNotBlank()) {
                                Text(
                                    text = serverUrl,
                                    fontSize = 13.sp,
                                    color = if (isCurrentlyLive) Color(0xFF10B981) else MaterialTheme.colorScheme.onSurfaceVariant,
                                    fontWeight = FontWeight.Medium
                                )
                            }

                            if (!isCurrentlyLive && statusText.isNotBlank()) {
                                Text(
                                    text = statusText,
                                    fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.error,
                                    modifier = Modifier.padding(top = 2.dp)
                                )
                            }
                        }

                        if (pendingCount > 0) {
                            Surface(
                                shape = RoundedCornerShape(8.dp),
                                color = MaterialTheme.colorScheme.primaryContainer,
                                modifier = Modifier.padding(start = 8.dp)
                            ) {
                                Text(
                                    text = "$pendingCount ta",
                                    modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onPrimaryContainer
                                )
                            }
                        }

                        if (syncManager.getServerUrl() != null || serverUrl.isNotBlank()) {
                            IconButton(
                                onClick = { isUnlinkDialogOpen = true },
                                modifier = Modifier.size(36.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.LinkOff,
                                    contentDescription = "Aloqani uzish",
                                    tint = MaterialTheme.colorScheme.error.copy(alpha = 0.75f),
                                    modifier = Modifier.size(22.dp)
                                )
                            }
                        }
                    }
                }

                // 2. ZIDDIYAT YECHIMI (Faqat ziddiyat chiqqanda)
                if (hasConflict) {
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = Color(0xFFF59E0B).copy(alpha = 0.12f),
                        border = BorderStroke(1.dp, Color(0xFFF59E0B).copy(alpha = 0.4f)),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(14.dp)) {
                            Text(
                                text = "Qaysi tahrir saqlansin?",
                                fontWeight = FontWeight.Bold,
                                fontSize = 14.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Spacer(modifier = Modifier.height(10.dp))
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(10.dp)
                            ) {
                                Button(
                                    onClick = { scope.launch { syncManager.resolveConflicts(true) } },
                                    modifier = Modifier.weight(1f),
                                    shape = RoundedCornerShape(10.dp)
                                ) {
                                    Text("Telefon", fontWeight = FontWeight.Bold)
                                }
                                OutlinedButton(
                                    onClick = { scope.launch { syncManager.resolveConflicts(false) } },
                                    modifier = Modifier.weight(1f),
                                    shape = RoundedCornerShape(10.dp)
                                ) {
                                    Text("Kompyuter", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }

                // 3. QR SKANERLASH TUGMASI (Katta, Chiroyli Hero Button)
                Surface(
                    shape = RoundedCornerShape(16.dp),
                    color = MaterialTheme.colorScheme.surface,
                    shadowElevation = 2.dp,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        horizontalAlignment = Alignment.CenterHorizontally
                    ) {
                        Button(
                            onClick = { isScannerOpen = true },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(54.dp),
                            shape = RoundedCornerShape(14.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.primary
                            )
                        ) {
                            Icon(
                                imageVector = Icons.Default.QrCodeScanner,
                                contentDescription = null,
                                modifier = Modifier.size(24.dp)
                            )
                            Spacer(modifier = Modifier.width(10.dp))
                            Text(
                                text = "QR Kodni Skanerlash",
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                    }
                }

                // 4. QO'LDA ULASH (Manual IP & Kod)
                Surface(
                    shape = RoundedCornerShape(16.dp),
                    color = MaterialTheme.colorScheme.surface,
                    shadowElevation = 2.dp,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(
                        modifier = Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(14.dp)
                    ) {
                        Text(
                            text = "Qo'lda ulash",
                            fontWeight = FontWeight.Bold,
                            fontSize = 15.sp,
                            color = MaterialTheme.colorScheme.onSurface
                        )

                        OutlinedTextField(
                            value = serverUrl,
                            onValueChange = {
                                serverUrl = it
                                isConnected = false
                            },
                            label = { Text("Kompyuter IP") },
                            placeholder = { Text("192.168.1.5:8080") },
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp),
                            modifier = Modifier.fillMaxWidth()
                        )

                        OutlinedTextField(
                            value = pairingCode,
                            onValueChange = { pairingCode = it },
                            label = { Text("Ulanish kodi (agar so'ralsa)") },
                            placeholder = { Text("8 xonali kod") },
                            singleLine = true,
                            shape = RoundedCornerShape(12.dp),
                            modifier = Modifier.fillMaxWidth()
                        )

                        Button(
                            onClick = { testAndConnect(serverUrl) },
                            enabled = !isLoading && serverUrl.isNotBlank(),
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(48.dp),
                            shape = RoundedCornerShape(12.dp),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = Color(0xFF0F766E)
                            )
                        ) {
                            if (isLoading) {
                                CircularProgressIndicator(strokeWidth = 2.dp, color = Color.White, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Bog'lanmoqda...", fontSize = 14.sp, fontWeight = FontWeight.Bold)
                            } else {
                                Icon(Icons.Default.Sync, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("Bog'lanish", fontSize = 14.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }
            }
        }

        if (pendingRePair != null) {
            AlertDialog(
                onDismissRequest = { pendingRePair = null },
                title = {
                    Text(
                        text = "⚠️ Yangi Kompyuterga Ulash",
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                },
                text = {
                    Text(
                        text = "Telefon avval boshqa kompyuter bazasiga ulangan edi. Yangi kompyuterga qayta bog'lanishni tasdiqlaysizmi?\n\nBarcha tovarlar va ma'lumotlar saqlanadi.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                },
                confirmButton = {
                    Button(
                        onClick = {
                            val target = pendingRePair!!
                            pendingRePair = null
                            scope.launch {
                                isLoading = true
                                statusText = "Yangi kompyuterga bog'lanmoqda..."
                                val res = syncManager.pairDesktop(target.first, target.second, force = true)
                                isLoading = false
                                if (res.isSuccess) {
                                    isConnected = true
                                    pairingCode = ""
                                    statusText = "Bog'landi: ${res.getOrNull() ?: ""}"
                                    syncManager.restartLiveSyncEngine()
                                    Toast.makeText(context, "🟢 Kompyuter bilan jonli aloqa o'rnatildi!", Toast.LENGTH_SHORT).show()
                                } else {
                                    isConnected = false
                                    statusText = "Xatolik: ${res.exceptionOrNull()?.message}"
                                }
                            }
                        }
                    ) {
                        Text("Ha, bog'lash", fontWeight = FontWeight.Bold)
                    }
                },
                dismissButton = {
                    TextButton(onClick = { pendingRePair = null }) {
                        Text("Bekor qilish")
                    }
                }
            )
        }

        if (isUnlinkDialogOpen) {
            AlertDialog(
                onDismissRequest = { isUnlinkDialogOpen = false },
                title = {
                    Text(
                        text = "Kompyuterdan uzish",
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.error
                    )
                },
                text = {
                    Text(
                        text = "Hozirgi kompyuter bilan aloqani uzmoqchimisiz?\n\nTelefon xotirasi tozalangan holda yangi kompyuterga ulanishga tayyor bo'ladi (tovarlar va savdolar o'chirilmaydi).",
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                },
                confirmButton = {
                    Button(
                        onClick = {
                            isUnlinkDialogOpen = false
                            scope.launch {
                                syncManager.unlinkDesktop()
                                serverUrl = ""
                                isConnected = false
                                statusText = "Kompyuter bilan aloqa uzildi."
                                Toast.makeText(context, "Aloqa uzildi", Toast.LENGTH_SHORT).show()
                            }
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                    ) {
                        Text("Ha, uzish", fontWeight = FontWeight.Bold)
                    }
                },
                dismissButton = {
                    TextButton(onClick = { isUnlinkDialogOpen = false }) {
                        Text("Bekor qilish")
                    }
                }
            )
        }
    }
}
