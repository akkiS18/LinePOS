package uz.pos.electro.data.business

import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.debt.*
import java.time.DateTimeException
import java.util.Locale

object EnvelopeRunner {
    fun run(input: String): JSONObject {
        val fixtures=JSONArray(input);val results=JSONObject()
        for(i in 0 until fixtures.length()) {
            val f=fixtures.getJSONObject(i);val store=f.getString("store")
            val result=try {
                val op=f.getString("op")
                val wire=if(op=="delimiter-flood")"[\"debt-envelope-v1\","+",".repeat(DebtEnvelope.MAX_ENVELOPE_CHARS-21)+"]" else if(op.startsWith("oversize-") && op!="oversize-unicode-sale")"x".repeat((if(op=="oversize-sale")DebtWire.MAX_PACKET_CHARS else DebtEnvelope.MAX_ENVELOPE_CHARS)+1) else f.getString("wire")
                if(op in listOf("max-items","max-items-plus-one","oversize-unicode-sale")) {
                    val s=DebtEnvelope.decodeSale(wire);val count=if(op=="max-items-plus-one")1001 else 1000
                    val items=(1..count).map { n -> s.items[0].copy(guid="00000000-0000-0000-0000-"+String.format(Locale.ROOT,"%012d",n),stockOperationGuid="00000000-0000-0000-0001-"+String.format(Locale.ROOT,"%012d",n),
                        productName=if(op=="oversize-unicode-sale")"界".repeat(256) else s.items[0].productName,
                        category=if(op=="oversize-unicode-sale")"界".repeat(256) else s.items[0].category,
                        warehouseName=if(op=="oversize-unicode-sale")"界".repeat(256) else s.items[0].warehouseName) }
                    val encoded=DebtEnvelope.encodeSale(s.copy(totalMinor=s.totalMinor*count,costMinor=s.costMinor*count,items=items))
                    check(DebtEnvelope.decodeSale(encoded).items.size==count){"Lost items"}
                    JSONObject().put("accepted",true)
                } else {
                    val encoded=if(op=="sale" || op=="oversize-sale") {
                        val s=DebtEnvelope.decodeSale(wire);val encoded=DebtEnvelope.encodeSale(s)
                        var blocked=false
                        try { (s.items as MutableList<DebtSaleItem>)[0]=s.items[0].copy(productName="tampered") }
                        catch(_: UnsupportedOperationException){blocked=true}
                        check(blocked){"Mutable sale items"};encoded
                    } else DebtEnvelope.encode(DebtEnvelope.decode(wire,store),store)
                    check(encoded==wire){"Noncanonical envelope roundtrip"}
                    JSONObject().put("hash",DebtWire.fingerprint(encoded))
                }
            } catch(_: IllegalArgumentException){JSONObject().put("error","invalid")}
            catch(_: ArithmeticException){JSONObject().put("error","invalid")}
            catch(_: DateTimeException){JSONObject().put("error","invalid")}
            val expected=f.getJSONObject("expected")
            check(result.length()==expected.length() && expected.keys().asSequence().all { result.has(it) && result.get(it)==expected.get(it) }){"Envelope ${f.getString("id")}: expected ${f.get("expected")}, got $result"}
            results.put(f.getString("id"),result)
        }
        println("Kotlin: ${fixtures.length()} shared envelope fixtures passed");return results
    }
}
