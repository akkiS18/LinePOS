package uz.pos.admin.ui

import androidx.compose.foundation.Canvas
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.GenericShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import uz.pos.admin.ui.theme.StarkActiveBlue
import uz.pos.admin.ui.theme.StarkBorderCyan
import uz.pos.admin.ui.theme.StarkBorderCyanDim
import uz.pos.admin.ui.theme.StarkCardGlass
import uz.pos.admin.ui.theme.StarkCapsuleBg
import uz.pos.admin.ui.theme.StarkCapsuleBorder
import uz.pos.admin.ui.theme.StarkDangerRed
import uz.pos.admin.ui.theme.StarkGlowGold
import uz.pos.admin.ui.theme.StarkReactorBlue
import uz.pos.admin.ui.theme.StarkReactorCore
import uz.pos.admin.ui.theme.StarkTextDim
import uz.pos.admin.ui.theme.StarkTextMuted
import uz.pos.admin.ui.theme.StarkTextWhite

// ================================================================
// SEB — STARK TECH / HUD GLASSMORPHISM COMPONENTS
// ================================================================

/**
 * Rasmdagi 1:1 Stark Tech shaffof shisha karta (Glass Card)
 * - O'ng yuqori burchagi va chap pastki burchagi 45 gradus qiya kesilgan
 * - Yuqori va yon chegaralarida neon moviy (Cyan) nur
 * - Pastki qismida nozik iliq oltin/sariq nur (warm arc glow)
 */
@Composable
fun StarkGlassCard(
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit
) {
    val cutSize = 28f
    val shape = starkCardShape(cutSize)

    Box(
        modifier = modifier
            .background(
                Brush.verticalGradient(
                    colors = listOf(
                        Color(0xEE0B1E33),
                        Color(0xE6081525)
                    )
                ),
                shape
            )
            .drawBehind {
                val w = size.width
                val h = size.height

                // Asosiy moviy ramka
                val borderPath = starkCardPath(w, h, cutSize)
                drawPath(
                    path = borderPath,
                    brush = Brush.verticalGradient(
                        colors = listOf(
                            StarkBorderCyan.copy(alpha = 0.9f),
                            StarkBorderCyanDim.copy(alpha = 0.4f),
                            StarkGlowGold.copy(alpha = 0.8f) // Pastki chegarada oltin nur
                        )
                    ),
                    style = Stroke(width = 1.6.dp.toPx())
                )

                // Pastki nozik oltin aksent chizig'i
                drawLine(
                    brush = Brush.horizontalGradient(
                        listOf(
                            Color.Transparent,
                            StarkGlowGold.copy(alpha = 0.85f),
                            Color.Transparent
                        )
                    ),
                    start = Offset(w * 0.2f, h - 1.5.dp.toPx()),
                    end = Offset(w * 0.85f, h - 1.5.dp.toPx()),
                    strokeWidth = 2.dp.toPx()
                )
            }
    ) {
        content()
    }
}

/**
 * Rasmdagi Iron Man "Arc Reactor" Bloklash / Faollashtirish kapsula tugmasi
 */
@Composable
fun StarkReactorButton(
    isActivated: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val textColor = if (isActivated) StarkDangerRed else StarkActiveBlue
    val actionText = if (isActivated) "Bloklash" else "Faollashtirish"
    val reactorTint = if (isActivated) StarkReactorBlue else Color(0xFF64748B)

    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
        modifier = modifier
            .background(StarkCapsuleBg, RoundedCornerShape(8.dp))
            .border(
                1.dp,
                Brush.horizontalGradient(
                    listOf(StarkCapsuleBorder, StarkBorderCyanDim.copy(alpha = 0.6f))
                ),
                RoundedCornerShape(8.dp)
            )
            .clickable { onClick() }
            .padding(horizontal = 10.dp, vertical = 6.dp)
    ) {
        // Chapda: Arc Reactor nuri
        ArcReactorIcon(
            tint = reactorTint,
            modifier = Modifier.size(24.dp)
        )

        Spacer(modifier = Modifier.width(8.dp))

        // O'rtada: Bloklash / Faollashtirish yozuvi
        Text(
            text = actionText,
            color = textColor,
            fontSize = 13.sp,
            fontWeight = FontWeight.Bold,
            fontFamily = FontFamily.SansSerif
        )

        Spacer(modifier = Modifier.width(10.dp))

        // O'ngda: Kichik S (Seb) ramzi
        Text(
            text = "S",
            color = StarkBorderCyanDim.copy(alpha = 0.7f),
            fontSize = 11.sp,
            fontWeight = FontWeight.ExtraBold,
            fontFamily = FontFamily.Monospace
        )
    }
}

/**
 * Iron Man Arc Reactor konsentrik doiralar nuri
 */
