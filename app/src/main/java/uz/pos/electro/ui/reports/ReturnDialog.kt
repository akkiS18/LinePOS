package uz.pos.electro.ui.reports

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.debt.DebtAccounting
import java.math.BigDecimal
import java.math.RoundingMode

private fun returnNumber(value: String): BigDecimal {
    val text = value.trim().replace(',', '.')
    require(text.length <= 40 && text.matches(Regex("[0-9]+(?:\\.[0-9]+)?"))) {
        "Musbat son yoki 0 kiriting (masalan: 10000 yoki 10000.00)"
    }
    return text.toBigDecimal()
}

private fun validateReturnQuote(value: JSONObject, saleGuid: String, isDebt: Boolean): JSONObject {
    require(value.getString("SaleGuid") == saleGuid) { "Qaytarish cheki mos emas" }
    val lines = value.getJSONArray("Lines")
    require(lines.length() > 0) { "Chek qatorlari topilmadi" }
    val ids = mutableSetOf<String>()
    for (i in 0 until lines.length()) {
        val line = lines.getJSONObject(i)
        require(line.getString("Guid").isNotBlank() && ids.add(line.getString("Guid")))
        line.getString("ProductName"); line.getString("WarehouseGuid")
        // JSON doubles may use scientific notation; normalize transport numbers,
        // while keeping user input strict and bounded.
        for (field in listOf("Sold", "Returned", "Revenue", "Refunded")) {
            val raw = line.get(field).toString()
            require(raw.length <= 64)
            val decimal = raw.toBigDecimal()
            require(decimal.scale() in -16..16 && decimal.precision() <= 32)
            line.put(field, decimal.toPlainString())
        }
        val sold = returnNumber(line.get("Sold").toString())
        val returned = returnNumber(line.get("Returned").toString())
        val revenue = returnNumber(line.get("Revenue").toString())
        val refunded = returnNumber(line.get("Refunded").toString())
        require(sold.signum() > 0 && returned <= sold && refunded <= revenue) { "Qaytarish tarixi mos emas" }
    }
    if (isDebt) {
        require(!value.isNull("DebtAccountGuid") && value.getString("DebtAccountGuid").isNotBlank()) {
            "Nasiya hisobining ma’lumoti topilmadi. Avval qarz sinxronini yakunlang."
        }
        // Strict integer parsing: missing/invalid balances must never turn into zero.
        value.get("AccountBalanceMinor").toString().toLong()
    }
    return value
}

