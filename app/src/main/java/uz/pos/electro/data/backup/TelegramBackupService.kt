package uz.pos.electro.data.backup

import android.os.Build
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import org.json.JSONObject
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit

data class TelegramUserInfo(
    val chatId: String,
    val name: String,
    val username: String?
)

object TelegramBackupService {

    private val TASHKENT_ZONE = TimeZone.getTimeZone("Asia/Tashkent")

    private val client: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .connectTimeout(30, TimeUnit.SECONDS)
            .writeTimeout(120, TimeUnit.SECONDS)
            .readTimeout(60, TimeUnit.SECONDS)
            .retryOnConnectionFailure(true)
            .build()
    }

    private fun getTashkentDateFormatter(pattern: String = "dd.MM.yyyy HH:mm"): SimpleDateFormat {
        return SimpleDateFormat(pattern, Locale.getDefault()).apply {
            timeZone = TASHKENT_ZONE
        }
    }

    /**
     * Telegram botga yuborilgan oxirgi xabarni o'qib, foydalanuvchining Chat ID sini avtomatik aniqlash
     */
    suspend fun fetchLatestChat(botToken: String): Result<TelegramUserInfo> = withContext(Dispatchers.IO) {
        runCatching {
            if (botToken.isBlank()) throw IllegalArgumentException("Bot Token kiritilmagan!")

            val url = "https://api.telegram.org/bot$botToken/getUpdates?limit=20"
            val request = Request.Builder().url(url).get().build()
            val response = client.newCall(request).execute()
            val body = response.body?.string() ?: ""

            if (!response.isSuccessful) {
                val err = try {
                    JSONObject(body).optString("description", "HTTP ${response.code}")
                } catch (_: Throwable) {
                    "HTTP ${response.code}"
                }
                throw Exception("Telegram server xatosi: $err")
            }

            val json = JSONObject(body)
            if (!json.optBoolean("ok", false)) {
                throw Exception(json.optString("description", "Noma'lum xatolik"))
            }

            val results = json.optJSONArray("result")
            if (results == null || results.length() == 0) {
                throw Exception("Botda hali START bosilmadi. Iltimos, Telegramda @SmartKassaBackupBot ga kirib 'START' tugmasini bosing va qaytib keling.")
            }

            var foundInfo: TelegramUserInfo? = null
            var lastUpdateId = 0L

            // Eng oxirgi yuborilgan xabardan boshlab qidiramiz
            for (i in results.length() - 1 downTo 0) {
                val update = results.optJSONObject(i) ?: continue
                val updateId = update.optLong("update_id", 0L)
                if (updateId > lastUpdateId) lastUpdateId = updateId

                val message = update.optJSONObject("message")
                    ?: update.optJSONObject("channel_post")
                    ?: update.optJSONObject("my_chat_member")?.optJSONObject("chat")
                
                val chat = if (message?.has("chat") == true) message.optJSONObject("chat") else message
                if (chat != null) {
                    val chatId = chat.optLong("id", 0L)
                    if (chatId != 0L) {
                        val from = update.optJSONObject("message")?.optJSONObject("from")
                        val firstName = from?.optString("first_name", "")?.ifBlank { null }
                            ?: chat.optString("first_name", "")
                        val username = from?.optString("username", "")?.ifBlank { null }
                            ?: chat.optString("username", "")

                        val displayName = firstName.ifBlank {
                            chat.optString("title", "Foydalanuvchi")
                        }

                        foundInfo = TelegramUserInfo(
                            chatId = chatId.toString(),
                            name = displayName,
                            username = username.ifBlank { null }
                        )
                        break
                    }
                }
            }

            // O'qilgan yangilanishlarni tasdiqlash (offset)
            if (lastUpdateId > 0L) {
                try {
                    val ackUrl = "https://api.telegram.org/bot$botToken/getUpdates?offset=${lastUpdateId + 1}&limit=1"
                    client.newCall(Request.Builder().url(ackUrl).get().build()).execute()
                } catch (_: Throwable) {}
            }

            foundInfo ?: throw Exception("Telegramdan chat topilmadi. Botga qaytadan START bosing.")
        }
    }

    /**
     * Telegramga oddiy matnli (HTML formatda) xabar yuborish
     */
    suspend fun sendTextMessage(
        botToken: String,
        chatId: String,
        messageHtml: String
    ): Result<String> = withContext(Dispatchers.IO) {
        runCatching {
            if (botToken.isBlank()) throw IllegalArgumentException("Bot Token kiritilmagan!")
            if (chatId.isBlank()) throw IllegalArgumentException("Chat ID kiritilmagan!")

            val url = "https://api.telegram.org/bot$botToken/sendMessage"
            val formBody = MultipartBody.Builder()
                .setType(MultipartBody.FORM)
                .addFormDataPart("chat_id", chatId.trim())
                .addFormDataPart("text", messageHtml)
                .addFormDataPart("parse_mode", "HTML")
                .build()

            val request = Request.Builder()
                .url(url)
                .post(formBody)
                .build()

            val response = client.newCall(request).execute()
            val body = response.body?.string() ?: ""

            if (!response.isSuccessful) {
                val errorMsg = try {
                    JSONObject(body).optString("description", "Server xatosi: ${response.code}")
                } catch (_: Throwable) {
                    "Xatolik: HTTP ${response.code}"
                }
                throw Exception(errorMsg)
            }

            val json = JSONObject(body)
            if (!json.optBoolean("ok", false)) {
                throw Exception(json.optString("description", "Noma'lum xatolik"))
            }

            "Xabar yuborildi"
        }
    }

    /**
     * Telegramga sinov xabarini yuborish (ulanish to'g'riligini tekshirish)
     */
    suspend fun sendTestMessage(
        botToken: String,
        chatId: String,
        storeName: String = "SMART Kassa"
    ): Result<String> = withContext(Dispatchers.IO) {
        val timeStr = getTashkentDateFormatter().format(Date())
        val message = """
            🎉 <b>SMART Kassa — Telegram Xabarnoma Muvaffaqiyatli Ulandi!</b>
            
            🏪 <b>Kassa:</b> $storeName (${Build.MODEL})
            📅 <b>Toshkent vaqti:</b> $timeStr (UTC+5)
            
            ✅ Ushbu chatga belgilangan vaqtda kassa bazasining xavfsiz zaxira nusxasi avtomatik tarzda kelib turadi.
        """.trimIndent()

        sendTextMessage(botToken, chatId, message).map {
            "Sinov xabari Telegramga muvaffaqiyatli yuborildi! 🚀"
        }
    }

    /**
     * Baza .db faylini Telegramga yuborish
     */
    suspend fun sendDatabaseBackup(
        botToken: String,
        chatId: String,
        databaseFile: File,
        storeName: String = "SMART Kassa",
        extraInfo: String? = null
    ): Result<String> = withContext(Dispatchers.IO) {
        runCatching {
            if (botToken.isBlank()) throw IllegalArgumentException("Bot Token kiritilmagan!")
            if (chatId.isBlank()) throw IllegalArgumentException("Chat ID kiritilmagan!")
            if (!databaseFile.exists() || databaseFile.length() == 0L) {
                throw IllegalArgumentException("Baza fayli topilmadi yoki bo'sh!")
            }

            val timeStr = getTashkentDateFormatter().format(Date())
            val sizeMb = String.format(Locale.US, "%.2f", databaseFile.length() / (1024.0 * 1024.0))

            val caption = buildString {
                append("📦 <b>SMART Kassa — Kunlik Baza Zaxirasi</b>\n\n")
                append("🏪 <b>Kassa:</b> $storeName (${Build.MODEL})\n")
                append("📅 <b>Vaqt (Toshkent):</b> $timeStr (UTC+5)\n")
                append("📁 <b>Hajmi:</b> $sizeMb MB\n")
                if (!extraInfo.isNullOrBlank()) {
                    append("📊 $extraInfo\n")
                }
                append("\n🛡️ <i>Ma'lumotlar xavfsiz saqlandi.</i>")
            }

            val url = "https://api.telegram.org/bot$botToken/sendDocument"
            val fileBody = databaseFile.asRequestBody("application/octet-stream".toMediaTypeOrNull())

            val multipartBody = MultipartBody.Builder()
                .setType(MultipartBody.FORM)
                .addFormDataPart("chat_id", chatId.trim())
                .addFormDataPart("caption", caption)
                .addFormDataPart("parse_mode", "HTML")
                .addFormDataPart("document", databaseFile.name, fileBody)
                .build()

            val request = Request.Builder()
                .url(url)
                .post(multipartBody)
                .build()

            val response = client.newCall(request).execute()
            val body = response.body?.string() ?: ""

            if (!response.isSuccessful) {
                val errorMsg = try {
                    JSONObject(body).optString("description", "Server xatosi: ${response.code}")
                } catch (_: Throwable) {
                    "Xatolik: HTTP ${response.code}"
                }
                throw Exception(errorMsg)
            }

            val json = JSONObject(body)
            if (!json.optBoolean("ok", false)) {
                throw Exception(json.optString("description", "Noma'lum xatolik"))
            }

            "Baza zaxirasi Telegramga muvaffaqiyatli yuklandi!"
        }
    }
}
