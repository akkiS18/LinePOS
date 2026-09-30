package uz.pos.admin.ui

import android.widget.Toast
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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Article
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.Autorenew
import androidx.compose.material.icons.filled.Block
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DeleteSweep
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import uz.pos.admin.data.AdminRepository
import uz.pos.admin.model.DeviceItem
import uz.pos.admin.util.AdminLogger
import uz.pos.admin.util.LogEntry
import uz.pos.admin.util.LogLevel
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.random.Random

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    repository: AdminRepository,
    onLogout: () -> Unit
) {
    val context = LocalContext.current
    var selectedTab by remember { mutableIntStateOf(0) }

    val devices by repository.getDevicesFlow().collectAsState(initial = emptyList())
    val currentGlobalCode by repository.getGlobalCodeFlow().collectAsState(initial = "1984")
    val errorMsg by repository.errorState.collectAsState()
    val logs by AdminLogger.logs.collectAsState()

    var deviceToDelete by remember { mutableStateOf<DeviceItem?>(null) }

    Scaffold(
        containerColor = Color(0xFF0F172A),
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color(0xFF1E293B),
                    titleContentColor = Color(0xFFF8FAFC)
                ),
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Surface(
                            shape = CircleShape,
                            color = Color(0xFF0B6477),
                            modifier = Modifier.size(36.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(
                                    imageVector = Icons.Default.Devices,
                                    contentDescription = null,
                                    tint = Color.White,
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                text = "SMART Admin",
                                fontSize = 18.sp,
                                fontWeight = FontWeight.Bold
                            )
                            Text(
                                text = "${devices.size} ta kassa ulangan",
                                fontSize = 11.sp,
                                color = Color(0xFF94A3B8)
                            )
                        }
                    }
                },
                actions = {
                    IconButton(onClick = onLogout) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.Logout,
                            contentDescription = "Chiqish",
                            tint = Color(0xFFEF4444)
                        )
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // Tablar: 📱 Qurilmalar | 🔑 Aktivatsiya Kodi | 📋 Loglar
            TabRow(
                selectedTabIndex = selectedTab,
                containerColor = Color(0xFF1E293B),
                contentColor = Color(0xFF2DD4BF),
                modifier = Modifier.clip(RoundedCornerShape(12.dp))
            ) {
                Tab(
                    selected = selectedTab == 0,
                    onClick = { selectedTab = 0 },
                    text = {
                        Text(
                            text = "📱 Qurilmalar (${devices.size})",
                            fontWeight = FontWeight.Bold,
                            fontSize = 12.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    text = {
                        Text(
                            text = "🔑 Kod",
                            fontWeight = FontWeight.Bold,
                            fontSize = 12.sp
                        )
                    }
                )
                Tab(
                    selected = selectedTab == 2,
                    onClick = { selectedTab = 2 },
                    text = {
                        Text(
                            text = "📋 Loglar (${logs.size})",
                            fontWeight = FontWeight.Bold,
                            fontSize = 12.sp
                        )
                    }
                )
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Xatolik xabarnomasi (agar Firebase ruxsat berilmagan bo'lsa)
            errorMsg?.let { error ->
                Card(
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = Color(0xFFEF4444).copy(alpha = 0.15f)),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 12.dp)
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(imageVector = Icons.Default.Warning, contentDescription = null, tint = Color(0xFFEF4444))
                        Spacer(modifier = Modifier.width(8.dp))
                        Column {
                            Text(
                                text = "Firebase Ruxsat Xatosi",
                                color = Color(0xFFEF4444),
                                fontWeight = FontWeight.Bold,
                                fontSize = 13.sp
                            )
                            Spacer(modifier = Modifier.height(2.dp))
                            Text(
                                text = error,
                                color = Color(0xFFFCA5A5),
                                fontSize = 11.sp,
                                lineHeight = 15.sp
                            )
                        }
                    }
                }
            }

            when (selectedTab) {
                0 -> {
                    // ==================== 📱 QURILMALAR BOSHQARUVI ====================
                    val activeCount = devices.count { it.isActivated }
                    val blockedCount = devices.size - activeCount

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        StatCard(
                            title = "Jami",
                            count = devices.size.toString(),
                            color = Color(0xFF38BDF8),
                            modifier = Modifier.weight(1f)
                        )
                        StatCard(
                            title = "Faol",
                            count = activeCount.toString(),
                            color = Color(0xFF10B981),
                            modifier = Modifier.weight(1f)
                        )
                        StatCard(
                            title = "Bloklangan",
                            count = blockedCount.toString(),
                            color = Color(0xFFEF4444),
                            modifier = Modifier.weight(1f)
                        )
                    }

                    Spacer(modifier = Modifier.height(14.dp))

                    if (devices.isEmpty()) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .weight(1f),
                            contentAlignment = Alignment.Center
                        ) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                Icon(
                                    imageVector = Icons.Default.Devices,
                                    contentDescription = null,
                                    tint = Color(0xFF475569),
                                    modifier = Modifier.size(60.dp)
                                )
                                Spacer(modifier = Modifier.height(12.dp))
                                Text(
                                    text = "Hali hech qanday kassa ulanmagan",
                                    color = Color(0xFF94A3B8),
                                    fontSize = 14.sp
                                )
                                Text(
                                    text = "Kassa ilovasi ochilganda bu yerda avtomatik paydo bo'ladi",
                                    color = Color(0xFF64748B),
                                    fontSize = 12.sp
                                )
                            }
                        }
                    } else {
                        LazyColumn(
                            modifier = Modifier.weight(1f),
                            verticalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            items(devices, key = { it.id }) { device ->
                                DeviceCard(
                                    device = device,
                                    onToggle = {
                                        val newStatus = !device.isActivated
                                        repository.toggleDeviceActivation(device.id, newStatus) { success, err ->
                                            if (success) {
                                                val msg = if (newStatus) "Qurilma faollashtirildi!" else "Qurilma bloklandi!"
                                                Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                            } else {
                                                Toast.makeText(context, "Xato: ${err ?: "Bajarilmadi"}", Toast.LENGTH_SHORT).show()
                                            }
                                        }
                                    },
                                    onDelete = {
                                        deviceToDelete = device
                                    }
                                )
                            }
                        }
                    }
                }
                1 -> {
                    // ==================== 🔑 GLOBAL AKTIVATSIYA KODI ====================
                    GlobalCodeSettingsTab(
                        currentCode = currentGlobalCode,
                        onSaveCode = { newCode ->
                            repository.updateGlobalCode(newCode) { success, err ->
                                if (success) {
                                    Toast.makeText(context, "Aktivatsiya kodi yangilandi: $newCode 🎉", Toast.LENGTH_LONG).show()
                                } else {
                                    Toast.makeText(context, "Xatolik: ${err ?: "Saqlab bo'lmadi"}", Toast.LENGTH_LONG).show()
                                }
                            }
                        }
                    )
                }
                2 -> {
                    // ==================== 📋 LOGLAR TABI ====================
                    LogsTab(
                        logs = logs,
                        onClearLogs = {
                            AdminLogger.clear()
                            Toast.makeText(context, "Loglar tozalandi", Toast.LENGTH_SHORT).show()
                        },
                        onPingFirebase = {
                            repository.pingFirebase { _, msg ->
                                Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                            }
                        }
                    )
                }
            }
        }
    }

    // O'chirishni tasdiqlash dialogi
    deviceToDelete?.let { device ->
        AlertDialog(
            onDismissRequest = { deviceToDelete = null },
            title = { Text("Qurilmani o'chirish") },
            text = { Text("${device.displayName} qurilmasini ro'yxatdan o'chirmoqchimisiz?") },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFEF4444)),
                    onClick = {
                        repository.deleteDevice(device.id) { success, err ->
                            if (success) {
                                Toast.makeText(context, "Qurilma o'chirildi", Toast.LENGTH_SHORT).show()
                            } else {
                                Toast.makeText(context, "Xato: ${err ?: "O'chirib bo'lmadi"}", Toast.LENGTH_SHORT).show()
                            }
                        }
                        deviceToDelete = null
                    }
                ) {
                    Text("Ha, o'chirish")
                }
            },
            dismissButton = {
                TextButton(onClick = { deviceToDelete = null }) {
                    Text("Bekor qilish")
                }
            }
        )
    }
}

