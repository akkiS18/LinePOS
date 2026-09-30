package uz.pos.electro.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat

// Line App Vibrant & Rich Brand Colors (Exact Logo Match)
val LinePrimary = Color(0xFF0B6477)          // Deep Ocean Blue
val LineOnPrimary = Color(0xFFFFFFFF)
val LinePrimaryContainer = Color(0xFFE0F2F1)  // Soft Mint/Cyan
val LineOnPrimaryContainer = Color(0xFF00363A)

val LineSecondary = Color(0xFF16A34A)        // Vibrant Emerald Green
val LineOnSecondary = Color(0xFFFFFFFF)
val LineSecondaryContainer = Color(0xFFDCFCE7)
val LineOnSecondaryContainer = Color(0xFF14532D)

// Light Palette
val LineBackground = Color(0xFFF2F5F8)       // iOS Style Soft Background
val LineSurface = Color(0xFFFFFFFF)          // 100% Pure White
val LineSurfaceVariant = Color(0xFFE2E8F0)   // Border & Subtitle
val LineOnSurface = Color(0xFF0F172A)        // Dark Crisp Text
val LineOnSurfaceVariant = Color(0xFF64748B) // Slate secondary text

val LineOutline = Color(0xFFCBD5E1)
val LineError = Color(0xFFEF4444)            // Apple Red
val LineErrorContainer = Color(0xFFFEE2E2)

// Dark Palette
val LineDarkBackground = Color(0xFF0F172A)
val LineDarkSurface = Color(0xFF1E293B)
val LineDarkSurfaceVariant = Color(0xFF334155)
val LineDarkOnSurface = Color(0xFFF8FAFC)
val LineDarkOnSurfaceVariant = Color(0xFF94A3B8)
val LineDarkPrimaryContainer = Color(0xFF134E5E)
val LineDarkOnPrimaryContainer = Color(0xFFCCFBF1)
val LineDarkOutline = Color(0xFF475569)

// Apple HIG Rounded Shapes
val AppleShapes = Shapes(
    extraSmall = RoundedCornerShape(12.dp),
    small = RoundedCornerShape(16.dp),
    medium = RoundedCornerShape(22.dp),
    large = RoundedCornerShape(28.dp),
    extraLarge = RoundedCornerShape(32.dp)
)

// Apple Typography (SF Pro style with clean SansSerif)
val AppleTypography = Typography(
    headlineLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 28.sp,
        lineHeight = 34.sp,
        letterSpacing = (-0.5).sp
    ),
    headlineMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 22.sp,
        lineHeight = 28.sp,
        letterSpacing = (-0.3).sp
    ),
    titleLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Bold,
        fontSize = 20.sp,
        lineHeight = 26.sp,
        letterSpacing = (-0.2).sp
    ),
    titleMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 17.sp,
        lineHeight = 22.sp,
        letterSpacing = (-0.1).sp
    ),
    titleSmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 15.sp,
        lineHeight = 20.sp
    ),
    bodyLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 22.sp
    ),
    bodyMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 14.sp,
        lineHeight = 18.sp
    ),
    bodySmall = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Normal,
        fontSize = 12.sp,
        lineHeight = 16.sp
    ),
    labelLarge = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.SemiBold,
        fontSize = 14.sp,
        lineHeight = 18.sp
    ),
    labelMedium = TextStyle(
        fontFamily = FontFamily.SansSerif,
        fontWeight = FontWeight.Medium,
        fontSize = 12.sp,
        lineHeight = 16.sp
    )
)

private val LightColorScheme = lightColorScheme(
    primary = LinePrimary,
    onPrimary = LineOnPrimary,
    primaryContainer = LinePrimaryContainer,
    onPrimaryContainer = LineOnPrimaryContainer,
    secondary = LineSecondary,
    onSecondary = LineOnSecondary,
    secondaryContainer = LineSecondaryContainer,
    onSecondaryContainer = LineOnSecondaryContainer,
    background = LineBackground,
    surface = LineSurface,
    surfaceVariant = LineSurfaceVariant,
    onSurface = LineOnSurface,
    onSurfaceVariant = LineOnSurfaceVariant,
    outline = LineOutline,
    error = LineError,
    errorContainer = LineErrorContainer
)

private val DarkColorScheme = darkColorScheme(
    primary = LinePrimary,
    onPrimary = LineOnPrimary,
    primaryContainer = LineDarkPrimaryContainer,
    onPrimaryContainer = LineDarkOnPrimaryContainer,
    secondary = LineSecondary,
    onSecondary = LineOnSecondary,
    secondaryContainer = LineSecondaryContainer,
    onSecondaryContainer = LineOnSecondaryContainer,
    background = LineDarkBackground,
    surface = LineDarkSurface,
    surfaceVariant = LineDarkSurfaceVariant,
    onSurface = LineDarkOnSurface,
    onSurfaceVariant = LineDarkOnSurfaceVariant,
    outline = LineDarkOutline,
    error = LineError,
    errorContainer = LineErrorContainer
)

@Composable
fun LinePOSTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColorScheme else LightColorScheme
    val view = LocalView.current

    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            // Light mode: Pure white status bar bg with dark/black icons
            // Dark mode: Dark status bar bg with white/light icons
            val statusBarBg = if (darkTheme) LineDarkBackground else Color.White
            val navBarBg = if (darkTheme) LineDarkBackground else Color.White

            window.statusBarColor = statusBarBg.toArgb()
            window.navigationBarColor = navBarBg.toArgb()

            val insetsController = WindowCompat.getInsetsController(window, view)
            insetsController.isAppearanceLightStatusBars = !darkTheme
            insetsController.isAppearanceLightNavigationBars = !darkTheme
        }
    }


    MaterialTheme(
        colorScheme = colorScheme,
        shapes = AppleShapes,
        typography = AppleTypography,
        content = content
    )
}
