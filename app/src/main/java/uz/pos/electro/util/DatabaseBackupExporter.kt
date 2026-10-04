package uz.pos.electro.util

import android.content.Context
import android.content.Intent
import android.database.sqlite.SQLiteDatabase
import android.net.Uri
import androidx.core.content.FileProvider
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.local.entity.ProductStockEntity
import uz.pos.electro.data.model.UnitType
import java.io.BufferedReader
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URL
import java.nio.charset.StandardCharsets
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID

object DatabaseBackupExporter {

    /**
     * SQLite Database (.db) faylini to'liq va atomik tarzda (WAL Checkpoint bilan) nusxalab tayyorlash
     */
    fun backupDatabaseFile(context: Context, database: AppDatabase? = null): Result<File> = runCatching {
        val live = requireNotNull(database) { "Zaxira uchun ochiq baza talab qilinadi" }.openHelper.writableDatabase
        val backupDir = File(context.cacheDir, "backups").apply { mkdirs() }
        val backupFile = File(backupDir, "LinePOS_${System.currentTimeMillis()}_${UUID.randomUUID()}.db")
        try {
            copySnapshot(live, backupFile)
            backupFile
        } catch (error: Throwable) {
            SQLiteDatabase.deleteDatabase(backupFile)
            throw error
        }
    }

    internal fun copySnapshot(source: androidx.sqlite.db.SupportSQLiteDatabase, destination: File) {
        fun quote(name: String) = "\"" + name.replace("\"", "\"\"") + "\""
        source.beginTransaction()
        try {
            val schema = mutableListOf<Triple<String, String, String>>()
            source.query("SELECT type,name,sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' AND name != 'android_metadata' ORDER BY CASE type WHEN 'table' THEN 0 ELSE 1 END").use { c ->
                while (c.moveToNext()) schema.add(Triple(c.getString(0), c.getString(1), c.getString(2)))
            }
            SQLiteDatabase.openOrCreateDatabase(destination, null).use { target ->
                target.execSQL("PRAGMA journal_mode=DELETE")
                target.beginTransaction()
                try {
                    schema.filter { it.first == "table" }.forEach { (_, name, sql) ->
                        target.execSQL(sql)
                        source.query("SELECT * FROM " + quote(name)).use { rows ->
                            val cols = rows.columnNames.joinToString(",") { quote(it) }
                            val marks = rows.columnNames.joinToString(",") { "?" }
                            target.compileStatement("INSERT INTO " + quote(name) + " (" + cols + ") VALUES (" + marks + ")").use { stmt ->
                                while (rows.moveToNext()) {
                                    stmt.clearBindings()
                                    for (i in 0 until rows.columnCount) when (rows.getType(i)) {
                                        android.database.Cursor.FIELD_TYPE_NULL -> stmt.bindNull(i + 1)
                                        android.database.Cursor.FIELD_TYPE_INTEGER -> stmt.bindLong(i + 1, rows.getLong(i))
                                        android.database.Cursor.FIELD_TYPE_FLOAT -> stmt.bindDouble(i + 1, rows.getDouble(i))
                                        android.database.Cursor.FIELD_TYPE_BLOB -> stmt.bindBlob(i + 1, rows.getBlob(i))
                                        else -> stmt.bindString(i + 1, rows.getString(i))
                                    }
                                    stmt.executeInsert()
                                }
                            }
                        }
                    }
                    // Preserve deleted high-water IDs, not just MAX(id) in existing rows.
                    val hasSequence = source.query("SELECT 1 FROM sqlite_master WHERE name='sqlite_sequence'").use { it.moveToFirst() }
                    if (hasSequence) {
                        target.execSQL("DELETE FROM sqlite_sequence")
                        source.query("SELECT name,seq FROM sqlite_sequence").use { c ->
                            while (c.moveToNext()) target.execSQL("INSERT INTO sqlite_sequence(name,seq) VALUES(?,?)", arrayOf(c.getString(0), c.getLong(1)))
                        }
                    }
                    schema.filter { it.first != "table" }.forEach { target.execSQL(it.third) }
                    target.version = source.version
                    target.setTransactionSuccessful()
                } finally { target.endTransaction() }
                target.rawQuery("PRAGMA integrity_check", null).use { check ->
                    require(check.moveToFirst() && check.getString(0) == "ok") { "Zaxira yaxlitligi tekshiruvdan o'tmadi" }
                }
            }
            java.io.RandomAccessFile(destination, "rw").use { it.fd.sync() }
            source.setTransactionSuccessful()
        } finally { source.endTransaction() }
    }

