using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

static class DebtRepositoryTests
{
    static string G(int i)=>"00000000-0000-0000-0000-"+i.ToString("D12");
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static void Exec(SqliteConnection c,SqliteTransaction? t,string sql,params object[] args) { using var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandText=sql;for(int i=0;i<args.Length;i++)cmd.Parameters.AddWithValue("@p"+i,args[i]);cmd.ExecuteNonQuery(); }
    static object Value(string path,string sql) { using var c=new SqliteConnection("Data Source="+path);c.Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;return cmd.ExecuteScalar()!; }
    static long Count(string path,string table)=>Convert.ToInt64(Value(path,"SELECT COUNT(*) FROM "+table));
    static void Reject(Action action) { try { action(); }catch(Exception e) when(e is ArgumentException or SqliteException or UnauthorizedAccessException or InvalidOperationException) { return; }throw new Exception("Invalid command was accepted"); }
    static void SaveSale(SqliteConnection c,SqliteTransaction t,string guid,long at=1) {
        Exec(c,t,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,created_at) VALUES(@p0,100,60,0,20,0,@p1)",guid,at);
        Exec(c,t,"UPDATE product_stocks SET quantity=quantity-1 WHERE product_guid='product'");
    }
    public static void Run() {
        var dir=Path.Combine(Path.GetTempPath(),"debt-repo-"+Guid.NewGuid());Directory.CreateDirectory(dir);var path=Path.Combine(dir,"test.db");
        try {
            var host=new DatabaseContext(path);
            host.SaveProduct(new Product {Guid="product",Name="Fixture",CostPrice=60,SellingPrice=100,StockQuantity=10});
            using(var c=new SqliteConnection("Data Source="+path)) {
                c.Open();using var stream=typeof(DebtRepositoryTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;using var reader=new StreamReader(stream);
                foreach(var statement in reader.ReadToEnd().Split("-- statement"))Exec(c,null,statement);
            }
            DebtRepository Repo(bool allowed=true,string? store=null)=>new(path,store??G(1),G(2),()=>allowed);
            var repo=Repo();repo.BindStore();repo.BindStore();Reject(()=>Repo(store:G(99)).BindStore());
            var customer=new DebtCustomerDraft(G(3),"Ali Oʻgʻli","","",1);
            Check(repo.CreateCustomer(customer)==G(3),"Customer result");repo.CreateCustomer(customer);
            Check(Count(path,"debt_customers")==1,"Duplicate customer");Reject(()=>repo.CreateCustomer(customer with {Name="Changed"}));
            Reject(()=>Repo(false).CreateCustomer(new(G(30),"Denied","","",1)));
            var open=new DebtSaleCommand(G(5),G(3),G(4),new string('0',64),10000,2000,0,1,"2030-01-01");
            repo.OpenSale(open,(c,t)=>SaveSale(c,t,G(4)));
            Repo().OpenSale(open,(_,_)=>throw new Exception("Sale replay must not call writer"));
            Check(repo.ReadAccounts(G(3)).Single().BalanceMinor==8000,"Opening counted twice");
            Check(Convert.ToInt64(Value(path,"SELECT quantity FROM product_stocks WHERE product_guid='product'"))==9,"Sale replay changed stock");
            Check(Convert.ToInt64(Value(path,"SELECT COUNT(*) FROM sync_journal WHERE group_id='"+G(5)+"' AND acked<>-1"))==0,"Debt sale escaped hold");
            Reject(()=>repo.OpenSale(open with {SaleFingerprint=new string('1',64)},(_,_)=>{}));
            Reject(()=>repo.OpenSale(open with {RequestGuid=G(55)},(_,_)=>{})); // No attaching old sale.
            var payment=new DebtPaymentCommand(G(6),G(3),3000,1000,20,2,FeeUsdRate:"12000");
            repo.TakePayment(payment);Repo().TakePayment(payment);
            Check(repo.ReadAccounts(G(3)).Single().BalanceMinor==4000,"Payment replay allocated twice");
            Check(Count(path,"debt_events")==2 && Count(path,"debt_event_lines")==1,"Duplicate event/line");
            Check(Convert.ToInt64(Value(path,"SELECT fee_minor FROM debt_events WHERE kind='payment'"))==20,"Fee lost");
            Check(Count(path,"sales")==1,"Collection created a sale");
            Reject(()=>repo.TakePayment(payment with {CashMinor=3001}));
            Reject(()=>repo.TakePayment(payment with {RequestGuid=G(70),TargetAccountGuid=G(90)}));
            Reject(()=>repo.TakePayment(payment with {RequestGuid=G(70),CashMinor=9000}));
            Reject(()=>Repo(store:G(99)).TakePayment(payment));
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"UPDATE sync_control SET applying=1"); }
            Reject(()=>repo.TakePayment(payment));
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"UPDATE sync_control SET applying=0,current_group='busy'"); }
            Reject(()=>repo.TakePayment(payment));
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"UPDATE sync_control SET current_group=''"); }
            // The same frozen command remains retryable after contact metadata changes.
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"UPDATE debt_customers SET name='Renamed',archived=1"); }
            repo.CreateCustomer(customer);Repo().TakePayment(payment);
            Reject(()=>repo.TakePayment(payment with {RequestGuid=G(70)}));
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"UPDATE debt_customers SET archived=0"); }
            var before=Count(path,"sync_journal");
            Reject(()=>repo.OpenSale(open with {RequestGuid=G(7),SaleGuid=G(8)},(c,t)=>{SaveSale(c,t,G(8));throw new InvalidOperationException("Crash before debt");}));
            Check(Count(path,"sales")==1 && Count(path,"sync_journal")==before,"Sale/stock/outbox failed rollback");
            Check(Convert.ToInt64(Value(path,"SELECT quantity FROM product_stocks WHERE product_guid='product'"))==9,"Stock failed rollback");
            Check((string)Value(path,"SELECT current_group FROM sync_control")=="","Group leaked after rollback");
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"CREATE TRIGGER fail_debt_outbox BEFORE INSERT ON sync_journal WHEN NEW.kind='debt_event' BEGIN SELECT RAISE(ABORT,'Injected outbox failure'); END"); }
            Reject(()=>repo.TakePayment(payment with {RequestGuid=G(9),CashMinor=1000,CardMinor=0,FeeMinor=0}));
            Check(Count(path,"debt_events")==2 && Count(path,"debt_command_receipts")==2 && Count(path,"debt_event_lines")==1 && Count(path,"sync_journal")==before,"Payment/receipt/line failed rollback");
            using(var c=new SqliteConnection("Data Source="+path)) { c.Open();Exec(c,null,"DROP TRIGGER fail_debt_outbox"); }
            // Two independent connections serialize the same request and return one durable result.
            var duplicate=payment with {RequestGuid=G(10),CashMinor=1000,CardMinor=0,FeeMinor=0};
            Task.WaitAll(Task.Run(()=>Repo().TakePayment(duplicate)),Task.Run(()=>Repo().TakePayment(duplicate)));
            Check(repo.ReadAccounts(G(3)).Single().BalanceMinor==3000 && Count(path,"debt_events")==3,"Concurrent duplicate applied twice");
            using var fixtureStream=typeof(DebtRepositoryTests).Assembly.GetManifestResourceStream("DebtRepositoryFixtures")!;
            using var fixture=JsonDocument.Parse(fixtureStream);
            foreach(var name in new[]{"customer","sale_open","payment"}) {
                var expected=fixture.RootElement.GetProperty(name);var request=expected.GetProperty("request").GetString()!;
                var payload=(string)Value(path,name=="customer"?"SELECT value FROM sync_meta WHERE key='debt_customer_create:"+request+"'":"SELECT payload FROM debt_events WHERE request_guid='"+request+"'");
                Check(payload==expected.GetProperty("payload").GetString(),"Canonical payload mismatch "+name);
                Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()==expected.GetProperty("hash").GetString(),"Canonical hash mismatch "+name);
            }
            Console.WriteLine("PASS debt repository: atomic sale/payment/outbox, restart/concurrent retry, payload/owner/permission guards, held groups, immutable allocation, shared canonical fixtures");
        } finally { SqliteConnection.ClearAllPools();Directory.Delete(dir,true); }
    }
}
