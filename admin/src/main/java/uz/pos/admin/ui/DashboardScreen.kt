package uz.pos.admin.ui

import android.widget.Toast
import androidx.compose.foundation.Image
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
import androidx.compose.material.icons.filled.Mic
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Save
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.TabRowDefaults
import androidx.compose.material3.TabRowDefaults.tabIndicatorOffset
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
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import uz.pos.admin.R
import uz.pos.admin.data.AdminRepository
import uz.pos.admin.model.DeviceItem
import uz.pos.admin.util.AdminLogger
import uz.pos.admin.util.LogEntry
import uz.pos.admin.util.LogLevel
import uz.pos.admin.ui.theme.CyberBg
import uz.pos.admin.ui.theme.CyberBorder
import uz.pos.admin.ui.theme.CyberCyan
import uz.pos.admin.ui.theme.CyberCyanAlpha
import uz.pos.admin.ui.theme.CyberCyanDim
import uz.pos.admin.ui.theme.CyberDanger
import uz.pos.admin.ui.theme.CyberDangerAlpha
import uz.pos.admin.ui.theme.CyberDivider
import uz.pos.admin.ui.theme.CyberOnline
import uz.pos.admin.ui.theme.CyberSurface
import uz.pos.admin.ui.theme.CyberSurface2
import uz.pos.admin.ui.theme.CyberTextMono
import uz.pos.admin.ui.theme.CyberTextPrimary
import uz.pos.admin.ui.theme.CyberTextSecondary
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

    val devicesFlow = remember(repository) { repository.getDevicesFlow() }
    val globalCodeFlow = remember(repository) { repository.getGlobalCodeFlow() }

    val devices by devicesFlow.collectAsState(initial = emptyList())
    val currentGlobalCode by globalCodeFlow.collectAsState(initial = "1984")
    val errorMsg by repository.errorState.collectAsState()
    val logs by AdminLogger.logs.collectAsState()

    var deviceToDelete by remember { mutableStateOf<DeviceItem?>(null) }

    val tabLabels = listOf(
        "NODES [${devices.size}]",
        "ACCESS_KEY",
        "SYS_LOG [${logs.size}]"
    )

    Scaffold(
        containerColor = CyberBg,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = CyberSurface,
                    titleContentColor = CyberTextPrimary
                ),
                title = {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        // Logo
                        Box(
                            modifier = Modifier
                                .size(34.dp)
                                .background(CyberBg, chamferedShape(7f))
                                .border(1.dp, CyberCyan.copy(alpha = 0.7f), chamferedShape(7f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Image(
                                painter = painterResource(id = R.drawable.seb_logo),
                                contentDescription = "Seb",
                                modifier = Modifier
                                    .size(28.dp)
                                    .clip(chamferedShape(5f))
                            )
                        }
                        Column {
                            Text(
                                text = "SEB",
                                fontSize = 15.sp,
                                fontWeight = FontWeight.ExtraBold,
                                color = CyberCyan,
                                fontFamily = FontFamily.Monospace,
                                letterSpacing = 3.sp
                            )
                            Text(
                                text = "SYSTEM ONLINE // ${devices.size} NODES",
                                fontSize = 9.sp,
                                color = CyberTextSecondary,
                                fontFamily = FontFamily.Monospace,
                                letterSpacing = 0.5.sp
                            )
                        }
                    }
                },
                actions = {
                    // Ovozli boshqaruv tugmasi (faqat mikrofon ikonkasi)
                    Box(
                        modifier = Modifier
                            .padding(end = 6.dp)
                            .background(Color(0x3338BDF8), chamferedShape(6f))
                            .border(0.8.dp, Color(0x8838BDF8), chamferedShape(6f))
                            .clickable {
                                val voiceIntent = android.content.Intent(context, uz.pos.admin.voice.SebVoiceActivity::class.java).apply {
                                    flags = android.content.Intent.FLAG_ACTIVITY_NEW_TASK or android.content.Intent.FLAG_ACTIVITY_MULTIPLE_TASK
                                }
                                context.startActivity(voiceIntent)
                            }
                            .padding(8.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Default.Mic,
                            contentDescription = "Voice",
                            tint = Color(0xFF38BDF8),
                            modifier = Modifier.size(18.dp)
                        )
                    }

                    // Chiqish tugmasi
                    Box(
                        modifier = Modifier
                            .padding(end = 8.dp)
                            .background(CyberDanger.copy(alpha = 0.1f), chamferedShape(6f))
                            .border(0.8.dp, CyberDanger.copy(alpha = 0.5f), chamferedShape(6f))
                    ) {
                        IconButton(onClick = onLogout) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.Logout,
                                contentDescription = "Chiqish",
                                tint = CyberDanger,
                                modifier = Modifier.size(18.dp)
                            )
                        }
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(CyberBg)
                .padding(paddingValues)
                .padding(12.dp)
        ) {
            // ===== HUD TABLAR =====
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(CyberSurface, chamferedShape(8f))
                    .border(0.8.dp, CyberBorder, chamferedShape(8f))
            ) {
                TabRow(
                    selectedTabIndex = selectedTab,
                    containerColor = Color.Transparent,
                    contentColor = CyberCyan,
                    indicator = { tabPositions ->
                        if (selectedTab < tabPositions.size) {
                            Box(
                                modifier = Modifier
                                    .tabIndicatorOffset(tabPositions[selectedTab])
                                    .height(2.dp)
                                    .background(CyberCyan)
                            )
                        }
                    }
                ) {
                    tabLabels.forEachIndexed { index, label ->
                        Tab(
                            selected = selectedTab == index,
                            onClick = { selectedTab = index },
                            text = {
                                Text(
                                    text = label,
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 10.sp,
                                    fontFamily = FontFamily.Monospace,
                                    color = if (selectedTab == index) CyberCyan else CyberTextSecondary,
                                    letterSpacing = 0.5.sp
                                )
                            }
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ===== XATO XABARI =====
            errorMsg?.let { error ->
                CyberCard(
                    borderColor = CyberDanger,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 10.dp)
                ) {
                    Row(
                        modifier = Modifier.padding(10.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Block,
                            contentDescription = null,
                            tint = CyberDanger,
                            modifier = Modifier.size(16.dp)
                        )
                        Column {
                            Text(
                                text = "FIREBASE_ERROR // PERMISSION DENIED",
                                color = CyberDanger,
                                fontWeight = FontWeight.Bold,
                                fontSize = 10.sp,
                                fontFamily = FontFamily.Monospace
                            )
                            Text(
                                text = error,
                                color = CyberDanger.copy(alpha = 0.7f),
                                fontSize = 10.sp,
                                fontFamily = FontFamily.Monospace,
                                lineHeight = 14.sp
                            )
                        }
                    }
                }
            }

            // ===== TAB CONTENT =====
            when (selectedTab) {
                0 -> DevicesTab(
                    devices = devices,
                    repository = repository,
                    context = context,
                    onRequestDelete = { deviceToDelete = it }
                )
                1 -> GlobalCodeSettingsTab(
                    currentCode = currentGlobalCode,
                    onSaveCode = { newCode ->
                        repository.updateGlobalCode(newCode) { success, err ->
                            if (success) {
                                Toast.makeText(context, "ACCESS_KEY yangilandi: $newCode", Toast.LENGTH_LONG).show()
                            } else {
                                Toast.makeText(context, "XATO: ${err ?: "Saqlab bo'lmadi"}", Toast.LENGTH_LONG).show()
                            }
                        }
                    }
                )
                2 -> LogsTab(
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

    // ===== O'CHIRISH DIALOGI =====
    deviceToDelete?.let { device ->
        AlertDialog(
            onDismissRequest = { deviceToDelete = null },
            containerColor = CyberSurface,
            shape = chamferedShape(14f),
            title = {
                Text(
                    text = "NODE O'CHIRISH",
                    color = CyberDanger,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.Bold,
                    letterSpacing = 2.sp
                )
            },
            text = {
                Text(
                    text = "${device.displayName} — bu nodeni ro'yxatdan o'chirmoqchimisiz?",
                    color = CyberTextPrimary,
                    fontFamily = FontFamily.Monospace,
                    fontSize = 13.sp
                )
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = CyberDanger),
                    shape = chamferedShape(8f),
                    onClick = {
                        repository.deleteDevice(device.id) { success, err ->
                            if (success) {
                                Toast.makeText(context, "Node o'chirildi", Toast.LENGTH_SHORT).show()
                            } else {
                                Toast.makeText(context, "Xato: ${err ?: "O'chirib bo'lmadi"}", Toast.LENGTH_SHORT).show()
                            }
                        }
                        deviceToDelete = null
                    }
                ) {
                    Text("CONFIRM_DELETE", fontFamily = FontFamily.Monospace, fontSize = 11.sp)
                }
            },
            dismissButton = {
                TextButton(onClick = { deviceToDelete = null }) {
                    Text("ABORT", color = CyberTextSecondary, fontFamily = FontFamily.Monospace, fontSize = 11.sp)
                }
            }
        )
    }
}

// ====================================================================
// QURILMALAR TABI
// ====================================================================
@Composable
private fun DevicesTab(
    devices: List<DeviceItem>,
    repository: AdminRepository,
    context: android.content.Context,
    onRequestDelete: (DeviceItem) -> Unit
) {
    val activeCount = devices.count { it.isActivated }
    val blockedCount = devices.size - activeCount

    Column(modifier = Modifier.fillMaxSize()) {
        // Statistika qatorlari (Stark Tech moviy shisha uslubida)
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            CyberStatTile(label = "JAMI", value = devices.size.toString(), color = Color(0xFF38BDF8), modifier = Modifier.weight(1f))
            CyberStatTile(label = "FAOL", value = activeCount.toString(), color = Color(0xFF7DD3FC), modifier = Modifier.weight(1f))
            CyberStatTile(label = "BLOKLANGAN", value = blockedCount.toString(), color = Color(0xFFF87171), modifier = Modifier.weight(1f))
        }

        Spacer(modifier = Modifier.height(12.dp))
        CyberDivider()
        Spacer(modifier = Modifier.height(12.dp))

        if (devices.isEmpty()) {
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(imageVector = Icons.Default.Devices, contentDescription = null, tint = CyberBorder, modifier = Modifier.size(52.dp))
                    Spacer(modifier = Modifier.height(10.dp))
                    Text("NO_NODES_DETECTED", color = CyberTextSecondary, fontSize = 12.sp, fontFamily = FontFamily.Monospace, letterSpacing = 1.sp)
                    Text("Kassa ilovasi ochilganda bu yerda paydo bo'ladi", color = CyberTextSecondary.copy(alpha = 0.6f), fontSize = 11.sp, fontFamily = FontFamily.Monospace)
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(devices, key = { it.id }) { device ->
                    DeviceCard(
                        device = device,
                        onToggle = {
                            val newStatus = !device.isActivated
                            repository.toggleDeviceActivation(device.id, newStatus) { success, err ->
                                if (success) {
                                    val msg = if (newStatus) "Node faollashtirildi" else "Node bloklandi"
                                    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
                                } else {
                                    Toast.makeText(context, "Xato: ${err ?: "Bajarilmadi"}", Toast.LENGTH_SHORT).show()
                                }
                            }
                        },
                        onDelete = { onRequestDelete(device) }
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        // Skrinshotdagi pastki imzo: "⬡ Seb Tech"
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 4.dp, vertical = 2.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(16.dp)
                    .background(Color(0x3338BDF8), CircleShape)
                    .border(0.8.dp, Color(0x8838BDF8), CircleShape),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "S",
                    color = Color(0xFF38BDF8),
                    fontSize = 10.sp,
                    fontWeight = FontWeight.ExtraBold,
                    fontFamily = FontFamily.Monospace
                )
            }
            Text(
                text = "Seb Tech",
                color = Color(0xFF64748B),
                fontSize = 11.sp,
                fontWeight = FontWeight.SemiBold,
                fontFamily = FontFamily.SansSerif
            )
        }
    }
}

// ====================================================================
// QURILMA KARTASI — DeviceCard (HUD uslubida)
// ====================================================================
@Composable
private fun DeviceCard(
    device: DeviceItem,
    onToggle: () -> Unit,
    onDelete: () -> Unit
) {
    StarkGlassCard(
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Yuqori qator: Qurilma ikonkasi, Nomi, Oxirgi vaqti va O'chirish
            Row(
                verticalAlignment = Alignment.Top,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(14.dp),
                    modifier = Modifier.weight(1f)
                ) {
                    // Qurilma ikonkasi (moviy shisha/kumush telefon yoki noutbuk)
                    Box(
                        modifier = Modifier
                            .size(44.dp)
                            .background(
                                Color(0x3338BDF8),
                                RoundedCornerShape(10.dp)
                            )
                            .border(
                                1.dp,
                                Color(0x6638BDF8),
                                RoundedCornerShape(10.dp)
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = if (device.model.contains("Desktop", ignoreCase = true) ||
                                device.model.contains("Windows", ignoreCase = true))
                                Icons.Default.Computer else Icons.Default.PhoneAndroid,
                            contentDescription = null,
                            tint = Color(0xFFBAE6FD),
                            modifier = Modifier.size(24.dp)
                        )
                    }

                    Column {
                        // Qurilma nomi
                        Text(
                            text = device.displayName,
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color(0xFFF8FAFC),
                            fontFamily = FontFamily.SansSerif
                        )

                        Spacer(modifier = Modifier.height(4.dp))

                        // Oltin-sariq holat nuqtasi + Oxirgi vaqt
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(6.dp)
                        ) {
                            Box(
                                modifier = Modifier
                                    .size(7.dp)
                                    .background(
                                        if (device.isOnline) Color(0xFFFBBF24) else Color(0xFF64748B),
                                        CircleShape
                                    )
                            )
                            val timeStr = if (device.lastActive > 0) {
                                SimpleDateFormat("dd.MM HH:mm", Locale.getDefault()).format(Date(device.lastActive))
                            } else "Noma'lum"
                            Text(
                                text = "Oxirgi: $timeStr",
                                fontSize = 12.sp,
                                color = Color(0xFF94A3B8),
                                fontFamily = FontFamily.SansSerif
                            )
                        }
                    }
                }

                // O'chirish (savatcha) ikonkasi — rasmdagi o'ng yuqoridagi nozik belgi
                IconButton(
                    onClick = onDelete,
                    modifier = Modifier.size(32.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Delete,
                        contentDescription = "O'chirish",
                        tint = Color(0xFF64748B),
                        modifier = Modifier.size(18.dp)
                    )
                }
            }

            Spacer(modifier = Modifier.height(14.dp))

            // Pastki qator: Chapda ID, o'ngda Arc Reactor Bloklash tugmasi
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Chapda: ID
                Text(
                    text = "ID: ${device.id.take(14)}...",
                    fontSize = 11.sp,
                    color = Color(0xFF64748B),
                    fontFamily = FontFamily.Monospace
                )

                // O'ngda: Arc Reactor Bloklash / Faollashtirish tugmasi
                StarkReactorButton(
                    isActivated = device.isActivated,
                    onClick = onToggle
                )
            }
        }
    }
}