    /**
     * Zaxira faylini (URI orqali) tekshirish va bazani xavfsiz qayta tiklash (Restore)
     */
    fun restoreDatabaseFromUri(context: Context, uri: Uri, database: AppDatabase? = null): Result<String> = runCatching {
        val live = requireNotNull(database) { "Tiklash uchun ochiq baza talab qilinadi" }
        val target = context.getDatabasePath(AppDatabase.databaseName(context))
        val stagingName = "restore_${UUID.randomUUID()}.db"
        val staging = context.getDatabasePath(stagingName)
        val scope = kotlinx.coroutines.CoroutineScope(kotlinx.coroutines.Dispatchers.IO + kotlinx.coroutines.SupervisorJob())
        var validator: AppDatabase? = null
        try {
            context.contentResolver.openInputStream(uri)?.use { input ->
                FileOutputStream(staging).use { output -> input.copyTo(output); output.fd.sync() }
            } ?: error("Tanlangan faylni ochib bo'lmadi")
            SQLiteDatabase.openDatabase(staging.path, null, SQLiteDatabase.OPEN_READONLY).use { checkDb ->
                checkDb.rawQuery("PRAGMA integrity_check", null).use { c ->
                    require(c.moveToFirst() && c.getString(0) == "ok") { "Baza shikastlangan" }
                }
                checkDb.rawQuery("PRAGMA foreign_key_check", null).use { c ->
                    require(!c.moveToFirst()) { "Baza bog'lanishlari shikastlangan" }
                }
            }
            validator = AppDatabase.buildDatabase(context, scope, stagingName)
            val checked = validator.openHelper.writableDatabase // validates Room identity and migrates supported versions
            checked.query("PRAGMA wal_checkpoint(TRUNCATE)").use { c ->
                check(c.moveToFirst() && c.getInt(0) == 0) { "Tiklanadigan bazani tayyorlab bo'lmadi" }
            }
            validator.close(); validator = null
            val recoveryDir = File(context.filesDir, "restore-recovery").apply { mkdirs() }
            val recovery = File(recoveryDir, "before_restore_${System.currentTimeMillis()}.db")
            copySnapshot(live.openHelper.writableDatabase, recovery)
            live.close()
            // All source connections are closed. Rename on the same filesystem is atomic.
            File(target.path + "-wal").delete()
            File(target.path + "-shm").delete()
            java.nio.file.Files.move(staging.toPath(), target.toPath(),
                java.nio.file.StandardCopyOption.ATOMIC_MOVE, java.nio.file.StandardCopyOption.REPLACE_EXISTING)
            "Baza tekshirilib tiklandi. Oldingi baza nusxasi saqlandi. Ilova qayta ochiladi."
        } finally {
            validator?.close()
            scope.coroutineContext[kotlinx.coroutines.Job]?.cancel()
            SQLiteDatabase.deleteDatabase(staging)
        }
    }

    /**
     * Desktop kompyuterdan Wi-Fi orqali to'liq SQLite bazasini tortib olib tiklash
     */
    fun restoreDatabaseFromDesktop(context: Context, serverUrl: String, database: AppDatabase? = null): Result<String> = runCatching {
        val entry = dagger.hilt.android.EntryPointAccessors.fromApplication(context.applicationContext, WifiRestoreEntryPoint::class.java)
        val manager = entry.wifiSyncManager()
        require(uz.pos.electro.data.sync.LocalSyncManager.normalizeUrl(serverUrl) == manager.getServerUrl()) { "Avval Wi-Fi sinxron oynasida shu kompyuter QR kodini skanerlang." }
        kotlinx.coroutines.runBlocking { manager.syncWithDesktop().getOrThrow() }
        "Kompyuterdagi tovarlar, omborlar va cheklar xavfsiz sinxronlandi."
    }

