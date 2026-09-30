package uz.pos.electro.data.repository

import android.content.Context
import android.content.SharedPreferences
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.HttpURLConnection
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CurrencyRepository @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val prefs: SharedPreferences = context.getSharedPreferences("pos_currency_prefs", Context.MODE_PRIVATE)

    companion object {
        private val CBU_API_URL = uz.pos.electro.BuildConfig.CBU_API_URL
        private const val KEY_USD_RATE = "last_usd_rate"
        private const val KEY_LAST_UPDATED = "last_usd_rate_updated"
        const val DEFAULT_USD_RATE = 12850.0 // Boshlang'ich kesh qiymati
    }

    /**
     * Keshdagi eng oxirgi Dollar kursini olish (100% oflayn ishlaydi)
     */
    fun getCachedUsdRate(): Double {
        return try {
            val str = prefs.getString(KEY_USD_RATE, null)
            if (str != null) {
                str.toDoubleOrNull() ?: DEFAULT_USD_RATE
            } else {
                val fl = prefs.getFloat(KEY_USD_RATE, DEFAULT_USD_RATE.toFloat())
                fl.toDouble()
            }
        } catch (e: Throwable) {
            try {
                val fl = prefs.getFloat(KEY_USD_RATE, DEFAULT_USD_RATE.toFloat())
                fl.toDouble()
            } catch (_: Throwable) {
                DEFAULT_USD_RATE
            }
        }
    }

    fun getLastUpdatedTimestamp(): Long {
        return try {
            prefs.getLong(KEY_LAST_UPDATED, System.currentTimeMillis())
        } catch (_: Throwable) {
            System.currentTimeMillis()
        }
    }

    fun updateCachedRate(newRate: Double) {
        if (newRate > 0) {
            prefs.edit()
                .putString(KEY_USD_RATE, newRate.toString())
                .putLong(KEY_LAST_UPDATED, System.currentTimeMillis())
                .apply()
        }
    }

    /**
     * O'zbekiston Markaziy bankidan real vaqtdagi dollar kursini yangilash
     */
    suspend fun fetchLatestUsdRate(): Result<Double> = withContext(Dispatchers.IO) {
        try {
            val url = URL(CBU_API_URL)
            val connection = url.openConnection() as HttpURLConnection
            connection.requestMethod = "GET"
            connection.connectTimeout = 6000
            connection.readTimeout = 6000

            if (connection.responseCode == HttpURLConnection.HTTP_OK) {
                val reader = BufferedReader(InputStreamReader(connection.inputStream))
                val response = reader.readText()
                reader.close()

                val jsonArray = JSONArray(response)
                var usdRate = getCachedUsdRate()

                for (i in 0 until jsonArray.length()) {
                    val item = jsonArray.getJSONObject(i)
                    if (item.optString("Ccy") == "USD") {
                        val rateStr = item.optString("Rate")
                        usdRate = rateStr.replace(',', '.').toDoubleOrNull() ?: usdRate
                        break
                    }
                }

                // Keshga saqlaymiz (to'liq double aniqligida)
                prefs.edit()
                    .remove(KEY_USD_RATE)
                    .putString(KEY_USD_RATE, usdRate.toString())
                    .putLong(KEY_LAST_UPDATED, System.currentTimeMillis())
                    .apply()

                Result.success(usdRate)
            } else {
                Result.failure(Exception("HTTP Xatolik: ${connection.responseCode}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
}
