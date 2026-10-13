package uz.pos.electro.data.sync

import android.content.Context
import android.os.Build
import android.util.Log
import androidx.room.withTransaction
import androidx.sqlite.db.SupportSQLiteDatabase
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
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
import uz.pos.electro.data.debt.DebtEnvelope
import uz.pos.electro.data.debt.DebtEnvelopeInbox
import uz.pos.electro.data.debt.DebtReceiveStatus
import uz.pos.electro.data.debt.DebtExtendedWire
import uz.pos.electro.data.debt.DebtCustomerEdits
import uz.pos.electro.data.debt.DebtService
import uz.pos.electro.data.debt.DebtWire
import java.net.HttpURLConnection
import java.net.URL
import javax.inject.Inject
import javax.inject.Singleton

enum class LiveSyncStatus { OFFLINE, CONNECTING, CONNECTED, CONFLICT }
class ServerChangedException(val serverId: String, message: String) : Exception(message)
private class SyncAuthenticationException(message: String) : Exception(message)
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
    private val taxSettingsRepository: TaxSettingsRepository,
    private val debtService: DebtService? = null
) {
    private val prefs = context.getSharedPreferences("pos_local_sync_prefs", Context.MODE_PRIVATE)
    private val syncScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val syncMutex = Mutex()
    private val discovery = LanDesktopDiscovery(context)
    private var lastDiscoveryAt = -10_000L
    private var customerEditMessage: String? = null
    private var debtCommandMessage: String? = null
    private var debtStockWatermarkSupported = false
    private var liveJob: Job? = null
    private val _liveSyncStatus = MutableStateFlow(LiveSyncStatus.OFFLINE)
    val liveSyncStatus: StateFlow<LiveSyncStatus> = _liveSyncStatus.asStateFlow()
    private val _dataUpdateEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val dataUpdateEvents: SharedFlow<Unit> = _dataUpdateEvents.asSharedFlow()
    private val _syncMessage = MutableStateFlow("")
    val syncMessage = _syncMessage.asStateFlow()
    private val _pendingCount = MutableStateFlow(0)
    val pendingCount = _pendingCount.asStateFlow()
    private val _hasConflict = MutableStateFlow(false)
    val hasConflict = _hasConflict.asStateFlow()
    private fun db(): SupportSQLiteDatabase = database.openHelper.writableDatabase
    private var cachedDebtInbox: DebtEnvelopeInbox? = null
    private var cachedDebtStore: String? = null

    private fun getDebtInbox(storeGuid: String): DebtEnvelopeInbox {
        val existing = cachedDebtInbox
        if (existing != null && cachedDebtStore == storeGuid) return existing
        val inbox = DebtEnvelopeInbox(
            database = database,
            store = storeGuid,
            canSync = { true },
            canImportActor = { it.isNotBlank() },
            resolveActorUser = { _ ->
                db().query("SELECT id FROM users ORDER BY id ASC LIMIT 1").use { c ->
                    if (c.moveToFirst()) c.getLong(0) else 1L
                }
            }
        )
        cachedDebtInbox = inbox
        cachedDebtStore = storeGuid
        return inbox
    }

    private fun scalar(db: SupportSQLiteDatabase, sql: String, vararg args: Any?): Any? =
        db.query(sql, args).use { c ->
            if (c.moveToFirst()) {
                when (c.getType(0)) {
                    android.database.Cursor.FIELD_TYPE_NULL -> null
                    android.database.Cursor.FIELD_TYPE_INTEGER -> c.getLong(0)
                    android.database.Cursor.FIELD_TYPE_FLOAT -> c.getDouble(0)
                    else -> c.getString(0)
                }
            } else null
        }

    private fun totalPendingCount(): Int {
        val legacy = pending().size
        val debt = db().query("SELECT COUNT(DISTINCT group_id) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked = -1 AND group_id <> ''").use { c ->
            if (c.moveToFirst()) c.getInt(0) else 0
        }
        val edits = db().query("SELECT COUNT(*) FROM sync_meta WHERE key LIKE 'debt_customer_edits:%'").use { c -> c.moveToFirst(); c.getInt(0) }
        val commands=db().query("SELECT value FROM sync_meta WHERE key GLOB 'debt_command_v2:*'").use { c ->
            var count=0;while(c.moveToNext()) { val e=JSONObject(c.getString(0));if(!e.has("result") && !e.has("error"))count++ };count
        }
        val incoming=db().query("SELECT COUNT(*) FROM debt_sync_inbox").use { c -> c.moveToFirst();c.getInt(0) }
        return legacy + debt + edits + commands + incoming
    }

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
    suspend fun pairDesktop(raw: String, code: String = "", force: Boolean = false): Result<String> = withContext(Dispatchers.IO) {
        runCatching {
            val qr = if (raw.trim().startsWith("{")) JSONObject(raw) else null
            val base = normalizeUrl(raw)
            val pairingCode = qr?.optString("pairingCode")?.takeIf { it.isNotBlank() } ?: code
            val id = prefs.getString("wifi_device_id", null) ?: java.util.UUID.randomUUID().toString().also { prefs.edit().putString("wifi_device_id", it).commit() }
            syncMutex.withLock {
                // A fresh QR for the same PC is just an address change. Reuse the token,
                // cursors and durable queues, even if the QR pairing code has expired.
                if (!token().isNullOrBlank() && metadata("server_id") != null) {
                    val ping = try { request(base, "/api/ping") }
                    catch (e: CancellationException) { throw e }
                    catch (_: Exception) { null }
                    if (ping?.optInt("protocol") == 2 && ping.optString("serverId") == metadata("server_id")) {
                        prefs.edit().putString("local_desktop_url", base).commit()
                        return@withLock
                    }
                }
                val reply = request(base, "/api/v2/pair", JSONObject().put("code", pairingCode).put("deviceId", id).put("deviceName", "${Build.MANUFACTURER} ${Build.MODEL}"), authenticated = false)
                val serverId = reply.getString("serverId")
                val oldId = metadata("server_id")
                val capabilities = reply.optJSONArray("capabilities")
                val hasDebt = capabilities != null && (0 until capabilities.length()).any { capabilities.getString(it) == DebtWire.CAPABILITY }
                val storeGuid = reply.optString("storeGuid").takeIf { it.isNotBlank() }

                database.withTransaction {
                    if(hasDebt && storeGuid!=null) {
                        DebtService.rebindDebtStore(db(),storeGuid)
                        debtService?.resetRepo(storeGuid)
                        cachedDebtInbox=null;cachedDebtStore=null
                    }
                    val epoch=reply.optString("historyEpoch").takeIf { it.isNotBlank() }
                    val changedHistory=epoch!=null && metadata("debt_history_epoch")!=null && epoch!=metadata("debt_history_epoch")
                    if(changedHistory) resetDebtHistory(epoch!!)
                    putMetadata("server_id",serverId)
                    putMetadata("cursor","0");putMetadata("debt_cursor","0")
                    putMetadata("debt_stock_watermark","0")
                    if(oldId!=null && oldId!=serverId) {
                        db().execSQL("DELETE FROM sync_versions")
                        db().execSQL("DELETE FROM sync_conflicts")
                        db().execSQL("UPDATE sync_journal SET base_revision=-1 WHERE acked=0")
                    }
                    if(epoch!=null) putMetadata("debt_history_epoch",epoch)
                }
                prefs.edit().putString("local_desktop_url", base).putString("wifi_v2_token", reply.getString("token")).commit()
            }
            currentCoroutineContext().ensureActive()
            restartLiveSyncEngine()
            "Line kassa"
        }
    }

    suspend fun unlinkDesktop() = withContext(Dispatchers.IO) {
        liveJob?.cancel()
        liveJob = null
        _liveSyncStatus.value = LiveSyncStatus.OFFLINE
        syncMutex.withLock {
        prefs.edit().remove("local_desktop_url").remove("wifi_v2_token").commit()
        cachedDebtInbox = null
        cachedDebtStore = null
        database.withTransaction {
            putMetadata("cursor","0")
            putMetadata("debt_cursor","0")
        }
        }
        _syncMessage.value = "Kompyuter bilan aloqa uzildi. Yangi QR kod orqali ulanishingiz mumkin."
    }
    suspend fun pingDesktop(serverUrl: String): Result<String> = withContext(Dispatchers.IO) {
        runCatching { syncMutex.withLock {
            val normUrl = normalizeUrl(serverUrl)
            val currentUrl = getServerUrl()
            val reply = request(normUrl, "/api/ping")
            require(reply.getInt("protocol") == 2)
            val incomingServerId = reply.getString("serverId")
            val currentServerId = metadata("server_id")

            if (currentServerId != null && incomingServerId == currentServerId) {
                // Same store PC! Silently adopt the new IP across Wi-Fi networks.
                if (normUrl != currentUrl) {
                    prefs.edit().putString("local_desktop_url", normUrl).commit()
                    restartLiveSyncEngine()
                }
            } else if (currentServerId != null && incomingServerId != currentServerId) {
                throw ServerChangedException(incomingServerId, "Kompyuter bazasi almashgan. Yangi QR kod orqali qayta ulang.")
            } else {
                prefs.edit().putString("local_desktop_url", normUrl).commit()
            }

            val capabilities = reply.optJSONArray("capabilities")
            val hasDebt = capabilities != null && (0 until capabilities.length()).any { capabilities.getString(it) == DebtWire.CAPABILITY }
            val storeGuid = reply.optString("storeGuid").takeIf { it.isNotBlank() }
            if (hasDebt && storeGuid != null) {
                val existingStore = scalar(db(), "SELECT store_guid FROM debt_scope WHERE id=1")
                if (existingStore != storeGuid) {
                    DebtService.rebindDebtStore(db(), storeGuid)
                    debtService?.resetRepo(storeGuid)
                    cachedDebtInbox = null
                    cachedDebtStore = null
                }
            }
            reply.getString("name")
        } }
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
                    _pendingCount.value = totalPendingCount()
                    _syncMessage.value = "V2 sinxron uchun kompyuterdagi QR yoki ulanish kodidan foydalaning."
                } else {
                    try { syncOnce(); _liveSyncStatus.value = LiveSyncStatus.CONNECTED }
                    catch (e: CancellationException) { throw e }
                    catch (e: PendingSyncConflict) { _syncMessage.value = e.message ?: "Tahrirni tanlang"; _liveSyncStatus.value = LiveSyncStatus.CONFLICT }
                    catch (e: Exception) {
                        _syncMessage.value = e.message ?: "Aloqa uzildi; amallar lokal navbatda."
                        _liveSyncStatus.value = LiveSyncStatus.OFFLINE
                        Log.w("WifiSyncV2", "Sync retry: ${e.message}")
                        // The old IP may now belong to another PC (401). Discovery never re-pairs.
                        if (e is java.io.IOException || e is SyncAuthenticationException) {
                            try { if (rediscoverDesktop()) continue }
                            catch (cancel: CancellationException) { throw cancel }
                            catch (failure: Exception) { Log.w("WifiSyncV2", "Discovery: ${failure.message}") }
                        }
                    }
                }
                delay(2000)
            }
        }
    }
    private suspend fun rediscoverDesktop(): Boolean = syncMutex.withLock {
        val now = android.os.SystemClock.elapsedRealtime()
        if (now - lastDiscoveryAt < 10_000) return@withLock false
        val serverId = metadata("server_id") ?: return@withLock false
        val deviceId = prefs.getString("wifi_device_id", null) ?: return@withLock false
        val savedToken = token() ?: return@withLock false
        lastDiscoveryAt = now
        val base = discovery.find(serverId, deviceId, savedToken) ?: return@withLock false
        currentCoroutineContext().ensureActive()
        val ping = request(base, "/api/ping")
        require(ping.optInt("protocol") == 2 && ping.optString("serverId") == serverId) { "Kompyuter tasdiqlanmadi." }
        currentCoroutineContext().ensureActive()
        // No pairing, rebind, cursor reset, or queue deletion on an IP-only change.
        check(prefs.edit().putString("local_desktop_url", base).commit()) { "Yangi IP saqlanmadi." }
        true
    }

    // Writes are captured durably by triggers. These methods only wake the drain early.
    private fun wake() { syncScope.launch { try { syncOnce() } catch (e: CancellationException) { throw e } catch (_: Exception) {} } }
    fun sendLiveSale(sale: SaleEntity, items: List<SaleItemEntity>) = wake()
    fun sendLiveProduct(product: ProductEntity, warehouseGuid: String? = null) = wake()
    fun sendLiveWarehouse(warehouse: WarehouseEntity) = wake()
    fun sendLiveStockTransfer(productGuid: String, fromWarehouseGuid: String, toWarehouseGuid: String, quantity: Double) = wake()
    suspend fun syncWithDesktop(): Result<SyncSummary> = runCatching { require(!getServerUrl().isNullOrBlank() && !token().isNullOrBlank()) { "Avval kompyuter QR kodini skanerlang." }; syncOnce(); SyncSummary(0, 0, "V2 sinxron yakunlandi") }
    suspend fun localReturnQuote(receipt: uz.pos.electro.data.local.relation.SaleWithItems): JSONObject = withContext(Dispatchers.IO) {
        database.withTransaction {
            // Do not substitute zero or current prices for unreadable historical amounts.
            val financials = uz.pos.electro.data.model.SaleAccounting.lines(receipt)
            val lines = JSONArray()
            receipt.items.sortedBy { it.id }.forEachIndexed { i, item ->
                var quantity = java.math.BigDecimal.ZERO
                var refunded = java.math.BigDecimal.ZERO
                db().query("SELECT quantity, refund_amount_uzs FROM return_items WHERE sale_item_guid=?", arrayOf(item.guid)).use { c ->
                    while (c.moveToNext()) {
                        quantity += c.getString(0).toBigDecimal()
                        refunded += c.getString(1).toBigDecimal()
                    }
                }
                lines.put(JSONObject().put("Guid", item.guid).put("ProductName", item.productName).put("WarehouseGuid", item.warehouseGuid)
                    .put("Sold", item.quantity).put("Returned", quantity.toPlainString()).put("Refunded", refunded.toPlainString()).put("Revenue", financials[i].totalPrice))
            }
            var returnedFee = java.math.BigDecimal.ZERO
            db().query("SELECT fee_reversal FROM returns WHERE sale_guid=?", arrayOf(receipt.sale.guid)).use { c ->
                while (c.moveToNext()) returnedFee += c.getString(0).toBigDecimal()
            }
            val remainingFee = java.math.BigDecimal.valueOf(receipt.sale.taxAmount) - returnedFee
            val res = JSONObject().put("SaleGuid", receipt.sale.guid).put("Lines", lines)
                .put("RemainingFee", remainingFee.toPlainString())
            if (receipt.sale.paymentType == PaymentType.DEBT) {
                db().query("SELECT guid, customer_guid, original_debt_minor FROM debt_accounts WHERE sale_guid=?", arrayOf(receipt.sale.guid)).use { c ->
                    require(c.moveToFirst()) { "Nasiya hisobining ma’lumoti topilmadi. Avval qarz sinxronini yakunlang." }
                    val accGuid = c.getString(0)
                    val deltas = mutableListOf<Long>()
                    db().query("SELECT debt_delta_minor FROM debt_event_lines WHERE account_guid=?", arrayOf(accGuid)).use { lc ->
                        while (lc.moveToNext()) deltas.add(lc.getLong(0))
                    }
                    res.put("DebtAccountGuid", accGuid).put("CustomerGuid", c.getString(1))
                        .put("AccountBalanceMinor", uz.pos.electro.data.debt.DebtAccounting.balance(c.getLong(2), deltas))
                }
            }
            res
        }
    }
    suspend fun returnHistory(receiptGuid: String): String = withContext(Dispatchers.IO) {
        val original = db().query("SELECT sale_guid FROM returns WHERE guid=?",arrayOf(receiptGuid)).use { if(it.moveToFirst()) it.getString(0) else null }
        val lines = mutableListOf<String>()
        if (original != null) lines.add("Asl chek: LP-" + original.replace("-", "").uppercase())
        db().query("SELECT guid,cash_refund+card_refund,reason,status FROM returns WHERE sale_guid=? ORDER BY created_at,guid",arrayOf(original ?: receiptGuid)).use { c ->
            while(c.moveToNext()) lines.add((if(c.getString(3).startsWith("reversal:")) "RV-" else "RT-") + c.getString(0).replace("-", "").uppercase() + " • " + c.getDouble(1) + " so‘m • " + c.getString(2))
        }
        lines.joinToString("\n")
    }
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
                val reply = request(getServerUrl() ?: error("Mahalliy kompyuterga ulang"), if (draft.getJSONObject("payload").has("ReturnGuid")) "/api/v2/returns/reverse" else "/api/v2/returns", draft.getJSONObject("payload"))
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

    // Kept until the caller acknowledges the result. A lost response/process restart
    // reuses the persisted body; repeated calls never allocate another payout.
    private suspend fun durableDebtCommand(kind: String, subject: String, payload: JSONObject): String = withContext(Dispatchers.IO) {
        val slot="debt_command_v2:$kind:$subject"
        syncMutex.withLock {
            val old=metadata(slot)?.let { JSONObject(it) }
            val entry=old ?: JSONObject().put("server",metadata("server_id") ?: error("Avval kompyuterga ulang"))
                .put("kind",kind).put("body",JSONObject(payload.toString()).put("RequestGuid",java.util.UUID.randomUUID().toString()).put("OccurredAt",System.currentTimeMillis()))
            val body=entry.getJSONObject("body")
            require(payload.keys().asSequence().all { body.get(it).toString()==payload.get(it).toString() }) {
                "Oldingi qarz so‘rovi natijasini avval tekshiring; uning summasini o‘zgartirib bo‘lmaydi."
            }
            if(old==null)putMetadata(slot,entry.toString())
            if(entry.has("result")) return@withLock entry.getString("result")
            if(entry.has("error")) error(entry.getString("error"))
            wake()
            throw IllegalStateException("Qarz so‘rovi navbatda saqlandi. Aloqa qaytgach kompyuter tasdiqlaydi; natijani tekshiring.")
        }
    }
    suspend fun acknowledgeDebtCommand(kind: String, subject: String) = withContext(Dispatchers.IO) {
        syncMutex.withLock {
            val key="debt_command_v2:$kind:$subject"
            val entry=metadata(key)?.let { JSONObject(it) }
            require(entry==null || entry.has("result") || entry.has("error")) { "So‘rov natijasi hali noma’lum" }
            db().execSQL("DELETE FROM sync_meta WHERE key=?",arrayOf(key))
        }
    }
    private suspend fun drainDebtCommands(base: String) {
        val entries=db().query("SELECT key,value FROM sync_meta WHERE key GLOB 'debt_command_v2:*' ORDER BY key").use { c ->
            buildList { while(c.moveToNext()) add(c.getString(0) to c.getString(1)) }
        }
        for((key,value) in entries) {
            val entry=JSONObject(value)
            if(entry.has("error")) { debtCommandMessage="Qarz so‘rovi rad etildi: "+entry.getString("error");continue }
            if(entry.has("result")) continue
            require(entry.getString("server")==metadata("server_id")) { "Qarz so‘rovi boshqa baza uchun saqlangan" }
            try {
                val result=request(base,"/api/v2/debt/"+entry.getString("kind"),entry.getJSONObject("body"))
                val guid=result.getString("resultGuid")
                require(guid==entry.getJSONObject("body").getString("RequestGuid"))
                entry.put("result",guid);putMetadata(key,entry.toString())
            } catch(e: DebtCommandRejected) {
                entry.put("error",e.message);putMetadata(key,entry.toString());debtCommandMessage="Qarz so‘rovi rad etildi: ${e.message}"
            }
        }
    }
    suspend fun reversePaymentOnDesktop(paymentEventGuid: String, reason: String, refundedFeeMinor: Long = 0L): String =
        durableDebtCommand("reverse_payment",paymentEventGuid,JSONObject().put("PaymentEventGuid",paymentEventGuid).put("Reason",reason).put("RefundedFeeMinor",refundedFeeMinor))
    suspend fun refundCreditOnDesktop(customerGuid: String, accountGuid: String, cashMinor: Long, cardMinor: Long, reason: String): String =
        durableDebtCommand("refund_credit",accountGuid,JSONObject().put("CustomerGuid",customerGuid).put("AccountGuid",accountGuid).put("CashMinor",cashMinor).put("CardMinor",cardMinor).put("Reason",reason))
    suspend fun transferCreditOnDesktop(customerGuid: String, sourceAccountGuid: String, targetAccountGuid: String, amountMinor: Long, reason: String): String =
        durableDebtCommand("transfer_credit",sourceAccountGuid,JSONObject().put("CustomerGuid",customerGuid).put("SourceAccountGuid",sourceAccountGuid).put("TargetAccountGuid",targetAccountGuid).put("AmountMinor",amountMinor).put("Reason",reason))
    private class DebtCommandRejected(message: String): IllegalArgumentException(message)

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
                                    val debtDelta=db().query("SELECT COALESCE(SUM(delta),0) FROM sync_journal WHERE kind='debt_stock' AND acked=-1 AND entity_guid=? AND warehouse_guid=?",arrayOf(op.guid,stock.warehouseGuid)).use { c -> c.moveToFirst();c.getDouble(0) }
                                    stocks.put(JSONObject().put("WarehouseGuid",stock.warehouseGuid).put("Quantity",stock.quantity + unsyncedQty - debtDelta - pending().filter { it.kind == "stock" && it.guid == op.guid && it.warehouse == stock.warehouseGuid }.sumOf { it.delta }))
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

    private fun resetDebtHistory(epoch: String) {
        db().execSQL("UPDATE sync_journal SET acked=-1 WHERE kind LIKE 'debt_%' AND acked=1")
        db().execSQL("DELETE FROM sync_meta WHERE key GLOB 'debt_remote_seq:*'")
        putMetadata("debt_stock_watermark","0")
        putMetadata("cursor","0");putMetadata("debt_cursor","0")
        putMetadata("debt_history_epoch",epoch)
    }
    private suspend fun syncDebtIfSupported(base: String) {
        customerEditMessage = null
        debtCommandMessage = null
        val ping = request(base, "/api/ping")
        require(ping.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan." }
        val capabilities = ping.optJSONArray("capabilities")
        val hasDebt = capabilities != null && (0 until capabilities.length()).any { capabilities.getString(it) == DebtWire.CAPABILITY }
        if (!hasDebt) return

        val storeGuid = ping.optString("storeGuid").takeIf { it.isNotBlank() } ?: return
        val existingStore = scalar(db(), "SELECT store_guid FROM debt_scope WHERE id=1")
        if (existingStore != storeGuid) {
            DebtService.rebindDebtStore(db(), storeGuid)
            debtService?.resetRepo(storeGuid)
            cachedDebtInbox = null
            cachedDebtStore = null
        }

        val remoteHistoryEpoch = ping.optString("historyEpoch").takeIf { it.isNotBlank() }
        val localHistoryEpoch = metadata("debt_history_epoch")
        if (remoteHistoryEpoch != null && localHistoryEpoch != null && remoteHistoryEpoch != localHistoryEpoch) {
            database.withTransaction { resetDebtHistory(remoteHistoryEpoch) }
        } else if (remoteHistoryEpoch != null && localHistoryEpoch == null) {
            putMetadata("debt_history_epoch", remoteHistoryEpoch)
        }

        debtStockWatermarkSupported=capabilities!=null && (0 until capabilities.length()).any { capabilities.getString(it)=="debtStockWatermarkV1" }
        val fullLedger = capabilities != null && (0 until capabilities.length()).any { capabilities.getString(it)==DebtExtendedWire.CAPABILITY }
        if(fullLedger && metadata("debt_extended_enabled")!="1") {
            database.withTransaction { putMetadata("debt_cursor","0");putMetadata("debt_extended_enabled","1") }
        }
        if(!fullLedger && metadata("debt_extended_enabled")=="1") putMetadata("debt_extended_enabled","0")
        val inbox = getDebtInbox(storeGuid)

        // 1. Debt Push
        val pendingGroups = db().query(
            "SELECT group_id FROM sync_journal WHERE kind LIKE 'debt_%' AND acked = -1 AND group_id <> '' GROUP BY group_id ORDER BY MIN(seq)"
        ).use { c ->
            buildList { while (c.moveToNext()) add(c.getString(0)) }
        }

        if (pendingGroups.isNotEmpty()) {
            val envelopes = mutableListOf<String>()
            var totalChars = 0L

            for (groupId in pendingGroups) {
                val kind=scalar(db(),"SELECT kind FROM debt_events WHERE guid=?",groupId) as? String
                if(!fullLedger && kind!=null && DebtExtendedWire.supports(kind)) continue
                val wire = database.withTransaction { inbox.exportApplied(db(), groupId) }
                    ?: error("Qarz paketi topilmadi: $groupId")
                if (envelopes.size >= 250 || totalChars + wire.length > 6 * 1024 * 1024) break
                envelopes.add(wire)
                totalChars += wire.length
            }

            if (envelopes.isNotEmpty()) {
                val pushBody = JSONObject().put("envelopes", JSONArray(envelopes))
                val pushReply = request(base, "/api/v2/debt/push", pushBody)
                val results = pushReply.getJSONArray("results")
                val ackedGroups = mutableListOf<String>()
                val groupSequences=mutableMapOf<String,Long>()
                val sentGroups=envelopes.map { DebtEnvelope.decode(it,storeGuid).guid }.toSet()

                for (i in 0 until results.length()) {
                    val res = results.getJSONObject(i)
                    val status = res.getString("status")
                    val guid = res.getString("guid")
                    require(guid in sentGroups) { "Nomaʼlum qarz tasdigʻi" }
                    if (status == "Applied" || status == "AlreadyApplied") {
                        ackedGroups.add(guid)
                        if(debtStockWatermarkSupported) { val seq=res.getLong("sequence");require(seq>0);groupSequences[guid]=seq }
                    }
                }

                if (ackedGroups.isNotEmpty()) {
                    database.withTransaction {
                        for (group in ackedGroups) {
                            groupSequences[group]?.let { putMetadata("debt_remote_seq:$group",it.toString()) }
                            db().execSQL("UPDATE sync_journal SET acked = 1 WHERE group_id = ?", arrayOf(group))
                            db().query("SELECT entity_guid FROM sync_journal WHERE group_id = ? AND kind = 'debt_sale'", arrayOf(group)).use { sc ->
                                val saleGuids = buildList { while (sc.moveToNext()) add(sc.getString(0)) }
                                if (saleGuids.isNotEmpty()) {
                                    saleDao.markSalesSyncedByGuids(saleGuids)
                                }
                            }
                        }
                    }
                }
            }
        }

        if(fullLedger && scalar(db(),"SELECT 1 FROM sync_journal WHERE kind LIKE 'debt_%' AND acked=-1 LIMIT 1")==null) drainDebtCommands(base)

        if(!fullLedger && scalar(db(),"SELECT 1 FROM sync_journal j JOIN debt_events e ON e.guid=j.entity_guid WHERE j.acked=-1 AND e.kind NOT IN ('sale_open','payment') LIMIT 1")!=null)
            debtCommandMessage="Qarz amallari navbatda. Ikkala dasturni ham yangilang."

        // New customer envelopes go first; edits never bypass customer creation.
        pushCustomerEdits(base, capabilities != null && (0 until capabilities.length()).any {
            capabilities.getString(it) == DebtCustomerEdits.CAPABILITY
        })

        // 2. Debt Pull
        val debtCursor = metadata("debt_cursor")?.toLongOrNull() ?: 0L
        val pullReply = request(base, "/api/v2/debt/pull?cursor=$debtCursor&full=${if(fullLedger)1 else 0}")
        require(pullReply.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan; qarz sinxron to'xtatildi." }
        require(pullReply.getString("storeGuid") == storeGuid) { "Do'kon qarz doirasi mos emas." }

        if (pullReply.optBoolean("cursorRollback", false)) {
            val serverEpoch = pullReply.optString("historyEpoch").takeIf { it.isNotBlank() } ?: remoteHistoryEpoch ?: ""
            database.withTransaction {
                resetDebtHistory(serverEpoch)
                pullReply.optJSONArray("customers")?.let { applyCustomerUpdates(db(), it) }
            }
            return
        }

        val pulledEpoch = pullReply.optString("historyEpoch").takeIf { it.isNotBlank() }
        if (pulledEpoch != null && pulledEpoch != metadata("debt_history_epoch")) {
            database.withTransaction { resetDebtHistory(pulledEpoch) }
            return
        }

        val pulledEnvelopes = pullReply.getJSONArray("envelopes")
        val newCursor = pullReply.getLong("cursor")
        val now = System.currentTimeMillis()

        database.withTransaction {
            val sql = db()
            for (i in 0 until pulledEnvelopes.length()) {
                val wire = pulledEnvelopes.getString(i)
                val p = DebtEnvelope.decode(wire, storeGuid)
                if(debtStockWatermarkSupported) {
                    val seq=pullReply.getJSONObject("sequences").getLong(p.guid);require(seq>0 && seq<=newCursor)
                    putMetadata("debt_remote_seq:${p.guid}",seq.toString())
                }
                inbox.receive(sql, p, wire, now)
            }
            pullReply.optJSONArray("customers")?.let { applyCustomerUpdates(sql, it) }
            putMetadata("debt_cursor", newCursor.toString())
        }

        // 3. Drain pending envelopes that were waiting for dependencies
        inbox.drainPending(now)
    }

    private suspend fun syncOnce() = withContext(Dispatchers.IO) { syncMutex.withLock {
        val base = getServerUrl() ?: return@withLock
        if(token().isNullOrBlank()) return@withLock

        // 1. Debt sync runs independently and is not blocked by legacy product conflicts.
        syncDebtIfSupported(base)

        // A known conflict needs a user choice, not another failed push every two seconds.
        // Probe transport while paused so loss/recovery of Wi-Fi remains visible.
        if (db().query("SELECT 1 FROM sync_conflicts LIMIT 1").use { it.moveToFirst() }) {
            _hasConflict.value = true
            _pendingCount.value = totalPendingCount()
            val ping = request(base, "/api/ping")
            require(ping.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan." }
            throw PendingSyncConflict("Narx yoki tovar tahriri farq qiladi. Telefon yoki kompyuter tahririni tanlang; navbat saqlanadi.")
        }
        // Bounded batches, but drain all prior operations before pulling authoritative stock.
        while(true) {
            currentCoroutineContext().ensureActive()
            val ops = freeze()
            _pendingCount.value = totalPendingCount()
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
        val reply = try {
            request(base,"/api/v2/pull?cursor=$cursor")
        } catch (e: Exception) {
            if (cursor > 0 && (e.message?.contains("kursor", ignoreCase = true) == true || e.message?.contains("400") == true)) {
                putMetadata("cursor", "0")
                request(base, "/api/v2/pull?cursor=0")
            } else {
                throw e
            }
        }
        if (reply.optBoolean("cursorRollback", false)) {
            putMetadata("cursor", "0")
        }
        require(reply.getString("serverId") == metadata("server_id")) { "Kompyuter bazasi almashgan; sinxron to'xtatildi." }
        applySnapshot(reply)
        _pendingCount.value = totalPendingCount()
        _hasConflict.value = db().query("SELECT 1 FROM sync_conflicts LIMIT 1").use { it.moveToFirst() }
        _syncMessage.value = debtCommandMessage ?: customerEditMessage ?: if(_pendingCount.value == 0) "Barcha amallar sinxronlandi." else "${_pendingCount.value} ta amal navbatda."
    } }
    private suspend fun applySnapshot(root: JSONObject) = database.withTransaction {
        val sql = db()
        sql.execSQL("UPDATE sync_control SET applying=1 WHERE id=1")
        try {
            val localPending = pending()
            for(i in 0 until root.getJSONArray("warehouses").length()) {
                val w = root.getJSONArray("warehouses").getJSONObject(i)
                val wGuid = w.getString("Guid")
                if (wGuid == "00000000-0000-0000-0000-000000000001") continue
                if(localPending.none { it.kind == "warehouse" && it.guid == wGuid }) {
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
                // Validate even an existing GUID: never ACK a debt header without its envelope.
                LegacySalePaymentType.decode(s.opt("PaymentType"))
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
                if (wh == "00000000-0000-0000-0000-000000000001") continue
                val unpushed = localPending.filter { it.kind == "stock" && it.guid == guid && it.warehouse == wh }.sumOf { it.delta }
                val watermark=if(debtStockWatermarkSupported)root.getLong("debtStockWatermark") else 0L
                // Keep phone-side debt movements not yet included in this server snapshot,
                // even when created concurrently while the HTTP pull was in flight.
                val localDebt=if(debtStockWatermarkSupported)sql.query("SELECT COALESCE(SUM(j.delta),0) FROM sync_journal j LEFT JOIN sync_meta m ON m.key='debt_remote_seq:'||j.group_id WHERE j.kind='debt_stock' AND j.entity_guid=? AND j.warehouse_guid=? AND (m.value IS NULL OR CAST(m.value AS INTEGER)>?)",arrayOf(guid,wh,watermark)).use { c -> c.moveToFirst();c.getDouble(0) } else 0.0
                productStockDao.upsertStock(guid,wh,stock.getDouble("Quantity") + unpushed + localDebt,stock.getLong("UpdatedAt"))
            }
            sql.execSQL("DELETE FROM warehouses WHERE guid = '00000000-0000-0000-0000-000000000001'")
            sql.execSQL("DELETE FROM product_stocks WHERE warehouse_guid = '00000000-0000-0000-0000-000000000001'")
            sql.execSQL("""
                UPDATE products 
                SET stock_quantity = COALESCE((
                    SELECT SUM(ps.quantity) 
                    FROM product_stocks ps 
                    JOIN warehouses w ON w.guid = ps.warehouse_guid AND w.is_deleted = 0 
                    WHERE ps.product_guid = products.guid
                ), 0.0)
            """)
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
            root.optJSONArray("customers")?.let { applyCustomerUpdates(sql, it) }
            cachedDebtInbox?.drainPending(sql,System.currentTimeMillis(),extendedOnly=true)
            if(debtStockWatermarkSupported) {
                require(root.optString("historyEpoch")==metadata("debt_history_epoch")) { "Tiklangan baza snapshoti yangilanishi kerak" }
                putMetadata("debt_stock_watermark",root.getLong("debtStockWatermark").toString())
            }
            putMetadata("cursor",root.getLong("cursor").toString())
            _dataUpdateEvents.tryEmit(Unit)
        } finally { sql.execSQL("UPDATE sync_control SET applying=0 WHERE id=1") }
        // Settings are unrelated to inventory capture. Existing CBU/Firebase behavior is preserved.
        if(root.has("cardTaxRate")) taxSettingsRepository.setCardTaxRate(root.getDouble("cardTaxRate"))
        if(root.has("usdRate")) currencyRepository.updateCachedRate(root.getDouble("usdRate"))
    }
    private fun applyCustomerUpdates(sql: SupportSQLiteDatabase, customers: JSONArray) {
        var anyChanged = false
        for (i in 0 until customers.length()) {
            val c = customers.getJSONObject(i)
            val guid = c.optString("guid")
            if (guid.isNullOrBlank()) continue
            val revision = c.optLong("revision", 0L)
            val name = c.optString("name", "")
            val phone = c.optString("phone", "")
            val note = c.optString("note", "")
            val archived = if (DebtCustomerEdits.archived(c)) 1L else 0L
            if (DebtCustomerEdits.pending(sql,guid).length() != 0) continue

            sql.query("SELECT revision,name,phone,note,archived FROM debt_customers WHERE guid=?", arrayOf(guid)).use { cursor ->
                if (cursor.moveToFirst()) {
                    val localRev = cursor.getLong(0)
                    // With no pending edits the desktop snapshot is authoritative, even
                    // if local optimistic revisions were higher than the server's counter.
                    if (revision != localRev || name != cursor.getString(1) || phone != cursor.getString(2) ||
                        note != cursor.getString(3) || archived != cursor.getLong(4)) {
                        sql.execSQL(
                            "UPDATE debt_customers SET name=?, phone=?, note=?, archived=?, revision=? WHERE guid=?",
                            arrayOf(name, phone, note, archived, revision, guid)
                        )
                        anyChanged = true
                    }
                }
            }
        }
        if (anyChanged) {
            _dataUpdateEvents.tryEmit(Unit)
        }
    }
    fun sendLiveCustomerChange() = wake()

    private suspend fun pushCustomerEdits(base: String, supported: Boolean) {
        customerEditMessage = null
        val keys = db().query("SELECT key FROM sync_meta WHERE key LIKE 'debt_customer_edits:%' ORDER BY key").use { c ->
            buildList { while(c.moveToNext()) add(c.getString(0)) }
        }
        if (keys.isEmpty()) return
        if (!supported) {
            customerEditMessage = "Mijoz tahrirlari saqlandi. Sinxronlash uchun kompyuter dasturini yangilang."
            return
        }
        for (key in keys) {
            val guid = key.removePrefix(DebtCustomerEdits.PREFIX)
            // Bound each customer's work so a long offline queue cannot starve others.
            for (batch in 0 until 50) {
                val queued = DebtCustomerEdits.pending(db(),guid)
                if (queued.length() == 0) break
                val command = queued.getJSONObject(0)
                try {
                    val response = request(base,"/api/v2/debt/customer_edit",command)
                    val status = response.getString("status")
                    if (status == "MissingCustomer") break // Creation envelope may still be waiting.
                    require(status in listOf("Applied","AlreadyApplied","Conflict")) { "Noma'lum mijoz tahriri javobi" }
                    val customer = response.getJSONObject("customer")
                    require(customer.getString("guid") == guid)
                    database.withTransaction {
                        val sql = db()
                        val current = DebtCustomerEdits.pending(sql,guid)
                        // A newer local save must never be removed by an older HTTP response.
                        if (current.length() > 0 && current.getJSONObject(0).getString("RequestGuid") == command.getString("RequestGuid")) {
                            if (status == "Conflict") {
                                sql.execSQL("INSERT OR REPLACE INTO sync_meta(key,value) VALUES(?,?)",
                                    arrayOf(DebtCustomerEdits.CONFLICT_PREFIX+guid,customer.toString()))
                            } else {
                                val rest = JSONArray()
                                for(i in 1 until current.length()) rest.put(current.getJSONObject(i))
                                if(rest.length() == 0) sql.execSQL("DELETE FROM sync_meta WHERE key=?",arrayOf(key))
                                else sql.execSQL("UPDATE sync_meta SET value=? WHERE key=?",arrayOf(rest.toString(),key))
                                sql.execSQL("DELETE FROM sync_meta WHERE key=?",arrayOf(DebtCustomerEdits.CONFLICT_PREFIX+guid))
                                applyCustomerUpdates(sql,JSONArray().put(customer))
                            }
                        }
                    }
                    if(status == "Conflict") {
                        customerEditMessage = "Mijoz tahriri farq qiladi. Telefon tahririni tanlash uchun mijozni qayta saqlang; kompyuterda ham shu qiymatlarni tanlashingiz mumkin."
                        break
                    }
                } catch (e: CancellationException) { throw e }
                catch (e: Exception) {
                    customerEditMessage = "Mijoz tahriri navbatda: ${e.message}"
                    break // Keep the durable body; do not block financial sync.
                }
            }
        }
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
        // Wi-Fi without Internet must still work while cellular data is enabled.
        val url = URL(base + path)
        val conn = (discovery.networkFor(address)?.openConnection(url) ?: url.openConnection()) as HttpURLConnection
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
            if (status == 401) throw SyncAuthenticationException("Qurilma ruxsati tasdiqlanmadi. Kompyuter qidirilmoqda; ruxsat bekor qilingan bo‘lsa QR orqali qayta ulang.")
            val reply = try { JSONObject(text) }
            catch (e: org.json.JSONException) { throw java.io.IOException("Manzilda LinePOS javobi olinmadi.", e) }
            if(status == 409) {
                val kind=reply.getString("kind");val guid=reply.getString("guid")
                db().execSQL("INSERT OR REPLACE INTO sync_conflicts(kind,entity_guid,revision,message) VALUES(?,?,?,?)",arrayOf(kind,guid,reply.getLong("revision"),reply.getString("error")))
                _hasConflict.value=true
                throw PendingSyncConflict(reply.getString("error"))
            }
            if (status == 400 && path in listOf("/api/v2/debt/reverse_payment","/api/v2/debt/refund_credit","/api/v2/debt/transfer_credit")) throw DebtCommandRejected(reply.optString("error","Qarz so‘rovi rad etildi"))
            if (status == 400 && path.startsWith("/api/v2/returns")) throw ReturnRejected(reply.optString("error", "Qaytarish rad etildi"))
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
            put("PaymentType", LegacySalePaymentType.encode(sale.paymentType))
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
        val paymentType = LegacySalePaymentType.decode(sJson.opt("PaymentType"))
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
