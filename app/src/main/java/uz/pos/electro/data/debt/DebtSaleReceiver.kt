package uz.pos.electro.data.debt

import androidx.sqlite.db.SupportSQLiteDatabase
import java.math.BigDecimal
import uz.pos.electro.data.debt.DebtRepository.Companion.exec
import uz.pos.electro.data.debt.DebtRepository.Companion.rows
import uz.pos.electro.data.debt.DebtRepository.Companion.scalar

// Internal participant: the inbox owns the Room transaction and full-body receipt.
internal class DebtSaleReceiver private constructor(
    private val sale: DebtSaleSnapshot, private val wire: String, private val group: String,
    private val user: Long, private val products: Map<String,Long>,
    private val stocks: Map<Pair<String,String>,BigDecimal>, private val totals: Map<String,BigDecimal>
) {
    companion object {
        private val limit=BigDecimal("1000000000000000000")
        private fun need(ok: Boolean) { check(ok) { "Debt sale integrity or precision conflict" } }
        private fun d(value: String)=BigDecimal(value)
        private fun same(a: BigDecimal,b: BigDecimal)=a.compareTo(b)==0
        private fun stored(value: Any?): BigDecimal {
            need(value is Double || value is Long)
            val v=(value as Number).toDouble()
            need(v.isFinite() && kotlin.math.abs(v)<=1e18)
            return BigDecimal.valueOf(v)
        }
        // Never round an immutable snapshot to make it fit the legacy REAL columns.
        private fun real(value: BigDecimal): Double {
            need(value.abs()<=limit)
            val result=value.toDouble();need(same(stored(result),value));return result
        }
        private fun minor(value: Long)=BigDecimal.valueOf(value).movePointLeft(2)
        private fun money(value: Long)=real(minor(value))
        private fun marker(guid: String)="debt-sale:$guid"
        private fun payload(wire: String,user: Long)="$user:${DebtWire.fingerprint(wire)}"

        fun prepare(db: SupportSQLiteDatabase,wire: String,ev: DebtWireEvent,
            resolveActorUser: (String)->Long?): DebtSaleReceiver? {
            val s=DebtEnvelope.decodeSale(wire)
            need(ev.account?.saleGuid==s.guid)
            // A component/legacy header alone does not prove that stock ran exactly once.
            need(scalar(db,"SELECT 1 FROM sales WHERE guid=@p0",s.guid)==null)
            need(scalar(db,"SELECT 1 FROM debt_events WHERE guid=@p0",ev.guid)==null)
            need(scalar(db,"SELECT 1 FROM sync_journal WHERE op_id=@p0",marker(s.guid))==null)
            money(s.totalMinor);money(s.costMinor);money(s.cashMinor);money(s.cardMinor);money(s.feeMinor)
            real(d(s.feeRate));real(d(s.usdRate))
            val user=resolveActorUser(ev.actorGuid)
            // Unlike desktop, Android has a users table. Never invent a local user or use 1.
            var missing=user==null || user<=0 || scalar(db,"SELECT 1 FROM users WHERE id=@p0",user)==null
            val products=linkedMapOf<String,Long>()
            val stocks=linkedMapOf<Pair<String,String>,BigDecimal>()
            for(i in s.items) {
                real(d(i.quantity));real(d(i.price));real(d(i.cost));real(d(i.stockDelta))
                need(scalar(db,"SELECT 1 FROM sale_items WHERE guid=@p0",i.guid)==null)
                need(scalar(db,"SELECT 1 FROM sync_journal WHERE op_id=@p0",i.stockOperationGuid)==null)
                val product=scalar(db,"SELECT id FROM products WHERE guid=@p0",i.productGuid) as? Long
                if(product==null)missing=true else products[i.productGuid]=product
                if(scalar(db,"SELECT 1 FROM warehouses WHERE guid=@p0",i.warehouseGuid)==null)missing=true
                val key=i.productGuid to i.warehouseGuid
                if(!stocks.containsKey(key)) {
                    val old=scalar(db,"SELECT quantity FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.first,key.second)
                    stocks[key]=if(old==null)BigDecimal.ZERO else stored(old)
                }
                stocks[key]=stocks.getValue(key)+d(i.stockDelta)
                real(stocks.getValue(key))
            }
            val totals=linkedMapOf<String,BigDecimal>()
            for(p in products.keys) {
                var total=BigDecimal.ZERO
                for(row in rows(db,"SELECT warehouse_guid,quantity FROM product_stocks WHERE product_guid=@p0",p)) {
                    if(!stocks.containsKey(p to (row[0] as String)))total+=stored(row[1])
                }
                for((key,value) in stocks)if(key.first==p)total+=value
                real(total);totals[p]=total
            }
            return if(missing)null else DebtSaleReceiver(s,wire,ev.guid,user!!,products,stocks,totals)
        }

        fun verify(db: SupportSQLiteDatabase,wire: String,group: String) {
            val s=DebtEnvelope.decodeSale(wire)
            val headers=rows(db,"SELECT id,total_amount,total_cost,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,payment_type,user_id FROM sales WHERE guid=@p0",s.guid)
            need(headers.size==1);val h=headers[0]
            need(same(stored(h[1]),minor(s.totalMinor)) && same(stored(h[2]),minor(s.costMinor)) &&
                same(stored(h[3]),minor(s.cashMinor)) && same(stored(h[4]),minor(s.cardMinor)) && same(stored(h[5]),minor(s.feeMinor)))
            need(same(stored(h[6]),d(s.feeRate)) && same(stored(h[7]),d(s.usdRate)) && h[8]==s.occurredAt && h[9]=="DEBT")
            val mark=rows(db,"SELECT kind,entity_guid,payload,group_id,warehouse_guid,delta FROM sync_journal WHERE op_id=@p0",marker(s.guid))
            need(mark.size==1 && mark[0][0]=="debt_sale" && mark[0][1]==s.guid && mark[0][2]==payload(wire,h[10] as Long) && mark[0][3]==group && mark[0][4]=="" && same(stored(mark[0][5]),BigDecimal.ZERO))
            val items=rows(db,"SELECT guid,sale_guid,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency,product_id FROM sale_items WHERE sale_id=@p0 ORDER BY id",h[0])
            need(items.size==s.items.size)
            for(n in items.indices) {
                val r=items[n];val i=s.items[n]
                val text=listOf(i.guid,s.guid,i.productGuid,i.productName,i.category,i.unit,i.warehouseGuid,i.warehouseName)
                for(k in text.indices)need(r[k]==text[k])
                need(same(stored(r[8]),d(i.quantity)) && same(stored(r[9]),d(i.price)) && same(stored(r[10]),d(i.cost)) && r[11]==i.costCurrency)
                need(scalar(db,"SELECT guid FROM products WHERE id=@p0",r[12])==i.productGuid)
                val move=rows(db,"SELECT kind,entity_guid,warehouse_guid,delta,payload,group_id FROM sync_journal WHERE op_id=@p0",i.stockOperationGuid)
                need(move.size==1 && move[0][0]=="debt_stock" && move[0][1]==i.productGuid && move[0][2]==i.warehouseGuid && same(stored(move[0][3]),d(i.stockDelta)) && move[0][4]==i.guid && move[0][5]==group)
            }
            // Later legitimate stock movements do not invalidate historical receipt replay.
        }
    }

    fun apply(db: SupportSQLiteDatabase) {
        need(scalar(db,"SELECT applying FROM sync_control WHERE id=1")==1L)
        exec(db,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES(@p0,@p1,@p2,'DEBT',@p3,@p4,@p5,@p6,@p7,@p8,@p9,0)",
            sale.guid,money(sale.totalMinor),money(sale.costMinor),money(sale.cashMinor),money(sale.cardMinor),money(sale.feeMinor),real(d(sale.feeRate)),real(d(sale.usdRate)),sale.occurredAt,user)
        val id=scalar(db,"SELECT id FROM sales WHERE guid=@p0",sale.guid)!!
        for(i in sale.items) {
            exec(db,"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                i.guid,id,sale.guid,products.getValue(i.productGuid),i.productGuid,i.productName,i.category,i.unit,i.warehouseGuid,i.warehouseName,real(d(i.quantity)),real(d(i.price)),real(d(i.cost)),i.costCurrency)
            exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,payload,group_id,acked) VALUES(@p0,'debt_stock',@p1,@p2,@p3,@p4,@p5,-1)",
                i.stockOperationGuid,i.productGuid,i.warehouseGuid,real(d(i.stockDelta)),i.guid,group)
        }
        for((key,value) in stocks) {
            // API26 SQLite predates ON CONFLICT DO UPDATE. The same writer transaction
            // makes this existence check + INSERT/UPDATE atomic without REPLACE semantics.
            if(scalar(db,"SELECT 1 FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.first,key.second)==null)
                exec(db,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3)",key.first,key.second,real(value),sale.occurredAt)
            else exec(db,"UPDATE product_stocks SET quantity=@p0,updated_at=MAX(updated_at,@p1) WHERE product_guid=@p2 AND warehouse_guid=@p3",real(value),sale.occurredAt,key.first,key.second)
        }
        for((product,total) in totals)exec(db,"UPDATE products SET stock_quantity=@p0 WHERE guid=@p1",real(total),product)
        exec(db,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,'debt_sale',@p1,@p2,@p3,-1)",marker(sale.guid),sale.guid,payload(wire,user),group)
        verify(db,wire,group)
    }
}
