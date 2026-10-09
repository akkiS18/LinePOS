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
public sealed record DebtCustomerRecord(string Guid, string StoreGuid, string Name, string Phone, string Note, bool Archived, long Revision, long CreatedAt);
public sealed record DebtCustomerUpdate(string CustomerGuid, string Name, string Phone, string Note, long Revision);
public sealed record DebtSaleCommand(string RequestGuid, string CustomerGuid, string SaleGuid, string SaleFingerprint,
    long TotalMinor, long CashMinor, long CardMinor, long OccurredAt, string? DueDate = null);
public sealed record DebtPaymentCommand(string RequestGuid, string CustomerGuid, long CashMinor, long CardMinor,
    long FeeMinor, long OccurredAt, string? TargetAccountGuid = null, string? FeeUsdRate = null);
public sealed record DebtOpenSaleCommand(string RequestGuid, string CustomerGuid, DebtSaleSnapshot Sale,
    string? DueDate = null, DebtCustomerDraft? NewCustomer = null, long? UserId = null);

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
            if(previous!=null){
                Need(previous==payload);
                Customer(c,t,draft.Guid,false);
                var prevEnv=Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0","debt_envelope_v1:"+draft.Guid) as string;
                if(prevEnv!=null) {
                    Need(prevEnv.Length>65 && prevEnv[64]=='\n');
                    var pw=prevEnv[65..];
                    Need(prevEnv==DebtWire.Fingerprint(pw)+"\n"+pw);
                }
                return draft.Guid;
            }
            Need(Scalar(c,t,"SELECT 1 FROM debt_events WHERE request_guid=@p0",draft.Guid)==null);
            Exec(c,t,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",draft.Guid,store,draft.Name,draft.Phone,draft.Note,draft.CreatedAt,device);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,payload);
            Outbox(c,t,draft.Guid,"debt_customer",draft.Guid,payload);

            var cWireObj=new DebtWireCustomer(draft.Guid,store,device,payload,DebtWire.Fingerprint(payload));
            var customerWire=DebtWire.EncodeCustomer(cWireObj,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:customer:"+draft.Guid,DebtWire.Fingerprint(customerWire)+"\n"+customerWire);

            var envelope=new DebtEnvelopePacket(draft.Guid,store,customerWire,"","");
            var envelopeWire=DebtEnvelope.Encode(envelope,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:"+draft.Guid,DebtWire.Fingerprint(envelopeWire)+"\n"+envelopeWire);

            return draft.Guid;
        });
    }
    private string? Replay(SqliteConnection c,SqliteTransaction t,string request,string payload) {
        Need(Scalar(c,t,"SELECT 1 FROM sync_meta WHERE key=@p0","debt_customer_create:"+request)==null);
        var row=Rows(c,t,"SELECT r.payload_hash,r.result,e.payload FROM debt_command_receipts r JOIN debt_events e ON e.guid=r.event_guid WHERE r.request_guid=@p0",request).SingleOrDefault();
        if(row==null)return null;
        Need((string)row[0]! == Hash(payload) && (string)row[2]! == payload && (string)row[1]! == request);
        var env=Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0","debt_envelope_v1:"+request) as string;
        if(env!=null) {
            Need(env.Length>65 && env[64]=='\n');
            var wire=env[65..];
            Need(env==DebtWire.Fingerprint(wire)+"\n"+wire);
        }
        return (string)row[1]!;
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
    public DebtCustomerRecord? ReadCustomer(string customerGuid) {
        Id(customerGuid);
        return Write((c,t)=>{
            Scope(c,t);
            var row=Rows(c,t,"SELECT guid,store_guid,name,phone,note,archived,revision,created_at FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",customerGuid,store).SingleOrDefault();
            if(row==null) return null;
            return new DebtCustomerRecord((string)row[0]!,(string)row[1]!,(string)row[2]!,(string)row[3]!,(string)row[4]!,Convert.ToInt64(row[5])!=0,Convert.ToInt64(row[6]),Convert.ToInt64(row[7]));
        });
    }
    public bool UpdateCustomer(DebtCustomerUpdate update) {
        Id(update.CustomerGuid);
        TextField(update.Name,256,true);
        TextField(update.Phone,64);
        TextField(update.Note,2048);
        Need(update.Revision>=0);
        return Write((c,t)=>{
            Scope(c,t);
            var row=Rows(c,t,"SELECT revision FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",update.CustomerGuid,store).SingleOrDefault();
            if(row==null) throw new ArgumentException("Mijoz topilmadi");
            var currentRev=Convert.ToInt64(row[0]);
            Need(update.Revision==currentRev);
            Exec(c,t,"UPDATE debt_customers SET name=@p0,phone=@p1,note=@p2,revision=revision+1 WHERE guid=@p3 AND store_guid=@p4",
                update.Name,update.Phone,update.Note,update.CustomerGuid,store);
            return true;
        });
    }
    public bool ArchiveCustomer(string customerGuid, bool archive=true) {
        Id(customerGuid);
        return Write((c,t)=>{
            Scope(c,t);
            var row=Rows(c,t,"SELECT 1 FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",customerGuid,store).SingleOrDefault();
            if(row==null) throw new ArgumentException("Mijoz topilmadi");
            Exec(c,t,"UPDATE debt_customers SET archived=@p0,revision=revision+1 WHERE guid=@p1 AND store_guid=@p2",
                archive?1:0,customerGuid,store);
            return true;
        });
    }
    internal static long StoredMinor(object? value) {
        var d=Convert.ToDouble(value,CultureInfo.InvariantCulture);Need(double.IsFinite(d) && d>=0);
        return checked((long)(decimal.Round(decimal.Parse(d.ToString("R",CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture),2,MidpointRounding.AwayFromZero)*100));
    }
    public string OpenSale(DebtOpenSaleCommand cmd) {
        Id(cmd.RequestGuid);Id(cmd.CustomerGuid);Id(cmd.Sale.Guid);Need(cmd.Sale.OccurredAt>=0);
        if(cmd.DueDate!=null)
            Need(DateOnly.TryParseExact(cmd.DueDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d) && d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)==cmd.DueDate);
        if(cmd.NewCustomer!=null) {
            Need(cmd.NewCustomer.Guid==cmd.CustomerGuid);
            TextField(cmd.NewCustomer.Name,256,true);TextField(cmd.NewCustomer.Phone,64);TextField(cmd.NewCustomer.Note,2048);Need(cmd.NewCustomer.CreatedAt>=0);
        }
        var s=cmd.Sale;
        var saleWire=DebtEnvelope.EncodeSale(s);
        var saleFingerprint=DebtWire.Fingerprint(saleWire);
        var debt=DebtAccounting.NewDebt(s.TotalMinor,s.CashMinor,s.CardMinor);Need(debt>0);
        var payload=Canonical("sale_open",store,actor,cmd.RequestGuid,cmd.CustomerGuid,s.Guid,saleFingerprint,
            Number(s.TotalMinor),Number(s.CashMinor),Number(s.CardMinor),Number(s.OccurredAt),cmd.DueDate??"");

        DebtSaleReceiver.Money(s.TotalMinor);DebtSaleReceiver.Money(s.CostMinor);DebtSaleReceiver.Money(s.CashMinor);DebtSaleReceiver.Money(s.CardMinor);DebtSaleReceiver.Money(s.FeeMinor);
        DebtSaleReceiver.Real(DebtSaleReceiver.D(s.FeeRate));DebtSaleReceiver.Real(DebtSaleReceiver.D(s.UsdRate));
        foreach(var i in s.Items) {
            DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Quantity));DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Price));DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Cost));DebtSaleReceiver.Real(DebtSaleReceiver.D(i.StockDelta));
        }

        var user=cmd.UserId??1L;Need(user>0);

        return Write((c,t)=> {
            Scope(c,t);var replay=Replay(c,t,cmd.RequestGuid,payload);if(replay!=null)return replay;
            Need(Scalar(c,t,"SELECT 1 FROM sales WHERE guid=@p0",s.Guid)==null);
            Need(Scalar(c,t,"SELECT 1 FROM debt_events WHERE guid=@p0",cmd.RequestGuid)==null);
            Need(Scalar(c,t,"SELECT 1 FROM sync_journal WHERE op_id=@p0",DebtSaleReceiver.Marker(s.Guid))==null);
            Need((string?)Scalar(c,t,"SELECT current_group FROM sync_control WHERE id=1")=="");

            string customerName;
            string customerWire="";
            if(cmd.NewCustomer!=null) {
                var cKey="debt_customer_create:"+cmd.NewCustomer.Guid;
                var cPayload=Canonical("customer",store,actor,cmd.NewCustomer.Guid,cmd.NewCustomer.Name,cmd.NewCustomer.Phone,cmd.NewCustomer.Note,Number(cmd.NewCustomer.CreatedAt));
                var prev=(string?)Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",cKey);
                if(prev!=null) {
                    Need(prev==cPayload);customerName=Customer(c,t,cmd.CustomerGuid,false);
                } else {
                    Need(Scalar(c,t,"SELECT 1 FROM debt_events WHERE request_guid=@p0",cmd.NewCustomer.Guid)==null);
                    Exec(c,t,"INSERT INTO debt_customers(guid,store_guid,name,phone,note,created_at,device_guid) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                        cmd.NewCustomer.Guid,store,cmd.NewCustomer.Name,cmd.NewCustomer.Phone,cmd.NewCustomer.Note,cmd.NewCustomer.CreatedAt,device);
                    Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",cKey,cPayload);
                    Outbox(c,t,cmd.NewCustomer.Guid,"debt_customer",cmd.NewCustomer.Guid,cPayload);
                    customerName=cmd.NewCustomer.Name;
                }
                var cWireObj=new DebtWireCustomer(cmd.NewCustomer.Guid,store,device,cPayload,DebtWire.Fingerprint(cPayload));
                customerWire=DebtWire.EncodeCustomer(cWireObj,store);
                var cWireKey="debt_wire_v1:customer:"+cmd.NewCustomer.Guid;
                var cWireVal=DebtWire.Fingerprint(customerWire)+"\n"+customerWire;
                var prevWire=Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",cWireKey);
                if(prevWire==null)Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",cWireKey,cWireVal);
                else Need((string)prevWire==cWireVal);
            } else {
                customerName=Customer(c,t,cmd.CustomerGuid);
            }

            var products=new Dictionary<string,long>(StringComparer.Ordinal);
            var stocks=new Dictionary<(string Product,string Warehouse),decimal>();
            foreach(var i in s.Items) {
                Need(Scalar(c,t,"SELECT 1 FROM sale_items WHERE guid=@p0",i.Guid)==null);
                Need(Scalar(c,t,"SELECT 1 FROM sync_journal WHERE op_id=@p0",i.StockOperationGuid)==null);
                var pId=Scalar(c,t,"SELECT id FROM products WHERE guid=@p0",i.ProductGuid);
                Need(pId!=null);products[i.ProductGuid]=Convert.ToInt64(pId,CultureInfo.InvariantCulture);
                Need(Scalar(c,t,"SELECT 1 FROM warehouses WHERE guid=@p0",i.WarehouseGuid)!=null);
                var key=(i.ProductGuid,i.WarehouseGuid);
                if(!stocks.ContainsKey(key)) {
                    var old=Scalar(c,t,"SELECT quantity FROM product_stocks WHERE product_guid=@p0 AND warehouse_guid=@p1",key.ProductGuid,key.WarehouseGuid);
                    stocks[key]=old==null?0:DebtSaleReceiver.Stored(old);
                }
                stocks[key]+=DebtSaleReceiver.D(i.StockDelta);
                DebtSaleReceiver.Real(stocks[key]);
            }

            var totals=new Dictionary<string,decimal>(StringComparer.Ordinal);
            foreach(var p in products.Keys) {
                decimal total=0;
                foreach(var row in Rows(c,t,"SELECT warehouse_guid,quantity FROM product_stocks WHERE product_guid=@p0",p)) {
                    var key=(p,(string)row[0]!);
                    if(!stocks.ContainsKey(key))total+=DebtSaleReceiver.Stored(row[1]);
                }
                total+=stocks.Where(x=>x.Key.Product==p).Sum(x=>x.Value);
                DebtSaleReceiver.Real(total);totals[p]=total;
            }

            Exec(c,t,"UPDATE sync_control SET current_group=@p0 WHERE id=1",cmd.RequestGuid);

            Exec(c,t,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,usd_rate,created_at,user_id,is_synced) VALUES(@p0,@p1,@p2,3,@p3,@p4,@p5,@p6,@p7,@p8,@p9,0)",
                s.Guid,DebtSaleReceiver.Money(s.TotalMinor),DebtSaleReceiver.Money(s.CostMinor),DebtSaleReceiver.Money(s.CashMinor),
                DebtSaleReceiver.Money(s.CardMinor),DebtSaleReceiver.Money(s.FeeMinor),DebtSaleReceiver.Real(DebtSaleReceiver.D(s.FeeRate)),
                DebtSaleReceiver.Real(DebtSaleReceiver.D(s.UsdRate)),s.OccurredAt,user);
            var saleId=Scalar(c,t,"SELECT id FROM sales WHERE guid=@p0",s.Guid)!;

            foreach(var i in s.Items) {
                Exec(c,t,"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,category_at_sale,unit_at_sale,warehouse_guid,warehouse_name,quantity,price_at_sale,cost_at_sale,cost_currency) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                    i.Guid,saleId,s.Guid,products[i.ProductGuid],i.ProductGuid,i.ProductName,i.Category,i.Unit,i.WarehouseGuid,i.WarehouseName,
                    DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Quantity)),DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Price)),
                    DebtSaleReceiver.Real(DebtSaleReceiver.D(i.Cost)),i.CostCurrency);
                Exec(c,t,"INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,payload,group_id,acked) VALUES(@p0,'debt_stock',@p1,@p2,@p3,@p4,@p5,-1)",
                    i.StockOperationGuid,i.ProductGuid,i.WarehouseGuid,DebtSaleReceiver.Real(DebtSaleReceiver.D(i.StockDelta)),i.Guid,cmd.RequestGuid);
            }

            foreach(var pair in stocks)
                Exec(c,t,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3) ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=excluded.quantity,updated_at=MAX(product_stocks.updated_at,excluded.updated_at)",
                    pair.Key.Product,pair.Key.Warehouse,DebtSaleReceiver.Real(pair.Value),s.OccurredAt);
            foreach(var pair in totals)
                Exec(c,t,"UPDATE products SET stock_quantity=@p0 WHERE guid=@p1",DebtSaleReceiver.Real(pair.Value),pair.Key);

            Exec(c,t,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES(@p0,'debt_sale',@p1,@p2,@p3,-1)",
                DebtSaleReceiver.Marker(s.Guid),s.Guid,DebtSaleReceiver.Payload(saleWire,user),cmd.RequestGuid);

            var seq=checked(Convert.ToInt64(Scalar(c,t,"SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0",device))+1);
            Exec(c,t,"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate) VALUES(@p0,@p0,1,'sale_open',@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,0,0,0,NULL)",
                cmd.RequestGuid,cmd.CustomerGuid,store,actor,device,seq,s.OccurredAt,payload,Hash(payload));
            Exec(c,t,"INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,due_date,customer_name_at_sale) VALUES(@p0,@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                s.Guid,cmd.CustomerGuid,store,cmd.RequestGuid,debt,cmd.DueDate,customerName);
            Exec(c,t,"INSERT INTO debt_command_receipts(request_guid,payload_hash,event_guid,result) VALUES(@p0,@p1,@p0,@p0)",
                cmd.RequestGuid,Hash(payload));
            Outbox(c,t,cmd.RequestGuid,"debt_event",cmd.RequestGuid,payload);

            Exec(c,t,"UPDATE sync_journal SET acked=-1 WHERE group_id=@p0",cmd.RequestGuid);
            Exec(c,t,"UPDATE sync_control SET current_group='' WHERE id=1");

            var accObj=new DebtWireAccount(s.Guid,s.Guid,cmd.RequestGuid,debt,cmd.DueDate,customerName);
            var evObj=new DebtWireEvent(cmd.RequestGuid,cmd.RequestGuid,"sale_open",cmd.CustomerGuid,store,actor,device,seq,s.OccurredAt,
                payload,Hash(payload),0,0,0,null,accObj,Array.Empty<DebtLine>());
            var eventWire=DebtWire.EncodeEvent(evObj,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:event:"+cmd.RequestGuid,DebtWire.Fingerprint(eventWire)+"\n"+eventWire);

            var envelope=new DebtEnvelopePacket(cmd.RequestGuid,store,customerWire,eventWire,saleWire);
            var envelopeWire=DebtEnvelope.Encode(envelope,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:"+cmd.RequestGuid,DebtWire.Fingerprint(envelopeWire)+"\n"+envelopeWire);

            DebtSaleReceiver.Verify(c,t,saleWire,cmd.RequestGuid);

            return cmd.RequestGuid;
        });
    }
    public string OpenSale(DebtSaleSnapshot sale,string requestGuid,string customerGuid,string? dueDate=null,DebtCustomerDraft? newCustomer=null,long? userId=null)
        => OpenSale(new DebtOpenSaleCommand(requestGuid,customerGuid,sale,dueDate,newCustomer,userId));
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
            Finish(c,t,q.RequestGuid,payload);

            var lines=effect.Lines.Select(l=>new DebtLine(l.AccountGuid,l.DeltaMinor)).ToList();
            var seq=Convert.ToInt64(Scalar(c,t,"SELECT device_sequence FROM debt_events WHERE guid=@p0",q.RequestGuid),CultureInfo.InvariantCulture);
            var evObj=new DebtWireEvent(q.RequestGuid,q.RequestGuid,"payment",q.CustomerGuid,store,actor,device,seq,q.OccurredAt,payload,Hash(payload),q.CashMinor,q.CardMinor,q.FeeMinor,q.FeeUsdRate,null,lines);
            var eventWire=DebtWire.EncodeEvent(evObj,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_wire_v1:event:"+q.RequestGuid,DebtWire.Fingerprint(eventWire)+"\n"+eventWire);

            var envelope=new DebtEnvelopePacket(q.RequestGuid,store,"",eventWire,"");
            var envelopeWire=DebtEnvelope.Encode(envelope,store);
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)","debt_envelope_v1:"+q.RequestGuid,DebtWire.Fingerprint(envelopeWire)+"\n"+envelopeWire);

            return q.RequestGuid;
        });
    }
}
