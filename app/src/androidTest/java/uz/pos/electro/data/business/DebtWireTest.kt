package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import org.json.JSONArray
import org.junit.Assert.*
import org.junit.Test
import uz.pos.electro.data.debt.DebtWire
import java.time.DateTimeException

class DebtWireTest {
    @Test fun sharedWireContractOnAndroidRuntime() {
        val input=InstrumentationRegistry.getInstrumentation().context.assets.open("debt-wire-fixtures.json").bufferedReader().use { it.readText() }
        val fixtures=JSONArray(input)
        for(i in 0 until fixtures.length()) {
            val f=fixtures.getJSONObject(i);val store=f.getString("store");var hash: String?=null;var accepted=false
            try {
                val kind=f.getString("op")
                if(kind=="peer") {
                    val caps=f.getJSONArray("capabilities")
                    DebtWire.requirePeer(store,f.getString("peer"),(0 until caps.length()).map { caps.getString(it) })
                } else {
                    val wire=if(kind=="oversize")"x".repeat(DebtWire.MAX_PACKET_CHARS+1) else f.getString("wire")
                    val encoded=if(kind=="customer")DebtWire.encodeCustomer(DebtWire.decodeCustomer(wire,store),store)
                        else DebtWire.encodeEvent(DebtWire.decodeEvent(wire,store),store)
                    assertEquals(f.getString("id"),wire,encoded);hash=DebtWire.fingerprint(encoded)
                }
                accepted=true
            } catch(_: IllegalArgumentException) { }
            catch(_: ArithmeticException) { }
            catch(_: DateTimeException) { }
            val expected=f.getJSONObject("expected")
            assertEquals(f.getString("id"),!expected.has("error"),accepted)
            if(expected.has("hash"))assertEquals(f.getString("id"),expected.getString("hash"),hash)
        }
    }
}
