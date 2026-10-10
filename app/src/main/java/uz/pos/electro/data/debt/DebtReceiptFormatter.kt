package uz.pos.electro.data.debt

import uz.pos.electro.data.local.relation.SaleWithItems
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

object DebtReceiptFormatter {

    fun buildCustomerStatementText(customer: DebtCustomerDetailDto): String {
        val numberFormat = NumberFormat.getNumberInstance(Locale.US)
        val dateFormat = SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault())
        val sb = StringBuilder()

        sb.appendLine("          SMART KASSA           ")
        sb.appendLine("      MIJOZ QARZ KO'CHIRMASI    ")
        sb.appendLine("--------------------------------")
        sb.appendLine("Mijoz: ${customer.customer.name}")
        if (customer.customer.phone.isNotBlank()) sb.appendLine("Tel: ${customer.customer.phone}")
        sb.appendLine("Sana: ${dateFormat.format(Date())}")
        sb.appendLine("--------------------------------")
        sb.appendLine("NASIYALAR (HISOB-FAKTURALAR):")
        for (acc in customer.accounts) {
            val status = if (acc.balanceMinor <= 0) "Yopilgan" else "${numberFormat.format(acc.balanceUz)} so'm qoldiq"
            val createdDate = dateFormat.format(Date(acc.createdAt))
            sb.appendLine("Chek: ${acc.receiptNumber} ($createdDate)")
            sb.appendLine("  Asl qarz: ${numberFormat.format(acc.originalDebtUz)} so'm | $status")
        }
        sb.appendLine("--------------------------------")
        sb.appendLine("TO'LOVLAR VA AMALLAR TARIXI:")
        for (ev in customer.events) {
            val evDate = dateFormat.format(Date(ev.occurredAt))
            sb.appendLine("$evDate • ${ev.kindDisplay}")
            sb.appendLine("  Summa: ${numberFormat.format(ev.totalPaymentUz)} so'm (Naqd: ${numberFormat.format(ev.cashMinor / 100.0)} / Karta: ${numberFormat.format(ev.cardMinor / 100.0)})")
        }
        sb.appendLine("--------------------------------")
        val balTitle = "JORIY BALANS:"
        val balUz = customer.balanceMinor / 100.0
        val balVal = if (customer.balanceMinor > 0) {
            "${numberFormat.format(balUz)} so'm"
        } else if (customer.balanceMinor < 0) {
            "+${numberFormat.format(kotlin.math.abs(balUz))} so'm (Haqdor)"
        } else {
            "0 so'm"
        }
        val space = maxOf(1, 32 - balTitle.length - balVal.length)
        sb.appendLine(balTitle + " ".repeat(space) + balVal)
        sb.appendLine("--------------------------------")
        sb.appendLine("     Hisob-kitob uchun rahmat!   ")
        return sb.toString()
    }
}
