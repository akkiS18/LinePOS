package uz.pos.electro.data.backup

import android.content.Context
import android.content.SharedPreferences

object BackupPreferences {
    private const val PREF_NAME = "smart_pos_backup_prefs"

    // Rasmiy @SmartKassaBackupBot
    const val DEFAULT_BOT_USERNAME = "SmartKassaBackupBot"
    const val DEFAULT_BOT_TOKEN = "8775476456:AAGo8ZgFbprnem7aWedIqp7QWpj9M0ExA9o"

    fun getBotDeepLink(): String = "https://t.me/$DEFAULT_BOT_USERNAME"

    private const val KEY_BOT_TOKEN = "telegram_bot_token"
    private const val KEY_CHAT_ID = "telegram_chat_id"
    private const val KEY_CHAT_NAME = "telegram_chat_name"
    private const val KEY_AUTO_BACKUP_ENABLED = "auto_backup_enabled"
    private const val KEY_BACKUP_HOUR = "backup_hour" // 0..23 (default 23:00)
    private const val KEY_BACKUP_MINUTE = "backup_minute" // 0..59 (default 00)
    private const val KEY_LAST_BACKUP_TIME = "last_backup_time"
    private const val KEY_LAST_BACKUP_STATUS = "last_backup_status"
    private const val KEY_LAST_BACKUP_FILE = "last_backup_file"
    private const val KEY_LAST_BACKUP_SALES_COUNT = "last_backup_sales_count"

    private fun getPrefs(context: Context): SharedPreferences {
        return context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE)
    }

    fun getChatId(context: Context): String {
        return getPrefs(context).getString(KEY_CHAT_ID, "") ?: ""
    }

    fun setChatId(context: Context, chatId: String) {
        getPrefs(context).edit().putString(KEY_CHAT_ID, chatId.trim()).apply()
    }

    fun getChatName(context: Context): String {
        return getPrefs(context).getString(KEY_CHAT_NAME, "") ?: ""
    }

    fun setChatName(context: Context, name: String) {
        getPrefs(context).edit().putString(KEY_CHAT_NAME, name.trim()).apply()
    }

    fun getBotToken(context: Context): String {
        val custom = getPrefs(context).getString(KEY_BOT_TOKEN, "") ?: ""
        return if (custom.isNotBlank()) custom.trim() else DEFAULT_BOT_TOKEN
    }

    fun getCustomBotToken(context: Context): String {
        return getPrefs(context).getString(KEY_BOT_TOKEN, "") ?: ""
    }

    fun setCustomBotToken(context: Context, token: String) {
        getPrefs(context).edit().putString(KEY_BOT_TOKEN, token.trim()).apply()
    }

    fun isAutoBackupEnabled(context: Context): Boolean {
        return getPrefs(context).getBoolean(KEY_AUTO_BACKUP_ENABLED, false)
    }

    fun setAutoBackupEnabled(context: Context, enabled: Boolean) {
        getPrefs(context).edit().putBoolean(KEY_AUTO_BACKUP_ENABLED, enabled).apply()
    }

    fun getBackupHour(context: Context): Int {
        return getPrefs(context).getInt(KEY_BACKUP_HOUR, 23)
    }

    fun getBackupMinute(context: Context): Int {
        return getPrefs(context).getInt(KEY_BACKUP_MINUTE, 0)
    }

    fun setBackupTime(context: Context, hour: Int, minute: Int) {
        getPrefs(context).edit()
            .putInt(KEY_BACKUP_HOUR, hour.coerceIn(0, 23))
            .putInt(KEY_BACKUP_MINUTE, minute.coerceIn(0, 59))
            .apply()
    }

    fun getLastBackupTime(context: Context): Long {
        return getPrefs(context).getLong(KEY_LAST_BACKUP_TIME, 0L)
    }

    fun getLastBackupStatus(context: Context): String {
        return getPrefs(context).getString(KEY_LAST_BACKUP_STATUS, "Hali zaxira olinmagan") ?: ""
    }

    fun getLastBackupFile(context: Context): String {
        return getPrefs(context).getString(KEY_LAST_BACKUP_FILE, "") ?: ""
    }

    fun getLastBackupSalesCount(context: Context): Int {
        return getPrefs(context).getInt(KEY_LAST_BACKUP_SALES_COUNT, -1)
    }

    fun setLastBackupSalesCount(context: Context, count: Int) {
        getPrefs(context).edit().putInt(KEY_LAST_BACKUP_SALES_COUNT, count).apply()
    }

    fun recordBackupSuccess(context: Context, fileName: String, salesCount: Int = -1) {
        val now = System.currentTimeMillis()
        val editor = getPrefs(context).edit()
            .putLong(KEY_LAST_BACKUP_TIME, now)
            .putString(KEY_LAST_BACKUP_STATUS, "Muvaffaqiyatli saqlandi ✅")
            .putString(KEY_LAST_BACKUP_FILE, fileName)
        if (salesCount >= 0) {
            editor.putInt(KEY_LAST_BACKUP_SALES_COUNT, salesCount)
        }
        editor.apply()
    }

    fun recordBackupFailure(context: Context, errorMessage: String) {
        getPrefs(context).edit()
            .putString(KEY_LAST_BACKUP_STATUS, "Xatolik: $errorMessage ⚠️")
            .apply()
    }

    fun recordNoSalesBackup(context: Context, currentSalesCount: Int) {
        val now = System.currentTimeMillis()
        getPrefs(context).edit()
            .putLong(KEY_LAST_BACKUP_TIME, now)
            .putString(KEY_LAST_BACKUP_STATUS, "Bugun yangi savdo bo'lmadi (o'tkazildi) ℹ️")
            .putInt(KEY_LAST_BACKUP_SALES_COUNT, currentSalesCount)
            .apply()
    }
}
