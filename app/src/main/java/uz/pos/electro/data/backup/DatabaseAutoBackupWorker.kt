package uz.pos.electro.data.backup

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.SaleDao
import uz.pos.electro.util.DatabaseBackupExporter
import java.io.File
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit

class DatabaseAutoBackupWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(appContext, workerParams) {

    @EntryPoint
    @InstallIn(SingletonComponent::class)
    interface AutoBackupEntryPoint {
        fun appDatabase(): AppDatabase
        fun saleDao(): SaleDao
    }

    override suspend fun doWork(): Result {
        val context = applicationContext

        // 1. Agar avtomatik zaxira o'chiq bo'lsa yoki Chat ID kiritilmagan bo'lsa, to'xtatamiz
        if (!BackupPreferences.isAutoBackupEnabled(context)) {
            return Result.success()
        }

        val chatId = BackupPreferences.getChatId(context)
        val botToken = BackupPreferences.getBotToken(context)

        if (chatId.isBlank() || botToken.isBlank()) {
            BackupPreferences.recordBackupFailure(context, "Chat ID yoki Bot Token kiritilmagan")
            return Result.success()
        }

        // Hilt orqali AppDatabase va SaleDao olish
        val entryPoint = try {
            EntryPointAccessors.fromApplication(context, AutoBackupEntryPoint::class.java)
        } catch (_: Throwable) {
            null
        }

        val appDatabase = entryPoint?.appDatabase()
        val saleDao = entryPoint?.saleDao()

        // 2. Savdolar mavjudligini tekshirish (faqat yangi savdo bo'lsa zaxiralash)
        val currentSalesCount = try {
            saleDao?.getAllSalesCount() ?: -1
        } catch (_: Throwable) {
            -1
        }
        var backupFile: File? = null
        return try {
            // 3. Baza nusxasini xavfsiz (WAL checkpoint bilan) yaratish
            val fileResult = DatabaseBackupExporter.backupDatabaseFile(context, appDatabase)
            backupFile = fileResult.getOrThrow()

            val extraInfo = if (currentSalesCount >= 0) "Jami savdolar: $currentSalesCount ta" else "Avtomatik orqa fon zaxirasi"

            // 4. Telegram orqali jo'natish
            val sendResult = TelegramBackupService.sendDatabaseBackup(
                botToken = botToken,
                chatId = chatId,
                databaseFile = backupFile,
                storeName = "SMART Kassa",
                extraInfo = extraInfo
            )

            if (sendResult.isSuccess) {
                BackupPreferences.recordBackupSuccess(context, backupFile.name, currentSalesCount)
                Result.success()
            } else {
                val errorMsg = sendResult.exceptionOrNull()?.localizedMessage ?: "Noma'lum xatolik"
                BackupPreferences.recordBackupFailure(context, errorMsg)
                Result.retry()
            }
        } catch (e: Exception) {
            BackupPreferences.recordBackupFailure(context, e.localizedMessage ?: "Zaxiralashda xatolik")
            Result.retry()
        } finally {
            // Keshda ortiqcha joy egallamasligi uchun vaqtinchalik nusxani o'chirish
            try {
                backupFile?.delete()
            } catch (_: Throwable) {}
        }
    }

    companion object {
        private const val WORK_NAME = "smart_pos_auto_backup_work"
        private val TASHKENT_ZONE = TimeZone.getTimeZone("Asia/Tashkent")

        /**
         * Har kuni Toshkent vaqti (Asia/Tashkent UTC+5) bo'yicha belgilangan soatda
         * internet ulanganda avtomatik ishga tushirish.
         */
        fun schedule(context: Context) {
            val isEnabled = BackupPreferences.isAutoBackupEnabled(context)
            val chatId = BackupPreferences.getChatId(context)

            if (!isEnabled || chatId.isBlank()) {
                cancel(context)
                return
            }

            val targetHour = BackupPreferences.getBackupHour(context)
            val targetMinute = BackupPreferences.getBackupMinute(context)

            // Toshkent soat mintaqasi bo'yicha vaqtni aniq hisoblash
            val now = Calendar.getInstance(TASHKENT_ZONE)
            val target = Calendar.getInstance(TASHKENT_ZONE).apply {
                set(Calendar.HOUR_OF_DAY, targetHour)
                set(Calendar.MINUTE, targetMinute)
                set(Calendar.SECOND, 0)
                set(Calendar.MILLISECOND, 0)
                if (before(now)) {
                    add(Calendar.DAY_OF_YEAR, 1)
                }
            }

            val initialDelayMillis = (target.timeInMillis - now.timeInMillis).coerceAtLeast(0L)

            // Shartlar: Internet bo'lishi shart, batareya o'ta past bo'lmasligi kerak
            val constraints = Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .setRequiresBatteryNotLow(true)
                .build()

            val workRequest = PeriodicWorkRequestBuilder<DatabaseAutoBackupWorker>(24, TimeUnit.HOURS)
                .setConstraints(constraints)
                .setInitialDelay(initialDelayMillis, TimeUnit.MILLISECONDS)
                .build()

            WorkManager.getInstance(context).enqueueUniquePeriodicWork(
                WORK_NAME,
                ExistingPeriodicWorkPolicy.UPDATE,
                workRequest
            )
        }

        fun cancel(context: Context) {
            WorkManager.getInstance(context).cancelUniqueWork(WORK_NAME)
        }
    }
}
