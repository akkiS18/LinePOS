import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.debt.*
import java.time.DateTimeException

object WireRunner {
    fun run(input: String): JSONObject {
        val fixtures=JSONArray(input);val results=JSONObject()
        for(i in 0 until fixtures.length()) {
            val f=fixtures.getJSONObject(i);val store=f.getString("store")
            val result=try {
                val kind=f.getString("op")
                if(kind=="peer") {
                    val c=f.getJSONArray("capabilities")
                    DebtWire.requirePeer(store,f.getString("peer"),(0 until c.length()).map { c.getString(it) })
                    JSONObject().put("accepted",true)
                } else {
                    val wire=if(kind=="oversize")"x".repeat(DebtWire.MAX_PACKET_CHARS+1) else f.getString("wire")
                    val encoded=if(kind=="customer")DebtWire.encodeCustomer(DebtWire.decodeCustomer(wire,store),store) else {
                        val decoded=DebtWire.decodeEvent(wire,store)
                        val frozen=DebtWire.encodeEvent(decoded,store)
                        if(decoded.lines.isNotEmpty()) {
                            var blocked=false
                            try { (decoded.lines as MutableList<DebtLine>)[0]=DebtLine("tampered",1) }
                            catch(_: UnsupportedOperationException){blocked=true}
                            check(blocked){"Decoded lines are mutable"}
                        }
                        frozen
                    }
                    check(encoded==wire){"Non-canonical roundtrip"}
                    JSONObject().put("hash",DebtWire.fingerprint(encoded))
                }
            } catch(_: IllegalArgumentException){JSONObject().put("error","invalid")}
            catch(_: ArithmeticException){JSONObject().put("error","invalid")}
            catch(_: DateTimeException){JSONObject().put("error","invalid")}
            check(result.similar(f.getJSONObject("expected"))){"Wire ${f.getString("id")}: expected ${f.get("expected")}, got $result"}
            results.put(f.getString("id"),result)
        }
        println("Kotlin: ${fixtures.length()} shared wire fixtures passed")
        return results
    }
}
