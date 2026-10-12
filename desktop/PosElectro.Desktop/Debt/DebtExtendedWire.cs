using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

namespace PosElectro.Desktop.Debt;

// V2 adds a reference only for extended events; V1 canonical bytes stay unchanged.
public static class DebtExtendedWire
{
    public const string Capability="debtLedgerExtendedV2";
    public static bool Supports(string kind)=>kind is "payment_reversal" or "credit_refund" or "credit_transfer" or "return_offset" or "return_reversal";
    private static void Need(bool ok) { if(!ok)throw new ArgumentException("Invalid extended debt event"); }
    public static void Validate(DebtWireEvent e,string store) {
        DebtWire.RequirePeer(store,e.StoreGuid,new[]{DebtWire.Capability});
        foreach(var id in new[]{e.Guid,e.CustomerGuid,e.DeviceGuid})DebtWire.Id(id);
        if(!(e.Kind.StartsWith("return_",StringComparison.Ordinal) && e.ActorGuid=="desktop-local"))DebtWire.Id(e.ActorGuid);
        Need(e.DeviceSequence>0 && e.OccurredAt>=0 && e.Account==null && e.Lines.Count>0 && e.Lines.Count<=DebtWire.MaxLines);
        Need(e.Payload.Length<=16384 && e.PayloadHash==DebtWire.Fingerprint(e.Payload));
        var p=DebtWire.CommandFields(e.Payload);
        Need(p.Length>=8 && p[0]==e.Kind && p[1]==store && p[2]==e.ActorGuid && p[3]==e.RequestGuid);
        if(e.Kind.StartsWith("return_",StringComparison.Ordinal)) {
            Need(e.RequestGuid.StartsWith(e.Kind+":",StringComparison.Ordinal));DebtWire.Id(e.RequestGuid[(e.Kind.Length+1)..]);
        } else { DebtWire.Id(e.RequestGuid);Need(e.Guid==e.RequestGuid); }
        if(e.ReferenceGuid!=null)DebtWire.Id(e.ReferenceGuid);
        var ids=new HashSet<string>();
        foreach(var l in e.Lines) { DebtWire.Id(l.AccountGuid);Need(ids.Add(l.AccountGuid) && l.DeltaMinor!=0 && l.DeltaMinor!=long.MinValue); }
        Need(new[]{e.CashMinor,e.CardMinor,e.FeeMinor}.All(n=>n!=long.MinValue));
        long N(int i)=>DebtWire.Number(p[i]);
        void Line(int i,string account,long delta)=>Need(e.Lines[i]==new DebtLine(account,delta));
        switch(e.Kind) {
            case "payment_reversal":
                Need(p.Length==8 && e.ReferenceGuid==p[4]);DebtWire.Id(p[4]);DebtWire.Text(p[7],1000,true);
                Need(N(5)>=0 && e.FeeMinor == -N(5) && N(6)==e.OccurredAt && e.CashMinor<=0 && e.CardMinor<=0 && (BigInteger)e.CashMinor+e.CardMinor<0);
                Need(e.FeeMinor>=e.CardMinor && e.Lines.All(l=>l.DeltaMinor>0));
                Need(e.Lines.Aggregate(BigInteger.Zero,(a,l)=>a+l.DeltaMinor)==-((BigInteger)e.CashMinor+e.CardMinor));break;
            case "credit_refund":
                Need(p.Length==10 && p[4]==e.CustomerGuid && e.ReferenceGuid==null && e.FeeMinor==0 && e.FeeUsdRate==null && e.Lines.Count==1);
                DebtWire.Id(p[5]);DebtWire.Text(p[9],1000,true);
                var total=checked(N(6)+N(7));Need(N(6)>=0 && N(7)>=0 && total>0 && N(8)==e.OccurredAt);
                Need(e.CashMinor == -N(6) && e.CardMinor == -N(7));Line(0,p[5],total);break;
            case "credit_transfer":
                Need(p.Length==10 && p[4]==e.CustomerGuid && e.ReferenceGuid==null && e.CashMinor==0 && e.CardMinor==0 && e.FeeMinor==0 && e.FeeUsdRate==null && e.Lines.Count==2);
                DebtWire.Id(p[5]);DebtWire.Id(p[6]);DebtWire.Text(p[9],1000,true);
                Need(p[5]!=p[6] && N(7)>0 && N(8)==e.OccurredAt);Line(0,p[5],N(7));Line(1,p[6],-N(7));break;
            case "return_offset":
                Need(p.Length==8 && p[4]==e.CustomerGuid && e.ReferenceGuid!=null && e.Lines.Count==1 && e.FeeUsdRate==null);
                DebtWire.Id(p[5]);Need(N(6)>0 && N(7)==e.OccurredAt && e.CashMinor<=0 && e.CardMinor<=0 && e.FeeMinor<=0);Line(0,p[5],-N(6));break;
            case "return_reversal":
                Need(p.Length==8 && p[4]==e.CustomerGuid && e.ReferenceGuid==p[5] && e.Lines.Count==1 && e.FeeUsdRate==null);
                DebtWire.Id(p[5]);DebtWire.Text(p[7],1000,true);
                Need(N(6)==e.OccurredAt && e.CashMinor>=0 && e.CardMinor>=0 && e.FeeMinor>=0 && e.Lines[0].DeltaMinor>0);break;
            default: throw new ArgumentException("Unsupported extended event");
        }
    }
}