@Composable
private fun LogsTab(
    logs: List<LogEntry>,
    onClearLogs: () -> Unit,
    onPingFirebase: () -> Unit
) {
    val clipboardManager = LocalClipboardManager.current
    val context = LocalContext.current

    Column(modifier = Modifier.fillMaxSize()) {
        // Asboblar paneli: Nusxalash, Ping, Tozalash
        Card(
            shape = RoundedCornerShape(12.dp),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
            modifier = Modifier.fillMaxWidth()
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Jami: ${logs.size} ta log",
                    fontSize = 12.sp,
                    color = Color(0xFF94A3B8),
                    fontWeight = FontWeight.SemiBold
                )

                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    // Test ping
                    IconButton(
                        onClick = onPingFirebase,
                        modifier = Modifier.size(34.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Refresh,
                            contentDescription = "Test ulanish",
                            tint = Color(0xFF38BDF8),
                            modifier = Modifier.size(18.dp)
                        )
                    }

                    // Nusxalash
                    IconButton(
                        onClick = {
                            if (logs.isNotEmpty()) {
                                val fullLogText = logs.reversed().joinToString("\n") { entry ->
                                    "[${entry.formattedTime}] [${entry.level}] [${entry.tag}] ${entry.message}"
                                }
                                clipboardManager.setText(AnnotatedString(fullLogText))
                                Toast.makeText(context, "Barcha loglar buferga nusxalandi 📋", Toast.LENGTH_SHORT).show()
                            } else {
                                Toast.makeText(context, "Nusxalash uchun loglar yo'q", Toast.LENGTH_SHORT).show()
                            }
                        },
                        modifier = Modifier.size(34.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.ContentCopy,
                            contentDescription = "Nusxalash",
                            tint = Color(0xFF2DD4BF),
                            modifier = Modifier.size(18.dp)
                        )
                    }

                    // Tozalash
                    IconButton(
                        onClick = onClearLogs,
                        modifier = Modifier.size(34.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.DeleteSweep,
                            contentDescription = "Tozalash",
                            tint = Color(0xFFEF4444),
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(10.dp))

        if (logs.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.Article,
                        contentDescription = null,
                        tint = Color(0xFF475569),
                        modifier = Modifier.size(48.dp)
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Hozircha hech qanday log yo'q",
                        color = Color(0xFF94A3B8),
                        fontSize = 13.sp
                    )
                }
            }
        } else {
            SelectionContainer(modifier = Modifier.weight(1f)) {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.fillMaxSize()
                ) {
                    items(logs) { entry ->
                        LogItemCard(entry = entry)
                    }
                }
            }
        }
    }
}

