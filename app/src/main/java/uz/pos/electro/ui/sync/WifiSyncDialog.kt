package uz.pos.electro.ui.sync

import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.heightIn
import androidx.compose.ui.platform.LocalConfiguration
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import kotlinx.coroutines.launch
import uz.pos.electro.data.sync.LiveSyncStatus
import uz.pos.electro.data.sync.LocalSyncManager
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

    val liveStatus by syncManager.liveSyncStatus.collectAsState()

    fun testAndConnect(urlToTest: String) {
        if (urlToTest.isBlank()) {
            statusText = "Iltimos, server IP manzilini kiriting"
            return
        }
        val normalized = runCatching { LocalSyncManager.normalizeUrl(urlToTest) }.getOrElse { statusText = it.message ?: "IP noto‘g‘ri"; return }
        serverUrl = normalized

        scope.launch {
            isLoading = true
            statusText = "Kompyuterga ulanish tekshirilmoqda..."
            val res = if (urlToTest.trim().startsWith("{") || pairingCode.isNotBlank())
                syncManager.pairDesktop(urlToTest, pairingCode) else syncManager.pingDesktop(normalized)
            isLoading = false
            if (res.isSuccess) {
                isConnected = true
                pairingCode = ""
                statusText = "Muvaffaqiyatli bog'landi: ${res.getOrNull()}"
                syncManager.restartLiveSyncEngine()
                Toast.makeText(context, "🟢 Kompyuter bilan jonli aloqa o'rnatildi!", Toast.LENGTH_SHORT).show()
            } else {
                isConnected = false
                val errorMsg = res.exceptionOrNull()?.message ?: "IP yoki Wi-Fi tarmog'ini tekshiring"
                statusText = "Ulanib bo'lmadi: $errorMsg"
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

    Dialog(onDismissRequest = onDismiss) {
        Surface(
            shape = RoundedCornerShape(20.dp),
            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 6.dp,
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = (LocalConfiguration.current.screenHeightDp * 0.85f).dp)
                    .verticalScroll(rememberScrollState())
                    .padding(20.dp)
            ) {
                // Sarlavha
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.Wifi,
                        contentDescription = "Wi-Fi",
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(26.dp)
                    )
                    Spacer(modifier = Modifier.width(10.dp))
                    Column {
                        Text(
                            text = "Kompyuterga Ulanish",
                            fontSize = 17.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = "Internetsiz Wi-Fi sinxronizatsiya",
                            fontSize = 11.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                Spacer(modifier = Modifier.height(14.dp))

                // QR Kodni skaner qilish tugmasi
                Button(
                    onClick = { isScannerOpen = true },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(44.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
                ) {
                    Icon(
                        imageVector = Icons.Default.QrCodeScanner,
                        contentDescription = "QR",
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("QR orqali bir marta ulash", fontWeight = FontWeight.Bold, fontSize = 13.sp)
                }

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    "QR skanerlang — kod terish shart emas. Keyingi ulanishlar ilova ochilganda avtomatik amalga oshadi.",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

                // Server URL maydoni
                OutlinedTextField(
                    value = serverUrl,
                    onValueChange = {
                        serverUrl = it
                        isConnected = false
                    },
                    label = { Text("Kompyuter IP (masalan: 192.168.1.5:8080)") },
                    placeholder = { Text("192.168.1.5:8080") },
                    singleLine = true,
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(modifier = Modifier.height(10.dp))

                OutlinedTextField(value = pairingCode, onValueChange = { pairingCode = it }, label = { Text("QR ishlamasa: 8 raqamli kod") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                Text("Navbat: $pendingCount ta. $syncMessage", fontSize = 12.sp)
                if (hasConflict) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(onClick = { scope.launch { syncManager.resolveConflicts(true) } }) { Text("Telefon tahriri") }
                        OutlinedButton(onClick = { scope.launch { syncManager.resolveConflicts(false) } }) { Text("Kompyuter tahriri") }
                    }
                }
                // Bog'lanish tugmasi
                Button(
                    onClick = { testAndConnect(serverUrl) },
                    enabled = !isLoading && serverUrl.isNotBlank(),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(42.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0F766E))
                ) {
                    if (isLoading) {
                        CircularProgressIndicator(strokeWidth = 2.dp, color = Color.White, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Bog'lanmoqda...", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    } else {
                        Icon(Icons.Default.Sync, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Bog'lanish / Ulanishni Tekshirish", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    }
                }

                // Holat matni
                if (statusText.isNotBlank()) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = statusText,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Medium,
                        color = if (isConnected || liveStatus == LiveSyncStatus.CONNECTED) Color(0xFF10B981) else Color(0xFFEF4444)
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                // JONLI SINXRON STATUS KARTASI
                val isCurrentlyLive = liveStatus == LiveSyncStatus.CONNECTED

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(14.dp))
                        .background(
                            if (isCurrentlyLive) Color(0xFF10B981).copy(alpha = 0.10f)
                            else if (liveStatus == LiveSyncStatus.CONNECTING || liveStatus == LiveSyncStatus.CONFLICT) Color(0xFFF59E0B).copy(alpha = 0.10f)
                            else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.05f)
                        )
                        .padding(14.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = RoundedCornerShape(10.dp),
                            color = if (isCurrentlyLive) Color(0xFF10B981)
                            else if (liveStatus == LiveSyncStatus.CONNECTING || liveStatus == LiveSyncStatus.CONFLICT) Color(0xFFF59E0B)
                            else Color(0xFF6B7280),
                            modifier = Modifier.size(38.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                if (liveStatus == LiveSyncStatus.CONNECTING) {
                                    CircularProgressIndicator(strokeWidth = 2.dp, color = Color.White, modifier = Modifier.size(18.dp))
                                } else if (isCurrentlyLive) {
                                    Icon(Icons.Default.CheckCircle, contentDescription = null, tint = Color.White, modifier = Modifier.size(20.dp))
                                } else {
                                    Icon(Icons.Default.Wifi, contentDescription = null, tint = Color.White, modifier = Modifier.size(20.dp))
                                }
                            }
                        }
                        Spacer(modifier = Modifier.width(12.dp))
                        Column {
                            Text(
                                text = if (isCurrentlyLive) "🟢 Jonli sinxronizatsiya faol"
                                else if (liveStatus == LiveSyncStatus.CONFLICT) "🟡 Tahrirni tanlang"
                                else if (liveStatus == LiveSyncStatus.CONNECTING) "🟡 Ulanmoqda..."
                                else "⚪ Aloqa yo'q",
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                text = if (isCurrentlyLive)
                                    "Kompyuter bilan bog'langan. Barcha tovarlar, omborlar va savdolar avtomatik almashinadi."
                                else if (liveStatus == LiveSyncStatus.CONFLICT)
                                    "Aloqa bor. Sinxron davom etishi uchun yuqorida telefon yoki kompyuter tahririni tanlang."
                                else if (liveStatus == LiveSyncStatus.CONNECTING)
                                    "Kompyuter bilan aloqa o'rnatilmoqda..."
                                else
                                    "Ikkala qurilma bitta Wi-Fi yoki telefon tarqatgan hotspotda bo'lishi kerak.",
                                fontSize = 11.sp,
                                lineHeight = 16.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                OutlinedButton(
                    onClick = onDismiss,
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("Yopish")
                }
            }
        }
    }
}
