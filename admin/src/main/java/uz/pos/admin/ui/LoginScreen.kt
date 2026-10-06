package uz.pos.admin.ui

import androidx.compose.animation.AnimatedVisibility
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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Backspace
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.material3.Icon
import uz.pos.admin.R
import uz.pos.admin.ui.theme.CyberBg
import uz.pos.admin.ui.theme.CyberBorder
import uz.pos.admin.ui.theme.CyberCyan
import uz.pos.admin.ui.theme.CyberCyanAlpha
import uz.pos.admin.ui.theme.CyberCyanDim
import uz.pos.admin.ui.theme.CyberDanger
import uz.pos.admin.ui.theme.CyberSurface
import uz.pos.admin.ui.theme.CyberSurface2
import uz.pos.admin.ui.theme.CyberTextPrimary
import uz.pos.admin.ui.theme.CyberTextSecondary

@Composable
fun LoginScreen(
    onLoginSuccess: () -> Unit
) {
    var pin by remember { mutableStateOf("") }
    var errorMessage by remember { mutableStateOf("") }

    val adminMasterPin = "2846"

    fun onDigitClick(digit: String) {
        if (pin.length < 4) {
            val newPin = pin + digit
            pin = newPin
            errorMessage = ""
            if (newPin.length == 4) {
                if (newPin == adminMasterPin) {
                    onLoginSuccess()
                } else {
                    errorMessage = "KIRISH RAD ETILDI // ACCESS DENIED"
                    pin = ""
                }
            }
        }
    }

    fun onBackspaceClick() {
        if (pin.isNotEmpty()) {
            pin = pin.dropLast(1)
            errorMessage = ""
        }
    }

    // ===== ASOSIY FON =====
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.radialGradient(
                    colors = listOf(Color(0xFF0A1825), CyberBg),
                    radius = 1200f
                )
            )
            .padding(24.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
            modifier = Modifier.fillMaxWidth()
        ) {

            // ===== TEPA SARLAVHA CHIZIQLARI (HUD corner decorators) =====
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                // Chap yuqori burchak qavs
                Box(
                    modifier = Modifier
                        .width(24.dp)
                        .height(2.dp)
                        .background(CyberCyan)
                )
                Box(
                    modifier = Modifier
                        .width(2.dp)
                        .height(24.dp)
                        .background(CyberCyan)
                        .align(Alignment.Top)
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ===== SEB LOGOSI =====
            Box(
                modifier = Modifier
                    .size(96.dp)
                    .background(CyberBg, chamferedShape(20f))
                    .border(1.5.dp, Brush.linearGradient(listOf(CyberCyan, CyberCyanDim)), chamferedShape(20f)),
                contentAlignment = Alignment.Center
            ) {
                Image(
                    painter = painterResource(id = R.drawable.seb_logo),
                    contentDescription = "Seb",
                    modifier = Modifier
                        .size(84.dp)
                        .clip(chamferedShape(16f))
                )
            }

            Spacer(modifier = Modifier.height(18.dp))

            // ===== ASOSIY SARLAVHA =====
            Text(
                text = "SEBASTIAN",
                fontSize = 22.sp,
                fontWeight = FontWeight.ExtraBold,
                color = CyberCyan,
                fontFamily = FontFamily.Monospace,
                letterSpacing = 5.sp
            )

            Spacer(modifier = Modifier.height(4.dp))

            // Quyi tag
            CyberBadge(
                label = "CORE ACCESS REQUIRED",
                sublabel = "AUTHENTICATE TO PROCEED",
                color = CyberCyanDim
            )

            Spacer(modifier = Modifier.height(28.dp))

            // ===== PIN INDIKATORLARI (HUD bloklar) =====
            CyberSectionHeader(label = "ENTER ACCESS CODE", color = CyberCyanDim)
            Spacer(modifier = Modifier.height(14.dp))

            Row(
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                repeat(4) { index ->
                    val filled = index < pin.length
                    Box(
                        modifier = Modifier
                            .size(width = 42.dp, height = 50.dp)
                            .background(
                                if (filled) CyberCyanAlpha else CyberSurface,
                                chamferedShape(8f)
                            )
                            .border(
                                width = if (filled) 1.5.dp else 0.8.dp,
                                color = if (filled) CyberCyan else CyberBorder,
                                shape = chamferedShape(8f)
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        if (filled) {
                            Box(
                                modifier = Modifier
                                    .size(10.dp)
                                    .background(CyberCyan, chamferedShape(3f))
                            )
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ===== XATO XABARI =====
            AnimatedVisibility(visible = errorMessage.isNotEmpty()) {
                CyberCard(
                    borderColor = CyberDanger,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.Center
                    ) {
                        Box(
                            modifier = Modifier
                                .size(6.dp)
                                .background(CyberDanger)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = errorMessage,
                            color = CyberDanger,
                            fontSize = 10.sp,
                            fontWeight = FontWeight.Bold,
                            fontFamily = FontFamily.Monospace,
                            letterSpacing = 1.sp
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(28.dp))

            // ===== KIBERPANK NUMPAD KLAVIATURA =====
            val rows = listOf(
                listOf("1", "2", "3"),
                listOf("4", "5", "6"),
                listOf("7", "8", "9"),
                listOf("CLR", "0", "DEL")
            )

            Column(
                verticalArrangement = Arrangement.spacedBy(10.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                for (row in rows) {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(14.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        for (key in row) {
                            when (key) {
                                "DEL" -> CyberKeypadButton(
                                    onClick = { onBackspaceClick() }
                                ) {
                                    Icon(
                                        imageVector = Icons.AutoMirrored.Filled.Backspace,
                                        contentDescription = "Del",
                                        tint = CyberCyanDim,
                                        modifier = Modifier.size(22.dp)
                                    )
                                }
                                "CLR" -> CyberKeypadButton(
                                    onClick = { pin = ""; errorMessage = "" },
                                    borderColor = CyberDanger.copy(alpha = 0.5f)
                                ) {
                                    Text(
                                        text = "CLR",
                                        fontSize = 12.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = CyberDanger,
                                        fontFamily = FontFamily.Monospace
                                    )
                                }
                                else -> CyberKeypadButton(onClick = { onDigitClick(key) }) {
                                    Text(
                                        text = key,
                                        fontSize = 22.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = CyberTextPrimary,
                                        fontFamily = FontFamily.Monospace
                                    )
                                }
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            // ===== PASTKI HUD DEKOR =====
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Box(
                    modifier = Modifier
                        .width(24.dp)
                        .height(2.dp)
                        .background(CyberCyanDim.copy(alpha = 0.5f))
                )
                Text(
                    text = "SEB // v2.0",
                    color = CyberTextSecondary,
                    fontSize = 9.sp,
                    fontFamily = FontFamily.Monospace,
                    letterSpacing = 1.sp
                )
                Box(
                    modifier = Modifier
                        .width(24.dp)
                        .height(2.dp)
                        .background(CyberCyanDim.copy(alpha = 0.5f))
                )
            }
        }
    }
}

@Composable
private fun CyberKeypadButton(
    onClick: () -> Unit,
    borderColor: Color = CyberCyanDim.copy(alpha = 0.5f),
    content: @Composable () -> Unit
) {
    Box(
        contentAlignment = Alignment.Center,
        modifier = Modifier
            .size(68.dp)
            .background(CyberSurface2, chamferedShape(12f))
            .border(1.dp, borderColor, chamferedShape(12f))
            .clickable { onClick() }
    ) {
        content()
    }
}
