package uz.pos.admin.ui.theme

import androidx.compose.ui.graphics.Color

// ====== SEB (STARK TECH / HUD GLASSMORPHISM) COLOR PALETTE ======
// Yashil rang butunlay olib tashlangan, faqat moviy shisha, oltin aksent va qora fon

// Asosiy fon ranglari
val StarkBg             = Color(0xFF040913)   // Chuqur kosmik qora-moviy fon
val StarkCardGlass      = Color(0xD90D1F33)   // Yarim shaffof moviy shisha (Glassmorphism)
val StarkCardSurface    = Color(0xFF0F253D)   // Karta ichki foni
val StarkBorderCyan     = Color(0xFF38BDF8)   // Yuqori va yon neon moviy hoshiya
val StarkBorderCyanDim  = Color(0xFF0284C7)   // Moviy oraliq hoshiya
val StarkGlowGold       = Color(0xFFF59E0B)   // Pastki nozik oltin/sariq nur aksenti (rasmdagi kabi)
val StarkDotActive      = Color(0xFFFBBF24)   // Oltin-sariq status nuqtasi

// Arc Reactor va tugma ranglari
val StarkReactorBlue    = Color(0xFF38BDF8)   // Arc reactor neon nuri
val StarkReactorCore    = Color(0xFFE0F2FE)   // Reaktor markaziy yorug'ligi
val StarkCapsuleBg      = Color(0xFF081422)   // Tugma kapsula korpusi
val StarkCapsuleBorder  = Color(0xFF1E3A5F)   // Kapsula hoshiyasi
val StarkDangerRed      = Color(0xFFF87171)   // Bloklash matni (yumshoq qizg'ish)
val StarkActiveBlue     = Color(0xFF38BDF8)   // Faollashtirish matni

// Matn ranglari
val StarkTextWhite      = Color(0xFFF8FAFC)   // Asosiy oq matn
val StarkTextMuted      = Color(0xFF94A3B8)   // Kulrang texnik matn
val StarkTextDim        = Color(0xFF64748B)   // ID va mayda matnlar

// Eski moslik uchun (legacy aliases)
val CyberBg             = StarkBg
val CyberSurface        = StarkCardGlass
val CyberSurface2       = StarkCardSurface
val CyberBorder         = StarkBorderCyanDim
val CyberCyan           = StarkBorderCyan
val CyberCyanDim        = StarkBorderCyanDim
val CyberCyanAlpha      = Color(0x2238BDF8)
val CyberDanger         = StarkDangerRed
val CyberDangerAlpha    = Color(0x22F87171)
val CyberOnline         = StarkBorderCyan
val CyberOffline        = StarkTextMuted
val CyberTextPrimary    = StarkTextWhite
val CyberTextSecondary  = StarkTextMuted
val CyberTextMono       = StarkBorderCyan
val CyberDivider        = StarkBorderCyanDim
