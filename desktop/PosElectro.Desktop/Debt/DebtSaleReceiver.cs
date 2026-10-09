using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using static PosElectro.Desktop.Debt.DebtRepository;

namespace PosElectro.Desktop.Debt;

// Internal participant only. The inbox owns the writer transaction and full-body receipt.
// A host must explicitly supply its authenticated actor -> local user mapping to enable it.
internal sealed class DebtSaleReceiver
{
    private readonly DebtSaleSnapshot sale;
    private readonly string wire, group;
    private readonly long user;
    private readonly Dictionary<string,long> products;
    private readonly Dictionary<(string Product,string Warehouse),decimal> stocks;
    private readonly Dictionary<string,decimal> totals;
    private DebtSaleReceiver(DebtSaleSnapshot sale,string wire,string group,long user,
        Dictionary<string,long> products,Dictionary<(string,string),decimal> stocks,Dictionary<string,decimal> totals) {
        this.sale=sale;this.wire=wire;this.group=group;this.user=user;
        this.products=products;this.stocks=stocks;this.totals=totals;
    }
    private static void Need(bool ok) { if(!ok)throw new InvalidOperationException("Debt sale integrity or precision conflict"); }
    private static decimal D(string value)=>decimal.Parse(value,NumberStyles.Float,CultureInfo.InvariantCulture);
    private static decimal Stored(object? value) {
        Need(value is double or long);
        var d=Convert.ToDouble(value,CultureInfo.InvariantCulture);
        Need(double.IsFinite(d) && Math.Abs(d)<=1e18);
        return D(d.ToString("R",CultureInfo.InvariantCulture));
    }
    // Reject precision loss in legacy REAL columns. Rounding a financial snapshot is not repair.
    private static double Real(decimal value) {
        Need(Math.Abs(value)<=1000000000000000000m);
        var result=(double)value;Need(Stored(result)==value);return result;
    }
    private static double Money(long minor)=>Real(minor/100m);
    private static string Marker(string saleGuid)=>"debt-sale:"+saleGuid;
    private static string Payload(string wire,long user)=>user.ToString(CultureInfo.InvariantCulture)+":"+DebtWire.Fingerprint(wire);

