package uz.pos.electro.data.business

import android.content.Context
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.*
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.*
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.repository.CurrencyRepository
import uz.pos.electro.data.repository.TaxSettingsRepository
import uz.pos.electro.data.sync.LocalSyncManager
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.ServerSocket
import java.net.Socket
import java.util.UUID
import java.util.concurrent.atomic.AtomicBoolean

class DebtLanSyncTest {
    private fun g(n: Int) = "00000000-0000-0000-0000-" + n.toString().padStart(12, '0')
    private val storeGuid = g(1)
    private val actorGuid = g(2)
    private val serverId = g(99)

    private fun user(db: AppDatabase) =
        db.openHelper.writableDatabase.execSQL("INSERT OR IGNORE INTO users(id,name,pin_code,role) VALUES(1,'Admin','0000','ADMIN')")
    private fun product(db: AppDatabase, pGuid: String, initialStock: Double = 10.0) =
        db.openHelper.writableDatabase.execSQL("INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('$pGuid','Test Product',50,100,$initialStock,'dona',500)")
    private fun warehouse(db: AppDatabase, wGuid: String) =
        db.openHelper.writableDatabase.execSQL("INSERT INTO warehouses(guid,name,updated_at) VALUES('$wGuid','Main Warehouse',500)")
    private fun stocks(db: AppDatabase, pGuid: String, wGuid: String, initialQuantity: Double = 10.0) =
        db.openHelper.writableDatabase.execSQL("INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('$pGuid','$wGuid',$initialQuantity,500)")

    /**
     * Minimal in-memory HTTP server to simulate desktop sync endpoints.
     */
    private class MockDesktopServer(private val port: Int) {
        private val serverSocket = ServerSocket(port)
        private val running = AtomicBoolean(true)
        val requests = mutableListOf<MockRequest>()
        var pushHandler: ((JSONObject) -> JSONObject)? = null
        var pullHandler: ((Long) -> JSONObject)? = null
        var capabilities = listOf(DebtWire.CAPABILITY)
        var serverId = ""
        var storeGuid = ""

        data class MockRequest(val method: String, val path: String, val body: String)

        fun start(scope: CoroutineScope) = scope.launch(Dispatchers.IO) {
            while (running.get()) {
                val client: Socket = try {
                    serverSocket.accept()
                } catch (_: Exception) {
                    break
                }
                scope.launch(Dispatchers.IO) {
                    handleClient(client)
                }
            }
        }

        private fun handleClient(socket: Socket) {
            socket.use { s ->
                val reader = BufferedReader(InputStreamReader(s.getInputStream()))
                val line = reader.readLine() ?: return
                val parts = line.split(" ")
                val method = parts[0]
                val path = parts.getOrElse(1) { "/" }

                var contentLength = 0
                while (true) {
                    val header = reader.readLine() ?: break
                    if (header.isEmpty()) break
                    if (header.startsWith("Content-Length:", ignoreCase = true)) {
                        contentLength = header.substringAfter(":").trim().toIntOrNull() ?: 0
                    }
                }

                val body = if (contentLength > 0) {
                    val buf = CharArray(contentLength)
                    var read = 0
                    while (read < contentLength) {
                        val r = reader.read(buf, read, contentLength - read)
                        if (r <= 0) break
                        read += r
                    }
                    String(buf, 0, read)
                } else ""

                synchronized(requests) {
                    requests.add(MockRequest(method, path, body))
                }

                val out = s.getOutputStream()
                val responseJson = when {
                    path.startsWith("/api/ping") -> {
                        JSONObject().apply {
                            put("serverId", serverId)
                            put("capabilities", JSONArray(capabilities))
                            put("storeGuid", storeGuid)
                        }
                    }
                    path.startsWith("/api/v2/pair") -> {
                        JSONObject().apply {
                            put("serverId", serverId)
                            put("token", "test-token")
                            put("capabilities", JSONArray(capabilities))
                            put("storeGuid", storeGuid)
                        }
                    }
                    path.startsWith("/api/v2/debt/push") && method == "POST" -> {
                        val reqObj = JSONObject(body)
                        pushHandler?.invoke(reqObj) ?: JSONObject().apply {
                            val envs = reqObj.getJSONArray("envelopes")
                            val res = JSONArray()
                            for (i in 0 until envs.length()) {
                                val envWire = envs.getString(i)
                                val decoded = DebtEnvelope.decode(envWire, storeGuid)
                                res.put(JSONObject().put("guid", decoded.guid).put("status", "Applied"))
                            }
                            put("results", res)
                        }
                    }
                    path.startsWith("/api/v2/debt/pull") && method == "GET" -> {
                        val cursor = path.substringAfter("cursor=", "0").substringBefore("&").toLongOrNull() ?: 0L
                        pullHandler?.invoke(cursor) ?: JSONObject().apply {
                            put("serverId", serverId)
                            put("storeGuid", storeGuid)
                            put("cursor", cursor)
                            put("envelopes", JSONArray())
                        }
                    }
                    else -> JSONObject().put("status", "ok")
                }

                val bytes = responseJson.toString().toByteArray(Charsets.UTF_8)
                val response = "HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: ${bytes.size}\r\nConnection: close\r\n\r\n"
                out.write(response.toByteArray(Charsets.US_ASCII))
                out.write(bytes)
                out.flush()
            }
        }

