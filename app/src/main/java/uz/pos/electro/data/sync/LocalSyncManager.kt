package uz.pos.electro.data.sync

import android.content.Context
import android.os.Build
import android.util.Log
import androidx.room.withTransaction
import androidx.sqlite.db.SupportSQLiteDatabase
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.*
import uz.pos.electro.data.local.entity.*
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.repository.CurrencyRepository
import uz.pos.electro.data.repository.TaxSettingsRepository
import java.net.HttpURLConnection
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

enum class LiveSyncStatus { OFFLINE, CONNECTING, CONNECTED, CONFLICT }
private class PendingSyncConflict(message: String) : Exception(message)
data class SyncSummary(val downloadedProducts: Int, val uploadedProducts: Int, val message: String)

@Singleton
class LocalSyncManager @Inject constructor(
    @ApplicationContext private val context: Context,
    private val database: AppDatabase,
    private val productDao: ProductDao,
    private val saleDao: SaleDao,
    private val warehouseDao: WarehouseDao,
    private val productStockDao: ProductStockDao,
    private val currencyRepository: CurrencyRepository,
    private val taxSettingsRepository: TaxSettingsRepository
) {
    private val prefs = context.getSharedPreferences("pos_local_sync_prefs", Context.MODE_PRIVATE)
    private val syncScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val syncMutex = Mutex()
    private var liveJob: Job? = null
    private val _liveSyncStatus = MutableStateFlow(LiveSyncStatus.OFFLINE)
    val liveSyncStatus: StateFlow<LiveSyncStatus> = _liveSyncStatus.asStateFlow()
    private val _syncMessage = MutableStateFlow("")
    val syncMessage = _syncMessage.asStateFlow()
    private val _pendingCount = MutableStateFlow(0)
    val pendingCount = _pendingCount.asStateFlow()
    private val _hasConflict = MutableStateFlow(false)
    val hasConflict = _hasConflict.asStateFlow()
    private fun db(): SupportSQLiteDatabase = database.openHelper.writableDatabase
    companion object {
        fun normalizeUrl(raw: String): String {
            val value = if (raw.trim().startsWith("{")) JSONObject(raw).getString("serverUrl") else raw.trim()
            val normalized = (if (value.startsWith("http://") || value.startsWith("https://")) value else "http://$value").trimEnd('/')
            val uri = java.net.URI(normalized)
            require(uri.host != null && uri.userInfo == null && uri.query == null && uri.fragment == null && uri.path.isNullOrEmpty()) { "Faqat kompyuter IP manzili va portini kiriting." }
            return normalized
        }
    }
    fun getServerUrl(): String? = prefs.getString("local_desktop_url", null)
    fun saveServerUrl(url: String) { prefs.edit().putString("local_desktop_url", normalizeUrl(url)).apply() }
    private fun token(): String? = prefs.getString("wifi_v2_token", null)
    suspend fun pairDesktop(raw: String, code: String = ""): Result<String> = withContext(Dispatchers.IO) {
        runCatching {
            val qr = if (raw.trim().startsWith("{")) JSONObject(raw) else null
            val base = normalizeUrl(raw)
            val pairingCode = qr?.optString("pairingCode")?.takeIf { it.isNotBlank() } ?: code
            val id = prefs.getString("wifi_device_id", null) ?: java.util.UUID.randomUUID().toString().also { prefs.edit().putString("wifi_device_id", it).commit() }
            syncMutex.withLock {
                val reply = request(base, "/api/v2/pair", JSONObject().put("code", pairingCode).put("deviceId", id).put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}"), authenticated = false)
                val serverId = reply.getString("serverId")
                val oldId = metadata("server_id")
                require(oldId == null || oldId == serverId) { "Bu baza boshqa kompyuterga bog'langan. Bazalarni alohida ko'chirish kerak; avtomatik aralashtirilmaydi." }
                database.withTransaction { putMetadata("server_id", serverId) }
                prefs.edit().putString("local_desktop_url", base).putString("wifi_v2_token", reply.getString("token")).commit()
            }
            "Line kassa"
        }
    }
    suspend fun pingDesktop(serverUrl: String): Result<String> = withContext(Dispatchers.IO) {
        runCatching { require(normalizeUrl(serverUrl) == getServerUrl()) { "Boshqa kompyuterga ulash uchun uning QR yoki ulanish kodidan foydalaning." }; val reply = request(normalizeUrl(serverUrl), "/api/ping"); require(reply.getInt("protocol") == 2); require(reply.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan. Avval qayta ulang." }; reply.getString("name") }
    }
    fun restartLiveSyncEngine() { liveJob?.cancel(); liveJob = null; startLiveSyncEngine() }
    fun startLiveSyncEngine() {
        if (liveJob?.isActive == true) return
        liveJob = syncScope.launch {
            // Only an explicit engine start shows a connection attempt. Background retries
            // retain OFFLINE/CONFLICT until their outcome is known.
            if (!getServerUrl().isNullOrBlank() && !token().isNullOrBlank()) {
                _liveSyncStatus.value = LiveSyncStatus.CONNECTING
            }
            while (isActive) {
                if (getServerUrl().isNullOrBlank() || token().isNullOrBlank()) {
                    _liveSyncStatus.value = LiveSyncStatus.OFFLINE
                    _pendingCount.value = pending().size
                    _syncMessage.value = "V2 sinxron uchun kompyuterdagi QR yoki ulanish kodidan foydalaning."
                } else {
                    try { syncOnce(); _liveSyncStatus.value = LiveSyncStatus.CONNECTED }
                    catch (e: CancellationException) { throw e }
                    catch (e: PendingSyncConflict) { _syncMessage.value = e.message ?: "Tahrirni tanlang"; _liveSyncStatus.value = LiveSyncStatus.CONFLICT }
                    catch (e: Exception) { _syncMessage.value = e.message ?: "Aloqa uzildi; amallar lokal navbatda."; _liveSyncStatus.value = LiveSyncStatus.OFFLINE; Log.w("WifiSyncV2", "Sync retry: ${e.message}") }
                }
                delay(2000)
            }
        }
    }
    // Writes are captured durably by triggers. These methods only wake the drain early.
    private fun wake() { syncScope.launch { try { syncOnce() } catch (e: CancellationException) { throw e } catch (_: Exception) {} } }
    fun sendLiveSale(sale: SaleEntity, items: List<SaleItemEntity>) = wake()
    fun sendLiveProduct(product: ProductEntity, warehouseGuid: String? = null) = wake()
    fun sendLiveWarehouse(warehouse: WarehouseEntity) = wake()
    fun sendLiveStockTransfer(productGuid: String, fromWarehouseGuid: String, toWarehouseGuid: String, quantity: Double) = wake()
    suspend fun syncWithDesktop(): Result<SyncSummary> = runCatching { require(!getServerUrl().isNullOrBlank() && !token().isNullOrBlank()) { "Avval kompyuter QR kodini skanerlang." }; syncOnce(); SyncSummary(0, 0, "V2 sinxron yakunlandi") }
    suspend fun getReturnDraft(saleGuid: String): JSONObject? = withContext(Dispatchers.IO) {
        db().query("SELECT payload,result,state FROM return_drafts WHERE sale_guid=?", arrayOf(saleGuid)).use { c ->
            if (!c.moveToFirst()) null else JSONObject().put("payload", JSONObject(c.getString(0)))
                .put("state", c.getString(2)).apply { if (!c.isNull(1)) put("result", JSONObject(c.getString(1))) }
        }
    }
    suspend fun quoteReturn(saleGuid: String): JSONObject = withContext(Dispatchers.IO) {
        syncOnce()
        syncMutex.withLock {
            request(getServerUrl() ?: error("Mahalliy kompyuterga ulang"), "/api/v2/returns/quote", JSONObject().put("SaleGuid", saleGuid))
        }
    }
    suspend fun saveReturnDraft(body: JSONObject) = withContext(Dispatchers.IO) {
        syncMutex.withLock {
            val saleGuid = body.getString("SaleGuid")
            val existing = getReturnDraft(saleGuid)
            require(existing == null || existing.getString("state") == "draft") { "Yuborilgan so'rov natijasini avval tekshiring" }
            val authority = metadata("server_id") ?: error("Avval mahalliy kompyuterga ulang")
            db().execSQL("INSERT OR REPLACE INTO return_drafts(sale_guid,request_guid,authority_guid,payload,state) VALUES(?,?,?,?,'draft')",
                arrayOf(saleGuid,body.getString("RequestGuid"),authority,body.toString()))
        }
    }
    suspend fun confirmReturn(saleGuid: String): JSONObject = withContext(Dispatchers.IO) {
        // Persisted payload survives process death and a lost response; never allocate another request ID on retry.
        val result = syncMutex.withLock {
            val draft = getReturnDraft(saleGuid) ?: error("Qaytarish loyihasi topilmadi")
            if (draft.has("result")) return@withLock draft.getJSONObject("result")
            val authority = db().query("SELECT authority_guid FROM return_drafts WHERE sale_guid=?",arrayOf(saleGuid)).use { it.moveToFirst(); it.getString(0) }
            require(authority == metadata("server_id")) { "So'rov boshqa kompyuterga tegishli" }
            db().execSQL("UPDATE return_drafts SET state='submitted' WHERE sale_guid=?",arrayOf(saleGuid))
            try {
                val reply = request(getServerUrl() ?: error("Mahalliy kompyuterga ulang"), "/api/v2/returns", draft.getJSONObject("payload"))
                db().execSQL("UPDATE return_drafts SET state='confirmed',result=? WHERE sale_guid=?",arrayOf(reply.toString(),saleGuid))
                reply
            } catch (e: ReturnRejected) {
                db().execSQL("UPDATE return_drafts SET state='draft' WHERE sale_guid=?",arrayOf(saleGuid))
                throw e
            }
        }
        try { syncOnce() } catch (e: CancellationException) { throw e } catch (_: Exception) { }
        result
    }
    suspend fun acknowledgeReturn(saleGuid: String) = withContext(Dispatchers.IO) {
        syncMutex.withLock { db().execSQL("DELETE FROM return_drafts WHERE sale_guid=? AND state='confirmed'",arrayOf(saleGuid)) }
    }
    private class ReturnRejected(message: String): IllegalArgumentException(message)
    private data class Pending(val seq: Long, val id: String, val kind: String, val guid: String, val warehouse: String, val delta: Double, val base: Long, val payload: String?, val group: String)
    private fun pending(): List<Pending> = db().query("SELECT seq,op_id,kind,entity_guid,warehouse_guid,delta,base_revision,payload,group_id FROM sync_journal WHERE acked=0 ORDER BY seq").use { c ->
        buildList { while (c.moveToNext()) add(Pending(c.getLong(0),c.getString(1),c.getString(2),c.getString(3),c.getString(4),c.getDouble(5),c.getLong(6),if(c.isNull(7)) null else c.getString(7),c.getString(8))) }
    }
    private fun metadata(key: String): String? = db().query("SELECT value FROM sync_meta WHERE key=?", arrayOf(key)).use { if(it.moveToFirst()) it.getString(0) else null }
    private fun putMetadata(key: String, value: String) { db().execSQL("INSERT OR REPLACE INTO sync_meta(key,value) VALUES(?,?)", arrayOf(key,value)) }
    private suspend fun freeze(): List<JSONObject> = database.withTransaction {
        // Coalesce unfrozen metadata edits only. Frozen operation bodies are immutable across retries.
        db().execSQL("UPDATE sync_journal SET base_revision=-1 WHERE payload IS NULL AND kind IN ('product','warehouse') AND EXISTS (SELECT 1 FROM sync_journal b WHERE b.kind=sync_journal.kind AND b.entity_guid=sync_journal.entity_guid AND b.base_revision=-1 AND b.payload IS NULL)")
        db().execSQL("DELETE FROM sync_journal WHERE payload IS NULL AND kind IN ('product','warehouse') AND seq NOT IN (SELECT MAX(seq) FROM sync_journal WHERE payload IS NULL AND kind IN ('product','warehouse') GROUP BY kind,entity_guid)")
        val candidates = pending()
        val seen = mutableSetOf<String>()
        val eligibleMetadata = candidates.filter { it.kind in listOf("product", "warehouse") && seen.add("${it.kind}:${it.guid}") }.map { it.id }.toSet()
        val blockedGroups = candidates.filter { it.kind in listOf("product", "warehouse") && it.id !in eligibleMetadata }.map { it.group }.filter { it.isNotBlank() }.toSet()
        val eligible = candidates.filter { it.group !in blockedGroups && (it.kind !in listOf("product", "warehouse") || it.id in eligibleMetadata) }
        val selected = eligible.take(250)
        val groups = selected.map { it.group }.filter { it.isNotBlank() }.toSet()
        val selectedIds = selected.map { it.id }.toSet()
        val ordered = eligible.filter { it.id in selectedIds || (it.group.isNotBlank() && it.group in groups) }
            .sortedBy { when(it.kind) { "warehouse" -> 0; "product" -> 1; "sale", "legacy_sale" -> 2; else -> 3 } }
        require(ordered.size <= 500) { "Bitta savdo yoki o‘tkazmada juda ko‘p amal bor; navbat saqlanadi." }
        ordered.map { op ->
            if (op.payload != null) JSONObject(op.payload) else {
                val data = when(op.kind) {
                    "product" -> {
                        val product = productDao.getProductByGuid(op.guid) ?: error("Navbatdagi tovar topilmadi.")
                        productToJson(product).also { json ->
                            if(op.base == -1L) {
                                val stocks = JSONArray()
                                for(stock in productStockDao.getStocksForProduct(op.guid)) {
                                    val unsyncedQty = db().query("SELECT COALESCE(SUM(i.quantity),0) FROM sale_items i JOIN sales s ON i.sale_id=s.id JOIN sync_journal j ON j.entity_guid=s.guid AND j.kind='legacy_sale' WHERE i.product_guid=? AND (i.warehouse_guid=? OR (i.warehouse_guid='' AND ?='main-default-warehouse'))",arrayOf(op.guid,stock.warehouseGuid,stock.warehouseGuid)).use { it.moveToFirst(); it.getDouble(0) }
                                    stocks.put(JSONObject().put("WarehouseGuid",stock.warehouseGuid).put("Quantity",stock.quantity + unsyncedQty - pending().filter { it.kind == "stock" && it.guid == op.guid && it.warehouse == stock.warehouseGuid }.sumOf { it.delta }))
                                }
                                json.put("InitialStocks",stocks)
                            }
                        }
                    }
                    "warehouse" -> {
                        val w = warehouseDao.getWarehouseByGuid(op.guid) ?: error("Navbatdagi ombor topilmadi.")
                        JSONObject().put("Guid",w.guid).put("Name",w.name).put("IsPrimary",w.isPrimary).put("IsDeleted",w.isDeleted).put("UpdatedAt",w.updatedAt)
                    }
                    "sale", "legacy_sale" -> {
                        val sale = saleDao.getSaleByGuid(op.guid) ?: error("Navbatdagi chek topilmadi.")
                        val full = saleDao.getSaleWithItemsById(sale.id) ?: error("Chek tovarlari topilmadi.")
                        saleToJson(full.sale, full.items)
                    }
                    "stock" -> JSONObject().put("WarehouseGuid",op.warehouse).put("Delta",op.delta)
                    else -> error("Noma'lum navbat turi.")
                }
                val json = JSONObject().put("group",op.group).put("id",op.id).put("kind",op.kind).put("guid",op.guid).put("baseRevision",maxOf(0,op.base)).put("bootstrap",op.base == -1L).put("data",data)
                db().execSQL("UPDATE sync_journal SET payload=? WHERE op_id=?",arrayOf(json.toString(),op.id))
                json
            }
        }
    }
    private suspend fun syncOnce() = withContext(Dispatchers.IO) { syncMutex.withLock {
        val base = getServerUrl() ?: return@withLock
        if(token().isNullOrBlank()) return@withLock
        // A known conflict needs a user choice, not another failed push every two seconds.
        // Probe transport while paused so loss/recovery of Wi-Fi remains visible.
        if (db().query("SELECT 1 FROM sync_conflicts LIMIT 1").use { it.moveToFirst() }) {
            _hasConflict.value = true
            _pendingCount.value = pending().size
            val ping = request(base, "/api/ping")
            require(ping.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan." }
            throw PendingSyncConflict("Narx yoki tovar tahriri farq qiladi. Telefon yoki kompyuter tahririni tanlang; navbat saqlanadi.")
        }
        // Bounded batches, but drain all prior operations before pulling authoritative stock.
        while(true) {
            currentCoroutineContext().ensureActive()
            val ops = freeze()
            _pendingCount.value = pending().size
            if(ops.isEmpty()) break
            val reply = request(base,"/api/v2/push",JSONObject().put("operations",JSONArray(ops)))
            val accepted = reply.getJSONArray("accepted")
            val sentIds = ops.map { it.getString("id") }.toSet()
            val acceptedIds = (0 until accepted.length()).map { accepted.getJSONObject(it).getString("id") }.toSet()
            require(acceptedIds == sentIds) { "Server barcha amallarni tasdiqlamadi; navbat saqlanadi." }
            database.withTransaction {
                for(i in 0 until accepted.length()) {
                    val ack = accepted.getJSONObject(i)
                    val id = ack.getString("id")
                    val kind = ack.getString("kind")
                    val guid = ack.getString("guid")
                    val op = ops.first { it.getString("id") == id }
                    db().execSQL("UPDATE sync_journal SET acked=1 WHERE op_id=?",arrayOf(id))
                    if(kind == "product" || kind == "warehouse") {
                        val revision = ack.getLong("revision")
                        db().execSQL("INSERT OR REPLACE INTO sync_versions(kind,entity_guid,revision) VALUES(?,?,?)",arrayOf(kind,guid,revision))
                        // An edit made locally during the request descends from our accepted edit.
                        db().execSQL("UPDATE sync_journal SET base_revision=? WHERE acked=0 AND payload IS NULL AND kind=? AND entity_guid=? AND (base_revision=? OR base_revision=-1)",arrayOf(revision,kind,guid,op.getLong("baseRevision")))
                    }
                    if(kind == "sale" || kind == "legacy_sale") saleDao.markSalesSyncedByGuids(listOf(guid))
                }
            }
        }
        val cursor = metadata("cursor")?.toLongOrNull() ?: 0L
        val reply = request(base,"/api/v2/pull?cursor=$cursor")
        require(reply.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan; sinxron to'xtatildi." }
        applySnapshot(reply)
        _pendingCount.value = pending().size
        _hasConflict.value = db().query("SELECT 1 FROM sync_conflicts LIMIT 1").use { it.moveToFirst() }
        _syncMessage.value = if(_pendingCount.value == 0) "Barcha amallar sinxronlandi." else "${_pendingCount.value} ta amal navbatda."
    } }
    private suspend fun applySnapshot(root: JSONObject) = database.withTransaction {
        val sql = db()
        sql.execSQL("UPDATE sync_control SET applying=1 WHERE id=1")
        try {
            val localPending = pending()
            for(i in 0 until root.getJSONArray("warehouses").length()) {
                val w = root.getJSONArray("warehouses").getJSONObject(i)
                if(localPending.none { it.kind == "warehouse" && it.guid == w.getString("Guid") }) {
                    val entity = parseWarehouseJson(w,System.currentTimeMillis())
                    val old = warehouseDao.getWarehouseByGuid(entity.guid)
                    if(old == null) warehouseDao.insertWarehouse(entity) else warehouseDao.updateWarehouse(entity.copy(id=old.id))
                    saveVersion("warehouse",entity.guid,w.getLong("Revision"))
                }
            }
            for(i in 0 until root.getJSONArray("products").length()) {
                val p = root.getJSONArray("products").getJSONObject(i)
                if(localPending.none { it.kind == "product" && it.guid == p.getString("Guid") }) {
                    val entity = parseProductJson(p,System.currentTimeMillis())
                    val old = productDao.getProductByGuid(entity.guid)
                    if(old == null) productDao.insertProduct(entity) else productDao.updateProduct(entity.copy(id=old.id))
                    saveVersion("product",entity.guid,p.getLong("Revision"))
                }
            }
            for(i in 0 until root.getJSONArray("sales").length()) {
                val s = root.getJSONArray("sales").getJSONObject(i)
                val guid = s.getString("Guid")
                if(saleDao.getSaleByGuid(guid) == null) {
                    val sale = parseSaleJson(s)
                    saleDao.insertSaleWithItems(sale,parseSaleItemsJson(s.getJSONArray("Items"),guid))
                }
                // Stock comes from the same snapshot. Never deduct a received receipt again.
            }
            for(i in 0 until root.getJSONArray("productStocks").length()) {
                val stock = root.getJSONArray("productStocks").getJSONObject(i)
                val guid = stock.getString("ProductGuid")
                val wh = stock.getString("WarehouseGuid")
                val unpushed = localPending.filter { it.kind == "stock" && it.guid == guid && it.warehouse == wh }.sumOf { it.delta }
                productStockDao.upsertStock(guid,wh,stock.getDouble("Quantity") + unpushed,stock.getLong("UpdatedAt"))
            }
            sql.execSQL("UPDATE products SET stock_quantity=(SELECT COALESCE(SUM(quantity),0) FROM product_stocks WHERE product_guid=products.guid)")
            for ((table, key) in listOf("returns" to "returns", "return_items" to "returnItems", "return_quarantine" to "returnQuarantine")) {
                val data = root.optJSONArray(key) ?: continue
                if (table == "return_quarantine") sql.execSQL("DELETE FROM return_quarantine")
                val columns = sql.query("PRAGMA table_info($table)").use { c -> buildList { while(c.moveToNext()) add(c.getString(1)) } }
                for (i in 0 until data.length()) {
                    val row = data.getJSONObject(i)
                    require(columns.all { row.has(it) }) { "Qaytarish tarixi to'liq emas" }
                    val values = columns.map { if(row.isNull(it)) null else row.get(it) }.toTypedArray()
                    sql.execSQL("INSERT OR IGNORE INTO $table (${columns.joinToString(",")}) VALUES (${columns.joinToString(",") { "?" }})", values)
                }
            }
            putMetadata("cursor",root.getLong("cursor").toString())
        } finally { sql.execSQL("UPDATE sync_control SET applying=0 WHERE id=1") }
        // Settings are unrelated to inventory capture. Existing CBU/Firebase behavior is preserved.
        if(root.has("cardTaxRate")) taxSettingsRepository.setCardTaxRate(root.getDouble("cardTaxRate"))
        if(root.has("usdRate")) currencyRepository.updateCachedRate(root.getDouble("usdRate"))
    }
    private fun saveVersion(kind: String,guid: String,revision: Long) { db().execSQL("INSERT OR REPLACE INTO sync_versions(kind,entity_guid,revision) VALUES(?,?,?)",arrayOf(kind,guid,revision)) }
    suspend fun resolveConflicts(keepLocal: Boolean) = withContext(Dispatchers.IO) { syncMutex.withLock {
        database.withTransaction {
            db().query("SELECT kind,entity_guid,revision FROM sync_conflicts").use { c ->
                while(c.moveToNext()) {
                    val kind=c.getString(0); val guid=c.getString(1); val revision=c.getLong(2)
                    if(keepLocal) db().execSQL("UPDATE sync_journal SET base_revision=?,payload=NULL WHERE acked=0 AND kind=? AND entity_guid=?",arrayOf(revision,kind,guid))
                    else db().execSQL("DELETE FROM sync_journal WHERE acked=0 AND kind=? AND entity_guid=?",arrayOf(kind,guid))
                }
            }
            db().execSQL("DELETE FROM sync_conflicts")
            putMetadata("cursor","0")
        }
        _hasConflict.value=false
    } }
    private fun request(base: String,path: String,body: JSONObject? = null,authenticated: Boolean = true): JSONObject {
        val address = java.net.InetAddress.getByName(java.net.URI(base).host)
        require(address.isSiteLocalAddress || address.isLinkLocalAddress || address.isLoopbackAddress) { "Wi-Fi sinxron faqat lokal tarmoqda ishlaydi." }
        val conn = URL(base + path).openConnection() as HttpURLConnection
        conn.connectTimeout=5000;conn.readTimeout=15000;conn.instanceFollowRedirects=false
        try {
            if(authenticated) {
                conn.setRequestProperty("Authorization","Bearer ${token() ?: error("Qurilmani QR orqali ulang.")}")
                conn.setRequestProperty("X-LinePOS-Server-Id", metadata("server_id") ?: error("Qurilmani QR orqali ulang."))
            }
            conn.requestMethod=if(body == null) "GET" else "POST"
            if(body != null) {
                val bytes=body.toString().toByteArray(Charsets.UTF_8)
                require(bytes.size <= 8*1024*1024) { "Sinxron so'rovi 8 MB dan katta." }
                conn.doOutput=true;conn.setRequestProperty("Content-Type","application/json; charset=utf-8");conn.setFixedLengthStreamingMode(bytes.size)
                conn.outputStream.use { it.write(bytes) }
            }
            val status=conn.responseCode
            val text=(if(status in 200..299) conn.inputStream else conn.errorStream)?.bufferedReader(Charsets.UTF_8)?.use { it.readText() } ?: "{}"
            val reply=JSONObject(text)
            if(status == 409) {
                val kind=reply.getString("kind");val guid=reply.getString("guid")
                db().execSQL("INSERT OR REPLACE INTO sync_conflicts(kind,entity_guid,revision,message) VALUES(?,?,?,?)",arrayOf(kind,guid,reply.getLong("revision"),reply.getString("error")))
                _hasConflict.value=true
                throw PendingSyncConflict(reply.getString("error"))
            }
            if (status == 400 && path == "/api/v2/returns") throw ReturnRejected(reply.optString("error", "Qaytarish rad etildi"))
            require(status in 200..299) { reply.optString("error","HTTP $status") }
            return reply
        } finally { conn.disconnect() }
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
            put("UsdRate", sale.usdRate)
            put("PaymentType", when (sale.paymentType) {
                PaymentType.CASH -> 0
                PaymentType.CARD -> 1
                PaymentType.SPLIT -> 2
                PaymentType.BRAK -> 6
                PaymentType.RETURN -> 7
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
                    put("Guid", item.guid)
                    put("SaleId", item.saleId)
                    put("SaleGuid", item.saleGuid)
                    put("ProductId", item.productId)
                    put("ProductGuid", item.productGuid)
                    put("ProductName", item.productName)
                    put("CategoryAtSale", item.categoryAtSale)
                    put("UnitAtSale", item.unitAtSale)
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
                7 -> PaymentType.RETURN
                else -> PaymentType.CASH
            }
            is String -> when (raw.uppercase()) {
                "CARD" -> PaymentType.CARD
                "SPLIT" -> PaymentType.SPLIT
                "BRAK" -> PaymentType.BRAK
                "RETURN" -> PaymentType.RETURN
                else -> PaymentType.CASH
            }
            else -> PaymentType.CASH
        }
        return SaleEntity(
            id = 0L,
            guid = sJson.optString("Guid", java.util.UUID.randomUUID().toString()),
            totalAmount = sJson.optDouble("TotalAmount", 0.0),
            totalCost = sJson.optDouble("TotalCost", 0.0),
            usdRate = sJson.optDouble("UsdRate", 0.0),
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
                    guid = iJson.optString("Guid", "").ifBlank { "$saleGuid:${j + 1}" },
                    productId = localProdId,
                    productGuid = pGuid,
                    productName = pName,
                    categoryAtSale = iJson.optString("CategoryAtSale", ""),
                    unitAtSale = iJson.optString("UnitAtSale", ""),
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
