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
        val backupDir = File(context.cacheDir, "backups").apply { if (!exists()) mkdirs() }
        val timeStamp = SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date())
        val backupFile = File(backupDir, "SMART_POS_Baza_$timeStamp.db")
        if (backupFile.exists()) backupFile.delete()

        // 1. Agar AppDatabase berilgan bo'lsa, avval SQLite VACUUM INTO yoki WAL checkpoint qilish
        if (database != null) {
            try {
                // SQLite 3.27+ da atomik klon yaratish
                database.openHelper.writableDatabase.execSQL("VACUUM INTO '${backupFile.absolutePath}'")
                if (backupFile.exists() && backupFile.length() > 0) {
                    return@runCatching backupFile
                }
            } catch (_: Throwable) {
                // Agar VACUUM INTO qo'llab-quvvatlanmasa, WAL faylini asosiy bazaga majburiy birlashtiramiz (TRUNCATE)
                try {
                    database.openHelper.writableDatabase.query("PRAGMA wal_checkpoint(TRUNCATE)").use { cursor ->
                        cursor.moveToFirst()
                    }
                } catch (_: Throwable) {}
            }
        }

        // 2. Asosiy baza faylini qidirish va nusxalash
        var dbFile = context.getDatabasePath(AppDatabase.databaseName(context))
        if (!dbFile.exists()) {
            dbFile = context.getDatabasePath("pos_database.db")
        }
        if (!dbFile.exists()) {
            throw Exception("Baza fayli topilmadi!")
        }

        FileInputStream(dbFile).use { input ->
            FileOutputStream(backupFile).use { output ->
                input.copyTo(output)
            }
        }

        backupFile
    }

    /**
     * Zaxira faylini (URI orqali) tekshirish va bazani xavfsiz qayta tiklash (Restore)
     */
    fun restoreDatabaseFromUri(context: Context, uri: Uri, database: AppDatabase? = null): Result<String> = runCatching {
        val tempRestoreFile = File(context.cacheDir, "temp_restore_${System.currentTimeMillis()}.db")
        if (tempRestoreFile.exists()) tempRestoreFile.delete()

        // 1. URI dan vaqtincha faylga ko'chirib olish
        context.contentResolver.openInputStream(uri)?.use { input ->
            FileOutputStream(tempRestoreFile).use { output ->
                input.copyTo(output)
            }
        } ?: throw Exception("Tanlangan faylni ochib bo'lmadi!")

        if (!tempRestoreFile.exists() || tempRestoreFile.length() == 0L) {
            throw Exception("Fayl bo'sh yoki o'qib bo'lmadi!")
        }

        // 2. SQLite 3 fayli ekanligini va jadvallar butunligini tekshirish
        var productsCount = 0
        var salesCount = 0
        try {
            val checkDb = SQLiteDatabase.openDatabase(tempRestoreFile.path, null, SQLiteDatabase.OPEN_READONLY)
            checkDb.use { db ->
                // Jadvallar mavjudligini tekshirish
                val cursorTables = db.rawQuery("SELECT name FROM sqlite_master WHERE type='table' AND name IN ('products', 'sales')", null)
                val tableNames = mutableListOf<String>()
                while (cursorTables.moveToNext()) {
                    tableNames.add(cursorTables.getString(0))
                }
                cursorTables.close()

                if (!tableNames.contains("products")) {
                    throw Exception("Fayl ichida tovarlar jadvali (products) topilmadi! Bu Line POS bazasi emas.")
                }

                try {
                    val pCur = db.rawQuery("SELECT COUNT(*) FROM products", null)
                    if (pCur.moveToFirst()) productsCount = pCur.getInt(0)
                    pCur.close()
                } catch (_: Throwable) {}

                try {
                    val sCur = db.rawQuery("SELECT COUNT(*) FROM sales", null)
                    if (sCur.moveToFirst()) salesCount = sCur.getInt(0)
                    sCur.close()
                } catch (_: Throwable) {}
            }
        } catch (e: Exception) {
            tempRestoreFile.delete()
            throw Exception("Yaroqsiz baza fayli: ${e.message}")
        }

        // 3. Joriy bazani yopish va fayllarni almashtirish
        try {
            database?.close()
        } catch (_: Throwable) {}

        val targetDbFile = context.getDatabasePath(AppDatabase.databaseName(context))
        val targetWalFile = File(targetDbFile.path + "-wal")
        val targetShmFile = File(targetDbFile.path + "-shm")

        // Eski WAL va SHM keshlarini o'chirish (yangi baza bilan ziddiyat bo'lmasligi uchun)
        if (targetWalFile.exists()) targetWalFile.delete()
        if (targetShmFile.exists()) targetShmFile.delete()

        // Yangi bazani asosiy o'rniga ko'chirish
        tempRestoreFile.copyTo(targetDbFile, overwrite = true)
        tempRestoreFile.delete()

        "Baza muvaffaqiyatli tiklandi!\n• Tovarlar: $productsCount ta\n• Savdolar: $salesCount ta\nIlova yangi baza bilan qayta ochiladi."
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
     * Kam qolgan tovarlar ichidan buyurtma (zakaz) uchun tanlangan tovarlar Excel (.xls) jadvali
     */
    fun exportReorderExcel(
        context: Context,
        products: List<ProductEntity>,
        usdRate: Double = 12850.0
    ): Result<File> = runCatching {
        val numberFormat = NumberFormat.getNumberInstance(Locale.US)
        val dateFormat = SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault())
        val timeStamp = SimpleDateFormat("yyyy-MM-dd_HH-mm", Locale.getDefault()).format(Date())

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
                    <x:Name>Buyurtma Ro'yxati</x:Name>
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
                        .reorder-header { background-color: #D97706; color: #ffffff; font-weight: bold; text-align: center; border: 1px solid #000000; padding: 8px; }
                        td { border: 1px solid #CBD5E1; padding: 6px 10px; }
                        .num { text-align: right; }
                        .center { text-align: center; }
                        .low-stock { color: #DC2626; font-weight: bold; text-align: center; }
                        .reorder-box { background-color: #FEF3C7; text-align: center; }
                    </style>
                </head>
                <body>
                    <table>
                        <tr>
                            <td colspan="9" class="title" style="border:none;">LINE POS - TOVAR BUYURTMA RO'YXATI (ZAKAZ)</td>
                        </tr>
                        <tr>
                            <td colspan="9" style="border:none; color: #64748B;">Sana: ${dateFormat.format(Date())} | Jami tanlangan: ${products.size} ta tovar</td>
                        </tr>
                        <tr><td colspan="9" style="border:none;"></td></tr>
                        
                        <tr class="header">
                            <th style="width: 40px; background-color: #0B6477; color: #ffffff;">T/r</th>
                            <th style="width: 140px; background-color: #0B6477; color: #ffffff;">Shtrix-kod</th>
                            <th style="width: 320px; background-color: #0B6477; color: #ffffff;">Mahsulot nomi</th>
                            <th style="width: 140px; background-color: #0B6477; color: #ffffff;">Kategoriya</th>
                            <th style="width: 90px; background-color: #0B6477; color: #ffffff;">Qoldiq</th>
                            <th style="width: 70px; background-color: #0B6477; color: #ffffff;">Birlik</th>
                            <th style="width: 120px; background-color: #0B6477; color: #ffffff;">Tan narxi</th>
                            <th class="reorder-header" style="width: 140px;">Buyurtma Miqdori</th>
                            <th style="width: 200px; background-color: #0B6477; color: #ffffff;">Izoh / Yetkazib beruvchi</th>
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

                append("""
                    <tr>
                        <td class="center">${index + 1}</td>
                        <td class="center">${product.barcode ?: "-"}</td>
                        <td><b>${product.name}</b></td>
                        <td>${product.category.ifBlank { "Barchasi" }}</td>
                        <td class="low-stock">$stockText</td>
                        <td class="center">$unitLabel</td>
                        <td class="num">$costPriceText</td>
                        <td class="reorder-box"></td>
                        <td>${product.note}</td>
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
        val excelFile = File(backupDir, "Line_Buyurtma_Royxati_$timeStamp.xls")

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