@Composable
fun ArcReactorIcon(
    tint: Color,
    modifier: Modifier = Modifier
) {
    Canvas(modifier = modifier) {
        val center = Offset(size.width / 2, size.height / 2)
        val radius = size.minDimension / 2

        // Tashqi aylana
        drawCircle(
            color = tint.copy(alpha = 0.8f),
            radius = radius * 0.9f,
            center = center,
            style = Stroke(width = 1.5.dp.toPx())
        )

        // Ichki aylana
        drawCircle(
            color = tint.copy(alpha = 0.6f),
            radius = radius * 0.6f,
            center = center,
            style = Stroke(width = 1.2.dp.toPx())
        )

        // Markaziy yorug'lik
        drawCircle(
            color = StarkReactorCore,
            radius = radius * 0.28f,
            center = center
        )

        // 3 ta radial chiziqlar (reaktor segmentlari)
        for (i in 0..2) {
            val angle = Math.toRadians((i * 120.0))
            val start = Offset(
                center.x + (radius * 0.35f * Math.cos(angle)).toFloat(),
                center.y + (radius * 0.35f * Math.sin(angle)).toFloat()
            )
            val end = Offset(
                center.x + (radius * 0.85f * Math.cos(angle)).toFloat(),
                center.y + (radius * 0.85f * Math.sin(angle)).toFloat()
            )
            drawLine(
                color = tint.copy(alpha = 0.8f),
                start = start,
                end = end,
                strokeWidth = 1.2.dp.toPx()
            )
        }
    }
}

/**
 * Rasmdagi karta shakli:
 * - O'ng yuqori burchagi qiya kesilgan (chamfer)
 * - Qolgan burchaklari silliq
 */
fun starkCardShape(cut: Float = 28f) = GenericShape { size, _ ->
    addPath(starkCardPath(size.width, size.height, cut))
}

fun starkCardPath(w: Float, h: Float, cut: Float): Path {
    val r = 16f // Yumaloq burchak radiusi
    return Path().apply {
        moveTo(r, 0f)
        lineTo(w - cut, 0f)      // Yuqori chiziq
        lineTo(w, cut)           // O'ng yuqori qiya kesish (45 gradus)
        lineTo(w, h - r)         // O'ng tomon
        lineTo(w - r, h)         // O'ng past
        lineTo(cut, h)           // Pastki chiziq
        lineTo(0f, h - cut)      // Chap pastki qiya kesish
        lineTo(0f, r)            // Chap tomon
        close()
    }
}

// ================================================================
// MOSLIK UCHUN ESKI KOMPONENTLAR (LEGACY)
// ================================================================

@Composable
fun CyberCard(
    modifier: Modifier = Modifier,
    borderColor: Color = StarkBorderCyan,
    cornerCut: Float = 20f,
    content: @Composable () -> Unit
) {
    StarkGlassCard(modifier = modifier, content = content)
}

@Composable
fun CyberBadge(
    label: String,
    sublabel: String = "",
    color: Color = StarkBorderCyan,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
            .background(color.copy(alpha = 0.12f), RoundedCornerShape(3.dp))
            .border(0.8.dp, color.copy(alpha = 0.5f), RoundedCornerShape(3.dp))
            .padding(horizontal = 8.dp, vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(5.dp)
    ) {
        Box(
            modifier = Modifier
                .size(5.dp)
                .background(color, CircleShape)
        )
        Text(
            text = label,
            color = color,
            fontSize = 10.sp,
            fontWeight = FontWeight.Bold,
            fontFamily = FontFamily.Monospace
        )
    }
}

@Composable
fun CyberSectionHeader(
    label: String,
    modifier: Modifier = Modifier,
    color: Color = StarkBorderCyanDim
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Box(
            modifier = Modifier
                .width(3.dp)
                .height(12.dp)
                .background(StarkBorderCyan)
        )
        Text(
            text = label,
            color = StarkTextMuted,
            fontSize = 10.sp,
            fontWeight = FontWeight.Bold,
            fontFamily = FontFamily.Monospace,
            letterSpacing = 1.5.sp
        )
        Box(
            modifier = Modifier
                .weight(1f)
                .height(0.6.dp)
                .background(
                    Brush.horizontalGradient(
                        listOf(StarkBorderCyanDim.copy(alpha = 0.4f), Color.Transparent)
                    )
                )
        )
    }
}

@Composable
fun CyberStatTile(
    label: String,
    value: String,
    color: Color = StarkBorderCyan,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .background(StarkCardGlass, RoundedCornerShape(10.dp))
            .border(0.8.dp, StarkBorderCyanDim.copy(alpha = 0.4f), RoundedCornerShape(10.dp))
            .padding(10.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text(
                text = value,
                fontSize = 20.sp,
                fontWeight = FontWeight.Bold,
                color = color,
                fontFamily = FontFamily.Monospace
            )
            Text(
                text = label,
                fontSize = 9.sp,
                color = StarkTextMuted,
                fontFamily = FontFamily.Monospace
            )
        }
    }
}

@Composable
fun CyberDivider(
    modifier: Modifier = Modifier,
    color: Color = StarkBorderCyanDim
) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(0.6.dp)
            .background(
                Brush.horizontalGradient(
                    listOf(Color.Transparent, color.copy(alpha = 0.4f), Color.Transparent)
                )
            )
    )
}

fun chamferedShape(cut: Float = 16f) = GenericShape { size, _ ->
    val w = size.width
    val h = size.height
    moveTo(cut, 0f)
    lineTo(w - cut, 0f)
    lineTo(w, cut)
    lineTo(w, h - cut)
    lineTo(w - cut, h)
    lineTo(cut, h)
    lineTo(0f, h - cut)
    lineTo(0f, cut)
    close()
}
