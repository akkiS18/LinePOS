package uz.pos.electro.util

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.core.content.FileProvider
import uz.pos.electro.data.model.ReportsSummary
import uz.pos.electro.data.model.SaleReportItem
import uz.pos.electro.data.model.UnitType
import java.io.File
import java.io.FileOutputStream
import java.io.OutputStreamWriter
import java.nio.charset.StandardCharsets
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

object ExcelExporter {

    /**
     * Hisobot ma'lumotlarini Android-da 100% kafolatlangan va formatlangan .xls faylga eksport qilish
     */
    fun exportAndShareReport(
        context: Context,
        periodTitle: String,
        summary: ReportsSummary,
        items: List<SaleReportItem>
    ): Result<File> {
        return runCatching {
            val numberFormat = NumberFormat.getNumberInstance(Locale.US)
            val dateFormat = SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault())

            val htmlContent = StringBuilder()
            htmlContent.append("""
                <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
                <head>
                    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
                    <!--[if gte mso 9]>
                    <xml>
                    <x:ExcelWorkbook>
                    <x:ExcelWorksheets>
                    <x:ExcelWorksheet>
                    <x:Name>Savdo Hisoboti</x:Name>
                    <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
                    </x:ExcelWorksheet>
                    </x:ExcelWorksheets>
                    </x:ExcelWorkbook>
                    </xml>
                    <![endif]-->
                    <style>
                        body { font-family: Calibri, Arial, sans-serif; }
                        .title { font-size: 16pt; font-weight: bold; color: #1E3A8A; }
                        .header { background-color: #1E7E34; color: #ffffff; font-weight: bold; text-align: center; border: 1px solid #000000; }
                        .kpi-title { font-weight: bold; background-color: #F3F4F6; }
                        .kpi-val { font-weight: bold; }
                        .profit-val { font-weight: bold; color: #15803D; }
                        td { padding: 6px 10px; border: 1px solid #D1D5DB; }
                        .num { text-align: right; }
                        .center { text-align: center; }
                    </style>
                </head>
                <body>
                    <table>
                        <tr>
                            <td colspan="13" class="title" style="border:none;">ELEKTRO POS - SAVDO VA FOYDA HISOBOTI</td>
                        </tr>
                        <tr>
                            <td colspan="13" style="border:none; font-size: 11pt;"><b>Hisobot davri:</b> $periodTitle</td>
                        </tr>
                        <tr>
                            <td colspan="13" style="border:none; font-size: 10pt; color: #6B7280;"><b>Generatsiya vaqti:</b> ${dateFormat.format(Date())}</td>
                        </tr>
                        <tr><td colspan="13" style="border:none;"></td></tr>
                        
                        <!-- KPI Xulosa -->
                        <tr>
                            <td colspan="5" class="kpi-title">UMUMIY MOLIYAVIY XULOSA</td>
                            <td colspan="8" class="kpi-title">KO'RSATKICH</td>
                        </tr>
                        <tr>
                            <td colspan="5">Jami Tushum:</td>
                            <td colspan="8" class="kpi-val">${numberFormat.format(summary.totalRevenue)} so'm</td>
                        </tr>
                        <tr>
                            <td colspan="5">Jami Tan Narxi:</td>
                            <td colspan="8" class="kpi-val">${numberFormat.format(summary.totalCost)} so'm</td>
                        </tr>
                        <tr>
                            <td colspan="5">Sof Foyda (karta solig‘idan keyin):</td>
                            <td colspan="8" class="profit-val">${numberFormat.format(summary.netProfit)} so'm</td>
                        </tr>
                        <tr>
                            <td colspan="5">Jami Cheklar Soni:</td>
                            <td colspan="8" class="kpi-val">${summary.salesCount} ta savdo; Qaytarish amallari: ${summary.returnCount}, sof qaytarilgan: ${numberFormat.format(summary.refundedAmount)}, tannarx tiklanishi: ${numberFormat.format(summary.costReversal)} so‘m; ${summary.brakCount} ta brak (${numberFormat.format(summary.brakCost)} so‘m). USD foyda: ${if (summary.usdComplete) String.format(Locale.US, "%.2f", summary.netProfitUsd) else "noma’lum: eski kurs saqlanmagan"}</td>
                        </tr>
                        <tr><td colspan="13" style="border:none;"></td></tr>

                        <!-- Jadval sarlavhasi -->
                        <tr class="header">
                            <td>№</td>
                            <td>Sana va Vaqt</td>
                            <td>Chek №</td>
                            <td>Kategoriya</td>
                            <td>Ombor</td>
                            <td>Mahsulot Nomi</td>
                            <td>Birligi</td>
                            <td>Miqdori</td>
                            <td>Tan Narxi (so'm)</td>
                            <td>Sotish Narxi (so'm)</td>
                            <td>Jami Summa (so'm)</td>
                            <td>Sof Foyda (so'm)</td>
                            <td>Rentabellik %</td>
                        </tr>
            """.trimIndent())

            items.forEachIndexed { index, item ->
                val unitStr = when (item.unitType) {
                    UnitType.METR -> "Metr"
                    UnitType.KG -> "Kg"
                    UnitType.DONA -> "Dona"
                }
                val qtyStr = if (item.quantity % 1.0 == 0.0) item.quantity.toLong().toString() else item.quantity.toString()
                val marginPercent = if (item.totalPrice > 0.0) (item.profit / item.totalPrice) * 100.0 else 0.0
                val marginStr = String.format(Locale.US, "%.1f%%", marginPercent)

                htmlContent.append("""
                    <tr>
                        <td class="center">${index + 1}</td>
                        <td>${dateFormat.format(Date(item.timestamp))}</td>
                        <td class="center">#${item.receiptNumber}</td>
                        <td>${item.category.ifBlank { "Boshqa" }}</td>
                        <td>${item.warehouseName.ifBlank { "Asosiy ombor" }}</td>
                        <td><b>${item.productName}</b></td>
                        <td class="center">$unitStr</td>
                        <td class="center">$qtyStr</td>
                        <td class="num">${numberFormat.format(item.costPrice)}</td>
                        <td class="num">${numberFormat.format(item.sellingPrice)}</td>
                        <td class="num"><b>${numberFormat.format(item.totalPrice)}</b></td>
                        <td class="num" style="color:#15803D;"><b>${numberFormat.format(item.profit)}</b></td>
                        <td class="num" style="font-weight:bold; color: #2563EB;">$marginStr</td>
                    </tr>
                """.trimIndent())
            }

            htmlContent.append("""
                    </table>
                </body>
                </html>
            """.trimIndent())

            val reportsDir = File(context.cacheDir, "reports").apply {
                if (!exists()) mkdirs()
            }

            val fileName = "Hisobot_${System.currentTimeMillis()}.xls"
            val file = File(reportsDir, fileName)

            FileOutputStream(file).use { fos ->
                OutputStreamWriter(fos, StandardCharsets.UTF_8).use { writer ->
                    writer.write(htmlContent.toString())
                    writer.flush()
                }
            }

            shareExcelFile(context, file)

            file
        }
    }

    private fun shareExcelFile(context: Context, file: File) {
        val uri: Uri = FileProvider.getUriForFile(
            context,
            "${context.packageName}.fileprovider",
            file
        )

        val shareIntent = Intent(Intent.ACTION_SEND).apply {
            type = "application/vnd.ms-excel"
            putExtra(Intent.EXTRA_STREAM, uri)
            putExtra(Intent.EXTRA_SUBJECT, "Electro POS Savdo Hisoboti")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }

        val chooser = Intent.createChooser(shareIntent, "Hisobotni ulashish / Saqlash")
        chooser.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(chooser)
    }
}
