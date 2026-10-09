using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

static class DebtSyncStoreTests
{
    static string G(int i)=>"00000000-0000-0000-0000-"+i.ToString("D12");
    static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
    static void Reject(Action action){try{action();}catch(Exception e) when(e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or SqliteException){return;}throw new Exception("Invalid import accepted");}
    static void Exec(SqliteConnection c,SqliteTransaction? t,string sql,params object[] args){using var q=c.CreateCommand();q.Transaction=t;q.CommandText=sql;for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("@p"+i,args[i]);q.ExecuteNonQuery();}
    static void Sql(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();Exec(c,null,sql);}
    static object Value(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar()!;}
    static long Count(string path,string table)=>Convert.ToInt64(Value(path,"SELECT COUNT(*) FROM "+table));
    static DebtRepository Repo(string path)=>new(path,G(1),G(2),()=>true);
    static DebtSyncStore Bridge(string path,bool allowed=true,bool actor=true,string? store=null)=>new(path,store??G(1),()=>allowed,_=>actor);
    static void Setup(string path){
        var host=new DatabaseContext(path);host.SaveProduct(new Product{Guid="product",Name="Fixture",CostPrice=60,SellingPrice=100,StockQuantity=10});
        using var c=new SqliteConnection("Data Source="+path);c.Open();using var stream=typeof(DebtSyncStoreTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;using var reader=new StreamReader(stream);
        foreach(var statement in reader.ReadToEnd().Split("-- statement"))Exec(c,null,statement);Repo(path).BindStore();
    }
    static void Sale(SqliteConnection db,SqliteTransaction tx,string guid){
        Exec(db,tx,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,created_at) VALUES(@p0,100,60,0,20,0,1)",guid);
        Exec(db,tx,"UPDATE product_stocks SET quantity=quantity-1 WHERE product_guid='product'");
    }
    static string NewPayment(DebtWireEvent e,int request,int customer,string account,long seq){
        var payload=DebtRepository.Canonical("payment",G(1),G(2),G(request),G(customer),"3000","1000","20","2","","12000");
        return DebtWire.EncodeEvent(e with {Guid=G(request),RequestGuid=G(request),CustomerGuid=G(customer),DeviceSequence=seq,Payload=payload,PayloadHash=DebtWire.Fingerprint(payload),Lines=new[]{new DebtLine(account,-4000)}},G(1));
    }
    public static void Run(){
        var dir=Path.Combine(Path.GetTempPath(),"debt-bridge-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var a=Path.Combine(dir,"a.db");var b=Path.Combine(dir,"b.db");var c=Path.Combine(dir,"c.db");var d=Path.Combine(dir,"d.db");
        try{
            foreach(var path in new[]{a,b,c,d})Setup(path);
            Repo(a).CreateCustomer(new(G(3),"Ali","","",1));
            Repo(a).OpenSale(new(G(5),G(3),G(4),new string('0',64),10000,2000,0,1),(db,tx)=>Sale(db,tx,G(4)));
            var customer=Bridge(a).ExportCustomer(G(3));var opening=Bridge(a).ExportEvent(G(5));
            Bridge(b).Apply(new[]{customer},new[]{opening},(db,tx,e)=>Sale(db,tx,e.Account!.SaleGuid));
            // Both devices accept a payment against the last known 8000 balance offline.
            var paymentA=new DebtPaymentCommand(G(6),G(3),3000,1000,20,2,FeeUsdRate:"12000");Repo(a).TakePayment(paymentA);
            Repo(b).TakePayment(new(G(7),G(3),7000,0,0,3));
            var wa=Bridge(a).ExportEvent(G(6));var wb=Bridge(b).ExportEvent(G(7));
            Bridge(a).Apply(Array.Empty<string>(),new[]{wb});Bridge(b).Apply(Array.Empty<string>(),new[]{wa});
            Check(Repo(a).ReadAccounts(G(3)).Single().BalanceMinor==-3000 && Repo(b).ReadAccounts(G(3)).Single().BalanceMinor==-3000,"Offline collections lost/reallocated");
            var before=Count(a,"sync_journal");Bridge(a).Apply(new[]{customer},new[]{opening,wa,wb},(_,_,_)=>throw new Exception("Echo called sale writer"));Repo(a).TakePayment(paymentA);
            Check(Count(a,"sync_journal")==before && Count(a,"sales")==1,"Echo produced another effect");
            // Reordered dependencies apply in a single transaction, opening before payment.
            Bridge(c).Apply(new[]{customer},new[]{wa,opening},(db,tx,e)=>Sale(db,tx,e.Account!.SaleGuid));
            Check(Repo(c).ReadAccounts(G(3)).Single().BalanceMinor==4000,"Reordered batch failed");
            Check(Convert.ToInt64(Value(c,"SELECT quantity FROM product_stocks WHERE product_guid='product'"))==9,"Imported stock");
            Sql(c,"UPDATE debt_customers SET name='Renamed',archived=1");
            Check(Bridge(c).ExportCustomer(G(3))==customer,"Contact edits changed creation packet");
            Bridge(c).Apply(new[]{customer},new[]{opening},(_,_,_)=>throw new Exception("Restart replay wrote sale"));
            Check((string)Value(c,"SELECT name FROM debt_customers") == "Renamed","Replay overwrote contact");
            Task.WaitAll(Task.Run(()=>Bridge(c).Apply(Array.Empty<string>(),new[]{wb})),Task.Run(()=>Bridge(c).Apply(Array.Empty<string>(),new[]{wb})));
            Check(Repo(c).ReadAccounts(G(3)).Single().BalanceMinor==-3000 && Count(c,"debt_events")==3,"Concurrent replay/archived contact changed money");
            Check(Bridge(c).ExportEvent(G(6))==wa && Bridge(c).ExportEvent(G(7))==wb,"Forwarding changed sender identity/allocation");
            Check(Convert.ToInt64(Value(c,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1"))==0,"Journal escaped hold");
            var e=DebtWire.DecodeEvent(wa,G(1));
            Reject(()=>Bridge(c).Apply(Array.Empty<string>(),new[]{DebtWire.EncodeEvent(e with {Lines=new[]{new DebtLine(G(90),-4000)}},G(1))}));
            Reject(()=>Bridge(c).Apply(Array.Empty<string>(),new[]{NewPayment(e,80,3,G(4),e.DeviceSequence)})); // Device/sequence collision.
            Reject(()=>Bridge(c,actor:false).Apply(Array.Empty<string>(),new[]{wa}));Reject(()=>Bridge(c,allowed:false).ExportEvent(G(6)));
            Reject(()=>Bridge(c,store:G(99)).ExportEvent(G(6)));
            Repo(c).CreateCustomer(new(G(30),"Other","","",1));
            Reject(()=>Bridge(c).Apply(Array.Empty<string>(),new[]{NewPayment(e,8,30,G(4),99)})); // Existing account, wrong owner.
            bool missing=false;try{Bridge(d).Apply(new[]{customer},new[]{wa});}catch(DebtDependencyException){missing=true;}Check(missing,"Missing dependency not distinguished");
            void Empty(){Check(Count(d,"debt_customers")==0 && Count(d,"debt_events")==0 && Count(d,"sync_journal")==0 && Count(d,"sales")==0,"Partial import survived rollback");Check(Convert.ToInt64(Value(d,"SELECT quantity FROM product_stocks"))==10,"Callback stock not rolled back");Check(Convert.ToInt64(Value(d,"SELECT applying FROM sync_control"))==0,"Applying flag leaked");}
            Empty();
            Reject(()=>Bridge(d).Apply(new[]{customer},new[]{opening},(db,tx,v)=>{Sale(db,tx,v.Account!.SaleGuid);throw new InvalidOperationException("Injected callback failure");}));Empty();
            Sql(d,"CREATE TRIGGER fail_seal BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_wire_v1:event:%' BEGIN SELECT RAISE(ABORT,'Injected seal failure'); END");
            Reject(()=>Bridge(d).Apply(new[]{customer},new[]{opening},(db,tx,v)=>Sale(db,tx,v.Account!.SaleGuid)));Empty();Sql(d,"DROP TRIGGER fail_seal");
            Bridge(d).Apply(new[]{customer},new[]{opening,wa},(db,tx,v)=>Sale(db,tx,v.Account!.SaleGuid));
            Sql(d,"UPDATE sync_meta SET value='corrupt' WHERE key='debt_wire_v1:event:"+G(6)+"'");Reject(()=>Bridge(d).ExportEvent(G(6)));
            Sql(d,"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor) VALUES('"+G(5)+"',1,'"+G(4)+"','"+G(3)+"','"+G(1)+"',-1)");Reject(()=>Bridge(d).ExportEvent(G(5)));
            Console.WriteLine("PASS debt DB bridge: real DB export/import, reordered dependencies, offline overcollection convergence, echo/restart/concurrent replay, full-body conflict, permissions/ownership, atomic callback/seal rollback, held journal, corrupt seal/gapped lines");
        }finally{SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