    /**
     * Ilovani toza va yangilangan baza bilan qayta ishga tushirish (Hot Restart)
     */
    fun restartApp(context: Context) {
        try {
            val packageManager = context.packageManager
            val intent = packageManager.getLaunchIntentForPackage(context.packageName)
            val componentName = intent?.component
            val restartIntent = Intent.makeRestartActivityTask(componentName)
            context.startActivity(restartIntent)
            Runtime.getRuntime().exit(0)
        } catch (_: Throwable) {
            // Agar avtomatik restart bo'lmasa, dasturni yopish
            Runtime.getRuntime().exit(0)
        }
    }

    /**
     * JSON fayldan tovarlar va ombor qoldiqlarini import qilish
     */
    suspend fun importInventoryJson(
        context: Context,
        jsonUri: Uri,
        productDao: ProductDao,
        productStockDao: ProductStockDao,
        defaultWarehouseGuid: String = "main-default-warehouse"
    ): Result<Int> = runCatching {
        val jsonText = StringBuilder()
        context.contentResolver.openInputStream(jsonUri)?.use { stream ->
            BufferedReader(InputStreamReader(stream, StandardCharsets.UTF_8)).use { reader ->
                var line: String?
                while (reader.readLine().also { line = it } != null) {
                    jsonText.append(line)
                }
            }
        } ?: throw Exception("JSON faylni o'qib bo'lmadi!")

        val jsonArray = org.json.JSONArray(jsonText.toString())
        var importedCount = 0
        val now = System.currentTimeMillis()

        for (i in 0 until jsonArray.length()) {
            val obj = jsonArray.getJSONObject(i)
            val name = obj.optString("name", "").trim()
            if (name.isEmpty()) continue

            val barcode = obj.optString("barcode", "").trim().ifEmpty { null }
            val category = obj.optString("category", "Barchasi").trim().ifEmpty { "Barchasi" }
            val costPrice = obj.optDouble("costPrice", 0.0)
            val costCurrency = obj.optString("costCurrency", "UZS")
            val sellingPrice = obj.optDouble("sellingPrice", 0.0)
            val stockQuantity = obj.optDouble("stockQuantity", 0.0)
            val unitStr = obj.optString("unitType", "DONA")
            val unitType = runCatching { UnitType.valueOf(unitStr) }.getOrDefault(UnitType.DONA)
            val minAlert = obj.optDouble("minStockAlert", 3.0)

            val existing = if (!barcode.isNullOrBlank()) {
                productDao.getProductByBarcode(barcode)
            } else {
                null
            }

            val productGuid = existing?.guid ?: UUID.randomUUID().toString()

            val entity = ProductEntity(
                id = existing?.id ?: 0L,
                guid = productGuid,
                barcode = barcode,
                name = name,
                category = category,
                costPrice = costPrice,
                costCurrency = costCurrency,
                sellingPrice = sellingPrice,
                stockQuantity = stockQuantity,
                unitType = unitType,
                minStockAlert = minAlert,
                isDeleted = false,
                updatedAt = now
            )

            productDao.insertProduct(entity)

            // Ombor qoldig'ini ham bog'lash
            try {
                productStockDao.insertOrUpdateStock(
                    ProductStockEntity(
                        productGuid = productGuid,
                        warehouseGuid = defaultWarehouseGuid,
                        quantity = stockQuantity,
                        updatedAt = now
                    )
                )
            } catch (_: Throwable) {}

            importedCount++
        }

        importedCount
    }

