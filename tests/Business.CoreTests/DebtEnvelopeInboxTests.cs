using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

static class DebtEnvelopeInboxTests
{
    static string G(int n)=>"00000000-0000-0000-0000-"+n.ToString("D12");
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Reject(Action action){try{action();}catch(Exception e) when(e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or SqliteException){return;}throw new Exception("Invalid inbox operation accepted");}
    static DebtRepository Repo(string p)=>new(p,G(1),G(2),()=>true);
    static DebtSyncStore Bridge(string p)=>new(p,G(1),()=>true,_=>true);
    static DebtEnvelopeInbox Inbox(string p,bool allowed=true,bool actor=true)=>new(p,G(1),()=>allowed,_=>actor);
    static void Exec(SqliteConnection c,SqliteTransaction? tx,string sql,params object[] args){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText=sql;for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("@p"+i,args[i]);q.ExecuteNonQuery();}
    static void Sql(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();Exec(c,null,sql);}
    static long N(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();using var q=c.CreateCommand();q.CommandText=sql;return Convert.ToInt64(q.ExecuteScalar());}
    static long Count(string p,string table)=>N(p,"SELECT COUNT(*) FROM "+table);
    static void Setup(string p){_ = new DatabaseContext(p);using var c=new SqliteConnection("Data Source="+p);c.Open();using var stream=typeof(DebtEnvelopeInboxTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;using var r=new StreamReader(stream);foreach(var statement in r.ReadToEnd().Split("-- statement"))Exec(c,null,statement);Repo(p).BindStore();}
    static void Sale(SqliteConnection db,SqliteTransaction tx,string guid)=>Exec(db,tx,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,created_at) VALUES(@p0,100,60,0,20,0,1)",guid);
    static string Packet(string customer,string ev,string sale="")=>DebtEnvelope.Encode(new(ev.Length==0?G(3):DebtWire.DecodeEvent(ev,G(1)).Guid,G(1),customer,ev,sale),G(1));
    static string Another(string payment,string customer,int request){var e=DebtWire.DecodeEvent(payment,G(1));var fields=DebtWire.CommandFields(e.Payload);fields[3]=G(request);var payload=DebtRepository.Canonical(fields);return Packet(customer,DebtWire.EncodeEvent(e with{Guid=G(request),RequestGuid=G(request),DeviceSequence=request,Payload=payload,PayloadHash=DebtWire.Fingerprint(payload)},G(1)));}
    public static void Run(){
        var dir=Path.Combine(Path.GetTempPath(),"debt-inbox-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var a=Path.Combine(dir,"a.db");var b=Path.Combine(dir,"b.db");var c=Path.Combine(dir,"c.db");var q=Path.Combine(dir,"q.db");
        try {
            foreach(var p in new[]{a,b,c,q})Setup(p);
            var sale=DebtEnvelope.EncodeSale(new(G(4),1,10000,6000,2000,0,0,"0","12000","DEBT",new[]{new DebtSaleItem(G(10),G(11),"Wire","Electric","m",G(12),"Main","1","100","60","UZS",G(13),"-1")}));
            Repo(a).CreateCustomer(new(G(3),"Ali","","",1));Repo(a).OpenSale(new(G(5),G(3),G(4),DebtWire.Fingerprint(sale),10000,2000,0,1),(db,tx)=>Sale(db,tx,G(4)));
            Repo(a).TakePayment(new(G(6),G(3),4000,0,0,2));
            var cw=Bridge(a).ExportCustomer(G(3));var ew=Bridge(a).ExportEvent(G(5));var pw=Bridge(a).ExportEvent(G(6));var packet=Packet(cw,pw);var opening=Packet(cw,ew,sale);var customer=Packet(cw,"");
            Check(Inbox(b).Receive(packet,10)==DebtReceiveStatus.WaitingForDependency,"Missing account falsely accepted");
            Check(Count(b,"debt_customers")==0 && Count(b,"debt_events")==0 && Count(b,"sales")==0,"Partial packet applied");
            Check(Inbox(b).ReadPending(G(6))==new DebtPendingPacket(packet,10,"missing_dependency"),"Frozen inbox body changed");
            Check(Inbox(b).Receive(packet,99)==DebtReceiveStatus.WaitingForDependency && Inbox(b).ReadPending(G(6))!.ReceivedAt==10,"Retry changed receive time");
            Reject(()=>Inbox(b).Receive(Packet("",pw),99));Reject(()=>Inbox(b,actor:false).ReadPending(G(6)));Reject(()=>Inbox(b,allowed:false).Receive(packet,10));
            Check(Inbox(b).Receive(opening,11)==DebtReceiveStatus.WaitingForSaleAdapter && Inbox(b).ExportApplied(G(5))==null,"Opening partially accepted");
            Check(Count(b,"debt_customers")==0 && Count(b,"sales")==0,"Opening wrote sale/contact");
            // Simulate an already committed dependency via the existing trusted component API.
            // This is deliberately NOT proof that the new inbox imports opening sale/stock.
            Bridge(b).Apply(new[]{cw},new[]{ew},(db,tx,e)=>Sale(db,tx,e.Account!.SaleGuid));
            Sql(b,"CREATE TRIGGER fail_outer BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected outer receipt failure'); END");
            var journal=Count(b,"sync_journal");Reject(()=>Inbox(b).Receive(packet,12));
            Check(Count(b,"debt_events")==1 && Count(b,"sync_journal")==journal && Inbox(b).ReadPending(G(6))!=null && N(b,"SELECT applying FROM sync_control")==0,"Outer receipt failure leaked effects");
            Sql(b,"DROP TRIGGER fail_outer");
            Sql(b,"CREATE TRIGGER fail_inbox_delete BEFORE DELETE ON debt_sync_inbox BEGIN SELECT RAISE(ABORT,'Injected completion failure'); END");
            Reject(()=>Inbox(b).Receive(packet,12));Check(Count(b,"debt_events")==1 && Inbox(b).ExportApplied(G(6))==null,"Inbox deletion not atomic");Sql(b,"DROP TRIGGER fail_inbox_delete");
            Check(Inbox(b).Receive(packet,12)==DebtReceiveStatus.Applied && Inbox(b).ReadPending(G(6))==null,"Retry failed");
            Check(Repo(b).ReadAccounts(G(3)).Single().BalanceMinor==4000 && Inbox(b).ExportApplied(G(6))==packet,"Wrong payment/relay");
            Sql(b,"UPDATE debt_customers SET name='Renamed',archived=1");
            var before=Count(b,"sync_journal");Check(Inbox(b).Receive(packet,20)==DebtReceiveStatus.AlreadyApplied && Count(b,"sync_journal")==before,"Replay wrote again");
            Reject(()=>Inbox(b).Receive(Packet("",pw),20));Reject(()=>Inbox(b,actor:false).Receive(packet,20));Reject(()=>Inbox(b,actor:false).ExportApplied(G(6)));
            var next=Another(pw,cw,7);var results=new DebtReceiveStatus[2];
            Task.WaitAll(Task.Run(()=>results[0]=Inbox(b).Receive(next,21)),Task.Run(()=>results[1]=Inbox(b).Receive(next,21)));
            Check(results.Count(x=>x==DebtReceiveStatus.Applied)==1 && results.Count(x=>x==DebtReceiveStatus.AlreadyApplied)==1,"Concurrent duplicate");
            Check(Repo(b).ReadAccounts(G(3)).Single().BalanceMinor==0,"Concurrent money changed");
            Check(N(b,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1")==0,"HELD released");
            // Customer creation + full-body receipt roll back together as well.
            Sql(c,"CREATE TRIGGER fail_customer_outer BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected customer receipt failure'); END");
            Reject(()=>Inbox(c).Receive(customer,1));Check(Count(c,"debt_customers")==0 && N(c,"SELECT COUNT(*) FROM sync_meta WHERE key LIKE 'debt_%'")==0,"Partial customer receipt");Sql(c,"DROP TRIGGER fail_customer_outer");
            Check(Inbox(c).Receive(customer,1)==DebtReceiveStatus.Applied && Inbox(c).Receive(customer,2)==DebtReceiveStatus.AlreadyApplied,"Customer replay");
            Reject(()=>Inbox(c).Receive(packet.Replace("debt-envelope-v1","debt-envelope-v2"),1));Reject(()=>Inbox(c).Receive(packet,-1));Check(Count(c,"debt_sync_inbox")==0,"Invalid frame persisted");
            for(int i=100;i<228;i++)Check(Inbox(q).Receive(Another(pw,cw,i),1)==DebtReceiveStatus.WaitingForDependency,"Capacity seed");
            bool full=false;try{Inbox(q).Receive(Another(pw,cw,300),1);}catch(DebtInboxFullException){full=true;}Check(full && Count(q,"debt_sync_inbox")==128,"Packet count cap");
            Check(Inbox(q).Receive(Another(pw,cw,100),2)==DebtReceiveStatus.WaitingForDependency,"Full inbox rejected identical retry");
            Check(Inbox(q).Receive(customer,1)==DebtReceiveStatus.Applied,"Full inbox blocked resolving dependency");
            // Deliberately inject occupied bytes; quota must count them, never evict history.
            Sql(q,"DELETE FROM debt_sync_inbox WHERE packet_guid<>'"+G(100)+"'; UPDATE debt_sync_inbox SET payload=hex(zeroblob(16777216))");
            full=false;try{Inbox(q).Receive(Another(pw,cw,300),1);}catch(DebtInboxFullException){full=true;}Check(full && Count(q,"debt_sync_inbox")==1,"Aggregate byte cap");
            Sql(b,"UPDATE sync_meta SET value='corrupt' WHERE key='debt_envelope_v1:"+G(6)+"'");Reject(()=>Inbox(b).ExportApplied(G(6)));Reject(()=>Inbox(b).Receive(packet,1));
            Console.WriteLine("PASS debt inbox: durable wait/retry, strict body replay, atomic receipt/inbox completion, concurrent once-only payment, permissions, capacity, immutable relay, opening gate, HELD");
        } finally {SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
