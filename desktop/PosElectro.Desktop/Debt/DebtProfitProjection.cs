using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Debt;

public static class DebtProfitProjection
{
    public sealed record Result(List<SaleReportItem> Lines, Dictionary<long,double> Receipts);
    sealed record Change(long Time,string Guid,long Delta=0,string Item="",long Revenue=0,long Basis=0,long Restored=0,
        long Fee=0,decimal? Rate=null,bool Returned=false);
    static long Minor(object? n) => checked((long)Math.Round(Convert.ToDecimal(n,CultureInfo.InvariantCulture)*100,0,MidpointRounding.AwayFromZero));
    static decimal? Rate(object? n) => n!=null && n!=DBNull.Value && decimal.TryParse(Convert.ToString(n,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out var r) && r>0 ? r : null;
    static decimal? Usd(long n,decimal? rate) => n==0 ? 0 : rate.HasValue ? Math.Round(n/100m/rate.Value,16,MidpointRounding.AwayFromZero) : null;
    static List<object[]> Rows(SqliteConnection db,SqliteTransaction tx,string sql,long? end=null) {
        using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;if(end.HasValue)cmd.Parameters.AddWithValue("@end",end.Value);
        using var r=cmd.ExecuteReader();var rows=new List<object[]>();while(r.Read()){var row=new object[r.FieldCount];r.GetValues(row);rows.Add(row);}return rows;
    }
    public static Result Build(SqliteConnection db,SqliteTransaction tx,List<Sale> sales,long start,long end)
    {
        var originals=sales.Where(s=>s.PaymentType==PaymentType.DEBT).ToDictionary(s=>s.Guid);
        var byGuid=sales.ToDictionary(s=>s.Guid);
        var accounts=Rows(db,tx,"SELECT guid,sale_guid,original_debt_minor FROM debt_accounts").ToDictionary(r=>(string)r[0]);
        var bySale=accounts.Values.ToDictionary(r=>(string)r[1]);
        var changes=new Dictionary<string,List<Change>>();
        void Add(string sale,Change c) {if(originals.ContainsKey(sale)&&c.Time<=end){if(!changes.ContainsKey(sale))changes[sale]=new();changes[sale].Add(c);}}
        var events=Rows(db,tx,"SELECT e.guid,e.occurred_at,e.kind,e.fee_minor,e.fee_usd_rate,l.account_guid,l.debt_delta_minor,l.line_index FROM debt_events e JOIN debt_event_lines l ON l.event_guid=e.guid WHERE e.occurred_at<=@end ORDER BY e.guid,l.line_index",end).GroupBy(r=>(string)r[0]);
        foreach(var group in events) {
            var rows=group.ToList();var first=rows[0];var kind=(string)first[2];var fee=kind is "payment" or "payment_reversal"?(long)first[3]:0L;
            var fees=DebtProfitAccounting.Allocate(fee,rows.Select(r=>Math.Abs((long)r[6])).ToList());
            for(int i=0;i<rows.Count;i++){var r=rows[i];var account=accounts[(string)r[5]];
                Add((string)account[1],new((long)r[1],group.Key,Delta:(long)r[6],Fee:fees[i],Rate:Rate(r[4])));}
        }
        var returns=Rows(db,tx,"SELECT r.guid,r.sale_guid,r.created_at,r.fee_reversal,i.sale_item_guid,i.refund_amount_uzs,i.cost_basis_uzs,i.cost_reversal_uzs FROM returns r JOIN return_items i ON i.return_guid=r.guid WHERE r.created_at<=@end ORDER BY r.guid,i.sale_item_guid",end).GroupBy(r=>(string)r[0]);
        var debtReturns=new HashSet<string>();
        foreach(var group in returns) {
            var rows=group.ToList();var first=rows[0];var sale=(string)first[1];if(!originals.ContainsKey(sale))continue;debtReturns.Add(group.Key);
            foreach(var r in rows)Add(sale,new((long)r[2],group.Key,Item:(string)r[4],Revenue:Minor(r[5]),Basis:Minor(r[6]),Restored:Minor(r[7]),Returned:true));
            Add(sale,new((long)first[2],group.Key,Fee:checked(-Minor(first[3])),Rate:byGuid.TryGetValue(group.Key,out var ret)?Rate(ret.UsdRate):null,Returned:true));
        }
        var output=new List<SaleReportItem>();var receiptProfits=new Dictionary<long,long>();
        foreach(var sale in sales.Where(s=>s.CreatedAt>=start&&s.CreatedAt<=end)) {
            var lines=SaleAccounting.Lines(sale);if(originals.ContainsKey(sale.Guid)||debtReturns.Contains(sale.Guid))foreach(var l in lines){l.Profit=0;l.ProfitUsd=0;}
            output.AddRange(lines);
        }
        foreach(var (guid,sale) in originals) {
            if(sale.CreatedAt>end)continue;
            if(!bySale.TryGetValue(guid,out var account))throw new InvalidOperationException("Nasiya foydasi: qarz hisobi topilmadi ("+guid+")");
            var templates=SaleAccounting.Lines(sale);var items=sale.Items.OrderBy(i=>i.Id).ToList();
            var index=items.Select((v,i)=>(v.Guid,i)).ToDictionary(p=>p.Guid,p=>p.i);
            var revenue=templates.Select(t=>Minor(t.TotalPrice)).ToList();var cost=templates.Select(t=>Minor(t.TotalCost)).ToList();
            var usdCost=items.Where(i=>i.CostCurrency=="USD").Sum(i=>i.CostAtSale*i.Quantity);
            var uzsCost=items.Where(i=>i.CostCurrency!="USD").Sum(i=>i.CostAtSale*i.Quantity);
            var saleRate=Rate(sale.UsdRate) ?? (usdCost>0?Rate((sale.TotalCost-uzsCost)/usdCost):null);
            Add(guid,new(sale.CreatedAt,guid,Fee:Minor(sale.TaxAmount),Rate:saleRate));
            var balance=(long)account[2];var returned=new long[items.Count];var basis=new long[items.Count];var restored=new long[items.Count];
            var feeTotals=new long[items.Count];var feeUsd=new decimal[items.Count];var unknownFee=new long[items.Count];
            var previous=new long[items.Count];var previousUsd=Enumerable.Repeat<decimal?>(0,items.Count).ToArray();
            foreach(var batch in changes[guid].GroupBy(c=>c.Time).OrderBy(g=>g.Key)) {
                foreach(var c in batch) {
                    balance=checked(balance+c.Delta);
                    if(c.Item!=""){var i=index[c.Item];returned[i]=checked(returned[i]+c.Revenue);basis[i]=checked(basis[i]+c.Basis);restored[i]=checked(restored[i]+c.Restored);}
                    var parts=DebtProfitAccounting.Allocate(c.Fee,revenue);
                    for(int i=0;i<parts.Count;i++){feeTotals[i]=checked(feeTotals[i]+parts[i]);var fx=Usd(parts[i],c.Rate);if(fx.HasValue)feeUsd[i]+=fx.Value;else unknownFee[i]=checked(unknownFee[i]+parts[i]);}
                }
                var gross=DebtProfitAccounting.Gross(Enumerable.Range(0,items.Count).Select(i=>new DebtProfitAccounting.Line(revenue[i],cost[i],returned[i],basis[i],restored[i])).ToList(),balance);
                var current=gross.Select((n,i)=>checked(n-feeTotals[i])).ToArray();
                var currentUsd=gross.Select((n,i)=>unknownFee[i]!=0?null:Usd(n,saleRate)-feeUsd[i]).ToArray();
                var returnGuid=batch.FirstOrDefault(c=>c.Returned)?.Guid;
                var target=returnGuid!=null&&byGuid.TryGetValue(returnGuid,out var ret)?ret:sale;
                for(int i=0;i<current.Length;i++) {
                    var delta=checked(current[i]-previous[i]);var fx=currentUsd[i]-previousUsd[i];var t=templates[i];
                    if(batch.Key>=start&&batch.Key<=end&&(delta!=0||!fx.HasValue||fx.Value!=0))output.Add(new SaleReportItem {
                        SaleId=target.Id,ReceiptNumber=target.ReceiptNumber,Timestamp=batch.Key,ProductId=t.ProductId,ProductName=t.ProductName,
                        Category=t.Category,WarehouseGuid=t.WarehouseGuid,WarehouseName=t.WarehouseName,UnitType=t.UnitType,
                        CostCurrency=t.CostCurrency,OriginalCost=t.OriginalCost,CostPrice=t.CostPrice,SellingPrice=t.SellingPrice,
                        Profit=delta/100.0,ProfitUsd=fx.HasValue?(double)fx.Value:null,IsReturn=returnGuid!=null,ProfitOnly=true });
                    receiptProfits[target.Id]=checked(receiptProfits.GetValueOrDefault(target.Id)+delta);
                }
                previous=current;previousUsd=currentUsd;
            }
            receiptProfits.TryAdd(sale.Id,0L);
        }
        return new(output,receiptProfits.ToDictionary(p=>p.Key,p=>p.Value/100.0));
    }
}
