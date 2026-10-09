using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PosElectro.Desktop.Debt;

public sealed record DebtCustomerDraft(string Guid, string Name, string Phone, string Note, long CreatedAt);
public sealed record DebtSaleCommand(string RequestGuid, string CustomerGuid, string SaleGuid, string SaleFingerprint,
    long TotalMinor, long CashMinor, long CardMinor, long OccurredAt, string? DueDate = null);
public sealed record DebtPaymentCommand(string RequestGuid, string CustomerGuid, long CashMinor, long CardMinor,
    long FeeMinor, long OccurredAt, string? TargetAccountGuid = null, string? FeeUsdRate = null);

// Local command boundary. The host supplies an authenticated actor/store and permission check.
// UI/sync adapters are deliberately not wired yet. Remote events must NOT call these local
// allocation methods: a future receiver must apply the sender's already-frozen event lines.
public sealed class DebtRepository
{
    private readonly string path, store, actor;
    private readonly Func<bool> canWrite;
    // A new writer epoch per repository instance is intentional. Copies/restores/restarts never
    // reuse a device sequence; existing events retain their original epoch and retry receipt.
    private readonly string device = Guid.NewGuid().ToString("D");
    public DebtRepository(string databasePath, string storeGuid, string actorGuid, Func<bool> canWrite)
    { Id(storeGuid); Id(actorGuid); path=databasePath; store=storeGuid; actor=actorGuid; this.canWrite=canWrite ?? throw new ArgumentNullException(nameof(canWrite)); }
    private static void Need(bool ok) { if (!ok) throw new ArgumentException("Invalid debt command or changed retry"); }
    private static void Id(string value) => Need(Guid.TryParseExact(value,"D",out var g) && g!=Guid.Empty && g.ToString("D")==value);
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static void TextField(string value, int limit, bool required=false) => Need(value!=null && value.Length<=limit && (!required || !string.IsNullOrWhiteSpace(value)) && !value.Any(c=>c<' '));
    private static readonly Encoding Utf8 = new UTF8Encoding(false,true);
    public static string Canonical(params string[] fields) => "[\"debt-command-v1\","+string.Join(",",fields.Select(f=>"\""+Convert.ToBase64String(Utf8.GetBytes(f))+"\""))+"]";
    private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(payload))).ToLowerInvariant();
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction t, string sql, params object?[] args) {
        var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText=sql;
        for(int i=0;i<args.Length;i++)cmd.Parameters.AddWithValue("@p"+i,args[i]??DBNull.Value);return cmd;
    }
    internal static void Exec(SqliteConnection c,SqliteTransaction t,string sql,params object?[] args) { using var cmd=Command(c,t,sql,args);cmd.ExecuteNonQuery(); }
    internal static object? Scalar(SqliteConnection c,SqliteTransaction t,string sql,params object?[] args) { using var cmd=Command(c,t,sql,args);var v=cmd.ExecuteScalar();return v==DBNull.Value?null:v; }
    internal static List<object?[]> Rows(SqliteConnection c,SqliteTransaction t,string sql,params object?[] args) {
        using var cmd=Command(c,t,sql,args);using var r=cmd.ExecuteReader();var rows=new List<object?[]>();
        while(r.Read()){var row=new object[r.FieldCount];r.GetValues(row);rows.Add(row.Select(v=>v==DBNull.Value?null:v).ToArray());}return rows;
    }
    private T Write<T>(Func<SqliteConnection,SqliteTransaction,T> action) {
        using var c=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path, ForeignKeys=true, DefaultTimeout=15 }.ToString());c.Open();
        using var t=c.BeginTransaction(deferred:false);
        if(!canWrite())throw new UnauthorizedAccessException("Debt write permission required");
        Need(Convert.ToInt64(Scalar(c,t,"PRAGMA foreign_keys"))==1);
        Need(Convert.ToInt64(Scalar(c,t,"SELECT version FROM debt_schema WHERE id=1"))==1);
        Need(Scalar(c,t,"SELECT applying FROM sync_control WHERE id=1") is long applying && applying==0);
        Need((string?)Scalar(c,t,"SELECT current_group FROM sync_control WHERE id=1")=="");
        var result=action(c,t);t.Commit();return result;
    }
    private void Scope(SqliteConnection c,SqliteTransaction t) => Need((string?)Scalar(c,t,"SELECT store_guid FROM debt_scope WHERE id=1")==store);
    public void BindStore() => Write((c,t)=> {
        var existing=(string?)Scalar(c,t,"SELECT store_guid FROM debt_scope WHERE id=1");
        if(existing==null)Exec(c,t,"INSERT INTO debt_scope(id,store_guid) VALUES(1,@p0)",store);else Need(existing==store);
        return true;
    });
    private string Customer(SqliteConnection c,SqliteTransaction t,string id,bool active=true) {
        var row=Rows(c,t,"SELECT name,archived FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",id,store).SingleOrDefault();
        Need(row!=null && (!active || Convert.ToInt64(row[1])==0));return (string)row![0]!;
    }
    private void Outbox(SqliteConnection c,SqliteTransaction t,string request,string kind,string entity,string payload) {
        // -1 is held, NOT acknowledged. Existing transport reads only acked=0. Stage 3 must
        // negotiate debtLedgerV1 before releasing a whole group (including its sale/stock).
        Exec(c,t,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,@p1,@p2,@p3,@p4,-1)","debt:"+request,kind,entity,payload,request);
    }
    public string CreateCustomer(DebtCustomerDraft draft) {
        Id(draft.Guid);TextField(draft.Name,256,true);TextField(draft.Phone,64);TextField(draft.Note,2048);Need(draft.CreatedAt>=0);
        var payload=Canonical("customer",store,actor,draft.Guid,draft.Name,draft.Phone,draft.Note,Number(draft.CreatedAt));
        return Write((c,t)=> {
            Scope(c,t);var key="debt_customer_create:"+draft.Guid;
            var previous=(string?)Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",key);
            if(previous!=null){Need(previous==payload);Customer(c,t,draft.Guid,false);return draft.Guid;}
            Need(Scalar(c,t,"SELECT 1 FROM debt_events WHERE request_guid=@p0",draft.Guid)==null);
            Exec(c,t,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",draft.Guid,store,draft.Name,draft.Phone,draft.Note,draft.CreatedAt,device);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,payload);
            Outbox(c,t,draft.Guid,"debt_customer",draft.Guid,payload);return draft.Guid;
        });
    }
    private string? Replay(SqliteConnection c,SqliteTransaction t,string request,string payload) {
        Need(Scalar(c,t,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:"+request)==null);
        var row=Rows(c,t,"SELECT r.payload_hash,r.result,e.payload FROM debt_command_receipts r JOIN debt_events e ON e.guid=r.event_guid WHERE r.request_guid=@p0",request).SingleOrDefault();
        if(row==null)return null;
        Need((string)row[0]! == Hash(payload) && (string)row[2]! == payload && (string)row[1]! == request);return (string)row[1]!;
    }
    private void Event(SqliteConnection c,SqliteTransaction t,string request,string customer,string kind,long at,string payload,long cash,long card,long fee,string? rate) {
        var seq=checked(Convert.ToInt64(Scalar(c,t,"SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0",device))+1);
        Exec(c,t,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p0,1,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",request,kind,customer,store,actor,device,seq,at,payload,Hash(payload),cash,card,fee,rate);
    }
    private void Finish(SqliteConnection c,SqliteTransaction t,string request,string payload) {
        Exec(c,t,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p0,@p0)",request,Hash(payload));
        Outbox(c,t,request,"debt_event",request,payload);
    }
    private List<DebtAccount> Accounts(SqliteConnection c,SqliteTransaction t,string customer) {
        var accounts=new List<DebtAccount>();
        foreach(var row in Rows(c,t,"SELECT a.guid,a.sale_guid,a.original_debt_minor,s.created_at FROM debt_accounts a JOIN sales s ON s.guid=a.sale_guid WHERE a.customer_guid=@p0 AND a.store_guid=@p1",customer,store)) {
            var guid=(string)row[0]!;
            var deltas=Rows(c,t,"SELECT debt_delta_minor FROM debt_event_lines WHERE account_guid=@p0",guid).Select(x=>Convert.ToInt64(x[0]));
            accounts.Add(new(guid,(string)row[1]!,customer,Convert.ToInt64(row[3]),DebtAccounting.Balance(Convert.ToInt64(row[2]),deltas)));
        }return accounts;
    }
    public IReadOnlyList<DebtAccount> ReadAccounts(string customerGuid) { Id(customerGuid);return Write((c,t)=>{Scope(c,t);Customer(c,t,customerGuid,false);return Accounts(c,t,customerGuid).AsReadOnly();}); }
    internal static long StoredMinor(object? value) {
        var d=Convert.ToDouble(value,CultureInfo.InvariantCulture);Need(double.IsFinite(d) && d>=0);
        return checked((long)(decimal.Round(decimal.Parse(d.ToString("R",CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture),2,MidpointRounding.AwayFromZero)*100));
    }
    public string OpenSale(DebtSaleCommand q,Action<SqliteConnection,SqliteTransaction> writeSale) {
        Id(q.RequestGuid);Id(q.CustomerGuid);Id(q.SaleGuid);Need(q.OccurredAt>=0);
        Need(Regex.IsMatch(q.SaleFingerprint,"\\A[0-9a-f]{64}\\z"));
        if(q.DueDate!=null)Need(DateOnly.TryParseExact(q.DueDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) && date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)==q.DueDate);
        var debt=DebtAccounting.NewDebt(q.TotalMinor,q.CashMinor,q.CardMinor);Need(debt>0);
        var payload=Canonical("sale_open",store,actor,q.RequestGuid,q.CustomerGuid,q.SaleGuid,q.SaleFingerprint,Number(q.TotalMinor),Number(q.CashMinor),Number(q.CardMinor),Number(q.OccurredAt),q.DueDate??"");
        return Write((c,t)=> {
            Scope(c,t);var replay=Replay(c,t,q.RequestGuid,payload);if(replay!=null)return replay;
            var name=Customer(c,t,q.CustomerGuid);
            Need(Scalar(c,t,"SELECT 1 FROM sales WHERE guid=@p0",q.SaleGuid)==null); // Never attach to an old receipt.
            Need((string?)Scalar(c,t,"SELECT current_group FROM sync_control WHERE id=1")=="");
            Exec(c,t,"UPDATE sync_control SET current_group=@p0 WHERE id=1",q.RequestGuid);
            writeSale(c,t); // Trusted cashier adapter must use THIS connection/transaction for sale/items/stock.
            var sale=Rows(c,t,"SELECT total_amount,cash_amount,card_amount,created_at FROM sales WHERE guid=@p0",q.SaleGuid).Single();
            Need(StoredMinor(sale[0])==q.TotalMinor && StoredMinor(sale[1])==q.CashMinor && StoredMinor(sale[2])==q.CardMinor && Convert.ToInt64(sale[3])==q.OccurredAt);
            Event(c,t,q.RequestGuid,q.CustomerGuid,"sale_open",q.OccurredAt,payload,0,0,0,null);
            Exec(c,t,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p0,@p1,@p2,@p3,@p4,@p5,@p6)",q.SaleGuid,q.CustomerGuid,store,q.RequestGuid,debt,q.DueDate,name);
            Finish(c,t,q.RequestGuid,payload);
            Exec(c,t,"UPDATE sync_journal SET acked=-1 WHERE group_id=@p0",q.RequestGuid);
            Exec(c,t,"UPDATE sync_control SET current_group='' WHERE id=1");return q.RequestGuid;
        });
    }
    public string TakePayment(DebtPaymentCommand q) {
        Id(q.RequestGuid);Id(q.CustomerGuid);if(q.TargetAccountGuid!=null)Id(q.TargetAccountGuid);Need(q.OccurredAt>=0);
        Need(q.CashMinor>=0 && q.CardMinor>=0 && q.FeeMinor>=0 && q.FeeMinor<=q.CardMinor);var total=checked(q.CashMinor+q.CardMinor);Need(total>0);
        if(q.FeeUsdRate!=null)Need(Regex.IsMatch(q.FeeUsdRate,"\\A[0-9]{1,12}(?:\\.[0-9]{1,8})?\\z") && decimal.Parse(q.FeeUsdRate,CultureInfo.InvariantCulture)>0);
        var payload=Canonical("payment",store,actor,q.RequestGuid,q.CustomerGuid,Number(q.CashMinor),Number(q.CardMinor),Number(q.FeeMinor),Number(q.OccurredAt),q.TargetAccountGuid??"",q.FeeUsdRate??"");
        return Write((c,t)=> {
            Scope(c,t);var replay=Replay(c,t,q.RequestGuid,payload);if(replay!=null)return replay;
            Customer(c,t,q.CustomerGuid);
            var allocation=DebtAccounting.Allocate(total,q.CustomerGuid,Accounts(c,t,q.CustomerGuid),q.TargetAccountGuid);
            var effect=DebtAccounting.Payment(q.CashMinor,q.CardMinor,q.FeeMinor,allocation);
            Event(c,t,q.RequestGuid,q.CustomerGuid,"payment",q.OccurredAt,payload,q.CashMinor,q.CardMinor,q.FeeMinor,q.FeeUsdRate);
            for(int i=0;i<effect.Lines.Count;i++)Exec(c,t,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",q.RequestGuid,i,effect.Lines[i].AccountGuid,q.CustomerGuid,store,effect.Lines[i].DeltaMinor);
            Finish(c,t,q.RequestGuid,payload);return q.RequestGuid;
        });
    }
}