@Composable
fun ReturnDialog(receipt: SaleWithItems, viewModel: ReportsViewModel, onDismiss: () -> Unit) {
    if (receipt.sale.paymentType == uz.pos.electro.data.model.PaymentType.RETURN) {
        ReturnReversalDialog(receipt, viewModel, onDismiss); return
    }
    val scope = rememberCoroutineScope()
    val warehouses by viewModel.warehouses.collectAsState()
    var quote by remember { mutableStateOf<JSONObject?>(null) }
    val quantities = remember { mutableStateMapOf<String, String>() }
    val destinations = remember { mutableStateMapOf<String, String>() }
    val damaged = remember { mutableStateMapOf<String, Boolean>() }
    var reason by remember { mutableStateOf("") }
    var cash by remember { mutableStateOf("0") }
    var card by remember { mutableStateOf("0") }
    var fee by remember { mutableStateOf("0") }
    var requestId by remember { mutableStateOf(java.util.UUID.randomUUID().toString()) }
    var state by remember { mutableStateOf("draft") }
    var message by remember { mutableStateOf("") }
    var result by remember { mutableStateOf<JSONObject?>(null) }
    var busy by remember { mutableStateOf(true) }
    var draftLoaded by remember { mutableStateOf(false) }
    val isDebt = receipt.sale.paymentType == PaymentType.DEBT
    val editable = !busy && draftLoaded && quote != null && state == "draft"
    fun number(value: String) = returnNumber(value)
    fun paymentNumber(value: String): BigDecimal = number(value).also {
        require(it.stripTrailingZeros().scale() <= 2) { "Pul summasida ko‘pi bilan 2 kasr xona bo‘lsin" }
    }
    fun refund(): BigDecimal {
        var sum = BigDecimal.ZERO
        val lines = requireNotNull(quote) { "Qaytarish ma’lumotlari yuklanmadi" }.getJSONArray("Lines")
        for (i in 0 until lines.length()) {
            val line = lines.getJSONObject(i); val guid = line.getString("Guid")
            val qty = number(quantities[guid] ?: "0")
            if (qty.signum() == 0) continue
            val sold = number(line.get("Sold").toString()); val prior = number(line.get("Returned").toString())
            require(qty > BigDecimal.ZERO && qty <= sold - prior) { "Qaytarish miqdorini tekshiring" }
            sum += number(line.get("Revenue").toString()).multiply(prior + qty).divide(sold, 2, RoundingMode.HALF_UP) - number(line.get("Refunded").toString())
        }
        return sum
    }
    fun debtSplit(amount: BigDecimal) = DebtAccounting.splitReturn(
        amount.movePointRight(2).longValueExact(),
        requireNotNull(quote).get("AccountBalanceMinor").toString().toLong()
    )
    fun payload(): JSONObject {
        require(draftLoaded && quote != null) { "Qaytarish ma’lumotlari yuklanmadi" }
        val items = JSONArray()
        quantities.forEach { (guid, text) -> val qty = number(text); if (qty.signum() != 0) items.put(JSONObject()
            .put("SaleItemGuid", guid).put("Quantity", qty.toPlainString())
            .put("WarehouseGuid", destinations[guid] ?: "").put("Resellable", damaged[guid] != true)) }
        require(items.length() > 0 && reason.isNotBlank()) { "Mahsulot miqdori va sababni kiriting" }
        val totalSum = refund()
        require(totalSum.signum() > 0) { "Qaytariladigan summa 0 dan katta bo‘lsin" }
        val cashValue = paymentNumber(cash)
        val cardValue = paymentNumber(card)
        val feeValue = paymentNumber(fee)
        val expectedRefund = if (isDebt) BigDecimal.valueOf(debtSplit(totalSum).refundMinor).movePointLeft(2) else totalSum
        require((cashValue + cardValue).compareTo(expectedRefund) == 0) { "Naqd va karta yig'indisini tekshiring" }
        return JSONObject().put("RequestGuid", requestId).put("SaleGuid", receipt.sale.guid).put("Reason", reason)
            .put("CashRefund", cashValue.toPlainString()).put("CardRefund", cardValue.toPlainString())
            .put("FeeReversal", feeValue.toPlainString()).put("Items", items)
    }
    LaunchedEffect(receipt.sale.guid) {
        try {
            // Offline preparation uses immutable local amounts; the authority rechecks every quantity.
            quote = try {
                validateReturnQuote(viewModel.returnSync.localReturnQuote(receipt), receipt.sale.guid, isDebt)
            } catch (e: CancellationException) { throw e
            } catch (e: Exception) {
                message = "Mahalliy ma’lumot o‘qilmadi: ${e.message}"
                null
            }
            try {
                quote = validateReturnQuote(viewModel.returnSync.quoteReturn(receipt.sale.guid), receipt.sale.guid, isDebt)
                message = ""
            } catch (e: CancellationException) { throw e
            } catch (e: Exception) {
                message = if (quote == null) "Qaytarish ma’lumoti tayyor emas. Oynani yopib, sinxrondan keyin qayta oching. ${e.message}"
                    else "Mahalliy ma’lumot ko‘rsatilmoqda. Yakuniy tasdiq uchun kompyuter bilan aloqa kerak. ${e.message}"
            }
            val lines = quote?.optJSONArray("Lines") ?: JSONArray()
            for (i in 0 until lines.length()) {
                val line = lines.getJSONObject(i)
                val id = line.optString("Guid", "")
                if (id.isNotBlank()) {
                    quantities[id] = "0"
                    destinations[id] = line.optString("WarehouseGuid", "")
                }
            }
            try {
                viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.let { draft ->
                    val body = draft.getJSONObject("payload")
                    if (body != null) {
                        state = draft.getString("state")
                        require(state in listOf("draft", "submitted", "confirmed"))
                        requestId = body.getString("RequestGuid")
                        java.util.UUID.fromString(requestId)
                        reason = body.getString("Reason")
                        cash = body.get("CashRefund").toString()
                        card = body.get("CardRefund").toString()
                        fee = body.get("FeeReversal").toString()
                        val items = body.getJSONArray("Items")
                        for (i in 0 until items.length()) {
                            val row = items.getJSONObject(i)
                            val id = row.getString("SaleItemGuid")
                            require(id.isNotBlank())
                            quantities[id] = row.get("Quantity").toString()
                            destinations[id] = row.getString("WarehouseGuid")
                            damaged[id] = !row.getBoolean("Resellable")
                        }
                    }
                    result = draft.optJSONObject("result")?.also { it.getString("Guid"); it.get("Refund") }
                }
                draftLoaded = true
            } catch (e: CancellationException) { throw e
            } catch (e: Exception) {
                message = e.message ?: "Loyiha ochilmadi"
            }
        } catch (e: CancellationException) { throw e
        } catch (e: Exception) {
            message = "Xatolik: ${e.message ?: "Yuklashda muammo bo'ldi"}"
        } finally {
            busy = false
        }
    }
    Dialog(onDismissRequest = { if (!busy) onDismiss() }) {
        Surface(shape = MaterialTheme.shapes.large) {
            Column(Modifier.padding(16.dp).verticalScroll(rememberScrollState())) {
                Text("Mahsulot qaytarish", style = MaterialTheme.typography.titleLarge)
                Text(receipt.sale.receiptNumber)
                if (result != null) {
                    Text("Saqlandi: RT-" + result!!.getString("Guid").replace("-", "").uppercase())
                    Text("Qaytarilgan summa: " + result!!.get("Refund") + " so‘m")
                    Button(enabled = !busy, onClick = { busy = true; scope.launch { try { viewModel.returnSync.acknowledgeReturn(receipt.sale.guid); onDismiss() } catch(e: CancellationException) { throw e } catch(e: Exception) { message = e.message ?: "Natija yopilmadi" } finally { busy = false } } }) { Text("Tushunarli") }
                    Text(message, color = MaterialTheme.colorScheme.error)
                } else {
                    val lines = quote?.optJSONArray("Lines") ?: JSONArray()
                    TextButton(enabled = editable, onClick = {
                        for (i in 0 until lines.length()) { val line=lines.getJSONObject(i)
                            quantities[line.getString("Guid")]=(number(line.get("Sold").toString())-number(line.get("Returned").toString())).toPlainString() }
                    }) { Text("Qolgan barchasini qaytarish") }
                    for (i in 0 until lines.length()) {
                        val line = lines.getJSONObject(i); val id = line.getString("Guid")
                        Text(line.getString("ProductName"), modifier = Modifier.padding(top = 12.dp))
                        Text("Sotilgan: ${line.get("Sold")} • Qaytgan: ${line.get("Returned")}")
                        OutlinedTextField(value = quantities[id] ?: "0", onValueChange = { quantities[id] = it }, enabled = editable, label = { Text("Qaytarish miqdori") })
                        var expanded by remember(id) { mutableStateOf(false) }
                        Box {
                            TextButton(enabled = editable, onClick = { expanded = true }) { Text(warehouses.find { it.guid == destinations[id] }?.name ?: "Qabul ombori") }
                            DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) { warehouses.forEach { wh -> DropdownMenuItem(text = { Text(wh.name) }, onClick = { destinations[id] = wh.guid; expanded = false }) } }
                        }
                        Row { Checkbox(checked = damaged[id] == true, onCheckedChange = { damaged[id] = it }, enabled = editable); Text("Nuqsonli — sotuv qoldig‘iga kirmaydi") }
                    }
                    OutlinedTextField(reason, { reason = it }, enabled = editable, label = { Text("Sabab") })
                    val amountCheck = runCatching { refund() }
                    val amount = amountCheck.getOrNull()
                    Text("Qaytariladigan tovar summasi: ${amount?.toPlainString() ?: "Miqdorni tekshiring"} so‘m")
                    // Zero is a valid empty preview, but never a financial return operation.
                    val splitCheck = runCatching { if (isDebt && amount != null && amount.signum() > 0) debtSplit(amount) else null }
                    val split = splitCheck.getOrNull()
                    val expectedRefund = if (isDebt) {
                        when {
                            amount == null -> null
                            amount.signum() == 0 -> BigDecimal.ZERO
                            split != null -> BigDecimal.valueOf(split.refundMinor).movePointLeft(2)
                            else -> null
                        }
                    } else amount
                    if (isDebt && expectedRefund != null) {
                        val offset = split?.let { BigDecimal.valueOf(it.debtOffsetMinor).movePointLeft(2) } ?: BigDecimal.ZERO
                        Text("Qarzdan chegiriladi: ${offset.toPlainString()} so‘m | Mijozga to'lanadi: ${expectedRefund.toPlainString()} so‘m", color = MaterialTheme.colorScheme.primary)
                    }
                    val previewError = amountCheck.exceptionOrNull() ?: splitCheck.exceptionOrNull()
                    if (!busy && quote != null && previewError != null) Text(previewError.message ?: "Hisobni tekshiring", color = MaterialTheme.colorScheme.error)
                    TextButton(enabled = editable && expectedRefund != null && amount != null && amount.signum() > 0,
                        onClick = { expectedRefund?.let { cash = it.toPlainString(); card = "0" } }) { Text("Barchasi naqd") }
                    OutlinedTextField(cash, { cash = it }, enabled = editable, label = { Text("Naqd, so‘m") })
                    OutlinedTextField(card, { card = it }, enabled = editable, label = { Text("Karta, so‘m") })
                    OutlinedTextField(fee, { fee = it }, enabled = editable, label = { Text("Qaytgan karta xarajati (odatda 0)") })
                    Text("Bankdan avtomatik pul o'tkazilmaydi. Tasdiq kelmaguncha qaytarishni yakunlamang.")
                    Text(message, color = MaterialTheme.colorScheme.error)
                    Button(enabled = !busy && draftLoaded && (state != "draft" || (quote != null && expectedRefund != null && amount != null && amount.signum() > 0)), onClick = {
                        busy = true
                        scope.launch { try {
                            if (state == "draft") viewModel.returnSync.saveReturnDraft(payload())
                            state = "submitted"; result = viewModel.returnSync.confirmReturn(receipt.sale.guid); state = "confirmed"
                        } catch (e: CancellationException) { throw e
                        } catch (e: Exception) {
                            message = e.message ?: "Aloqa xatosi — shu so'rovni qayta tekshiring"
                            try { state = viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.getString("state") ?: "draft" }
                            catch (cancel: CancellationException) { throw cancel }
                            catch (readError: Exception) { draftLoaded = false; message = "So‘rov holati o‘qilmadi. Oynani qayta oching. ${readError.message}" }
                        } finally { busy = false } }
                    }) { Text(if(state == "submitted") "Natijani qayta tekshirish" else "Qaytarishni tasdiqlash") }
                    TextButton(enabled = editable, onClick = { busy = true; scope.launch { try { viewModel.returnSync.saveReturnDraft(payload()); onDismiss() } catch(e: CancellationException) { throw e } catch(e: Exception) { message = e.message ?: "Saqlanmadi" } finally { busy = false } } }) { Text("Loyihani saqlash") }
                    TextButton(enabled = !busy, onClick = onDismiss) { Text("Yopish") }
                }
            }
        }
    }
}