    internal static DebtSaleReceiver? Prepare(SqliteConnection db,SqliteTransaction tx,string wire,
        DebtWireEvent ev,Func<string,long?> resolveActorUser) {
        var s=DebtEnvelope.DecodeSale(wire);
        Need(ev.Account!=null && ev.Account.SaleGuid==s.Guid);
        // Without a full inbox receipt, an existing sale/event is not proof that stock ran once.
        Need(Scalar(db,tx,"SELECT 1 FROM sales WHERE guid=@p0",s.Guid)==null);
        Need(Scalar(db,tx,"SELECT 1 FROM debt_events WHERE guid=@p0",ev.Guid)==null);
        Need(Scalar(db,tx,"SELECT 1 FROM sync_journal WHERE op_id=@p0",Marker(s.Guid))==null);
        Money(s.TotalMinor);Money(s.CostMinor);Money(s.CashMinor);Money(s.CardMinor);Money(s.FeeMinor);
        Real(D(s.FeeRate));Real(D(s.UsdRate));
        var user=resolveActorUser(ev.ActorGuid);
        // Desktop has no users table. The trusted host owns this mapping; the wire
        // may never choose UserId. Android must also validate its local users dependency.
        var missing=user.GetValueOrDefault()<=0;
        var products=new Dictionary<string,long>(StringComparer.Ordinal);
        var stocks=new Dictionary<(string Product,string Warehouse),decimal>();
        foreach(var i in s.Items) {
            Real(D(i.Quantity));Real(D(i.Price));Real(D(i.Cost));Real(D(i.StockDelta));
            Need(Scalar(db,tx,"SELECT 1 FROM sale_items WHERE guid=@p0",i.Guid)==null);
            Need(Scalar(db,tx,"SELECT 1 FROM sync_journal WHERE op_id=@p0",i.StockOperationGuid)==null);
            var product=Scalar(db,tx,"SELECT id FROM products WHERE guid=@p0",i.ProductGuid);
            if(product==null)missing=true;else products[i.ProductGuid]=Convert.ToInt64(product,CultureInfo.InvariantCulture);
            if(Scalar(db,tx,"SELECT 1 FROM warehouses WHERE guid=@p0",i.WarehouseGuid)==null)missing=true;
            var key=(i.ProductGuid,i.WarehouseGuid);
            if(!stocks.ContainsKey(key)) {
                var old=Scalar(db,tx,"SELECT quantity FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.ProductGuid,key.WarehouseGuid);
                stocks[key]=old==null?0:Stored(old);
            }
            stocks[key]+=D(i.StockDelta);Real(stocks[key]); // Repeated basket rows accumulate once each.
        }
        var totals=new Dictionary<string,decimal>(StringComparer.Ordinal);
        foreach(var p in products.Keys) {
            decimal total=0;
            foreach(var row in Rows(db,tx,"SELECT warehouse_guid,quantity FROM product_stocks WHERE product_guid=@p0",p)) {
                var key=(p,(string)row[0]!);
                if(!stocks.ContainsKey(key))total+=Stored(row[1]);
            }
            total+=stocks.Where(x=>x.Key.Product==p).Sum(x=>x.Value);
            Real(total);totals[p]=total;
        }
        return missing?null:new(s,wire,ev.Guid,user!.Value,products,stocks,totals);
    }

    internal void Apply(SqliteConnection db,SqliteTransaction tx) {
        Need(Convert.ToInt64(Scalar(db,tx,"SELECT applying FROM sync_control WHERE id=1"))==1);
        Exec(db,tx,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES(@p0,@p1,@p2,3,@p3,@p4,@p5,@p6,@p7,@p8,@p9,0)",
            sale.Guid,Money(sale.TotalMinor),Money(sale.CostMinor),Money(sale.CashMinor),Money(sale.CardMinor),Money(sale.FeeMinor),Real(D(sale.FeeRate)),Real(D(sale.UsdRate)),sale.OccurredAt,user);
        var id=Scalar(db,tx,"SELECT id FROM sales WHERE guid=@p0",sale.Guid)!;
        foreach(var i in sale.Items) {
            Exec(db,tx,"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                i.Guid,id,sale.Guid,products[i.ProductGuid],i.ProductGuid,i.ProductName,i.Category,i.Unit,i.WarehouseGuid,i.WarehouseName,Real(D(i.Quantity)),Real(D(i.Price)),Real(D(i.Cost)),i.CostCurrency);
            Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,payload,group_id,acked) VALUES(@p0,'debt_stock',@p1,@p2,@p3,@p4,@p5,-1)",
                i.StockOperationGuid,i.ProductGuid,i.WarehouseGuid,Real(D(i.StockDelta)),i.Guid,group);
        }
        foreach(var pair in stocks)Exec(db,tx,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3) ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=excluded.quantity,updated_at=MAX(product_stocks.updated_at,excluded.updated_at)",
            pair.Key.Product,pair.Key.Warehouse,Real(pair.Value),sale.OccurredAt);
        foreach(var pair in totals)Exec(db,tx,"UPDATE products SET stock_quantity=@p0 WHERE guid=@p1",Real(pair.Value),pair.Key);
        Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,'debt_sale',@p1,@p2,@p3,-1)",Marker(sale.Guid),sale.Guid,Payload(wire,user),group);
        Verify(db,tx,wire,group);
    }

    internal static void Verify(SqliteConnection db,SqliteTransaction tx,string wire,string group) {
        var s=DebtEnvelope.DecodeSale(wire);
        var rows=Rows(db,tx,"SELECT id,total_amount,total_cost,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,payment_type,user_id FROM sales WHERE guid=@p0",s.Guid);
        Need(rows.Count==1);var h=rows[0];
        Need(Stored(h[1])==s.TotalMinor/100m && Stored(h[2])==s.CostMinor/100m && Stored(h[3])==s.CashMinor/100m && Stored(h[4])==s.CardMinor/100m && Stored(h[5])==s.FeeMinor/100m);
        Need(Stored(h[6])==D(s.FeeRate) && Stored(h[7])==D(s.UsdRate) && Convert.ToInt64(h[8])==s.OccurredAt && Convert.ToInt64(h[9])==3);
        var marker=Rows(db,tx,"SELECT kind,entity_guid,payload,group_id,warehouse_guid,delta FROM sync_journal WHERE op_id=@p0",Marker(s.Guid));
        Need(marker.Count==1 && (string)marker[0][0]! =="debt_sale" && (string)marker[0][1]! ==s.Guid && (string)marker[0][2]! ==Payload(wire,Convert.ToInt64(h[10])) && (string)marker[0][3]! ==group && (string)marker[0][4]! =="" && Stored(marker[0][5])==0);
        var items=Rows(db,tx,"SELECT guid,sale_guid,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency,product_id FROM sale_items WHERE sale_id=@p0 ORDER BY id",h[0]);
        Need(items.Count==s.Items.Count);
        for(int n=0;n<items.Count;n++) {
            var r=items[n];var i=s.Items[n];
            var text=new[]{i.Guid,s.Guid,i.ProductGuid,i.ProductName,i.Category,i.Unit,i.WarehouseGuid,i.WarehouseName};
            for(int k=0;k<text.Length;k++)Need((string?)r[k]==text[k]);
            Need(Stored(r[8])==D(i.Quantity) && Stored(r[9])==D(i.Price) && Stored(r[10])==D(i.Cost) && (string?)r[11]==i.CostCurrency);
            Need((string?)Scalar(db,tx,"SELECT guid FROM products WHERE id=@p0",r[12])==i.ProductGuid);
            var move=Rows(db,tx,"SELECT kind,entity_guid,warehouse_guid,delta,payload,group_id FROM sync_journal WHERE op_id=@p0",i.StockOperationGuid);
            Need(move.Count==1 && (string?)move[0][0]=="debt_stock" && (string?)move[0][1]==i.ProductGuid && (string?)move[0][2]==i.WarehouseGuid && Stored(move[0][3])==D(i.StockDelta) && (string?)move[0][4]==i.Guid && (string?)move[0][5]==group);
        }
        // Today's stock can legitimately change after import; replay verifies movements, not balances.
    }
}