        fun stop() {
            running.set(false)
            try { serverSocket.close() } catch (_: Exception) {}
        }
    }

    @Test
    fun debtLanSyncPushAndPullFullLifecycle() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val dbName = "debt-lan-test-${UUID.randomUUID()}.db"
        val db = AppDatabase.buildDatabase(context, scope, dbName)

        val serverPort = 9180 + (System.currentTimeMillis() % 800).toInt()
        val mockServer = MockDesktopServer(serverPort).apply {
            this.serverId = this@DebtLanSyncTest.serverId
            this.storeGuid = this@DebtLanSyncTest.storeGuid
        }
        mockServer.start(scope)

        try {
            // Configure local sync manager and preferences
            val prefs = context.getSharedPreferences("pos_local_sync_prefs", Context.MODE_PRIVATE)
            prefs.edit().clear()
                .putString("server_ip", "127.0.0.1")
                .putInt("server_port", serverPort)
                .putString("token", "test-token")
                .putString("server_id", serverId)
                .putString("debt_store_guid", storeGuid)
                .apply()

            val repo = DebtRepository(db, storeGuid, actorGuid) { true }
            repo.bindStore()
            val prodGuid = g(501)
            val whGuid = g(502)
            user(db); product(db, prodGuid, 10.0); warehouse(db, whGuid); stocks(db, prodGuid, whGuid, 10.0)

            val manager = LocalSyncManager(
                context = context,
                database = db,
                productDao = db.productDao(),
                saleDao = db.saleDao(),
                warehouseDao = db.warehouseDao(),
                productStockDao = db.productStockDao(),
                currencyRepository = CurrencyRepository(context),
                taxSettingsRepository = TaxSettingsRepository(context)
            )

            // 1. Create Customer + Debt Sale on mobile
            val custGuid = g(101)
            val custDraft = DebtCustomerDraft(custGuid, "Test Xaridor", "+998901112233", "izoh", 1000)
            assertEquals(custGuid, repo.createCustomer(custDraft))

            val saleGuid = g(201)
            val reqGuid = g(202)
            val itemGuid = g(203)
            val stockOpGuid = g(204)
            val saleItems = listOf(
                DebtSaleItem(itemGuid, prodGuid, "Test Product", "Barchasi", "dona", whGuid, "Main Warehouse", "2", "100", "50", "UZS", stockOpGuid, "-2")
            )
            val saleSnapshot = DebtSaleSnapshot(saleGuid, 2000, 20000, 10000, 5000, 5000, 0, "0", "12850", "DEBT", saleItems)
            val openCmd = DebtOpenSaleCommand(reqGuid, custGuid, saleSnapshot, "2030-01-01", null, 1)
            assertEquals(reqGuid, repo.openSale(openCmd))

            // Verify journal is held (acked = -1)
            val sql = db.openHelper.writableDatabase
            val heldCount = DebtRepository.scalar(sql, "SELECT COUNT(*) FROM sync_journal WHERE acked = -1") as Long
            assertTrue("Expected held journal entries", heldCount >= 2)

            val saleEntity = db.saleDao().getSaleByGuid(saleGuid)
            assertNotNull("Sale should exist locally", saleEntity)
            assertFalse("Sale should not be synced yet", saleEntity!!.isSynced)

            // Setup pull envelope from server: customer created on desktop
            val desktopCustGuid = g(301)
            val desktopCustPayload = DebtRepository.canonical("customer", storeGuid, actorGuid, desktopCustGuid, "Desktop Mijoz", "+998909998877", "", "3000")
            val desktopCustHash = DebtWire.fingerprint(desktopCustPayload)
            val desktopCustWire = DebtWire.encodeCustomer(DebtWireCustomer(desktopCustGuid, storeGuid, g(88), desktopCustPayload, desktopCustHash), storeGuid)
            val desktopCustEnv = DebtEnvelope.encode(DebtEnvelopePacket(desktopCustGuid, storeGuid, desktopCustWire, "", ""), storeGuid)

            mockServer.pullHandler = { cursor ->
                JSONObject().apply {
                    put("serverId", serverId)
                    put("storeGuid", storeGuid)
                    put("cursor", 15L)
                    put("envelopes", JSONArray().put(desktopCustEnv))
                }
            }

            // 2. Perform syncWithDesktop
            val syncResult = manager.syncWithDesktop()
            assertTrue("First sync should succeed: " + syncResult.exceptionOrNull()?.message, syncResult.isSuccess)

            // 3. Verify Debt Push completed and ACKed
            val ackedCount = DebtRepository.scalar(sql, "SELECT COUNT(*) FROM sync_journal WHERE group_id = '$reqGuid' AND acked = 1") as Long
            assertTrue("Sale journal entries should be acked=1", ackedCount > 0)

            val updatedSale = db.saleDao().getSaleByGuid(saleGuid)
            assertNotNull(updatedSale)
            assertTrue("Sale should now be marked as synced", updatedSale!!.isSynced)

            // 4. Verify Debt Pull applied desktop customer and advanced cursor
            val pulledCustCount = DebtRepository.scalar(sql, "SELECT COUNT(*) FROM debt_customers WHERE guid = '$desktopCustGuid'") as Long
            assertEquals("Pulled customer should exist in local database", 1L, pulledCustCount)

            val savedCursor = DebtRepository.scalar(sql, "SELECT value FROM sync_meta WHERE key = 'debt_cursor'") as String?
            assertEquals("Debt cursor should be advanced to 15", "15", savedCursor)

            // 5. Verify Conflict Isolation: metadata conflict does not block debt operations
            sql.execSQL("INSERT INTO sync_conflicts(kind, entity_guid, revision, message) VALUES('product', 'p1', 1, 'Conflict test')")

            // Add another local payment while conflict is active
            val payReqGuid = g(401)
            val payCmd = DebtPaymentCommand(payReqGuid, custGuid, 2000, 0, 0, 4000)
            assertEquals(payReqGuid, repo.takePayment(payCmd))

            mockServer.pullHandler = { cursor ->
                JSONObject().apply {
                    put("serverId", serverId)
                    put("storeGuid", storeGuid)
                    put("cursor", cursor)
                    put("envelopes", JSONArray())
                }
            }

            // Trigger sync again with active conflict (fails at legacy check after debt sync completes)
            val secondSync = manager.syncWithDesktop()
            assertFalse("Second sync reports conflict error on legacy phase", secondSync.isSuccess)
            assertTrue("Conflict should be flagged", manager.hasConflict.value)

            // Verify payment was pushed and acked despite product conflict
            val payAcked = DebtRepository.scalar(sql, "SELECT COUNT(*) FROM sync_journal WHERE group_id = '$payReqGuid' AND acked = 1") as Long
            assertEquals("Payment should be acked even with active legacy product conflict", 1L, payAcked)

        } finally {
            mockServer.stop()
            db.close()
            context.deleteDatabase(dbName)
        }
    }
}
