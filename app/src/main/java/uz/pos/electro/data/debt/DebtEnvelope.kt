package uz.pos.electro.data.debt

import java.math.BigInteger
import java.util.Collections
import java.util.UUID

// Historical prices are UZS; original cost currency and per-item stock deltas are frozen.
data class DebtSaleItem(val guid: String,val productGuid: String,val productName: String,val category: String,
    val unit: String,val warehouseGuid: String,val warehouseName: String,val quantity: String,val price: String,val cost: String,
    val costCurrency: String,val stockOperationGuid: String,val stockDelta: String)
data class DebtSaleSnapshot(val guid: String,val occurredAt: Long,val totalMinor: Long,val costMinor: Long,
    val cashMinor: Long,val cardMinor: Long,val feeMinor: Long,val feeRate: String,val usdRate: String,val paymentType: String,
    val items: List<DebtSaleItem>)
data class DebtEnvelopePacket(val guid: String,val storeGuid: String,val customerWire: String,val eventWire: String,val saleWire: String)

// Pure protocol only. Does not write SQL, authorize a peer, persist an inbox, or ACK.
object DebtEnvelope {
    const val MAX_ENVELOPE_CHARS=6*1024*1024
    const val MAX_ITEMS=1000
    private val scale=BigInteger.TEN.pow(8)
    private fun decimalUnits(s: String): BigInteger {
        require(s.matches(Regex("(?:0|[1-9][0-9]{0,11})(?:\\.[0-9]{0,7}[1-9])?")))
        val p=s.split('.')
        return p[0].toBigInteger()*scale+if(p.size==1)BigInteger.ZERO else p[1].padEnd(8,'0').toBigInteger()
    }
    private fun rounded(value: BigInteger,divisor: BigInteger): Long {
        val n=(value+divisor/BigInteger.valueOf(2))/divisor
        require(n.signum()>=0 && n<=BigInteger.valueOf(Long.MAX_VALUE));return n.toLong()
    }
    private fun validateWarehouseId(s: String) {
        val isUuid = runCatching { UUID.fromString(s) }.isSuccess
        if (isUuid) {
            DebtWire.id(s)
        } else {
            DebtWire.text(s, 64, true)
        }
    }
    private fun validate(s: DebtSaleSnapshot) {
        DebtWire.id(s.guid);require(s.occurredAt>=0 && s.totalMinor>0 && s.costMinor>=0 && s.cashMinor>=0 && s.cardMinor>=0)
        require(s.paymentType=="DEBT" && s.feeMinor>=0 && s.feeMinor<=s.cardMinor)
        require(BigInteger.valueOf(s.cashMinor)+BigInteger.valueOf(s.cardMinor)<BigInteger.valueOf(s.totalMinor) && s.items.size in 1..MAX_ITEMS)
        val fx=decimalUnits(s.usdRate);val feeRate=decimalUnits(s.feeRate);val hundred=BigInteger.valueOf(100)
        require(feeRate<=hundred*scale && s.feeMinor==rounded(BigInteger.valueOf(s.cardMinor)*feeRate,hundred*scale))
        val ids=mutableSetOf<String>();val ops=mutableSetOf<String>();var revenue=BigInteger.ZERO;var cost=BigInteger.ZERO
        for(i in s.items) {
            DebtWire.id(i.guid);DebtWire.id(i.productGuid);validateWarehouseId(i.warehouseGuid);DebtWire.id(i.stockOperationGuid)
            require(ids.add(i.guid) && ops.add(i.stockOperationGuid))
            DebtWire.text(i.productName,256,true);DebtWire.text(i.category,256);DebtWire.text(i.unit,32,true);DebtWire.text(i.warehouseName,256,true)
            val q=decimalUnits(i.quantity);val price=decimalUnits(i.price);val unitCost=decimalUnits(i.cost)
            require(q.signum()>0 && i.stockDelta=="-"+i.quantity && i.costCurrency in listOf("UZS","USD"))
            require(i.costCurrency!="USD" || fx.signum()>0)
            revenue+=q*price;cost+=q*unitCost*(if(i.costCurrency=="USD")fx else scale)
        }
        require(s.totalMinor==rounded(revenue*hundred,scale*scale) && s.costMinor==rounded(cost*hundred,scale*scale*scale))
    }
    fun encodeSale(value: DebtSaleSnapshot): String {
        require(value.items.size<=MAX_ITEMS);val s=value.copy(items=value.items.toList());validate(s)
        val f=mutableListOf(s.guid,s.occurredAt.toString(),s.totalMinor.toString(),s.costMinor.toString(),s.cashMinor.toString(),s.cardMinor.toString(),s.feeMinor.toString(),s.feeRate,s.usdRate,s.paymentType,s.items.size.toString())
        for(i in s.items)f.addAll(listOf(i.guid,i.productGuid,i.productName,i.category,i.unit,i.warehouseGuid,i.warehouseName,i.quantity,i.price,i.cost,i.costCurrency,i.stockOperationGuid,i.stockDelta))
        return DebtWire.pack("debt-sale-v1",f)
    }
    fun decodeSale(wire: String): DebtSaleSnapshot {
        val p=DebtWire.unpack(wire,"debt-sale-v1",11+13*MAX_ITEMS);require(p.size>=11)
        val count=DebtWire.number(p[10]);require(count in 1L..MAX_ITEMS.toLong() && p.size.toLong()==11+13*count)
        val items=(0 until count.toInt()).map { i -> val x=11+13*i;DebtSaleItem(p[x],p[x+1],p[x+2],p[x+3],p[x+4],p[x+5],p[x+6],p[x+7],p[x+8],p[x+9],p[x+10],p[x+11],p[x+12]) }
        return DebtSaleSnapshot(p[0],DebtWire.number(p[1]),DebtWire.number(p[2]),DebtWire.number(p[3]),DebtWire.number(p[4]),DebtWire.number(p[5]),DebtWire.number(p[6]),p[7],p[8],p[9],Collections.unmodifiableList(items)).also { validate(it) }
    }
    private fun validate(p: DebtEnvelopePacket,expectedStore: String) {
        DebtWire.id(p.guid);DebtWire.requirePeer(expectedStore,p.storeGuid,listOf(DebtWire.CAPABILITY))
        val c=p.customerWire.takeIf { it.isNotEmpty() }?.let { DebtWire.decodeCustomer(it,expectedStore) }
        if(p.eventWire.isEmpty()) { require(c!=null && p.guid==c.guid && p.saleWire.isEmpty());return }
        val e=DebtWire.decodeEvent(p.eventWire,expectedStore)
        require(p.guid==e.guid && (c==null || c.guid==e.customerGuid) && (c==null || c.guid!=e.requestGuid))
        if(e.kind=="payment") { require(p.saleWire.isEmpty());return }
        val sale=decodeSale(p.saleWire);val command=DebtWire.commandFields(e.payload)
        require(sale.guid==e.account!!.saleGuid && sale.occurredAt==e.occurredAt)
        require(sale.totalMinor==DebtWire.number(command[7]) && sale.cashMinor==DebtWire.number(command[8]) && sale.cardMinor==DebtWire.number(command[9]))
        require(DebtWire.fingerprint(p.saleWire)==command[6])
    }
    fun encode(p: DebtEnvelopePacket,expectedStore: String): String {
        validate(p,expectedStore);return DebtWire.pack("debt-envelope-v1",listOf(p.guid,p.storeGuid,p.customerWire,p.eventWire,p.saleWire),MAX_ENVELOPE_CHARS)
    }
    fun decode(wire: String,expectedStore: String): DebtEnvelopePacket {
        val f=DebtWire.unpack(wire,"debt-envelope-v1",5,MAX_ENVELOPE_CHARS);require(f.size==5)
        return DebtEnvelopePacket(f[0],f[1],f[2],f[3],f[4]).also { validate(it,expectedStore) }
    }
}
