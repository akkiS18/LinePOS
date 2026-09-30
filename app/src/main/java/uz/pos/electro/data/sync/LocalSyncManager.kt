package uz.pos.electro.data.sync

import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import android.util.Log
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.SaleDao
import uz.pos.electro.data.local.dao.WarehouseDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity
import uz.pos.electro.data.local.entity.WarehouseEntity
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.repository.CurrencyRepository
import uz.pos.electro.data.repository.TaxSettingsRepository
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

enum class LiveSyncStatus {
    CONNECTED,   // 🟢 Jonli bog'langan
    CONNECTING,  // 🟠 Ulanmoqda...
    OFFLINE      // ⚪ Aloqa yo'q
}

data class TransferCheckResult(
    val direction: String,
    val desktopProductsCount: Int,
    val desktopSalesCount: Int,
    val desktopModifiedProducts: Int,
    val desktopUnsyncedSales: Int,
    val phoneModifiedProducts: Int,
    val phoneUnsyncedSales: Int,
    val hasConflict: Boolean
)

data class TransferExecutionResult(
    val success: Boolean,
    val direction: String,
    val transferredProducts: Int,
    val transferredSales: Int,
    val message: String
)

data class SyncSummary(
    val downloadedProducts: Int,
    val uploadedProducts: Int,
    val message: String
)