// ====================================================================
// GLOBAL KOD TABI
// ====================================================================
@Composable
private fun GlobalCodeSettingsTab(
    currentCode: String,
    onSaveCode: (String) -> Unit
) {
    var inputCode by remember(currentCode) { mutableStateOf(currentCode) }

    Column(modifier = Modifier.fillMaxWidth()) {
        // Amaldagi kod ko'rsatish bloki
        CyberCard(
            modifier = Modifier.fillMaxWidth(),
            borderColor = CyberCyanDim
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                CyberSectionHeader(label = "DATA LINK // SECURE CHANNEL")
                Spacer(modifier = Modifier.height(14.dp))

                // Amaldagi kod
                Box(
                    modifier = Modifier
                        .background(CyberCyanAlpha, chamferedShape(10f))
                        .border(1.dp, CyberCyan.copy(alpha = 0.6f), chamferedShape(10f))
                        .padding(horizontal = 24.dp, vertical = 14.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(
                            text = "AMALDAGI_KOD",
                            fontSize = 9.sp,
                            color = CyberTextSecondary,
                            fontFamily = FontFamily.Monospace,
                            letterSpacing = 2.sp
                        )
                        Text(
                            text = currentCode,
                            fontSize = 34.sp,
                            fontWeight = FontWeight.ExtraBold,
                            color = CyberCyan,
                            fontFamily = FontFamily.Monospace,
                            letterSpacing = 6.sp
                        )
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                Text(
                    text = "Har qanday yangi kassa (mobil yoki desktop) ishga tushganda\nshu kodni kiritishi kerak. Mos kelsa avtomatik faollashadi.",
                    fontSize = 10.sp,
                    color = CyberTextSecondary,
                    fontFamily = FontFamily.Monospace,
                    lineHeight = 14.sp,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        // Kodni o'zgartirish bloki
        CyberCard(
            modifier = Modifier.fillMaxWidth(),
            borderColor = CyberBorder
        ) {
            Column(modifier = Modifier.padding(14.dp)) {
                CyberSectionHeader(label = "COMMIT NEW ACCESS_KEY")
                Spacer(modifier = Modifier.height(12.dp))

                OutlinedTextField(
                    value = inputCode,
                    onValueChange = { inputCode = it.take(8) },
                    label = {
                        Text(
                            "Yangi aktivatsiya kodi",
                            fontFamily = FontFamily.Monospace,
                            fontSize = 11.sp
                        )
                    },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = chamferedShape(8f),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = CyberCyan,
                        unfocusedBorderColor = CyberBorder,
                        focusedLabelColor = CyberCyan,
                        unfocusedLabelColor = CyberTextSecondary,
                        focusedTextColor = CyberTextPrimary,
                        unfocusedTextColor = CyberTextPrimary,
                        cursorColor = CyberCyan
                    )
                )

                Spacer(modifier = Modifier.height(10.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    // Tasodifiy kod
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .background(CyberSurface2, chamferedShape(7f))
                            .border(0.8.dp, CyberBorder, chamferedShape(7f))
                            .clickable {
                                inputCode = Random.nextInt(1000, 9999).toString()
                            }
                            .padding(vertical = 10.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(5.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Autorenew,
                                contentDescription = null,
                                tint = CyberTextSecondary,
                                modifier = Modifier.size(14.dp)
                            )
                            Text(
                                "GEN_RANDOM",
                                color = CyberTextSecondary,
                                fontSize = 10.sp,
                                fontFamily = FontFamily.Monospace
                            )
                        }
                    }

                    // Saqlash
                    Box(
                        modifier = Modifier
                            .weight(1.3f)
                            .background(CyberCyan.copy(alpha = 0.12f), chamferedShape(7f))
                            .border(0.8.dp, CyberCyan.copy(alpha = 0.7f), chamferedShape(7f))
                            .clickable {
                                if (inputCode.isNotBlank()) onSaveCode(inputCode)
                            }
                            .padding(vertical = 10.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(5.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Save,
                                contentDescription = null,
                                tint = CyberCyan,
                                modifier = Modifier.size(14.dp)
                            )
                            Text(
                                "COMMIT_KEY",
                                color = CyberCyan,
                                fontWeight = FontWeight.Bold,
                                fontSize = 10.sp,
                                fontFamily = FontFamily.Monospace
                            )
                        }
                    }
                }
            }
        }
    }
}

// ====================================================================
// LOGLAR TABI
// ====================================================================
@Composable
private fun LogsTab(
    logs: List<LogEntry>,
    onClearLogs: () -> Unit,
    onPingFirebase: () -> Unit
) {
    val clipboardManager = LocalClipboardManager.current
    val context = LocalContext.current

    Column(modifier = Modifier.fillMaxSize()) {
        // Asboblar paneli
        CyberCard(
            modifier = Modifier.fillMaxWidth(),
            borderColor = CyberBorder
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "SYS_LOGS // ${logs.size} ENTRIES",
                    fontSize = 10.sp,
                    color = CyberTextSecondary,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.SemiBold
                )
                Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    IconButton(onClick = onPingFirebase, modifier = Modifier.size(32.dp)) {
                        Icon(Icons.Default.Refresh, contentDescription = null, tint = CyberCyanDim, modifier = Modifier.size(16.dp))
                    }
                    IconButton(onClick = {
                        if (logs.isNotEmpty()) {
                            val fullLogText = logs.reversed().joinToString("\n") { e ->
                                "[${e.formattedTime}] [${e.level}] [${e.tag}] ${e.message}"
                            }
                            clipboardManager.setText(AnnotatedString(fullLogText))
                            Toast.makeText(context, "Loglar buferga nusxalandi", Toast.LENGTH_SHORT).show()
                        }
                    }, modifier = Modifier.size(32.dp)) {
                        Icon(Icons.Default.ContentCopy, contentDescription = null, tint = CyberCyan, modifier = Modifier.size(16.dp))
                    }
                    IconButton(onClick = onClearLogs, modifier = Modifier.size(32.dp)) {
                        Icon(Icons.Default.DeleteSweep, contentDescription = null, tint = CyberDanger, modifier = Modifier.size(16.dp))
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        if (logs.isEmpty()) {
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.AutoMirrored.Filled.Article, contentDescription = null, tint = CyberBorder, modifier = Modifier.size(44.dp))
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("NO_LOG_ENTRIES", color = CyberTextSecondary, fontSize = 11.sp, fontFamily = FontFamily.Monospace)
                }
            }
        } else {
            SelectionContainer(modifier = Modifier.weight(1f)) {
                LazyColumn(
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                    modifier = Modifier.fillMaxSize()
                ) {
                    items(logs) { entry -> LogItemCard(entry = entry) }
                }
            }
        }
    }
}

// ====================================================================
// LOG ITEM KARTA — terminal uslubida
// ====================================================================
@Composable
private fun LogItemCard(entry: LogEntry) {
    val (levelColor, levelLabel) = when (entry.level) {
        LogLevel.ERROR   -> Pair(CyberDanger,               "ERR")
        LogLevel.WARNING -> Pair(Color(0xFFF59E0B),          "WARN")
        LogLevel.SUCCESS -> Pair(CyberOnline,               "OK")
        LogLevel.INFO    -> Pair(CyberCyan,                 "INFO")
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(CyberSurface, RoundedCornerShape(4.dp))
            .border(0.5.dp, levelColor.copy(alpha = 0.3f), RoundedCornerShape(4.dp))
            .padding(8.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.Top
    ) {
        // Level indicator chiziq
        Box(
            modifier = Modifier
                .width(3.dp)
                .height(40.dp)
                .background(levelColor, RoundedCornerShape(2.dp))
        )
        Column(modifier = Modifier.weight(1f)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    // Level badge
                    Text(
                        text = "[$levelLabel]",
                        color = levelColor,
                        fontSize = 9.sp,
                        fontWeight = FontWeight.Bold,
                        fontFamily = FontFamily.Monospace
                    )
                    Text(
                        text = "[${entry.tag}]",
                        color = CyberTextSecondary,
                        fontSize = 9.sp,
                        fontFamily = FontFamily.Monospace
                    )
                }
                Text(
                    text = entry.formattedTime,
                    color = CyberTextSecondary.copy(alpha = 0.6f),
                    fontSize = 9.sp,
                    fontFamily = FontFamily.Monospace
                )
            }
            Spacer(modifier = Modifier.height(3.dp))
            Text(
                text = entry.message,
                color = CyberTextPrimary.copy(alpha = 0.85f),
                fontSize = 11.sp,
                lineHeight = 15.sp,
                fontFamily = FontFamily.Monospace
            )
        }
    }
}