@Composable
private fun LogItemCard(entry: LogEntry) {
    val (badgeBg, badgeTextColor, badgeLabel) = when (entry.level) {
        LogLevel.ERROR -> Triple(Color(0xFFEF4444).copy(alpha = 0.2f), Color(0xFFEF4444), "ERROR")
        LogLevel.WARNING -> Triple(Color(0xFFF59E0B).copy(alpha = 0.2f), Color(0xFFF59E0B), "WARN")
        LogLevel.SUCCESS -> Triple(Color(0xFF10B981).copy(alpha = 0.2f), Color(0xFF10B981), "OK")
        LogLevel.INFO -> Triple(Color(0xFF38BDF8).copy(alpha = 0.2f), Color(0xFF38BDF8), "INFO")
    }

    Card(
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(10.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    // Badge
                    Surface(
                        shape = RoundedCornerShape(4.dp),
                        color = badgeBg,
                        modifier = Modifier.padding(end = 6.dp)
                    ) {
                        Text(
                            text = badgeLabel,
                            color = badgeTextColor,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    }

                    // Tag
                    Text(
                        text = "[${entry.tag}]",
                        color = Color(0xFF94A3B8),
                        fontSize = 11.sp,
                        fontWeight = FontWeight.SemiBold
                    )
                }

                // Time
                Text(
                    text = entry.formattedTime,
                    color = Color(0xFF64748B),
                    fontSize = 11.sp,
                    fontFamily = FontFamily.Monospace
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Xabar matni
            Text(
                text = entry.message,
                color = Color(0xFFF1F5F9),
                fontSize = 12.sp,
                lineHeight = 16.sp,
                fontFamily = FontFamily.Monospace
            )
        }
    }
}

@Composable
private fun DeviceCard(
    device: DeviceItem,
    onToggle: () -> Unit,
    onDelete: () -> Unit
) {
    Card(
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Surface(
                        shape = CircleShape,
                        color = if (device.isActivated) Color(0xFF10B981).copy(alpha = 0.2f) else Color(0xFFEF4444).copy(alpha = 0.2f),
                        modifier = Modifier.size(40.dp)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = if (device.model.contains("Desktop", ignoreCase = true) || device.model.contains("Windows", ignoreCase = true))
                                    Icons.Default.Computer
                                else
                                    Icons.Default.PhoneAndroid,
                                contentDescription = null,
                                tint = if (device.isActivated) Color(0xFF10B981) else Color(0xFFEF4444),
                                modifier = Modifier.size(22.dp)
                            )
                        }
                    }
                    Spacer(modifier = Modifier.width(10.dp))
                    Column {
                        Text(
                            text = device.displayName,
                            fontSize = 15.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFFF8FAFC)
                        )
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Box(
                                modifier = Modifier
                                    .size(7.dp)
                                    .clip(CircleShape)
                                    .background(if (device.isOnline) Color(0xFF10B981) else Color.Gray)
                            )
                            Spacer(modifier = Modifier.width(5.dp))
                            val timeStr = if (device.lastActive > 0) {
                                SimpleDateFormat("dd.MM HH:mm", Locale.getDefault()).format(Date(device.lastActive))
                            } else "Noma'lum"
                            Text(
                                text = if (device.isOnline) "Onlayn" else "Oxirgi: $timeStr",
                                fontSize = 11.sp,
                                color = Color(0xFF94A3B8)
                            )
                        }
                    }
                }

                // O'chirish tugmasi
                IconButton(onClick = onDelete) {
                    Icon(
                        imageVector = Icons.Default.Delete,
                        contentDescription = "O'chirish",
                        tint = Color(0xFF64748B),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "ID: ${device.id.take(12)}...",
                    fontSize = 11.sp,
                    color = Color(0xFF64748B)
                )

                // Bloklash / Faollashtirish tugmasi
                Button(
                    onClick = onToggle,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = if (device.isActivated) Color(0xFF334155) else Color(0xFF10B981)
                    ),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.height(34.dp)
                ) {
                    Icon(
                        imageVector = if (device.isActivated) Icons.Default.Block else Icons.Default.Check,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp),
                        tint = if (device.isActivated) Color(0xFFEF4444) else Color.White
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = if (device.isActivated) "Bloklash" else "Faollashtirish",
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold,
                        color = if (device.isActivated) Color(0xFFEF4444) else Color.White
                    )
                }
            }
        }
    }
}

