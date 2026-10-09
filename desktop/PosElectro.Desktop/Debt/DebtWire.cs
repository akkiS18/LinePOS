using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PosElectro.Desktop.Debt;

public sealed record DebtWireAccount(string Guid, string SaleGuid, string OpeningEventGuid,
    long OriginalDebtMinor, string? DueDate, string CustomerNameAtSale);
public sealed record DebtWireCustomer(string Guid, string StoreGuid, string DeviceGuid, string Payload, string PayloadHash);
public sealed record DebtWireEvent(string Guid, string RequestGuid, string Kind, string CustomerGuid,
    string StoreGuid, string ActorGuid, string DeviceGuid, long DeviceSequence, long OccurredAt,
    string Payload, string PayloadHash, long CashMinor, long CardMinor, long FeeMinor,
    string? FeeUsdRate, DebtWireAccount? Account, IReadOnlyList<DebtLine> Lines);

// Pure, bounded codec for a debt component. NOT an atomic sale/stock envelope or an
// authorization/database receiver. Never advertise capability until transport is integrated.
public static class DebtWire
{
    public const string Capability = "debtLedgerV1";
    public const int MaxPacketChars = 2 * 1024 * 1024;
    public const int MaxLines = 10000;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private static void Need(bool ok) { if (!ok) throw new ArgumentException("Invalid debt wire component"); }
    internal static void Id(string value) => Need(System.Guid.TryParseExact(value,"D",out var g) && g!=System.Guid.Empty && g.ToString("D")==value);
    private static string Num(long n) => n.ToString(CultureInfo.InvariantCulture);
    internal static long Number(string s) {
        Need(Regex.IsMatch(s,"\\A(?:0|-?[1-9][0-9]{0,18})\\z"));
        Need(long.TryParse(s,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n) && n!=long.MinValue);return n;
    }
    internal static void Text(string s,int max,bool required=false) => Need(s.Length<=max && (!required || !string.IsNullOrWhiteSpace(s)) && !s.Any(c=>c<' '));
    private static void Date(string? s) {
        if(s!=null)Need(DateOnly.TryParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d) && d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)==s);
    }
    private static void Rate(string? s) {
        if(s!=null)Need(Regex.IsMatch(s,"\\A[0-9]{1,12}(?:\\.[0-9]{1,8})?\\z") && decimal.Parse(s,CultureInfo.InvariantCulture)>0);
    }
    private static string? Optional(string s) => s.Length==0?null:s;
    public static string Fingerprint(string canonicalWire) => Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(canonicalWire))).ToLowerInvariant();
    internal static string Pack(string tag,IEnumerable<string> fields,int maxChars=MaxPacketChars) {
        var result="[\""+tag+"\","+string.Join(",",fields.Select(f=>"\""+Convert.ToBase64String(Utf8.GetBytes(f))+"\""))+"]";
        Need(result.Length<=maxChars);return result;
    }
    internal static string[] Unpack(string wire,string tag,int maxFields,int maxChars=MaxPacketChars) {
        Need(wire.Length<=maxChars);
        var prefix="[\""+tag+"\",";Need(wire.StartsWith(prefix,StringComparison.Ordinal) && wire.EndsWith("]",StringComparison.Ordinal));
        var parts=wire.Substring(prefix.Length,wire.Length-prefix.Length-1).Split(new[]{','},maxFields+1,StringSplitOptions.None);Need(parts.Length<=maxFields);
        var fields=new string[parts.Length];
        for(int i=0;i<parts.Length;i++) {
            var token=parts[i];Need(token.Length>=2 && token[0]=='"' && token[^1]=='"');var b64=token[1..^1];
            byte[] bytes;try { bytes=Convert.FromBase64String(b64); } catch(FormatException e) { throw new ArgumentException("Invalid base64",e); }
            Need(Convert.ToBase64String(bytes)==b64);fields[i]=Utf8.GetString(bytes);
        }
        Need(Pack(tag,fields,maxChars)==wire);return fields;
    }
    internal static string[] CommandFields(string payload) => Unpack(payload,"debt-command-v1",12);
    public static void RequirePeer(string expectedStore,string peerStore,IEnumerable<string> peerCapabilities) {
        Id(expectedStore);Id(peerStore);Need(expectedStore==peerStore && peerCapabilities.Contains(Capability,StringComparer.Ordinal));
    }
    private static void Validate(DebtWireCustomer c,string expectedStore) {
        Id(expectedStore);Id(c.Guid);Id(c.StoreGuid);Id(c.DeviceGuid);Need(c.StoreGuid==expectedStore && c.Payload.Length<=16384);
        var p=Unpack(c.Payload,"debt-command-v1",8);Need(p.Length==8 && p[0]=="customer" && p[1]==c.StoreGuid && p[3]==c.Guid);
        Id(p[2]);Text(p[4],256,true);Text(p[5],64);Text(p[6],2048);Need(Number(p[7])>=0);
        Need(c.PayloadHash==Fingerprint(c.Payload));
    }
    public static string EncodeCustomer(DebtWireCustomer c,string expectedStore) {
        Validate(c,expectedStore);return Pack("debt-customer-v1",new[]{c.Guid,c.StoreGuid,c.DeviceGuid,c.Payload,c.PayloadHash});
    }
    public static DebtWireCustomer DecodeCustomer(string wire,string expectedStore) {
        var p=Unpack(wire,"debt-customer-v1",5);Need(p.Length==5);
        var c=new DebtWireCustomer(p[0],p[1],p[2],p[3],p[4]);Validate(c,expectedStore);return c;
    }
    private static void Validate(DebtWireEvent e,string expectedStore) {
        Id(expectedStore);Id(e.Guid);Id(e.RequestGuid);Id(e.CustomerGuid);Id(e.StoreGuid);Id(e.ActorGuid);Id(e.DeviceGuid);
        Need(e.StoreGuid==expectedStore && e.Guid==e.RequestGuid && e.DeviceSequence>0 && e.OccurredAt>=0);
        Need(e.Payload.Length<=16384 && e.PayloadHash==Fingerprint(e.Payload) && e.Lines.Count<=MaxLines);
        var p=Unpack(e.Payload,"debt-command-v1",12);
        Need(p.Length>=5 && p[0]==e.Kind && p[1]==e.StoreGuid && p[2]==e.ActorGuid && p[3]==e.RequestGuid && p[4]==e.CustomerGuid);
        if(e.Kind=="sale_open") {
            Need(p.Length==12 && e.Account!=null && e.Lines.Count==0 && e.CashMinor==0 && e.CardMinor==0 && e.FeeMinor==0 && e.FeeUsdRate==null);
            var a=e.Account!;Id(a.Guid);Id(a.SaleGuid);Id(a.OpeningEventGuid);Date(a.DueDate);Text(a.CustomerNameAtSale,256,true);
            Need(a.Guid==a.SaleGuid && a.SaleGuid==p[5] && a.OpeningEventGuid==e.Guid && Regex.IsMatch(p[6],"\\A[0-9a-f]{64}\\z"));
            Need(a.OriginalDebtMinor>0 && a.OriginalDebtMinor==DebtAccounting.NewDebt(Number(p[7]),Number(p[8]),Number(p[9])));
            Need(Number(p[10])==e.OccurredAt && Optional(p[11])==a.DueDate);
        } else if(e.Kind=="payment") {
            Need(p.Length==11 && e.Account==null && e.Lines.Count>0);
            Need(e.CashMinor>=0 && e.CardMinor>=0 && e.FeeMinor>=0 && e.FeeMinor<=e.CardMinor);Rate(e.FeeUsdRate);
            BigInteger total=(BigInteger)e.CashMinor+e.CardMinor;Need(total>0 && total<=long.MaxValue);
            Need(Number(p[5])==e.CashMinor && Number(p[6])==e.CardMinor && Number(p[7])==e.FeeMinor && Number(p[8])==e.OccurredAt && Optional(p[10])==e.FeeUsdRate);
            var target=Optional(p[9]);if(target!=null)Id(target);
            var seen=new HashSet<string>(StringComparer.Ordinal);BigInteger delta=0;
            foreach(var line in e.Lines) {
                Id(line.AccountGuid);Need(seen.Add(line.AccountGuid) && line.DeltaMinor<0 && line.DeltaMinor!=long.MinValue);
                Need(target==null || target==line.AccountGuid);delta+=line.DeltaMinor;
            }
            Need(delta==-total);
        } else throw new ArgumentException("Unsupported debt event kind");
    }
    public static string EncodeEvent(DebtWireEvent value,string expectedStore) {
        // Freeze caller-owned collections before validation/serialization.
        Need(value.Lines.Count<=MaxLines);var e=value with {Lines=Array.AsReadOnly(value.Lines.ToArray())};Validate(e,expectedStore);
        var a=e.Account;
        var fields=new List<string>{e.Guid,e.RequestGuid,e.Kind,e.CustomerGuid,e.StoreGuid,e.ActorGuid,e.DeviceGuid,Num(e.DeviceSequence),Num(e.OccurredAt),
            e.Payload,e.PayloadHash,Num(e.CashMinor),Num(e.CardMinor),Num(e.FeeMinor),e.FeeUsdRate??"",
            a?.Guid??"",a?.SaleGuid??"",a?.OpeningEventGuid??"",a==null?"":Num(a.OriginalDebtMinor),a?.DueDate??"",a?.CustomerNameAtSale??"",Num(e.Lines.Count)};
        foreach(var line in e.Lines){fields.Add(line.AccountGuid);fields.Add(Num(line.DeltaMinor));}
        return Pack("debt-event-v1",fields);
    }
    public static DebtWireEvent DecodeEvent(string wire,string expectedStore) {
        var p=Unpack(wire,"debt-event-v1",22+2*MaxLines);Need(p.Length>=22);
        long count=Number(p[21]);Need(count>=0 && count<=MaxLines && p.Length==22+2*count);
        DebtWireAccount? account=null;
        if(p[15].Length>0)account=new(p[15],p[16],p[17],Number(p[18]),Optional(p[19]),p[20]);
        else Need(p.Skip(16).Take(5).All(s=>s.Length==0));
        var lines=new List<DebtLine>();for(int i=0;i<count;i++)lines.Add(new(p[22+2*i],Number(p[23+2*i])));
        var e=new DebtWireEvent(p[0],p[1],p[2],p[3],p[4],p[5],p[6],Number(p[7]),Number(p[8]),p[9],p[10],Number(p[11]),Number(p[12]),Number(p[13]),Optional(p[14]),account,lines.AsReadOnly());
        Validate(e,expectedStore);return e;
    }
}
