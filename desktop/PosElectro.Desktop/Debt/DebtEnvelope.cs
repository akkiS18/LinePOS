using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace PosElectro.Desktop.Debt;

// Frozen historical fields; prices are UZS, cost retains its original currency.
// Stock is a per-item DELTA, never a sender's current balance.
public sealed record DebtSaleItem(string Guid,string ProductGuid,string ProductName,string Category,
    string Unit,string WarehouseGuid,string WarehouseName,string Quantity,string Price,string Cost,
    string CostCurrency,string StockOperationGuid,string StockDelta);
public sealed record DebtSaleSnapshot(string Guid,long OccurredAt,long TotalMinor,long CostMinor,
    long CashMinor,long CardMinor,long FeeMinor,string FeeRate,string UsdRate,string PaymentType,
    IReadOnlyList<DebtSaleItem> Items);
public sealed record DebtEnvelopePacket(string Guid,string StoreGuid,string CustomerWire,string EventWire,string SaleWire);

// PURE protocol only: not a DB receiver, authorization policy, inbox or network ACK.
public static class DebtEnvelope
{
    public const int MaxEnvelopeChars=6*1024*1024;
    public const int MaxItems=1000;
    private static readonly BigInteger Scale=BigInteger.Pow(10,8);
    private static void Need(bool ok) { if(!ok)throw new ArgumentException("Invalid debt envelope"); }
    private static string N(long n)=>n.ToString(CultureInfo.InvariantCulture);
    // Unique decimal spelling, <=12 integral / 8 fractional digits, no exponent or -0.
    private static BigInteger DecimalUnits(string s) {
        Need(Regex.IsMatch(s,"\\A(?:0|[1-9][0-9]{0,11})(?:\\.[0-9]{0,7}[1-9])?\\z"));
        var p=s.Split('.');return BigInteger.Parse(p[0],CultureInfo.InvariantCulture)*Scale+
            (p.Length==1?BigInteger.Zero:BigInteger.Parse(p[1].PadRight(8,'0'),CultureInfo.InvariantCulture));
    }
    private static long Rounded(BigInteger value,BigInteger divisor) {
        // All inputs nonnegative; aggregate first, then half-away-from-zero to tiyin.
        var n=(value+divisor/2)/divisor;Need(n>=0 && n<=long.MaxValue);return (long)n;
    }
    private static void Validate(DebtSaleSnapshot s) {
        DebtWire.Id(s.Guid);Need(s.OccurredAt>=0 && s.TotalMinor>0 && s.CostMinor>=0 && s.CashMinor>=0 && s.CardMinor>=0);
        Need(s.PaymentType=="DEBT" && s.FeeMinor>=0 && s.FeeMinor<=s.CardMinor);
        Need((BigInteger)s.CashMinor+s.CardMinor<s.TotalMinor && s.Items.Count>0 && s.Items.Count<=MaxItems);
        var fx=DecimalUnits(s.UsdRate);var feeRate=DecimalUnits(s.FeeRate);Need(feeRate<=100*Scale);
        Need(s.FeeMinor==Rounded((BigInteger)s.CardMinor*feeRate,100*Scale));
        var ids=new HashSet<string>(StringComparer.Ordinal);var ops=new HashSet<string>(StringComparer.Ordinal);
        BigInteger revenue=0,cost=0;
        foreach(var i in s.Items) {
            DebtWire.Id(i.Guid);DebtWire.Id(i.ProductGuid);DebtWire.Id(i.WarehouseGuid);DebtWire.Id(i.StockOperationGuid);
            Need(ids.Add(i.Guid) && ops.Add(i.StockOperationGuid));
            DebtWire.Text(i.ProductName,256,true);DebtWire.Text(i.Category,256);DebtWire.Text(i.Unit,32,true);DebtWire.Text(i.WarehouseName,256,true);
            var q=DecimalUnits(i.Quantity);var price=DecimalUnits(i.Price);var unitCost=DecimalUnits(i.Cost);
            Need(q>0 && i.StockDelta=="-"+i.Quantity && (i.CostCurrency=="UZS" || i.CostCurrency=="USD"));
            Need(i.CostCurrency!="USD" || fx>0);
            revenue+=q*price;cost+=q*unitCost*(i.CostCurrency=="USD"?fx:Scale);
        }
        Need(s.TotalMinor==Rounded(revenue*100,Scale*Scale));
        Need(s.CostMinor==Rounded(cost*100,Scale*Scale*Scale));
    }
    public static string EncodeSale(DebtSaleSnapshot value) {
        Need(value.Items.Count<=MaxItems);var s=value with{Items=Array.AsReadOnly(value.Items.ToArray())};Validate(s);
        var f=new List<string>{s.Guid,N(s.OccurredAt),N(s.TotalMinor),N(s.CostMinor),N(s.CashMinor),N(s.CardMinor),N(s.FeeMinor),s.FeeRate,s.UsdRate,s.PaymentType,N(s.Items.Count)};
        foreach(var i in s.Items)f.AddRange(new[]{i.Guid,i.ProductGuid,i.ProductName,i.Category,i.Unit,i.WarehouseGuid,i.WarehouseName,i.Quantity,i.Price,i.Cost,i.CostCurrency,i.StockOperationGuid,i.StockDelta});
        return DebtWire.Pack("debt-sale-v1",f);
    }
    public static DebtSaleSnapshot DecodeSale(string wire) {
        var p=DebtWire.Unpack(wire,"debt-sale-v1",11+13*MaxItems);Need(p.Length>=11);
        var count=DebtWire.Number(p[10]);Need(count>0 && count<=MaxItems && p.Length==11+13*count);
        var items=new List<DebtSaleItem>();
        for(int i=0;i<count;i++){var x=11+13*i;items.Add(new(p[x],p[x+1],p[x+2],p[x+3],p[x+4],p[x+5],p[x+6],p[x+7],p[x+8],p[x+9],p[x+10],p[x+11],p[x+12]));}
        var sale=new DebtSaleSnapshot(p[0],DebtWire.Number(p[1]),DebtWire.Number(p[2]),DebtWire.Number(p[3]),DebtWire.Number(p[4]),DebtWire.Number(p[5]),DebtWire.Number(p[6]),p[7],p[8],p[9],items.AsReadOnly());
        Validate(sale);return sale;
    }
    private static void Validate(DebtEnvelopePacket p,string expectedStore) {
        DebtWire.Id(p.Guid);DebtWire.RequirePeer(expectedStore,p.StoreGuid,new[]{DebtWire.Capability});
        DebtWireCustomer? c=p.CustomerWire.Length==0?null:DebtWire.DecodeCustomer(p.CustomerWire,expectedStore);
        if(p.EventWire.Length==0) {Need(c!=null && p.Guid==c.Guid && p.SaleWire.Length==0);return;}
        var e=DebtWire.DecodeEvent(p.EventWire,expectedStore);Need(p.Guid==e.Guid && (c==null || c.Guid==e.CustomerGuid));
        Need(c==null || c.Guid!=e.RequestGuid);
        if(e.Kind=="payment") {Need(p.SaleWire.Length==0);return;}
        var sale=DecodeSale(p.SaleWire);var command=DebtWire.CommandFields(e.Payload);
        Need(sale.Guid==e.Account!.SaleGuid && sale.OccurredAt==e.OccurredAt);
        Need(sale.TotalMinor==DebtWire.Number(command[7]) && sale.CashMinor==DebtWire.Number(command[8]) && sale.CardMinor==DebtWire.Number(command[9]));
        Need(DebtWire.Fingerprint(p.SaleWire)==command[6]);
    }
    public static string Encode(DebtEnvelopePacket p,string expectedStore) {
        Validate(p,expectedStore);return DebtWire.Pack("debt-envelope-v1",new[]{p.Guid,p.StoreGuid,p.CustomerWire,p.EventWire,p.SaleWire},MaxEnvelopeChars);
    }
    public static DebtEnvelopePacket Decode(string wire,string expectedStore) {
        var f=DebtWire.Unpack(wire,"debt-envelope-v1",5,MaxEnvelopeChars);Need(f.Length==5);
        var p=new DebtEnvelopePacket(f[0],f[1],f[2],f[3],f[4]);Validate(p,expectedStore);return p;
    }
}
