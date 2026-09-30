package uz.pos.admin.util

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

data class LogEntry(
    val timestamp: Long = System.currentTimeMillis(),
    val level: LogLevel = LogLevel.INFO,
    val tag: String,
    val message: String
) {
    val formattedTime: String
        get() = SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date(timestamp))
}

enum class LogLevel {
    INFO,
    SUCCESS,
    WARNING,
    ERROR
}

object AdminLogger {
    private val _logs = MutableStateFlow<List<LogEntry>>(emptyList())
    val logs: StateFlow<List<LogEntry>> = _logs.asStateFlow()

    init {
        i("TIZIM", "Admin Panel ishga tushirildi")
    }

    fun log(tag: String, message: String, level: LogLevel = LogLevel.INFO) {
        val entry = LogEntry(tag = tag, message = message, level = level)
        // Eng yangi loglar tepada bo'ladi, 300 tagacha saqlanadi
        _logs.value = listOf(entry) + _logs.value.take(300)
        when (level) {
            LogLevel.ERROR -> android.util.Log.e("AdminPOS", "[$tag] $message")
            LogLevel.WARNING -> android.util.Log.w("AdminPOS", "[$tag] $message")
            else -> android.util.Log.d("AdminPOS", "[$tag] $message")
        }
    }

    fun i(tag: String, message: String) = log(tag, message, LogLevel.INFO)
    fun s(tag: String, message: String) = log(tag, message, LogLevel.SUCCESS)
    fun w(tag: String, message: String) = log(tag, message, LogLevel.WARNING)
    fun e(tag: String, message: String, throwable: Throwable? = null) {
        val fullMsg = if (throwable != null) "$message (${throwable.localizedMessage})" else message
        log(tag, fullMsg, LogLevel.ERROR)
    }

    fun clear() {
        _logs.value = emptyList()
        i("TIZIM", "Loglar tozalandi")
    }
}
