using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using static PosElectro.Desktop.Debt.DebtRepository;

namespace PosElectro.Desktop.Debt;

public sealed class DebtDependencyException : InvalidOperationException
{
    public DebtDependencyException(string message) : base(message) { }
}

// Transactional debt COMPONENT bridge. No network call site, inbox, cursor or ACK.
// The future envelope adapter must validate/persist its sale/items/stock body too.
public sealed class DebtSyncStore
{
    private readonly string path, store;
    private readonly Func<bool> canSync;
    private readonly Func<string,bool> canImportActor;
    public DebtSyncStore(string databasePath,string storeGuid,Func<bool> canSync,Func<string,bool> canImportActor) {
        DebtWire.RequirePeer(storeGuid,storeGuid,new[]{DebtWire.Capability});
        path=databasePath;store=storeGuid;this.canSync=canSync;this.canImportActor=canImportActor;
    }
    private static void Need(bool ok) { if(!ok)throw new InvalidOperationException("Debt integrity conflict"); }
    private static long N(object? value) => Convert.ToInt64(value,CultureInfo.InvariantCulture);
    private static string S(object? value) => (string)(value ?? throw new InvalidOperationException("Missing debt field"));
    internal T Write<T>(Func<SqliteConnection,SqliteTransaction,T> action) {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,ForeignKeys=true,DefaultTimeout=15}.ToString());db.Open();
        using var tx=db.BeginTransaction(deferred:false);
        if(!canSync())throw new UnauthorizedAccessException("Debt sync permission required");
        Need(N(Scalar(db,tx,"PRAGMA foreign_keys"))==1 && N(Scalar(db,tx,"SELECT version FROM debt_schema WHERE id=1"))==1);
        Need((string?)Scalar(db,tx,"SELECT store_guid FROM debt_scope WHERE id=1")==store);
        Need(Scalar(db,tx,"SELECT applying FROM sync_control WHERE id=1") is long applying && applying==0);
        Need((string?)Scalar(db,tx,"SELECT current_group FROM sync_control WHERE id=1")=="");
        var result=action(db,tx);tx.Commit();return result;
    }
    internal void Authorize(string actor) { if(!canImportActor(actor))throw new UnauthorizedAccessException("Debt source actor rejected"); }
    private static string SealKey(string kind,string guid) => "debt_wire_v1:"+kind+":"+guid;
    private static void Seal(SqliteConnection db,SqliteTransaction tx,string kind,string guid,string wire) {
        var key=SealKey(kind,guid);var value=DebtWire.Fingerprint(wire)+"\n"+wire;
        var old=Scalar(db,tx,"SELECT value FROM sync_meta WHERE key=@p0",key);
        if(old==null)Exec(db,tx,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,value);else Need(S(old)==value);
    }
    private static void Journal(SqliteConnection db,SqliteTransaction tx,string id,string kind,string payload) {
        Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,@p1,@p2,@p3,@p2,-1)","debt:"+id,kind,id,payload);
    }
    internal string CustomerWire(SqliteConnection db,SqliteTransaction tx,string guid) {
        var row=Rows(db,tx,"SELECT store_guid,device_guid FROM debt_customers WHERE guid=@p0",guid).SingleOrDefault();
        if(row==null)throw new DebtDependencyException("Debt customer missing");
        Need(S(row[0])==store);
        var payload=Scalar(db,tx,"SELECT value FROM sync_meta WHERE key=@p0","debt_customer_create:"+guid);
        Need(payload!=null);var p=S(payload);
        return DebtWire.EncodeCustomer(new(guid,store,S(row[1]),p,DebtWire.Fingerprint(p)),store);
    }
    internal string EventWire(SqliteConnection db,SqliteTransaction tx,string guid) {
        var h=Rows(db,tx,"SELECT request_guid,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,schema_version,reference_guid FROM debt_events WHERE guid=@p0",guid).SingleOrDefault();
        if(h==null)throw new DebtDependencyException("Debt event missing");
        Need(N(h[14])==1 && h[15]==null && S(h[3])==store);
        var receipt=Rows(db,tx,"SELECT payload_hash,event_guid,result FROM debt_command_receipts WHERE request_guid=@p0",h[0]).SingleOrDefault();
        Need(receipt!=null && S(receipt[0])==S(h[9]) && S(receipt[1])==guid && S(receipt[2])==guid);
        var accountRow=Rows(db,tx,"SELECT guid,sale_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale,customer_guid,store_guid FROM debt_accounts WHERE opening_event_guid=@p0",guid).SingleOrDefault();
        DebtWireAccount? account=null;
        if(accountRow!=null) {
            Need(S(accountRow[6])==S(h[2]) && S(accountRow[7])==store);
            account=new(S(accountRow[0]),S(accountRow[1]),S(accountRow[2]),N(accountRow[3]),(string?)accountRow[4],S(accountRow[5]));
        }
        var lines=new List<DebtLine>();
        foreach(var row in Rows(db,tx,"SELECT line_index,account_guid,debt_delta_minor,customer_guid,store_guid FROM debt_event_lines WHERE event_guid=@p0 ORDER BY line_index",guid)) {
            Need(N(row[0])==lines.Count && S(row[3])==S(h[2]) && S(row[4])==store);
            lines.Add(new(S(row[1]),N(row[2])));
        }
        return DebtWire.EncodeEvent(new(guid,S(h[0]),S(h[1]),S(h[2]),store,S(h[4]),S(h[5]),N(h[6]),N(h[7]),S(h[8]),S(h[9]),N(h[10]),N(h[11]),N(h[12]),(string?)h[13],account,lines.AsReadOnly()),store);
    }
    public string ExportCustomer(string guid) => Write((db,tx)=>{var wire=CustomerWire(db,tx,guid);Seal(db,tx,"customer",guid,wire);return wire;});
    public string ExportEvent(string guid) => Write((db,tx)=>{var wire=EventWire(db,tx,guid);Seal(db,tx,"event",guid,wire);return wire;});
    internal void Customer(SqliteConnection db,SqliteTransaction tx,DebtWireCustomer c,string wire) {
        var p=DebtWire.CommandFields(c.Payload);Authorize(p[2]);
        Need(Scalar(db,tx,"SELECT 1 FROM debt_events WHERE request_guid=@p0",c.Guid)==null);
        if(Scalar(db,tx,"SELECT 1 FROM debt_customers WHERE guid=@p0",c.Guid)!=null)Need(CustomerWire(db,tx,c.Guid)==wire);
        else {
            // Never repair orphaned metadata or change an already-sealed creation request.
            Need(Scalar(db,tx,"SELECT 1 FROM sync_meta WHERE key=@p0 OR key=@p1","debt_customer_create:"+c.Guid,SealKey("customer",c.Guid))==null);
            Exec(db,tx,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",c.Guid,store,p[4],p[5],p[6],long.Parse(p[7],CultureInfo.InvariantCulture),c.DeviceGuid);
            Exec(db,tx,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_customer_create:"+c.Guid,c.Payload);
            Journal(db,tx,c.Guid,"debt_customer",c.Payload);
        }
        Seal(db,tx,"customer",c.Guid,wire);
    }
    internal void Event(SqliteConnection db,SqliteTransaction tx,DebtWireEvent e,string wire,Action<SqliteConnection,SqliteTransaction,DebtWireEvent>? writeSale) {
        Authorize(e.ActorGuid);
        Need(Scalar(db,tx,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:"+e.RequestGuid)==null);
        Need(Scalar(db,tx,"SELECT 1 FROM debt_customers WHERE guid=@p0",e.RequestGuid)==null);
        if(Scalar(db,tx,"SELECT 1 FROM debt_events WHERE guid=@p0",e.Guid)!=null) {
            Need(EventWire(db,tx,e.Guid)==wire);Seal(db,tx,"event",e.Guid,wire);return;
        }
        Need(Scalar(db,tx,"SELECT 1 FROM sync_meta WHERE key=@p0",SealKey("event",e.Guid))==null);
        var customerStore=Scalar(db,tx,"SELECT store_guid FROM debt_customers WHERE guid=@p0",e.CustomerGuid);
        if(customerStore==null)throw new DebtDependencyException("Debt customer missing");Need(S(customerStore)==store);
        // Archived contacts do not invalidate a previously accepted offline collection.
        foreach(var line in e.Lines) {
            var owner=Rows(db,tx,"SELECT customer_guid,store_guid FROM debt_accounts WHERE guid=@p0",line.AccountGuid).SingleOrDefault();
            if(owner==null)throw new DebtDependencyException("Debt account missing");
            Need(S(owner[0])==e.CustomerGuid && S(owner[1])==store);
        }
        if(e.Account!=null) {
            Need(Scalar(db,tx,"SELECT 1 FROM sales WHERE guid=@p0",e.Account.SaleGuid)==null);
            if(writeSale==null)throw new DebtDependencyException("Atomic sale adapter missing");
            writeSale(db,tx,e); // Trusted adapter: sale/items/stock ONLY on supplied transaction.
            var sale=Rows(db,tx,"SELECT total_amount,cash_amount,card_amount,created_at FROM sales WHERE guid=@p0",e.Account.SaleGuid).Single();
            var p=DebtWire.CommandFields(e.Payload);
            Need(StoredMinor(sale[0])==long.Parse(p[7],CultureInfo.InvariantCulture) && StoredMinor(sale[1])==long.Parse(p[8],CultureInfo.InvariantCulture) && StoredMinor(sale[2])==long.Parse(p[9],CultureInfo.InvariantCulture) && N(sale[3])==e.OccurredAt);
        }
        Exec(db,tx,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p1,1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14)",e.Guid,e.RequestGuid,e.Kind,e.CustomerGuid,store,e.ActorGuid,e.DeviceGuid,e.DeviceSequence,e.OccurredAt,e.Payload,e.PayloadHash,e.CashMinor,e.CardMinor,e.FeeMinor,e.FeeUsdRate);
        if(e.Account is { } a)Exec(db,tx,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",a.Guid,a.SaleGuid,e.CustomerGuid,store,a.OpeningEventGuid,a.OriginalDebtMinor,a.DueDate,a.CustomerNameAtSale);
        for(int i=0;i<e.Lines.Count;i++)Exec(db,tx,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",e.Guid,i,e.Lines[i].AccountGuid,e.CustomerGuid,store,e.Lines[i].DeltaMinor);
        Exec(db,tx,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p2,@p2)",e.RequestGuid,e.PayloadHash,e.Guid);
        Journal(db,tx,e.Guid,"debt_event",e.Payload);Seal(db,tx,"event",e.Guid,wire);
    }
    public void Apply(IReadOnlyList<string> customerPackets,IReadOnlyList<string> eventPackets,Action<SqliteConnection,SqliteTransaction,DebtWireEvent>? writeSale=null) {
        Need((long)customerPackets.Count+eventPackets.Count<=500);
        var customers=customerPackets.ToArray();var events=eventPackets.ToArray();
        Need(customers.Concat(events).Sum(s=>(long)s.Length)<=8*1024*1024);
        var cs=customers.Select(w=>(Wire:w,Value:DebtWire.DecodeCustomer(w,store))).ToArray();
        var es=events.Select(w=>(Wire:w,Value:DebtWire.DecodeEvent(w,store))).OrderBy(x=>x.Value.Kind=="sale_open"?0:1).ToArray();
        Write((db,tx)=> {
            Exec(db,tx,"UPDATE sync_control SET applying=1 WHERE id=1");
            foreach(var c in cs)Customer(db,tx,c.Value,c.Wire);
            foreach(var e in es)Event(db,tx,e.Value,e.Wire,writeSale);
            Exec(db,tx,"UPDATE sync_control SET applying=0 WHERE id=1");return true;
        });
    }
}