@Composable
private fun ReturnReversalDialog(receipt: SaleWithItems, viewModel: ReportsViewModel, onDismiss: () -> Unit) {
    val scope = rememberCoroutineScope()
    var reason by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(true) }
    var submitted by remember { mutableStateOf(false) }
    var result by remember { mutableStateOf<JSONObject?>(null) }
    var message by remember { mutableStateOf("") }
    var id by remember { mutableStateOf(java.util.UUID.randomUUID().toString()) }
    var draftLoaded by remember { mutableStateOf(false) }
    LaunchedEffect(receipt.sale.guid) {
        try { viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.let {
            reason = it.getJSONObject("payload").getString("Reason"); id = it.getJSONObject("payload").getString("RequestGuid")
            submitted = it.getString("state") != "draft"; result = it.optJSONObject("result")?.also { value -> value.getString("Guid") }
        }; draftLoaded = true
        } catch(e: CancellationException) { throw e }
        catch(e: Exception) { message = "So‘rov holati o‘qilmadi. Oynani qayta oching. ${e.message}" }
        finally { busy = false }
    }
    AlertDialog(onDismissRequest = { if(!busy) onDismiss() }, title = { Text("Qaytarishni bekor qilish") },
        text = { Column {
            Text("Asl qaytarish o‘chirilmaydi. Pul va ombor ta’siri qarama-qarshi yozuv bilan tiklanadi. Bankdan avtomatik pul olinmaydi.")
            Text(receipt.sale.receiptNumber)
            if(result != null) Text("Saqlandi: RV-" + result!!.getString("Guid").replace("-", "").uppercase())
            else OutlinedTextField(reason, { reason = it }, enabled = !busy && !submitted, label = { Text("Bekor qilish sababi") })
            Text(message)
        } },
        confirmButton = { TextButton(enabled = !busy && draftLoaded, onClick = {
            busy = true
            scope.launch { try {
                if(result != null) { viewModel.returnSync.acknowledgeReturn(receipt.sale.guid); onDismiss() }
                else {
                    require(reason.isNotBlank()) { "Sababni kiriting" }
                    if(!submitted) viewModel.returnSync.saveReturnDraft(JSONObject().put("RequestGuid", id).put("SaleGuid",receipt.sale.guid).put("ReturnGuid",receipt.sale.guid).put("Reason",reason))
                    submitted = true; result = viewModel.returnSync.confirmReturn(receipt.sale.guid)
                }
            } catch(e: CancellationException) { throw e
            } catch(e: Exception) {
                message = e.message ?: "Natijani qayta tekshiring"
                try { submitted = viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.getString("state") == "submitted" }
                catch(cancel: CancellationException) { throw cancel }
                catch(readError: Exception) { draftLoaded = false; message = "So‘rov holati o‘qilmadi. Oynani qayta oching. ${readError.message}" }
            }
            finally { busy = false } }
        }) { Text(if(result != null) "Tushunarli" else if(submitted) "Natijani tekshirish" else "Bekor qilishni tasdiqlash") } },
        dismissButton = { TextButton(enabled = !busy, onClick = onDismiss) { Text("Yopish") } })
}