    /**
     * Ombordagi barcha tovarlar jadvalini chiroyli va formatlangan Excel (.xls) qilib tayyorlash
     */
    fun exportInventoryExcel(
        context: Context,
        products: List<ProductEntity>,
        usdRate: Double = 12850.0
    ): Result<File> = runCatching {
        val numberFormat = NumberFormat.getNumberInstance(Locale.US)
        val dateFormat = SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault())
        val timeStamp = SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date())

        val totalCostUzs = products.sumOf { p ->
            val costInUzs = if (p.costCurrency == "USD") p.costPrice * usdRate else p.costPrice
            costInUzs * p.stockQuantity
        }
        val totalSellingUzs = products.sumOf { p -> p.sellingPrice * p.stockQuantity }
        val expectedProfitUzs = totalSellingUzs - totalCostUzs

        val html = StringBuilder().apply {
            append("""
                <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
                <head>
                    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
                    <!--[if gte mso 9]>
                    <xml>
                    <x:ExcelWorkbook>
                    <x:ExcelWorksheets>
                    <x:ExcelWorksheet>
                    <x:Name>Ombor Jadvali</x:Name>
                    <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
                    </x:ExcelWorksheet>
                    </x:ExcelWorksheets>
                    </x:ExcelWorkbook>
                    </xml>
                    <![endif]-->
                    <style>
                        body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; }
                        .title { font-size: 16pt; font-weight: bold; color: #0B6477; }
                        .header { background-color: #0B6477; color: #ffffff; font-weight: bold; text-align: center; border: 1px solid #000000; padding: 8px; }
                        .kpi-title { font-weight: bold; background-color: #F1F5F9; border: 1px solid #CBD5E1; padding: 6px; }
                        .kpi-val { font-weight: bold; border: 1px solid #CBD5E1; padding: 6px; }
                        .profit-val { font-weight: bold; color: #16A34A; border: 1px solid #CBD5E1; padding: 6px; }
                        td { padding: 6px 10px; border: 1px solid #E2E8F0; }
                        .num { text-align: right; }
                        .center { text-align: center; }
                    </style>
                </head>
                <body>
                    <table>
                        <tr>
                            <td colspan="9" class="title" style="border:none;">LINE POS — OMBOR MAHSULOTLARI JADVALI</td>
                        </tr>
                        <tr>
                            <td colspan="9" style="border:none;"><b>Eksport vaqti:</b> ${dateFormat.format(Date())}</td>
                        </tr>
                        <tr>
                            <td colspan="9" style="border:none;"><b>Dollar kursi (CBU):</b> 1 USD = ${numberFormat.format(usdRate)} so'm</td>
                        </tr>
                        <tr><td colspan="9" style="border:none;"></td></tr>

                        <!-- KPI Xulosa -->
                        <tr>
                            <td colspan="2" class="kpi-title">Jami Mahsulotlar Turi:</td>
                            <td colspan="2" class="kpi-val center">${products.size} xil</td>
                            <td colspan="2" class="kpi-title">Jami Qoldiq Qiymati (Tan narxda):</td>
                            <td colspan="3" class="kpi-val num">${numberFormat.format(totalCostUzs)} so'm</td>
                        </tr>
                        <tr>
                            <td colspan="2" class="kpi-title">Kutilayotgan Jami Savdo:</td>
                            <td colspan="2" class="kpi-val num">${numberFormat.format(totalSellingUzs)} so'm</td>
                            <td colspan="2" class="kpi-title">Kutilayotgan Sof Foyda:</td>
                            <td colspan="3" class="profit-val num">${numberFormat.format(expectedProfitUzs)} so'm</td>
                        </tr>
                        <tr><td colspan="9" style="border:none;"></td></tr>

                        <!-- Jadval Sarlavhalari -->
                        <tr>
                            <th class="header">№</th>
                            <th class="header">Mahsulot Nomi</th>
                            <th class="header">Kategoriya</th>
                            <th class="header">Shtrix-kod</th>
                            <th class="header">O'lchov Birligi</th>
                            <th class="header">Tan Narxi</th>
                            <th class="header">Sotish Narxi (so'm)</th>
                            <th class="header">Ombordagi Qoldiq</th>
                            <th class="header">Jami Qoldiq Qiymati</th>
                        </tr>
            """.trimIndent())

            products.forEachIndexed { index, product ->
                val unitLabel = when (product.unitType) {
                    UnitType.METR -> "Metr"
                    UnitType.KG -> "Kg"
                    UnitType.DONA -> "Dona"
                }
                val stockText = if (product.stockQuantity % 1.0 == 0.0) product.stockQuantity.toLong().toString() else product.stockQuantity.toString()
                val costPriceText = if (product.costCurrency == "USD") "$${product.costPrice}" else "${numberFormat.format(product.costPrice)} so'm"
                val itemCostInUzs = if (product.costCurrency == "USD") product.costPrice * usdRate else product.costPrice
                val totalValue = itemCostInUzs * product.stockQuantity

                append("""
                    <tr>
                        <td class="center">${index + 1}</td>
                        <td><b>${product.name}</b></td>
                        <td>${product.category.ifBlank { "Barchasi" }}</td>
                        <td class="center">${product.barcode ?: "-"}</td>
                        <td class="center">$unitLabel</td>
                        <td class="num">$costPriceText</td>
                        <td class="num"><b>${numberFormat.format(product.sellingPrice)}</b></td>
                        <td class="center"><b>$stockText</b></td>
                        <td class="num">${numberFormat.format(totalValue)} so'm</td>
                    </tr>
                """.trimIndent())
            }

            append("""
                    </table>
                </body>
                </html>
            """.trimIndent())
        }

        val backupDir = File(context.cacheDir, "backups").apply { if (!exists()) mkdirs() }
        val excelFile = File(backupDir, "Line_Ombor_Jadvali_$timeStamp.xls")

        OutputStreamWriter(FileOutputStream(excelFile), StandardCharsets.UTF_8).use { writer ->
            writer.write(html.toString())
        }

        excelFile
    }

    /**
     * Barcha tovarlar JSON nusxasini yaratish
     */
    fun exportInventoryJson(context: Context, products: List<ProductEntity>): Result<File> = runCatching {
        val timeStamp = SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date())
        val backupDir = File(context.cacheDir, "backups").apply { if (!exists()) mkdirs() }
        val jsonFile = File(backupDir, "Line_Ombor_JSON_$timeStamp.json")

        val jsonArray = org.json.JSONArray()
        products.forEach { p ->
            val obj = org.json.JSONObject().apply {
                put("id", p.id)
                put("barcode", p.barcode ?: "")
                put("name", p.name)
                put("category", p.category)
                put("costPrice", p.costPrice)
                put("costCurrency", p.costCurrency)
                put("sellingPrice", p.sellingPrice)
                put("stockQuantity", p.stockQuantity)
                put("unitType", p.unitType.name)
                put("minStockAlert", p.minStockAlert)
            }
            jsonArray.put(obj)
        }

        FileOutputStream(jsonFile).use { output ->
            output.write(jsonArray.toString(2).toByteArray(StandardCharsets.UTF_8))
        }

        jsonFile
    }

    /**
     * Faylni Telegram, Google Drive, Gmail yoki boshqa ilovalarga to'g'ridan-to'g'ri ulashish (Share Intent)
     */
    fun shareBackupFile(context: Context, file: File, mimeType: String, chooserTitle: String) {
        val fileUri: Uri = FileProvider.getUriForFile(
            context,
            "${context.packageName}.fileprovider",
            file
        )

        val shareIntent = Intent(Intent.ACTION_SEND).apply {
            type = mimeType
            putExtra(Intent.EXTRA_STREAM, fileUri)
            putExtra(Intent.EXTRA_SUBJECT, file.name)
            putExtra(Intent.EXTRA_TEXT, "Line POS ombor ma'lumotlari nusxasi: ${file.name}")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }

        val chooser = Intent.createChooser(shareIntent, chooserTitle).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(chooser)
    }
}

@dagger.hilt.EntryPoint
@dagger.hilt.InstallIn(dagger.hilt.components.SingletonComponent::class)
interface WifiRestoreEntryPoint {
    fun wifiSyncManager(): uz.pos.electro.data.sync.LocalSyncManager
}
