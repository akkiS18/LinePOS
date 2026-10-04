package uz.pos.electro.ui.reports

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.SaleAccounting
import java.math.BigDecimal
import java.math.RoundingMode

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
    val editable = !busy && state == "draft"
    fun number(value: String) = value.replace(',', '.').toBigDecimal()
    fun refund(): BigDecimal {
        var sum = BigDecimal.ZERO
        val lines = quote?.getJSONArray("Lines") ?: error("Chek ma'lumoti yo'q")
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
    fun payload(): JSONObject {
        val items = JSONArray()
        quantities.forEach { (guid, text) -> val qty = number(text); if (qty.signum() != 0) items.put(JSONObject()
            .put("SaleItemGuid", guid).put("Quantity", qty.toPlainString())
            .put("WarehouseGuid", destinations[guid] ?: "").put("Resellable", damaged[guid] != true)) }
        require(items.length() > 0 && reason.isNotBlank()) { "Mahsulot miqdori va sababni kiriting" }
        require(number(cash) + number(card) == refund()) { "Naqd va karta yig'indisini tekshiring" }
        return JSONObject().put("RequestGuid", requestId).put("SaleGuid", receipt.sale.guid).put("Reason", reason)
            .put("CashRefund", number(cash).toPlainString()).put("CardRefund", number(card).toPlainString())
            .put("FeeReversal", number(fee).toPlainString()).put("Items", items)
    }
    LaunchedEffect(receipt.sale.guid) {
        // Offline preparation uses immutable local amounts; the authority rechecks every quantity.
        val localLines = JSONArray()
        val financials = SaleAccounting.lines(receipt)
        receipt.items.sortedBy { it.id }.forEachIndexed { i, item -> localLines.put(JSONObject()
            .put("Guid", item.guid).put("ProductName", item.productName).put("WarehouseGuid", item.warehouseGuid)
            .put("Sold", item.quantity).put("Returned", 0).put("Refunded", 0).put("Revenue", financials[i].totalPrice)) }
        quote = JSONObject().put("Lines", localLines)
        try { quote = viewModel.returnSync.quoteReturn(receipt.sale.guid) }
        catch (e: Exception) { message = "Oflayn loyiha. Yakuniy tasdiq uchun mahalliy kompyuter kerak. ${e.message}" }
        val lines = quote!!.getJSONArray("Lines")
        for (i in 0 until lines.length()) { val line = lines.getJSONObject(i); val id = line.getString("Guid")
            quantities[id] = "0"; destinations[id] = line.getString("WarehouseGuid") }
        try {
            viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.let { draft ->
                val body = draft.getJSONObject("payload"); state = draft.getString("state"); requestId = body.getString("RequestGuid")
                reason = body.getString("Reason"); cash = body.get("CashRefund").toString(); card = body.get("CardRefund").toString(); fee = body.get("FeeReversal").toString()
                val items = body.getJSONArray("Items"); for (i in 0 until items.length()) { val row = items.getJSONObject(i); val id = row.getString("SaleItemGuid")
                    quantities[id] = row.get("Quantity").toString(); destinations[id] = row.getString("WarehouseGuid"); damaged[id] = !row.getBoolean("Resellable") }
                result = draft.optJSONObject("result")
            }
        } catch (e: Exception) { message = e.message ?: "Loyiha ochilmadi" }
        busy = false
    }
    Dialog(onDismissRequest = { if (!busy) onDismiss() }) {
        Surface(shape = MaterialTheme.shapes.large) {
            Column(Modifier.padding(16.dp).verticalScroll(rememberScrollState())) {
                Text("Mahsulot qaytarish", style = MaterialTheme.typography.titleLarge)
                Text(receipt.sale.receiptNumber)
                if (result != null) {
                    Text("Saqlandi: RT-" + result!!.getString("Guid").replace("-", "").uppercase())
                    Text("Qaytarilgan summa: " + result!!.get("Refund") + " so‘m")
                    Button(enabled = !busy, onClick = { busy = true; scope.launch { try { viewModel.returnSync.acknowledgeReturn(receipt.sale.guid); onDismiss() } finally { busy = false } } }) { Text("Tushunarli") }
                } else {
                    val lines = quote?.optJSONArray("Lines") ?: JSONArray()
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
                    val amount = runCatching { refund().toPlainString() }.getOrNull()
                    Text("Qaytariladigan summa: ${amount ?: "Miqdorni tekshiring"}")
                    TextButton(enabled = editable && amount != null, onClick = { cash = amount ?: "0"; card = "0" }) { Text("Barchasi naqd") }
                    OutlinedTextField(cash, { cash = it }, enabled = editable, label = { Text("Naqd, so‘m") })
                    OutlinedTextField(card, { card = it }, enabled = editable, label = { Text("Karta, so‘m") })
                    OutlinedTextField(fee, { fee = it }, enabled = editable, label = { Text("Qaytgan karta xarajati (odatda 0)") })
                    Text("Bankdan avtomatik pul o'tkazilmaydi. Tasdiq kelmaguncha qaytarishni yakunlamang.")
                    Text(message, color = MaterialTheme.colorScheme.error)
                    Button(enabled = !busy, onClick = {
                        busy = true
                        scope.launch { try {
                            if (state == "draft") viewModel.returnSync.saveReturnDraft(payload())
                            state = "submitted"; result = viewModel.returnSync.confirmReturn(receipt.sale.guid); state = "confirmed"
                        } catch (e: Exception) {
                            message = e.message ?: "Aloqa xatosi — shu so'rovni qayta tekshiring"
                            state = viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.optString("state") ?: "draft"
                        } finally { busy = false } }
                    }) { Text(if(state == "submitted") "Natijani qayta tekshirish" else "Qaytarishni tasdiqlash") }
                    TextButton(enabled = editable, onClick = { busy = true; scope.launch { try { viewModel.returnSync.saveReturnDraft(payload()); onDismiss() } catch(e: Exception) { message = e.message ?: "Saqlanmadi" } finally { busy = false } } }) { Text("Loyihani saqlash") }
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
    LaunchedEffect(receipt.sale.guid) {
        try { viewModel.returnSync.getReturnDraft(receipt.sale.guid)?.let {
            reason = it.getJSONObject("payload").getString("Reason"); id = it.getJSONObject("payload").getString("RequestGuid")
            submitted = it.getString("state") != "draft"; result = it.optJSONObject("result")
        } } finally { busy = false }
    }
    AlertDialog(onDismissRequest = { if(!busy) onDismiss() }, title = { Text("Qaytarishni bekor qilish") },
        text = { Column {
            Text("Asl qaytarish o‘chirilmaydi. Pul va ombor ta’siri qarama-qarshi yozuv bilan tiklanadi. Bankdan avtomatik pul olinmaydi.")
            Text(receipt.sale.receiptNumber)
            if(result != null) Text("Saqlandi: RV-" + result!!.getString("Guid").replace("-", "").uppercase())
            else OutlinedTextField(reason, { reason = it }, enabled = !busy && !submitted, label = { Text("Bekor qilish sababi") })
            Text(message)
        } },
        confirmButton = { TextButton(enabled = !busy, onClick = {
            busy = true
            scope.launch { try {
                if(result != null) { viewModel.returnSync.acknowledgeReturn(receipt.sale.guid); onDismiss() }
                else {
                    require(reason.isNotBlank()) { "Sababni kiriting" }
                    if(!submitted) viewModel.returnSync.saveReturnDraft(JSONObject().put("RequestGuid", id).put("SaleGuid",receipt.sale.guid).put("ReturnGuid",receipt.sale.guid).put("Reason",reason))
                    submitted = true; result = viewModel.returnSync.confirmReturn(receipt.sale.guid)
                }
            } catch(e: Exception) { message = e.message ?: "Natijani qayta tekshiring" }
            finally { busy = false } }
        }) { Text(if(result != null) "Tushunarli" else if(submitted) "Natijani tekshirish" else "Bekor qilishni tasdiqlash") } },
        dismissButton = { TextButton(enabled = !busy, onClick = onDismiss) { Text("Yopish") } })
}
