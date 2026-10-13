package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase
import uz.pos.electro.data.local.relation.SaleWithItems
import uz.pos.electro.data.model.*
import java.math.BigDecimal
import java.math.RoundingMode

/** Rebuilds cash-recognised profit from immutable facts, without writing financial history. */
object DebtProfitProjection {
    data class Result(val lines: List<SaleReportItem>, val receipts: Map<Long,Double>)
    private data class Change(val time:Long,val guid:String,val delta:Long=0,val item:String="",val revenue:Long=0,
        val basis:Long=0,val restored:Long=0,val fee:Long=0,val rate:BigDecimal?=null,val returned:Boolean=false)
    private fun minor(n:Any?) = n.toString().toBigDecimal().movePointRight(2).setScale(0,RoundingMode.HALF_UP).longValueExact()
    private fun rate(n:Any?) = n?.toString()?.toBigDecimalOrNull()?.takeIf { it.signum()>0 }
    private fun usd(n:Long,r:BigDecimal?) = if(n==0L) BigDecimal.ZERO else r?.let { BigDecimal.valueOf(n,2).divide(it,16,RoundingMode.HALF_UP) }
    fun build(db:SupportSQLiteDatabase, sales:List<SaleWithItems>, start:Long,end:Long):Result {
        val originals=sales.filter { it.sale.paymentType==PaymentType.DEBT }.associateBy { it.sale.guid }
        val byGuid=sales.associateBy { it.sale.guid }
        val accounts=DebtRepository.rows(db,"SELECT guid,sale_guid,original_debt_minor FROM debt_accounts").associateBy { it[0] as String }
        val bySale=accounts.values.associateBy { it[1] as String }
        val changes=mutableMapOf<String,MutableList<Change>>()
        fun add(sale:String,c:Change) { if(sale in originals && c.time<=end) changes.getOrPut(sale){ mutableListOf() }.add(c) }
        val events=DebtRepository.rows(db,"SELECT e.guid,e.occurred_at,e.kind,e.fee_minor,e.fee_usd_rate,l.account_guid,l.debt_delta_minor,l.line_index FROM debt_events e JOIN debt_event_lines l ON l.event_guid=e.guid WHERE e.occurred_at<=? ORDER BY e.guid,l.line_index",end).groupBy { it[0] as String }
        for((guid,rows) in events) {
            val first=rows.first(); val kind=first[2] as String
            val fee=if(kind in listOf("payment","payment_reversal")) first[3] as Long else 0L
            val weights=rows.map { (it[6] as Long).let { n -> require(n != Long.MIN_VALUE); kotlin.math.abs(n) } }
            val fees=DebtProfitAccounting.allocate(fee,weights)
            for((i,r) in rows.withIndex()) {
                val account=accounts[r[5] as String] ?: error("Qarz hisobi topilmadi")
                add(account[1] as String,Change(r[1] as Long,guid,delta=r[6] as Long,fee=fees[i],rate=rate(r[4])))
            }
        }
        val returns=DebtRepository.rows(db,"SELECT r.guid,r.sale_guid,r.created_at,r.fee_reversal,i.sale_item_guid,i.refund_amount_uzs,i.cost_basis_uzs,i.cost_reversal_uzs FROM returns r JOIN return_items i ON i.return_guid=r.guid WHERE r.created_at<=? ORDER BY r.guid,i.sale_item_guid",end).groupBy { it[0] as String }
        val debtReturnGuids=mutableSetOf<String>()
        for((guid,rows) in returns) {
            val sale=rows.first()[1] as String
            if(sale !in originals) continue
            debtReturnGuids.add(guid)
            for(r in rows) add(sale,Change(r[2] as Long,guid,item=r[4] as String,revenue=minor(r[5]),basis=minor(r[6]),restored=minor(r[7]),returned=true))
            add(sale,Change(rows.first()[2] as Long,guid,fee=-minor(rows.first()[3]),rate=rate(byGuid[guid]?.sale?.usdRate),returned=true))
        }
        val output=mutableListOf<SaleReportItem>();val receiptProfits=mutableMapOf<Long,Long>()
        for(receipt in sales) {
            if(receipt.sale.createdAt !in start..end) continue
            val deferred=receipt.sale.guid in originals || receipt.sale.guid in debtReturnGuids
            output.addAll(SaleAccounting.lines(receipt).map { if(deferred)it.copy(profit=0.0,profitUsd=0.0) else it })
        }
        for((guid,receipt) in originals) {
            val sale=receipt.sale; if(sale.createdAt>end)continue
            val account=bySale[guid] ?: error("Nasiya foydasi: qarz hisobi topilmadi ($guid)")
            val templates=SaleAccounting.lines(receipt)
            val items=receipt.items.sortedBy { it.id }; val index=items.mapIndexed { i,it->it.guid to i }.toMap()
            val revenue=templates.map { minor(it.totalPrice) };val cost=templates.map { minor(it.totalCost) }
            val usdCost=items.filter { it.costCurrency=="USD" }.sumOf { it.costAtSale*it.quantity }
            val uzsCost=items.filter { it.costCurrency!="USD" }.sumOf { it.costAtSale*it.quantity }
            val saleRate=rate(sale.usdRate) ?: if(usdCost>0)rate((sale.totalCost-uzsCost)/usdCost) else null
            add(guid,Change(sale.createdAt,guid,fee=minor(sale.taxAmount),rate=saleRate))
            var balance=account[2] as Long
            val returned=LongArray(items.size);val basis=LongArray(items.size);val restored=LongArray(items.size)
            val feeTotals=LongArray(items.size);val feeUsd=Array(items.size){BigDecimal.ZERO};val unknownFee=LongArray(items.size)
            var previous=List(items.size){0L};var previousUsd=List<BigDecimal?>(items.size){BigDecimal.ZERO}
            for((time,batch) in changes.getValue(guid).groupBy { it.time }.toSortedMap()) {
                for(c in batch) {
                    balance=Math.addExact(balance,c.delta)
                    if(c.item.isNotEmpty()) {
                        val i=index[c.item] ?: error("Qaytarishning asl mahsulot qatori topilmadi")
                        returned[i]=Math.addExact(returned[i],c.revenue);basis[i]=Math.addExact(basis[i],c.basis);restored[i]=Math.addExact(restored[i],c.restored)
                    }
                    val parts=DebtProfitAccounting.allocate(c.fee,revenue)
                    for(i in parts.indices) {
                        feeTotals[i]=Math.addExact(feeTotals[i],parts[i])
                        val fx=usd(parts[i],c.rate)
                        if(fx==null)unknownFee[i]=Math.addExact(unknownFee[i],parts[i]) else feeUsd[i]+=fx
                    }
                }
                val gross=DebtProfitAccounting.gross(items.indices.map { DebtProfitAccounting.Line(revenue[it],cost[it],returned[it],basis[it],restored[it]) },balance)
                val current=gross.indices.map { Math.subtractExact(gross[it],feeTotals[it]) }
                val currentUsd=gross.indices.map { if(unknownFee[it]!=0L)null else usd(gross[it],saleRate)?.subtract(feeUsd[it]) }
                val returnGuid=batch.firstOrNull { it.returned }?.guid
                val target=returnGuid?.let { byGuid[it] } ?: receipt
                for(i in current.indices) {
                    val delta=Math.subtractExact(current[i],previous[i])
                    val fx=if(currentUsd[i]!=null && previousUsd[i]!=null)currentUsd[i]!!.subtract(previousUsd[i]) else null
                    if(time in start..end && (delta!=0L || fx==null || fx.signum()!=0)) {
                        output.add(templates[i].copy(saleId=target.sale.id,timestamp=time,receiptNumber=target.sale.receiptNumber,
                            quantity=0.0,totalPrice=0.0,totalCost=0.0,taxAmount=0.0,cashAmount=0.0,cardAmount=0.0,debtAmount=0.0,
                            profit=delta/100.0,profitUsd=fx?.toDouble(),isReturn=returnGuid!=null,profitOnly=true))
                    }
                    receiptProfits[target.sale.id]=Math.addExact(receiptProfits[target.sale.id]?:0L,delta)
                }
                previous=current;previousUsd=currentUsd
            }
            receiptProfits.putIfAbsent(sale.id,0L)
        }
        return Result(output,receiptProfits.mapValues { it.value/100.0 })
    }
}