@Singleton
class LocalSyncManager @Inject constructor(
    @ApplicationContext private val context: Context,
    private val productDao: ProductDao,
    private val saleDao: SaleDao,
    private val warehouseDao: WarehouseDao,
    private val productStockDao: ProductStockDao,
    private val currencyRepository: CurrencyRepository,
    private val taxSettingsRepository: TaxSettingsRepository
) {
    private val prefs: SharedPreferences = context.getSharedPreferences("pos_local_sync_prefs", Context.MODE_PRIVATE)
    private val syncScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var liveJob: Job? = null

    private val _liveSyncStatus = MutableStateFlow(LiveSyncStatus.OFFLINE)
    val liveSyncStatus: StateFlow<LiveSyncStatus> = _liveSyncStatus.asStateFlow()

    companion object {
        private const val KEY_SERVER_URL = "local_desktop_url"
        private const val KEY_LAST_SYNC_TIME = "last_sync_timestamp"

        fun normalizeUrl(raw: String): String {
            var clean = raw.trim()
            if (clean.startsWith("{") && clean.endsWith("}")) {
                try {
                    val json = JSONObject(clean)
                    if (json.has("serverUrl")) {
                        clean = json.getString("serverUrl").trim()
                    }
                } catch (_: Exception) {}
            }
            if (!clean.startsWith("http://", ignoreCase = true) && !clean.startsWith("https://", ignoreCase = true)) {
                clean = "http://$clean"
            }
            return clean.trimEnd('/')
        }
    }

    fun getServerUrl(): String? {
        return prefs.getString(KEY_SERVER_URL, null)?.let { normalizeUrl(it) }
    }

    fun saveServerUrl(url: String) {
        val cleanUrl = normalizeUrl(url)
        prefs.edit().putString(KEY_SERVER_URL, cleanUrl).apply()
    }

    fun getLastSyncTime(): Long {
        return prefs.getLong(KEY_LAST_SYNC_TIME, 0L)
    }

    private fun updateLastSyncTime(timestamp: Long) {
        prefs.edit().putLong(KEY_LAST_SYNC_TIME, timestamp).apply()
    }

    suspend fun pingDesktop(serverUrl: String): Result<String> = withContext(Dispatchers.IO) {
        try {
            val base = normalizeUrl(serverUrl)
            val url = URL("$base/api/ping")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 4000
            conn.readTimeout = 4000
            conn.requestMethod = "GET"

            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                val text = conn.inputStream.bufferedReader().use { it.readText() }
                val json = JSONObject(text)
                val name = json.optString("name", "Desktop POS")
                Result.success(name)
            } else {
                Result.failure(Exception("Ulanish xatosi: HTTP ${conn.responseCode}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    /**
     * O'tkazishdan oldin ikkala qurilmadagi o'zgarishlar sonini hisoblash va konfliktni aniqlash
     */
    suspend fun checkTransferDiff(direction: String): Result<TransferCheckResult> = withContext(Dispatchers.IO) {
        val rawUrl = getServerUrl() ?: return@withContext Result.failure(Exception("Kompyuter IP manzili topilmadi!"))
        val baseUrl = normalizeUrl(rawUrl)
        val lastSync = getLastSyncTime()

        try {
            val phoneActiveCount = productDao.getActiveProductsCount()
            val phoneTotalSales = saleDao.getAllSalesCount()
            val phoneModifiedProducts = productDao.getModifiedProductsCount(lastSync)
            val phoneUnsyncedSales = saleDao.getUnsyncedSalesCount()

            val checkPayload = JSONObject().apply {
                put("direction", direction)
                put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
                put("phoneProductsCount", phoneActiveCount)
                put("phoneSalesCount", phoneTotalSales)
                put("phoneLastSyncTime", lastSync)
                put("phoneModifiedProducts", phoneModifiedProducts)
                put("phoneUnsyncedSales", phoneUnsyncedSales)
            }

            val checkUrl = URL("$baseUrl/api/sync/check")
            val conn = checkUrl.openConnection() as HttpURLConnection
            conn.connectTimeout = 5000
            conn.readTimeout = 5000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

            OutputStreamWriter(conn.outputStream, "UTF-8").use { writer ->
                writer.write(checkPayload.toString())
                writer.flush()
            }

            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                val respText = conn.inputStream.bufferedReader().use { it.readText() }
                val root = JSONObject(respText)

                val result = TransferCheckResult(
                    direction = direction,
                    desktopProductsCount = root.optInt("desktopProductsCount", 0),
                    desktopSalesCount = root.optInt("desktopSalesCount", 0),
                    desktopModifiedProducts = root.optInt("desktopModifiedProducts", 0),
                    desktopUnsyncedSales = root.optInt("desktopUnsyncedSales", 0),
                    phoneModifiedProducts = phoneModifiedProducts,
                    phoneUnsyncedSales = phoneUnsyncedSales,
                    hasConflict = root.optBoolean("hasConflict", false)
                )
                Result.success(result)
            } else {
                Result.failure(Exception("Tekshirishda xatolik: HTTP ${conn.responseCode}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    /**
     * Aniq yo'nalish bo'yicha ma'lumotlarni o'tkazish yoki Smart Merge qilish
     */
    suspend fun executeTransfer(direction: String, isSmartMerge: Boolean = false): Result<TransferExecutionResult> = withContext(Dispatchers.IO) {
        val rawUrl = getServerUrl() ?: return@withContext Result.failure(Exception("Kompyuter server manzili topilmadi!"))
        val baseUrl = normalizeUrl(rawUrl)
        val finalDirection = if (isSmartMerge) "smart_merge" else direction

        try {
            val transferPayload = JSONObject().apply {
                put("direction", finalDirection)
                put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}".trim())
                put("clientTimestamp", System.currentTimeMillis())
            }

            val localSales = saleDao.getUnsyncedSales()
            val localSaleIdsToMark = mutableListOf<Long>()

            if (finalDirection == "phone_to_desktop" || finalDirection == "smart_merge") {
                val localProducts = productDao.getAllProductsIncludingDeleted()
                val productsJsonArray = JSONArray()

                for (p in localProducts) {
                    val pJson = JSONObject().apply {
                        put("Id", p.id)
                        put("Guid", p.guid)
                        put("Barcode", p.barcode ?: JSONObject.NULL)
                        put("Name", p.name)
                        put("Category", p.category)
                        put("CostPrice", p.costPrice)
                        put("CostCurrency", p.costCurrency)
                        put("SellingPrice", p.sellingPrice)
                        put("SellingPrice2", if (p.sellingPrice2 != null) p.sellingPrice2 else JSONObject.NULL)
                        put("StockQuantity", p.stockQuantity)
                        put("UnitType", when (p.unitType) {
                            UnitType.DONA -> 0
                            UnitType.METR -> 1
                            UnitType.KG -> 2
                        })
                        put("MinStockAlert", p.minStockAlert)
                        put("IsDeleted", p.isDeleted)
                        put("Note", p.note)
                        put("UpdatedAt", p.updatedAt)
                    }
                    productsJsonArray.put(pJson)
                }
                transferPayload.put("Products", productsJsonArray)

                val localWarehouses = warehouseDao.getAllWarehousesList()
                val warehousesJsonArray = JSONArray()
                for (w in localWarehouses) {
                    warehousesJsonArray.put(warehouseToJson(w))
                }
                transferPayload.put("Warehouses", warehousesJsonArray)

                val salesJsonArray = JSONArray()
                for (saleWithItems in localSales) {
                    localSaleIdsToMark.add(saleWithItems.sale.id)
                    val sJson = JSONObject().apply {
                        put("Id", saleWithItems.sale.id)
                        put("Guid", saleWithItems.sale.guid)
                        put("TotalAmount", saleWithItems.sale.totalAmount)
                        put("TotalCost", saleWithItems.sale.totalCost)
                        put("PaymentType", when (saleWithItems.sale.paymentType) {
                            PaymentType.CASH -> 0
                            PaymentType.CARD -> 1
                            PaymentType.SPLIT -> 2
                            PaymentType.BRAK -> 6
                        })
                        put("CashAmount", saleWithItems.sale.cashAmount)
                        put("CardAmount", saleWithItems.sale.cardAmount)
                        put("TaxAmount", saleWithItems.sale.taxAmount)
                        put("TaxRate", saleWithItems.sale.taxRate)
                        put("CreatedAt", saleWithItems.sale.createdAt)
                        put("UserId", saleWithItems.sale.userId)
                        put("IsSynced", true)

                        val itemsArray = JSONArray()
                        for (item in saleWithItems.items) {
                            val iJson = JSONObject().apply {
                                put("Id", item.id)
                                put("SaleId", item.saleId)
                                put("SaleGuid", item.saleGuid)
                                put("ProductId", item.productId)
                                put("ProductGuid", item.productGuid)
                                put("ProductName", item.productName)
                                put("Quantity", item.quantity)
                                put("PriceAtSale", item.priceAtSale)
                                put("CostAtSale", item.costAtSale)
                                put("CostCurrency", item.costCurrency)
                            }
                            itemsArray.put(iJson)
                        }
                        put("Items", itemsArray)
                    }
                    salesJsonArray.put(sJson)
                }
                transferPayload.put("Sales", salesJsonArray)
            } else {
                transferPayload.put("Products", JSONArray())
                transferPayload.put("Sales", JSONArray())
            }

            val transferUrl = URL("$baseUrl/api/sync/transfer")
            val conn = transferUrl.openConnection() as HttpURLConnection
            conn.connectTimeout = 8000
            conn.readTimeout = 8000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

            OutputStreamWriter(conn.outputStream, "UTF-8").use { writer ->
                writer.write(transferPayload.toString())
                writer.flush()
            }

            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                val respText = conn.inputStream.bufferedReader().use { it.readText() }
                val root = JSONObject(respText)
                val serverTimestamp = root.optLong("serverTimestamp", System.currentTimeMillis())

                // Agar kompyuterdan tovarlar olingan bo'lsa (desktop_to_phone yoki smart_merge)
                if (root.has("products")) {
                    val productsArray = root.optJSONArray("products") ?: JSONArray()
                    val toUpsert = mutableListOf<ProductEntity>()

                    for (i in 0 until productsArray.length()) {
                        val p = productsArray.getJSONObject(i)
                        val guid = p.optString("Guid", "")
                        val barcode = if (p.isNull("Barcode") || p.optString("Barcode").isBlank()) null else p.optString("Barcode")

                        val existing = if (guid.isNotBlank()) productDao.getProductByGuid(guid) else null
                        val existingByBarcode = if (existing == null && barcode != null) productDao.getProductByBarcode(barcode) else null
                        val localId = existing?.id ?: existingByBarcode?.id ?: 0L

                        val unitType = when (p.optInt("UnitType", 0)) {
                            0 -> UnitType.DONA
                            1 -> UnitType.METR
                            2 -> UnitType.KG
                            else -> UnitType.DONA
                        }

                        toUpsert.add(
                            ProductEntity(
                                id = localId,
                                guid = if (guid.isNotBlank()) guid else (existing?.guid ?: java.util.UUID.randomUUID().toString()),
                                barcode = barcode,
                                name = p.optString("Name"),
                                category = p.optString("Category", "Barchasi"),
                                costPrice = p.optDouble("CostPrice", 0.0),
                                costCurrency = p.optString("CostCurrency", "UZS"),
                                sellingPrice = p.optDouble("SellingPrice", 0.0),
                                sellingPrice2 = if (p.has("SellingPrice2") && !p.isNull("SellingPrice2")) p.optDouble("SellingPrice2") else null,
                                stockQuantity = p.optDouble("StockQuantity", 0.0),
                                unitType = unitType,
                                minStockAlert = p.optDouble("MinStockAlert", 3.0),
                                isDeleted = p.optBoolean("IsDeleted", false),
                                note = p.optString("Note", ""),
                                updatedAt = p.optLong("UpdatedAt", serverTimestamp)
                            )
                        )
                    }

                    if (toUpsert.isNotEmpty()) {
                        productDao.insertProducts(toUpsert)
                    }
                }

                // Agar kompyuterdan cheklar olingan bo'lsa (desktop_to_phone yoki smart_merge)
                var receivedSalesCount = 0
                if (root.has("sales")) {
                    val salesArray = root.optJSONArray("sales") ?: JSONArray()
                    for (i in 0 until salesArray.length()) {
                        val sJson = salesArray.getJSONObject(i)
                        val sGuid = sJson.optString("Guid", "")
                        if (sGuid.isBlank()) continue

                        val existingSale = saleDao.getSaleByGuid(sGuid)
                        if (existingSale != null) {
                            if (!existingSale.isSynced) {
                                saleDao.markSalesSyncedByGuids(listOf(sGuid))
                            }
                            continue
                        }

                        val totalAmount = sJson.optDouble("TotalAmount", 0.0)
                        val totalCost = sJson.optDouble("TotalCost", 0.0)
                        val paymentType = when (val raw = sJson.opt("PaymentType")) {
                            is Number -> when (raw.toInt()) {
                                1 -> PaymentType.CARD
                                2 -> PaymentType.SPLIT
                                6 -> PaymentType.BRAK
                                else -> PaymentType.CASH
                            }
                            is String -> when {
                                raw.equals("CARD", ignoreCase = true) || raw == "1" -> PaymentType.CARD
                                raw.equals("SPLIT", ignoreCase = true) || raw == "2" -> PaymentType.SPLIT
                                raw.equals("BRAK", ignoreCase = true) || raw == "6" -> PaymentType.BRAK
                                else -> PaymentType.CASH
                            }
                            else -> PaymentType.CASH
                        }
                        val cashAmount = sJson.optDouble("CashAmount", if (paymentType == PaymentType.CASH) totalAmount else 0.0)
                        val cardAmount = sJson.optDouble("CardAmount", if (paymentType == PaymentType.CARD) totalAmount else 0.0)
                        val taxAmount = sJson.optDouble("TaxAmount", 0.0)
                        val taxRate = sJson.optDouble("TaxRate", 0.0)
                        val createdAt = sJson.optLong("CreatedAt", serverTimestamp)
                        val userId = sJson.optLong("UserId", 1L)

                        val saleEntity = SaleEntity(
                            id = 0L,
                            guid = sGuid,
                            totalAmount = totalAmount,
                            totalCost = totalCost,
                            paymentType = paymentType,
                            cashAmount = cashAmount,
                            cardAmount = cardAmount,
                            taxAmount = taxAmount,
                            taxRate = taxRate,
                            createdAt = createdAt,
                            userId = userId,
                            isSynced = true
                        )

                        val itemsArray = sJson.optJSONArray("Items") ?: JSONArray()
                        val saleItems = mutableListOf<SaleItemEntity>()
                        for (j in 0 until itemsArray.length()) {
                            val iJson = itemsArray.getJSONObject(j)
                            val pGuid = iJson.optString("ProductGuid", "")
                            val pName = iJson.optString("ProductName", "")

                            val localProd = if (pGuid.isNotBlank()) productDao.getProductByGuid(pGuid) else null
                            val localProdId = localProd?.id ?: iJson.optLong("ProductId", 0L)

                            saleItems.add(
                                SaleItemEntity(
                                    id = 0L,
                                    saleId = 0L,
                                    saleGuid = sGuid,
                                    productId = localProdId,
                                    productGuid = pGuid,
                                    productName = pName,
                                    quantity = iJson.optDouble("Quantity", 1.0),
                                    priceAtSale = iJson.optDouble("PriceAtSale", 0.0),
                                    costAtSale = iJson.optDouble("CostAtSale", 0.0),
                                    costCurrency = iJson.optString("CostCurrency", "UZS"),
                                    warehouseGuid = iJson.optString("WarehouseGuid", ""),
                                    warehouseName = iJson.optString("WarehouseName", "")
                                )
                            )
                        }

                        saleDao.insertSaleWithItems(saleEntity, saleItems)
                        receivedSalesCount++
                    }
                }

                // Agar kompyuterdan omborlar olingan bo'lsa (desktop_to_phone yoki smart_merge)
                if (root.has("warehouses")) {
                    val whArr = root.optJSONArray("warehouses") ?: JSONArray()
                    for (i in 0 until whArr.length()) {
                        val wh = parseWarehouseJson(whArr.getJSONObject(i), serverTimestamp)
                        val existing = if (wh.guid.isNotBlank()) warehouseDao.getWarehouseByGuid(wh.guid) else null
                        if (existing == null) {
                            val toSave = wh.copy(id = 0L)
                            if (toSave.isPrimary) {
                                warehouseDao.clearPrimaryStatus()
                            }
                            warehouseDao.insertWarehouse(toSave)
                        } else if (wh.updatedAt >= existing.updatedAt || wh.name != existing.name || wh.isPrimary != existing.isPrimary || wh.isDeleted != existing.isDeleted) {
                            val toSave = wh.copy(id = existing.id)
                            if (toSave.isPrimary) {
                                warehouseDao.clearPrimaryStatus()
                            }
                            warehouseDao.insertWarehouse(toSave)
                        }
                    }
                }

                // Agar telefondan cheklar kompyuterga o'tkazilgan bo'lsa, ularni sinxronlandi deb belgilash
                if (localSaleIdsToMark.isNotEmpty()) {
                    saleDao.markSalesSynced(localSaleIdsToMark)
                }

                updateLastSyncTime(serverTimestamp)

                val pCount = root.optInt("syncedProducts", 0)
                val sCount = root.optInt("syncedSales", 0)
                val finalSalesCount = if (sCount > 0) sCount else receivedSalesCount

                val message = when (finalDirection) {
                    "phone_to_desktop" -> "✅ Telefondan $pCount ta tovar va $finalSalesCount ta chek kompyuterga muvaffaqiyatli o'tkazildi!"
                    "desktop_to_phone" -> "✅ Kompyuterdagi $pCount ta tovar va $finalSalesCount ta chek telefonga muvaffaqiyatli yuklandi!"
                    "smart_merge" -> "✅ Aqlli birlashtirish (Smart Merge) yakunlandi: ikkala qurilma ma'lumotlari tenglashtirildi!"
                    else -> "Muvaffaqiyatli yakunlandi!"
                }

                Result.success(
                    TransferExecutionResult(
                        success = true,
                        direction = finalDirection,
                        transferredProducts = pCount,
                        transferredSales = finalSalesCount,
                        message = message
                    )
                )
            } else {
                Result.failure(Exception("O'tkazish xatosi: HTTP ${conn.responseCode}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun syncWithDesktop(): Result<SyncSummary> {
        val rawUrl = getServerUrl()
        if (!rawUrl.isNullOrBlank()) {
            val baseUrl = normalizeUrl(rawUrl)
            try {
                productStockDao.deleteInvalidStocks()
                pushLocalWarehousesInternal(baseUrl)
                pushLocalStocksInternal(baseUrl)
            } catch (_: Exception) { }
        }
        val res = executeTransfer("smart_merge", isSmartMerge = true)
        if (res.isSuccess && !rawUrl.isNullOrBlank()) {
            val baseUrl = normalizeUrl(rawUrl)
            try {
                pullDeltaInternal(baseUrl)
            } catch (_: Exception) { }
        }
        return if (res.isSuccess) {
            val d = res.getOrNull()!!
            Result.success(SyncSummary(d.transferredProducts, d.transferredProducts, d.message))
        } else {
            Result.failure(res.exceptionOrNull() ?: Exception("Xatolik"))
        }
    }

    // ==========================================
    // UZLUKSIZ (REAL-TIME) WI-FI JONLI SINXRON
    // ==========================================

    fun restartLiveSyncEngine() {
        liveJob?.cancel()
        liveJob = null
        startLiveSyncEngine()
    }

    fun startLiveSyncEngine() {
        if (liveJob?.isActive == true) return

        liveJob = syncScope.launch {
            while (isActive) {
                val rawUrl = getServerUrl()
                if (rawUrl.isNullOrBlank()) {
                    _liveSyncStatus.value = LiveSyncStatus.OFFLINE
                    delay(3000)
                    continue
                }

                val baseUrl = normalizeUrl(rawUrl)
                _liveSyncStatus.value = LiveSyncStatus.CONNECTING

                try {
                    // 1. Aloqa o'rnatilganda avval oflayn qolgan cheklarni o'tkazamiz
                    pushUnsyncedSalesInternal(baseUrl)
                    // 2. Mahalliy omborlarni kompyuterga uzatamiz
                    pushLocalWarehousesInternal(baseUrl)
                    // 3. Oflaynda o'zgargan tovarlarni kompyuterga uzatamiz
                    pushModifiedProductsInternal(baseUrl)
                    // 4. Mahalliy ombor qoldiqlarini kompyuterga uzatamiz
                    pushLocalStocksInternal(baseUrl)
                    // 5. Kompyuterdan oxirgi o'zgarishlarni yuklab olamiz
                    pullDeltaInternal(baseUrl)

                    // 5. Doimiy SSE oqimini ochamiz (qurilma modeli bilan)
                    val deviceModel = "${Build.MANUFACTURER} ${Build.MODEL}".trim()
                    val deviceParam = java.net.URLEncoder.encode(deviceModel, "UTF-8")
                    val sseUrl = URL("$baseUrl/api/sync/events?device=$deviceParam")
                    val conn = sseUrl.openConnection() as HttpURLConnection
                    conn.connectTimeout = 6000
                    conn.readTimeout = 30000 // Server har 10 soniyada keep-alive yuboradi
                    conn.requestMethod = "GET"
                    conn.setRequestProperty("Accept", "text/event-stream")

                    if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                        _liveSyncStatus.value = LiveSyncStatus.CONNECTED
                        Log.d("LocalSyncManager", "🟢 Desktop bilan doimiy jonli aloqa o'rnatildi ($baseUrl)")

                        val reader = BufferedReader(InputStreamReader(conn.inputStream, "UTF-8"))
                        var currentEvent = ""

                        while (isActive) {
                            val line = reader.readLine() ?: break // null bo'lsa aloqa uzildi
                            val trimmed = line.trim()
                            if (trimmed.isEmpty()) {
                                currentEvent = ""
                                continue
                            }

                            if (trimmed.startsWith(":")) {
                                // Server ping xabari (: ping)
                                continue
                            } else if (trimmed.startsWith("event:")) {
                                currentEvent = trimmed.substring("event:".length).trim()
                            } else if (trimmed.startsWith("data:")) {
                                val dataStr = trimmed.substring("data:".length).trim()
                                handleIncomingLiveEvent(currentEvent, dataStr)
                            }
                        }
                    }
                } catch (e: Exception) {
                    Log.d("LocalSyncManager", "SSE aloqa uzildi: ${e.message}")
                }

                _liveSyncStatus.value = LiveSyncStatus.CONNECTING
                delay(3000) // 3 soniyadan so'ng qayta ulanishga urinish
            }
        }
    }

    private suspend fun handleIncomingLiveEvent(eventType: String, dataStr: String) = withContext(Dispatchers.IO) {
        try {
            when (eventType) {
                "connected" -> {
                    val json = JSONObject(dataStr)
                    val serverTimestamp = json.optLong("serverTimestamp", System.currentTimeMillis())
                    updateLastSyncTime(serverTimestamp)
                    if (json.has("cardTaxRate")) {
                        val tax = json.optDouble("cardTaxRate", -1.0)
                        if (tax >= 0.0) {
                            taxSettingsRepository.setCardTaxRate(tax)
                            Log.d("LocalSyncManager", "⚡ Serverdan karta komissiyasi yangilandi: $tax%")
                        }
                    }
                    if (json.has("usdRate")) {
                        val usd = json.optDouble("usdRate", 0.0)
                        if (usd > 0.0) {
                            currencyRepository.updateCachedRate(usd)
                            Log.d("LocalSyncManager", "⚡ Serverdan dollar kursi yangilandi: $usd so'm")
                        }
                    }
                }

                "sale_created" -> {
                    val sJson = JSONObject(dataStr)
                    val sGuid = sJson.optString("Guid", "")
                    if (sGuid.isNotBlank()) {
                        val existing = saleDao.getSaleByGuid(sGuid)
                        if (existing == null) {
                            val saleEntity = parseSaleJson(sJson)
                            val items = parseSaleItemsJson(sJson.optJSONArray("Items") ?: JSONArray(), sGuid)
                            val now = System.currentTimeMillis()
                            saleDao.insertSaleWithItems(saleEntity, items)

                            val primaryWh = warehouseDao.getPrimaryWarehouse() ?: warehouseDao.getAllWarehousesList().firstOrNull()
                            val primaryGuid = primaryWh?.guid ?: "main-default-warehouse"

                            // Delta usulida ombor qoldiqlarini zudlik bilan kamaytirish
                            for (item in items) {
                                val targetWhGuid = if (item.warehouseGuid.isNotBlank()) item.warehouseGuid else primaryGuid
                                if (item.productGuid.isNotBlank()) {
                                    productDao.decreaseStockByGuid(item.productGuid, item.quantity, now)
                                    productStockDao.deductStock(item.productGuid, targetWhGuid, item.quantity, now)
                                } else if (item.productId > 0) {
                                    productDao.decreaseStock(item.productId, item.quantity, now)
                                }
                            }
                            Log.d("LocalSyncManager", "⚡ Kompyuterdan jonli savdo cheki qabul qilindi ($sGuid)")
                        }
                    }
                }

                "product_updated" -> {
                    val pJson = JSONObject(dataStr)
                    val prod = parseProductJson(pJson, System.currentTimeMillis())
                    val existing = if (prod.guid.isNotBlank()) {
                        productDao.getProductByGuid(prod.guid)
                    } else if (!prod.barcode.isNullOrBlank()) {
                        productDao.getProductByBarcode(prod.barcode!!)
                    } else null

                    val toSave = prod.copy(id = existing?.id ?: 0L)
                    productDao.insertProduct(toSave)

                    val whGuid = if (pJson.has("WarehouseGuid") && pJson.getString("WarehouseGuid").isNotBlank()) {
                        pJson.getString("WarehouseGuid")
                    } else if (pJson.has("warehouseGuid") && pJson.getString("warehouseGuid").isNotBlank()) {
                        pJson.getString("warehouseGuid")
                    } else null

                    if (!whGuid.isNullOrBlank() && whGuid != "null" && toSave.guid.isNotBlank()) {
                        productStockDao.upsertStock(toSave.guid, whGuid, toSave.stockQuantity, System.currentTimeMillis())
                        Log.d("LocalSyncManager", "⚡ Kompyuterdan tovar yangilandi: ${toSave.name} (Ombor: $whGuid, Qoldiq: ${toSave.stockQuantity})")
                    } else {
                        Log.d("LocalSyncManager", "⚡ Kompyuterdan tovar yangilandi: ${toSave.name}")
                    }
                }

                "warehouse_updated" -> {
                    val wJson = JSONObject(dataStr)
                    val wh = parseWarehouseJson(wJson, System.currentTimeMillis())
                    val existing = if (wh.guid.isNotBlank()) warehouseDao.getWarehouseByGuid(wh.guid) else null
                    val toSave = wh.copy(id = existing?.id ?: 0L)
                    if (toSave.isPrimary) {
                        warehouseDao.clearPrimaryStatus()
                    }
                    warehouseDao.insertWarehouse(toSave)
                    Log.d("LocalSyncManager", "⚡ Kompyuterdan ombor yangilandi: ${toSave.name}")
                }

                "stock_transferred" -> {
                    val trJson = JSONObject(dataStr)
                    val prodGuid = trJson.optString("productGuid", "")
                    val fromWh = trJson.optString("fromWarehouseGuid", "")
                    val toWh = trJson.optString("toWarehouseGuid", "")
                    val qty = trJson.optDouble("quantity", 0.0)
                    val ts = trJson.optLong("timestamp", System.currentTimeMillis())
                    if (prodGuid.isNotBlank() && fromWh.isNotBlank() && toWh.isNotBlank() && qty > 0) {
                        productStockDao.transferStock(prodGuid, fromWh, toWh, qty, ts)
                        Log.d("LocalSyncManager", "⚡ Kompyuterdan omborlararo o'tkazma qabul qilindi: $prodGuid ($fromWh -> $toWh : $qty)")
                    }
                }

                "settings_updated" -> {
                    val json = JSONObject(dataStr)
                    if (json.has("cardTaxRate")) {
                        val tax = json.optDouble("cardTaxRate", -1.0)
                        if (tax >= 0.0) {
                            taxSettingsRepository.setCardTaxRate(tax)
                            Log.d("LocalSyncManager", "⚡ Kompyuterdan karta komissiyasi yangilandi: $tax%")
                        }
                    }
                    if (json.has("usdRate")) {
                        val usd = json.optDouble("usdRate", 0.0)
                        if (usd > 0.0) {
                            currencyRepository.updateCachedRate(usd)
                            Log.d("LocalSyncManager", "⚡ Kompyuterdan dollar kursi yangilandi: $usd so'm")
                        }
                    }
                }
            }
        } catch (e: Exception) {
            Log.e("LocalSyncManager", "Live event parsing xatosi: ${e.message}", e)
        }
    }

    fun sendLiveSale(sale: SaleEntity, items: List<SaleItemEntity>) {
        syncScope.launch {
            val rawUrl = getServerUrl() ?: return@launch
            val baseUrl = normalizeUrl(rawUrl)
            try {
                val saleJson = saleToJson(sale, items)
                val url = URL("$baseUrl/api/sync/live_sale")
                val conn = url.openConnection() as HttpURLConnection
                conn.connectTimeout = 4000
                conn.readTimeout = 4000
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

                OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(saleJson.toString()) }

                if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                    saleDao.markSalesSyncedByGuids(listOf(sale.guid))
                    val resp = conn.inputStream.bufferedReader().use { it.readText() }
                    val json = JSONObject(resp)
                    val serverTimestamp = json.optLong("serverTimestamp", System.currentTimeMillis())
                    updateLastSyncTime(serverTimestamp)
                    Log.d("LocalSyncManager", "⚡ Jonli savdo cheki kompyuterga uzatildi (${sale.guid})")
                }
            } catch (e: Exception) {
                Log.d("LocalSyncManager", "Live sale jo'natishda xatolik (oflayn saqlandi): ${e.message}")
            }
        }
    }

    fun sendLiveProduct(product: ProductEntity, warehouseGuid: String? = null) {
        syncScope.launch {
            val rawUrl = getServerUrl() ?: return@launch
            val baseUrl = normalizeUrl(rawUrl)
            try {
                val prodJson = productToJson(product, warehouseGuid)
                val url = URL("$baseUrl/api/sync/live_product")
                val conn = url.openConnection() as HttpURLConnection
                conn.connectTimeout = 4000
                conn.readTimeout = 4000
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

                OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(prodJson.toString()) }

                if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                    val resp = conn.inputStream.bufferedReader().use { it.readText() }
                    val json = JSONObject(resp)
                    val serverTimestamp = json.optLong("serverTimestamp", System.currentTimeMillis())
                    updateLastSyncTime(serverTimestamp)
                    Log.d("LocalSyncManager", "⚡ Jonli tovar kompyuterga uzatildi (${product.name})")
                }
            } catch (e: Exception) {
                Log.d("LocalSyncManager", "Live product jo'natishda xatolik: ${e.message}")
            }
        }
    }

    fun sendLiveWarehouse(warehouse: WarehouseEntity) {
        syncScope.launch {
            val rawUrl = getServerUrl() ?: return@launch
            val baseUrl = normalizeUrl(rawUrl)
            try {
                val whJson = warehouseToJson(warehouse)
                val url = URL("$baseUrl/api/sync/live_warehouse")
                val conn = url.openConnection() as HttpURLConnection
                conn.connectTimeout = 4000
                conn.readTimeout = 4000
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

                OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(whJson.toString()) }

                if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                    val resp = conn.inputStream.bufferedReader().use { it.readText() }
                    val json = JSONObject(resp)
                    val serverTimestamp = json.optLong("serverTimestamp", System.currentTimeMillis())
                    updateLastSyncTime(serverTimestamp)
                    Log.d("LocalSyncManager", "⚡ Jonli ombor kompyuterga uzatildi (${warehouse.name})")
                }
            } catch (e: Exception) {
                Log.d("LocalSyncManager", "Live warehouse jo'natishda xatolik: ${e.message}")
            }
        }
    }

    fun sendLiveStockTransfer(productGuid: String, fromWarehouseGuid: String, toWarehouseGuid: String, quantity: Double) {
        syncScope.launch {
            val rawUrl = getServerUrl() ?: return@launch
            val baseUrl = normalizeUrl(rawUrl)
            try {
                val json = JSONObject().apply {
                    put("productGuid", productGuid)
                    put("fromWarehouseGuid", fromWarehouseGuid)
                    put("toWarehouseGuid", toWarehouseGuid)
                    put("quantity", quantity)
                    put("timestamp", System.currentTimeMillis())
                }
                val url = URL("$baseUrl/api/sync/live_transfer_stock")
                val conn = url.openConnection() as HttpURLConnection
                conn.connectTimeout = 4000
                conn.readTimeout = 4000
                conn.requestMethod = "POST"
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

                OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(json.toString()) }

                if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                    val resp = conn.inputStream.bufferedReader().use { it.readText() }
                    val respJson = JSONObject(resp)
                    val serverTimestamp = respJson.optLong("serverTimestamp", System.currentTimeMillis())
                    updateLastSyncTime(serverTimestamp)
                    Log.d("LocalSyncManager", "⚡ Omborlararo o'tkazma kompyuterga uzatildi ($productGuid: $quantity)")
                }
            } catch (e: Exception) {
                Log.d("LocalSyncManager", "Live stock transfer jo'natishda xatolik: ${e.message}")
            }
        }
    }

    private suspend fun pushLocalWarehousesInternal(baseUrl: String) = withContext(Dispatchers.IO) {
        try {
            val localWarehouses = warehouseDao.getAllWarehousesList()
            if (localWarehouses.isEmpty()) return@withContext
            val arr = JSONArray()
            for (w in localWarehouses) {
                arr.put(warehouseToJson(w))
            }
            val url = URL("$baseUrl/api/sync/push_warehouses")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 4000
            conn.readTimeout = 4000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
            OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(arr.toString()) }
            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                Log.d("LocalSyncManager", "📥 ${localWarehouses.size} ta ombor kompyuterga uzatildi.")
            }
        } catch (e: Exception) {
            Log.d("LocalSyncManager", "Push warehouses xatosi: ${e.message}")
        }
    }

    private suspend fun pushModifiedProductsInternal(baseUrl: String) = withContext(Dispatchers.IO) {
        try {
            val lastSync = getLastSyncTime()
            val modifiedProducts = productDao.getProductsChangedSince(lastSync)
            if (modifiedProducts.isEmpty()) return@withContext
            val arr = JSONArray()
            for (p in modifiedProducts) {
                val stocks = productStockDao.getStocksForProduct(p.guid)
                val primaryStock = stocks.firstOrNull { it.quantity > 0 && !it.warehouseGuid.isNullOrBlank() && it.warehouseGuid != "null" }
                    ?: stocks.firstOrNull { !it.warehouseGuid.isNullOrBlank() && it.warehouseGuid != "null" }
                val whGuid = primaryStock?.warehouseGuid
                arr.put(productToJson(p, whGuid))
            }
            val url = URL("$baseUrl/api/sync/push_products")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 5000
            conn.readTimeout = 5000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
            OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(arr.toString()) }
            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                Log.d("LocalSyncManager", "📥 Oflaynda o'zgargan ${modifiedProducts.size} ta tovar kompyuterga uzatildi.")
            }
        } catch (e: Exception) {
            Log.d("LocalSyncManager", "Push modified products xatosi: ${e.message}")
        }
    }

    private suspend fun pushLocalStocksInternal(baseUrl: String) = withContext(Dispatchers.IO) {
        try {
            productStockDao.deleteInvalidStocks()
            val stocks = productStockDao.getAllProductStocksList()
            if (stocks.isEmpty()) return@withContext
            val arr = JSONArray()
            for (s in stocks) {
                if (!s.warehouseGuid.isNullOrBlank() && s.warehouseGuid != "null" && s.warehouseGuid.trim().isNotEmpty()) {
                    arr.put(JSONObject().apply {
                        put("ProductGuid", s.productGuid)
                        put("WarehouseGuid", s.warehouseGuid)
                        put("Quantity", s.quantity)
                        put("UpdatedAt", s.updatedAt)
                    })
                }
            }
            val url = URL("$baseUrl/api/sync/push_stocks")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 4000
            conn.readTimeout = 4000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
            OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(arr.toString()) }
            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                Log.d("LocalSyncManager", "📥 ${stocks.size} ta ombor qoldig'i kompyuterga uzatildi.")
            }
        } catch (e: Exception) {
            Log.d("LocalSyncManager", "Push stocks xatosi: ${e.message}")
        }
    }

    private suspend fun pushUnsyncedSalesInternal(baseUrl: String) = withContext(Dispatchers.IO) {
        val unsynced = saleDao.getUnsyncedSales()
        if (unsynced.isEmpty()) return@withContext

        val salesArray = JSONArray()
        val guids = mutableListOf<String>()

        for (saleWithItems in unsynced) {
            guids.add(saleWithItems.sale.guid)
            salesArray.put(saleToJson(saleWithItems.sale, saleWithItems.items))
        }

        try {
            val url = URL("$baseUrl/api/sync/push_unsynced")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 5000
            conn.readTimeout = 5000
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")

            OutputStreamWriter(conn.outputStream, "UTF-8").use { it.write(salesArray.toString()) }

            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                saleDao.markSalesSyncedByGuids(guids)
                Log.d("LocalSyncManager", "📥 Oflaynda to'plangan ${guids.size} ta savdo cheki kompyuterga sinxronlandi.")
            }
        } catch (e: Exception) {
            Log.d("LocalSyncManager", "Push unsynced xatosi: ${e.message}")
        }
    }

    private suspend fun pullDeltaInternal(baseUrl: String) = withContext(Dispatchers.IO) {
        val lastSync = getLastSyncTime()
        try {
            val url = URL("$baseUrl/api/sync/delta?since=$lastSync")
            val conn = url.openConnection() as HttpURLConnection
            conn.connectTimeout = 5000
            conn.readTimeout = 5000
            conn.requestMethod = "GET"

            if (conn.responseCode == HttpURLConnection.HTTP_OK) {
                val text = conn.inputStream.bufferedReader().use { it.readText() }
                val root = JSONObject(text)
                val serverTimestamp = root.optLong("serverTimestamp", System.currentTimeMillis())

                if (root.has("products")) {
                    val productsArr = root.optJSONArray("products") ?: JSONArray()
                    val toUpsert = mutableListOf<ProductEntity>()
                    for (i in 0 until productsArr.length()) {
                        val pJson = productsArr.getJSONObject(i)
                        val prod = parseProductJson(pJson, serverTimestamp)
                        val existing = if (prod.guid.isNotBlank()) productDao.getProductByGuid(prod.guid) else null
                        val existingByBc = if (existing == null && !prod.barcode.isNullOrBlank()) productDao.getProductByBarcode(prod.barcode!!) else null
                        toUpsert.add(prod.copy(id = existing?.id ?: existingByBc?.id ?: 0L))
                    }
                    if (toUpsert.isNotEmpty()) {
                        productDao.insertProducts(toUpsert)
                    }
                }

                if (root.has("sales")) {
                    val salesArr = root.optJSONArray("sales") ?: JSONArray()
                    val now = System.currentTimeMillis()
                    val primaryWh = warehouseDao.getPrimaryWarehouse() ?: warehouseDao.getAllWarehousesList().firstOrNull()
                    val primaryGuid = primaryWh?.guid ?: "main-default-warehouse"

                    for (i in 0 until salesArr.length()) {
                        val sJson = salesArr.getJSONObject(i)
                        val sGuid = sJson.optString("Guid", "")
                        if (sGuid.isBlank()) continue

                        val existing = saleDao.getSaleByGuid(sGuid)
                        if (existing == null) {
                            val sale = parseSaleJson(sJson)
                            val items = parseSaleItemsJson(sJson.optJSONArray("Items") ?: JSONArray(), sGuid)
                            saleDao.insertSaleWithItems(sale, items)

                            // Delta hisobi: ombordan sotilgan miqdorni ayiramiz
                            for (item in items) {
                                val targetWhGuid = if (item.warehouseGuid.isNotBlank()) item.warehouseGuid else primaryGuid
                                if (item.productGuid.isNotBlank()) {
                                    productDao.decreaseStockByGuid(item.productGuid, item.quantity, now)
                                    productStockDao.deductStock(item.productGuid, targetWhGuid, item.quantity, now)
                                } else if (item.productId > 0) {
                                    productDao.decreaseStock(item.productId, item.quantity, now)
                                }
                            }
                        }
                    }
                }

                if (root.has("warehouses")) {
                    val whArr = root.optJSONArray("warehouses") ?: JSONArray()
                    for (i in 0 until whArr.length()) {
                        val wh = parseWarehouseJson(whArr.getJSONObject(i), serverTimestamp)
                        val existing = if (wh.guid.isNotBlank()) warehouseDao.getWarehouseByGuid(wh.guid) else null
                        val toSave = wh.copy(id = existing?.id ?: 0L)
                        if (toSave.isPrimary) {
                            warehouseDao.clearPrimaryStatus()
                        }
                        warehouseDao.insertWarehouse(toSave)
                    }
                }

                if (root.has("productStocks")) {
                    val stocksArr = root.optJSONArray("productStocks") ?: JSONArray()
                    for (i in 0 until stocksArr.length()) {
                        val sJson = stocksArr.getJSONObject(i)
                        val pGuid = sJson.optString("ProductGuid", "")
                        val wGuid = sJson.optString("WarehouseGuid", "")
                        val qty = sJson.optDouble("Quantity", 0.0)
                        val ts = sJson.optLong("UpdatedAt", serverTimestamp)
                        if (pGuid.isNotBlank() && wGuid.isNotBlank() && wGuid != "null") {
                            productStockDao.upsertStock(pGuid, wGuid, qty, ts)
                        }
                    }
                    if (stocksArr.length() > 0) {
                        Log.d("LocalSyncManager", "📥 Kompyuterdan ${stocksArr.length()} ta ombor qoldiqlari yangilandi.")
                    }
                }

                updateLastSyncTime(serverTimestamp)
            }
        } catch (e: Exception) {
            Log.d("LocalSyncManager", "Pull delta xatosi: ${e.message}")
        }
    }

    private fun productToJson(p: ProductEntity, warehouseGuid: String? = null): JSONObject {
        return JSONObject().apply {
            put("Id", p.id)
            put("Guid", p.guid)
            put("Barcode", p.barcode ?: JSONObject.NULL)
            put("Name", p.name)
            put("Category", p.category)
            put("CostPrice", p.costPrice)
            put("CostCurrency", p.costCurrency)
            put("SellingPrice", p.sellingPrice)
            put("SellingPrice2", if (p.sellingPrice2 != null) p.sellingPrice2 else JSONObject.NULL)
            put("StockQuantity", p.stockQuantity)
            put("UnitType", when (p.unitType) {
                UnitType.DONA -> 0
                UnitType.METR -> 1
                UnitType.KG -> 2
            })
            put("MinStockAlert", p.minStockAlert)
            put("IsDeleted", p.isDeleted)
            put("Note", p.note)
            put("UpdatedAt", p.updatedAt)
            if (!warehouseGuid.isNullOrBlank()) {
                put("WarehouseGuid", warehouseGuid)
                put("warehouseGuid", warehouseGuid)
            }
        }
    }

    private fun saleToJson(sale: SaleEntity, items: List<SaleItemEntity>): JSONObject {
        return JSONObject().apply {
            put("Id", sale.id)
            put("Guid", sale.guid)
            put("TotalAmount", sale.totalAmount)
            put("TotalCost", sale.totalCost)
            put("PaymentType", when (sale.paymentType) {
                PaymentType.CASH -> 0
                PaymentType.CARD -> 1
                PaymentType.SPLIT -> 2
                PaymentType.BRAK -> 6
            })
            put("CashAmount", sale.cashAmount)
            put("CardAmount", sale.cardAmount)
            put("TaxAmount", sale.taxAmount)
            put("TaxRate", sale.taxRate)
            put("CreatedAt", sale.createdAt)
            put("UserId", sale.userId)
            put("IsSynced", true)

            val itemsArray = JSONArray()
            for (item in items) {
                val iJson = JSONObject().apply {
                    put("Id", item.id)
                    put("SaleId", item.saleId)
                    put("SaleGuid", item.saleGuid)
                    put("ProductId", item.productId)
                    put("ProductGuid", item.productGuid)
                    put("ProductName", item.productName)
                    put("Quantity", item.quantity)
                    put("PriceAtSale", item.priceAtSale)
                    put("CostAtSale", item.costAtSale)
                    put("CostCurrency", item.costCurrency)
                    put("WarehouseGuid", item.warehouseGuid)
                    put("WarehouseName", item.warehouseName)
                }
                itemsArray.put(iJson)
            }
            put("Items", itemsArray)
        }
    }

    private fun parseProductJson(p: JSONObject, defaultTimestamp: Long): ProductEntity {
        val guid = p.optString("Guid", "")
        val rawBarcode = p.optString("Barcode", "").trim()
        val barcode = if (rawBarcode.isBlank() || rawBarcode.equals("null", ignoreCase = true)) null else rawBarcode
        val unitType = when (p.optInt("UnitType", 0)) {
            0 -> UnitType.DONA
            1 -> UnitType.METR
            2 -> UnitType.KG
            else -> UnitType.DONA
        }
        return ProductEntity(
            id = 0L,
            guid = if (guid.isNotBlank()) guid else java.util.UUID.randomUUID().toString(),
            barcode = barcode,
            name = p.optString("Name"),
            category = p.optString("Category", "Barchasi"),
            costPrice = p.optDouble("CostPrice", 0.0),
            costCurrency = p.optString("CostCurrency", "UZS"),
            sellingPrice = p.optDouble("SellingPrice", 0.0),
            sellingPrice2 = if (p.has("SellingPrice2") && !p.isNull("SellingPrice2")) p.optDouble("SellingPrice2") else null,
            stockQuantity = p.optDouble("StockQuantity", 0.0),
            unitType = unitType,
            minStockAlert = p.optDouble("MinStockAlert", 3.0),
            isDeleted = p.optBoolean("IsDeleted", false),
            note = p.optString("Note", ""),
            updatedAt = p.optLong("UpdatedAt", defaultTimestamp)
        )
    }

    private fun parseSaleJson(sJson: JSONObject): SaleEntity {
        val paymentType = when (val raw = sJson.opt("PaymentType")) {
            is Number -> when (raw.toInt()) {
                1 -> PaymentType.CARD
                2 -> PaymentType.SPLIT
                6 -> PaymentType.BRAK
                else -> PaymentType.CASH
            }
            is String -> when (raw.uppercase()) {
                "CARD" -> PaymentType.CARD
                "SPLIT" -> PaymentType.SPLIT
                "BRAK" -> PaymentType.BRAK
                else -> PaymentType.CASH
            }
            else -> PaymentType.CASH
        }
        return SaleEntity(
            id = 0L,
            guid = sJson.optString("Guid", java.util.UUID.randomUUID().toString()),
            totalAmount = sJson.optDouble("TotalAmount", 0.0),
            totalCost = sJson.optDouble("TotalCost", 0.0),
            paymentType = paymentType,
            cashAmount = sJson.optDouble("CashAmount", 0.0),
            cardAmount = sJson.optDouble("CardAmount", 0.0),
            taxAmount = sJson.optDouble("TaxAmount", 0.0),
            taxRate = sJson.optDouble("TaxRate", 0.0),
            createdAt = sJson.optLong("CreatedAt", System.currentTimeMillis()),
            userId = sJson.optLong("UserId", 1L),
            isSynced = true
        )
    }

    private suspend fun parseSaleItemsJson(itemsArray: JSONArray, saleGuid: String): List<SaleItemEntity> {
        val saleItems = mutableListOf<SaleItemEntity>()
        for (j in 0 until itemsArray.length()) {
            val iJson = itemsArray.getJSONObject(j)
            val pGuid = iJson.optString("ProductGuid", "")
            val pName = iJson.optString("ProductName", "")

            val localProd = if (pGuid.isNotBlank()) productDao.getProductByGuid(pGuid) else null
            val localProdId = localProd?.id ?: iJson.optLong("ProductId", 0L)

            saleItems.add(
                SaleItemEntity(
                    id = 0L,
                    saleId = 0L,
                    saleGuid = saleGuid,
                    productId = localProdId,
                    productGuid = pGuid,
                    productName = pName,
                    quantity = iJson.optDouble("Quantity", 1.0),
                    priceAtSale = iJson.optDouble("PriceAtSale", 0.0),
                    costAtSale = iJson.optDouble("CostAtSale", 0.0),
                    costCurrency = iJson.optString("CostCurrency", "UZS"),
                    warehouseGuid = iJson.optString("WarehouseGuid", ""),
                    warehouseName = iJson.optString("WarehouseName", "")
                )
            )
        }
        return saleItems
    }

    private fun warehouseToJson(w: WarehouseEntity): JSONObject {
        return JSONObject().apply {
            put("Id", w.id)
            put("Guid", w.guid)
            put("Name", w.name)
            put("IsPrimary", w.isPrimary)
            put("IsDeleted", w.isDeleted)
            put("UpdatedAt", w.updatedAt)
        }
    }

    private fun parseWarehouseJson(w: JSONObject, defaultTimestamp: Long): WarehouseEntity {
        return WarehouseEntity(
            id = 0L,
            guid = w.optString("Guid", java.util.UUID.randomUUID().toString()),
            name = w.optString("Name", "Ombor"),
            isPrimary = w.optBoolean("IsPrimary", false),
            isDeleted = w.optBoolean("IsDeleted", false),
            updatedAt = w.optLong("UpdatedAt", defaultTimestamp)
        )
    }
}
