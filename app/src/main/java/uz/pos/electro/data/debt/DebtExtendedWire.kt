package uz.pos.electro.data.debt

import java.math.BigInteger

/** V2 adds a reference only for extended events; V1 canonical bytes stay unchanged. */
object DebtExtendedWire {
    const val CAPABILITY = "debtLedgerExtendedV2"
    fun supports(kind: String) = kind in listOf("payment_reversal","credit_refund","credit_transfer","return_offset","return_reversal")
    fun validate(e: DebtWireEvent, store: String) {
        DebtWire.requirePeer(store,e.storeGuid,listOf(DebtWire.CAPABILITY))
        listOf(e.guid,e.customerGuid,e.deviceGuid).forEach { DebtWire.id(it) }
        if(!(e.kind.startsWith("return_") && e.actorGuid=="desktop-local")) DebtWire.id(e.actorGuid)
        require(e.deviceSequence>0 && e.occurredAt>=0 && e.account==null && e.lines.isNotEmpty() && e.lines.size<=DebtWire.MAX_LINES)
        require(e.payload.length<=16384 && e.payloadHash==DebtWire.fingerprint(e.payload))
        val p=DebtWire.commandFields(e.payload)
        require(p.size>=8 && p[0]==e.kind && p[1]==store && p[2]==e.actorGuid && p[3]==e.requestGuid)
        if(e.kind.startsWith("return_")) {
            require(e.requestGuid.startsWith(e.kind+":")); DebtWire.id(e.requestGuid.removePrefix(e.kind+":"))
        } else { DebtWire.id(e.requestGuid); require(e.guid==e.requestGuid) }
        e.referenceGuid?.let { DebtWire.id(it) }
        val ids=mutableSetOf<String>()
        e.lines.forEach { DebtWire.id(it.accountGuid); require(ids.add(it.accountGuid) && it.deltaMinor!=0L && it.deltaMinor!=Long.MIN_VALUE) }
        require(listOf(e.cashMinor,e.cardMinor,e.feeMinor).none { it==Long.MIN_VALUE })
        fun n(i: Int)=DebtWire.number(p[i])
        fun line(i: Int, account: String, delta: Long) { require(e.lines[i]==DebtLine(account,delta)) }
        when(e.kind) {
            "payment_reversal" -> {
                require(p.size==8 && e.referenceGuid==p[4]); DebtWire.id(p[4]); DebtWire.text(p[7],1000,true)
                require(n(5)>=0 && e.feeMinor == -n(5) && n(6)==e.occurredAt && e.cashMinor<=0 && e.cardMinor<=0 && BigInteger.valueOf(e.cashMinor)+BigInteger.valueOf(e.cardMinor)<BigInteger.ZERO)
                require(e.feeMinor>=e.cardMinor && e.lines.all { it.deltaMinor>0 })
                require(e.lines.fold(BigInteger.ZERO) { a,l -> a+BigInteger.valueOf(l.deltaMinor) } == -(BigInteger.valueOf(e.cashMinor)+BigInteger.valueOf(e.cardMinor)))
            }
            "credit_refund" -> {
                require(p.size==10 && p[4]==e.customerGuid && e.referenceGuid==null && e.feeMinor==0L && e.feeUsdRate==null && e.lines.size==1)
                DebtWire.id(p[5]);DebtWire.text(p[9],1000,true)
                val total=Math.addExact(n(6),n(7));require(n(6)>=0 && n(7)>=0 && total>0 && n(8)==e.occurredAt)
                require(e.cashMinor == -n(6) && e.cardMinor == -n(7)); line(0,p[5],total)
            }
            "credit_transfer" -> {
                require(p.size==10 && p[4]==e.customerGuid && e.referenceGuid==null && e.cashMinor==0L && e.cardMinor==0L && e.feeMinor==0L && e.feeUsdRate==null && e.lines.size==2)
                DebtWire.id(p[5]);DebtWire.id(p[6]);DebtWire.text(p[9],1000,true)
                require(p[5]!=p[6] && n(7)>0 && n(8)==e.occurredAt);line(0,p[5],n(7));line(1,p[6],-n(7))
            }
            "return_offset" -> {
                require(p.size==8 && p[4]==e.customerGuid && e.referenceGuid!=null && e.lines.size==1 && e.feeUsdRate==null)
                DebtWire.id(p[5]);require(n(6)>0 && n(7)==e.occurredAt && e.cashMinor<=0 && e.cardMinor<=0 && e.feeMinor<=0)
                line(0,p[5],-n(6))
            }
            "return_reversal" -> {
                require(p.size==8 && p[4]==e.customerGuid && e.referenceGuid==p[5] && e.lines.size==1 && e.feeUsdRate==null)
                DebtWire.id(p[5]);DebtWire.text(p[7],1000,true)
                require(n(6)==e.occurredAt && e.cashMinor>=0 && e.cardMinor>=0 && e.feeMinor>=0 && e.lines[0].deltaMinor>0)
            }
            else -> error("Unsupported extended event")
        }
    }
}