@Composable
private fun StatCard(
    title: String,
    count: String,
    color: Color,
    modifier: Modifier = Modifier
) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
        modifier = modifier
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(text = count, fontSize = 20.sp, fontWeight = FontWeight.Bold, color = color)
            Text(text = title, fontSize = 11.sp, color = Color(0xFF94A3B8))
        }
    }
}

@Composable
private fun GlobalCodeSettingsTab(
    currentCode: String,
    onSaveCode: (String) -> Unit
) {
    var inputCode by remember(currentCode) { mutableStateOf(currentCode) }

    Column(modifier = Modifier.fillMaxWidth()) {
        Card(
            shape = RoundedCornerShape(18.dp),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier.padding(18.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Surface(
                    shape = CircleShape,
                    color = Color(0xFF0B6477).copy(alpha = 0.2f),
                    modifier = Modifier.size(54.dp)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Icon(
                            imageVector = Icons.Default.Key,
                            contentDescription = null,
                            tint = Color(0xFF2DD4BF),
                            modifier = Modifier.size(28.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = "Amaldagi Aktivatsiya Kodi",
                    fontSize = 13.sp,
                    color = Color(0xFF94A3B8)
                )

                Text(
                    text = currentCode,
                    fontSize = 36.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = Color(0xFF2DD4BF)
                )

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = "Har qanday yangi kassa (mobil yoki desktop) ishga tushganda do'kon egasi shu kodni terishi kerak. Kod mos kelsa qurilma avtomatik faollashadi.",
                    fontSize = 12.sp,
                    color = Color(0xFF94A3B8),
                    lineHeight = 16.sp,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )
            }
        }

        Spacer(modifier = Modifier.height(16.dp))

        Card(
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = Color(0xFF1E293B)),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = "Kodni O'zgartirish",
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color(0xFFF8FAFC)
                )

                Spacer(modifier = Modifier.height(10.dp))

                OutlinedTextField(
                    value = inputCode,
                    onValueChange = { inputCode = it.take(8) },
                    label = { Text("Yangi aktivatsiya kodi") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                Spacer(modifier = Modifier.height(12.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedButton(
                        onClick = {
                            val randomPin = Random.nextInt(1000, 9999).toString()
                            inputCode = randomPin
                        },
                        shape = RoundedCornerShape(10.dp),
                        modifier = Modifier.weight(1f)
                    ) {
                        Icon(imageVector = Icons.Default.Autorenew, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Tasodifiy", fontSize = 12.sp)
                    }

                    Button(
                        onClick = {
                            if (inputCode.isNotBlank()) {
                                onSaveCode(inputCode)
                            }
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF0B6477)),
                        shape = RoundedCornerShape(10.dp),
                        modifier = Modifier.weight(1.2f)
                    ) {
                        Icon(imageVector = Icons.Default.Save, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Saqlash", fontWeight = FontWeight.Bold, fontSize = 12.sp)
                    }
                }
            }
        }
    }
}
