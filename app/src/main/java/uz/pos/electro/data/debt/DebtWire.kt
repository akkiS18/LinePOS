package uz.pos.electro.data.debt

import java.math.BigInteger
import java.nio.ByteBuffer
import java.nio.CharBuffer
import java.nio.charset.CodingErrorAction
import java.nio.charset.CharacterCodingException
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.LocalDate
import java.util.Base64
import java.util.Collections
import java.util.UUID

data class DebtWireAccount(val guid: String, val saleGuid: String, val openingEventGuid: String,
    val originalDebtMinor: Long, val dueDate: String?, val customerNameAtSale: String)
data class DebtWireCustomer(val guid: String, val storeGuid: String, val deviceGuid: String, val payload: String, val payloadHash: String)
data class DebtWireEvent(val guid: String, val requestGuid: String, val kind: String, val customerGuid: String,
    val storeGuid: String, val actorGuid: String, val deviceGuid: String, val deviceSequence: Long, val occurredAt: Long,
    val payload: String, val payloadHash: String, val cashMinor: Long, val cardMinor: Long, val feeMinor: Long,
    val feeUsdRate: String?, val account: DebtWireAccount?, val lines: List<DebtLine>)

// A pure debt component codec. Does not authorize a peer, write SQL, or constitute a
// complete atomic sale/stock envelope. Capability is NOT yet advertised by the app.
object DebtWire {
    const val CAPABILITY = "debtLedgerV1"
    const val MAX_PACKET_CHARS = 2 * 1024 * 1024
    const val MAX_LINES = 10000
    internal fun id(s: String) { require(UUID.fromString(s).toString()==s && s!="00000000-0000-0000-0000-000000000000") }
    internal fun number(s: String): Long {
        require(s.matches(Regex("0|-?[1-9][0-9]{0,18}")))
        val n=s.toLongOrNull();require(n!=null && n!=Long.MIN_VALUE);return n
    }
    internal fun text(s: String,max: Int,required: Boolean=false) {
        require(s.length<=max && (!required || s.any { !it.isWhitespace() && it!='\u0085' }) && s.none { it<' ' })
    }
    private fun date(s: String?) {
        if(s!=null) { val d=LocalDate.parse(s);require(d.year in 1..9999 && d.toString()==s && s.matches(Regex("[0-9]{4}-[0-9]{2}-[0-9]{2}"))) }
    }
    private fun rate(s: String?) { if(s!=null)require(s.matches(Regex("[0-9]{1,12}(?:\\.[0-9]{1,8})?")) && s.toBigDecimal().signum()>0) }
    private fun optional(s: String) = s.takeIf { it.isNotEmpty() }
    private fun utf8(s: String): ByteArray = try {
        val b=StandardCharsets.UTF_8.newEncoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).encode(CharBuffer.wrap(s))
        ByteArray(b.remaining()).also { b.get(it) }
    } catch(e: CharacterCodingException) { throw IllegalArgumentException("Invalid UTF-8",e) }
    private fun decodeUtf8(b: ByteArray): String = try {
        StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(b)).toString()
    } catch(e: CharacterCodingException) { throw IllegalArgumentException("Invalid UTF-8",e) }
    fun fingerprint(canonicalWire: String): String = MessageDigest.getInstance("SHA-256").digest(utf8(canonicalWire)).joinToString("") { "%02x".format(it.toInt() and 255) }
    internal fun pack(tag: String,fields: List<String>,maxChars: Int=MAX_PACKET_CHARS): String {
        val result="[\"$tag\","+fields.joinToString(",") { "\""+Base64.getEncoder().encodeToString(utf8(it))+"\"" }+"]"
        require(result.length<=maxChars);return result
    }
    internal fun unpack(wire: String,tag: String,maxFields: Int,maxChars: Int=MAX_PACKET_CHARS): List<String> {
        require(wire.length<=maxChars)
        val prefix="[\"$tag\",";require(wire.startsWith(prefix) && wire.endsWith("]"))
        val parts=wire.substring(prefix.length,wire.length-1).split(',');require(parts.size<=maxFields)
        val fields=parts.map { token ->
            require(token.length>=2 && token.first()=='"' && token.last()=='"')
            val b64=token.substring(1,token.length-1);val bytes=Base64.getDecoder().decode(b64)
            require(Base64.getEncoder().encodeToString(bytes)==b64);decodeUtf8(bytes)
        }
        require(pack(tag,fields,maxChars)==wire);return fields
    }
    internal fun commandFields(payload: String): List<String> = unpack(payload,"debt-command-v1",12)
    fun requirePeer(expectedStore: String,peerStore: String,peerCapabilities: Collection<String>) {
        id(expectedStore);id(peerStore);require(expectedStore==peerStore && CAPABILITY in peerCapabilities)
    }
    private fun validate(c: DebtWireCustomer,expectedStore: String) {
        id(expectedStore);id(c.guid);id(c.storeGuid);id(c.deviceGuid);require(c.storeGuid==expectedStore && c.payload.length<=16384)
        val p=unpack(c.payload,"debt-command-v1",8);require(p.size==8 && p[0]=="customer" && p[1]==c.storeGuid && p[3]==c.guid)
        id(p[2]);text(p[4],256,true);text(p[5],64);text(p[6],2048);require(number(p[7])>=0)
        require(c.payloadHash==fingerprint(c.payload))
    }
    fun encodeCustomer(c: DebtWireCustomer,expectedStore: String): String {
        validate(c,expectedStore);return pack("debt-customer-v1",listOf(c.guid,c.storeGuid,c.deviceGuid,c.payload,c.payloadHash))
    }
    fun decodeCustomer(wire: String,expectedStore: String): DebtWireCustomer {
        val p=unpack(wire,"debt-customer-v1",5);require(p.size==5)
        return DebtWireCustomer(p[0],p[1],p[2],p[3],p[4]).also { validate(it,expectedStore) }
    }
    private fun validate(e: DebtWireEvent,expectedStore: String) {
        id(expectedStore);id(e.guid);id(e.requestGuid);id(e.customerGuid);id(e.storeGuid);id(e.actorGuid);id(e.deviceGuid)
        require(e.storeGuid==expectedStore && e.guid==e.requestGuid && e.deviceSequence>0 && e.occurredAt>=0)
        require(e.payload.length<=16384 && e.payloadHash==fingerprint(e.payload) && e.lines.size<=MAX_LINES)
        val p=unpack(e.payload,"debt-command-v1",12)
        require(p.size>=5 && p[0]==e.kind && p[1]==e.storeGuid && p[2]==e.actorGuid && p[3]==e.requestGuid && p[4]==e.customerGuid)
        when(e.kind) {
            "sale_open" -> {
                require(p.size==12 && e.account!=null && e.lines.isEmpty() && e.cashMinor==0L && e.cardMinor==0L && e.feeMinor==0L && e.feeUsdRate==null)
                val a=e.account;id(a.guid);id(a.saleGuid);id(a.openingEventGuid);date(a.dueDate);text(a.customerNameAtSale,256,true)
                require(a.guid==a.saleGuid && a.saleGuid==p[5] && a.openingEventGuid==e.guid && p[6].matches(Regex("[0-9a-f]{64}")))
                require(a.originalDebtMinor>0 && a.originalDebtMinor==DebtAccounting.newDebt(number(p[7]),number(p[8]),number(p[9])))
                require(number(p[10])==e.occurredAt && optional(p[11])==a.dueDate)
            }
            "payment" -> {
                require(p.size==11 && e.account==null && e.lines.isNotEmpty())
                require(e.cashMinor>=0 && e.cardMinor>=0 && e.feeMinor>=0 && e.feeMinor<=e.cardMinor);rate(e.feeUsdRate)
                val total=BigInteger.valueOf(e.cashMinor)+BigInteger.valueOf(e.cardMinor);require(total>BigInteger.ZERO && total<=BigInteger.valueOf(Long.MAX_VALUE))
                require(number(p[5])==e.cashMinor && number(p[6])==e.cardMinor && number(p[7])==e.feeMinor && number(p[8])==e.occurredAt && optional(p[10])==e.feeUsdRate)
                val target=optional(p[9]);target?.let { id(it) }
                val seen=mutableSetOf<String>();var delta=BigInteger.ZERO
                for(line in e.lines) {
                    id(line.accountGuid);require(seen.add(line.accountGuid) && line.deltaMinor<0 && line.deltaMinor!=Long.MIN_VALUE)
                    require(target==null || target==line.accountGuid);delta+=BigInteger.valueOf(line.deltaMinor)
                }
                require(delta == -total)
            }
            else -> throw IllegalArgumentException("Unsupported debt event kind")
        }
    }
    fun encodeEvent(value: DebtWireEvent,expectedStore: String): String {
        require(value.lines.size<=MAX_LINES);val e=value.copy(lines=value.lines.toList());validate(e,expectedStore)
        val a=e.account
        val fields=mutableListOf(e.guid,e.requestGuid,e.kind,e.customerGuid,e.storeGuid,e.actorGuid,e.deviceGuid,e.deviceSequence.toString(),e.occurredAt.toString(),
            e.payload,e.payloadHash,e.cashMinor.toString(),e.cardMinor.toString(),e.feeMinor.toString(),e.feeUsdRate ?: "",
            a?.guid ?: "",a?.saleGuid ?: "",a?.openingEventGuid ?: "",a?.originalDebtMinor?.toString() ?: "",a?.dueDate ?: "",a?.customerNameAtSale ?: "",e.lines.size.toString())
        for(line in e.lines){fields.add(line.accountGuid);fields.add(line.deltaMinor.toString())}
        return pack("debt-event-v1",fields)
    }
    fun decodeEvent(wire: String,expectedStore: String): DebtWireEvent {
        val p=unpack(wire,"debt-event-v1",22+2*MAX_LINES);require(p.size>=22)
        val count=number(p[21]);require(count>=0 && count<=MAX_LINES && p.size.toLong()==22+2*count)
        val account=if(p[15].isNotEmpty())DebtWireAccount(p[15],p[16],p[17],number(p[18]),optional(p[19]),p[20])
            else { require(p.subList(16,21).all { it.isEmpty() });null }
        val lines=(0 until count.toInt()).map { DebtLine(p[22+2*it],number(p[23+2*it])) }
        return DebtWireEvent(p[0],p[1],p[2],p[3],p[4],p[5],p[6],number(p[7]),number(p[8]),p[9],p[10],number(p[11]),number(p[12]),number(p[13]),optional(p[14]),account,
            Collections.unmodifiableList(lines)).also { validate(it,expectedStore) }
    }
}
