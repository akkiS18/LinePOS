package uz.pos.electro

import android.app.Application
import android.util.Log
import dagger.hilt.android.HiltAndroidApp
import java.io.File
import java.io.PrintWriter
import java.io.StringWriter

@HiltAndroidApp
class POSApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        val defaultHandler = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            try {
                val sw = StringWriter()
                throwable.printStackTrace(PrintWriter(sw))
                val stackTrace = sw.toString()
                Log.e("POSApplication", "FATAL CRASH on ${thread.name}:\n$stackTrace")
                val crashFile = File(filesDir, "crash_log.txt")
                crashFile.writeText("Time: ${System.currentTimeMillis()}\nThread: ${thread.name}\n\n$stackTrace")
            } catch (_: Throwable) {}
            defaultHandler?.uncaughtException(thread, throwable)
        }

        // Avtomatik Telegram zaxira rejasini yangilash
        try {
            uz.pos.electro.data.backup.DatabaseAutoBackupWorker.schedule(this)
        } catch (_: Throwable) {}
    }
}

