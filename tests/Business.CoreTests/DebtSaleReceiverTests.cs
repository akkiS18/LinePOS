using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

static class DebtSaleReceiverTests
{
    static string G(int n)=>"00000000-0000-0000-0000-"+n.ToString("D12");
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Reject(Action action){try{action();}catch(Exception e) when(e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or SqliteException){return;}throw new Exception("Invalid sale import accepted");}
    static DebtRepository Repo(string p)=>new(p,G(1),G(2),()=>true);
    static DebtEnvelopeInbox Inbox(string p,Func<string,long?>? map=null)=>new(p,G(1),()=>true,_=>true,map ?? (actor=>actor==G(2)?2:null));
    static void Exec(SqliteConnection c,SqliteTransaction? tx,string sql,params object[] args){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText=sql;for(int i=0;i<args.Length;i++)q.Parameters.AddWithValue("@p"+i,args[i]);q.ExecuteNonQuery();}
    static void Sql(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();Exec(c,null,sql);}
    static object? Value(string path,string sql){using var c=new SqliteConnection("Data Source="+path);c.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    static long Count(string p,string table)=>Convert.ToInt64(Value(p,"SELECT COUNT(*) FROM "+table));
    static void Setup(string p){_=new DatabaseContext(p);using var c=new SqliteConnection("Data Source="+p);c.Open();using var stream=typeof(DebtSaleReceiverTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;using var r=new StreamReader(stream);foreach(var statement in r.ReadToEnd().Split("-- statement"))Exec(c,null,statement);Repo(p).BindStore();}
    static void Product(string p)=>Sql(p,$"INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('{G(11)}','Current renamed product',999,999,5,0,500)");
    static void Warehouse(string p)=>Sql(p,$"INSERT INTO warehouses(guid,name,updated_at) VALUES('{G(12)}','Current renamed warehouse',500),('{G(14)}','Other',500)");
    static void Stocks(string p)=>Sql(p,$"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('{G(11)}','{G(12)}',1,500),('{G(11)}','{G(14)}',4,500)");
    static double Stock(string p)=>Convert.ToDouble(Value(p,$"SELECT quantity FROM product_stocks WHERE product_guid='{G(11)}' AND warehouse_guid='{G(12)}'"));
    static void Dependencies(string p){Product(p);Warehouse(p);Stocks(p);}
    static void Empty(string p){Check(Count(p,"sales")==0 && Count(p,"sale_items")==0 && Count(p,"debt_customers")==0 && Count(p,"debt_accounts")==0 && Count(p,"debt_events")==0,"Partial financial writes");Check(Convert.ToInt64(Value(p,"SELECT applying FROM sync_control"))==0 && (string?)Value(p,"SELECT current_group FROM sync_control")=="","Writer state leaked");}
    static string Packet(string cw,string ew,DebtSaleSnapshot sale) {
        var sw=DebtEnvelope.EncodeSale(sale);var e=DebtWire.DecodeEvent(ew,G(1));var fields=DebtWire.CommandFields(e.Payload);
        fields[6]=DebtWire.Fingerprint(sw);fields[7]=sale.TotalMinor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payload=DebtRepository.Canonical(fields);
        e=e with{Payload=payload,PayloadHash=DebtWire.Fingerprint(payload),Account=e.Account! with{OriginalDebtMinor=sale.TotalMinor-sale.CashMinor-sale.CardMinor}};
        return DebtEnvelope.Encode(new(e.Guid,G(1),cw,DebtWire.EncodeEvent(e,G(1)),sw),G(1));
    }
    public static void Run() {
        var dir=Path.Combine(Path.GetTempPath(),"debt-sale-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        int next=0;string Fresh(){var p=Path.Combine(dir,(next++)+".db");Setup(p);return p;}
        try {
            var source=Fresh();
            var sale=new DebtSaleSnapshot(G(4),100,20000,12000,2000,3000,60,"2","12000","DEBT",new[]{
                new DebtSaleItem(G(10),G(11),"Old cable","Old category","METR",G(12),"Old warehouse","1.25","100","0.005","USD",G(13),"-1.25"),
                new DebtSaleItem(G(20),G(11),"Old cable","Old category","METR",G(12),"Old warehouse","0.75","100","60","UZS",G(23),"-0.75")});
            var sw=DebtEnvelope.EncodeSale(sale);
            Repo(source).CreateCustomer(new(G(3),"Ali","","",1));
            Repo(source).OpenSale(new(G(5),G(3),G(4),DebtWire.Fingerprint(sw),20000,2000,3000,100),(db,tx)=>Exec(db,tx,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,created_at) VALUES(@p0,200,120,3,20,30,100)",G(4)));
            var bridge=new DebtSyncStore(source,G(1),()=>true,_=>true);
            var cw=bridge.ExportCustomer(G(3));var ew=bridge.ExportEvent(G(5));var wire=Packet(cw,ew,sale);
            var p=Fresh();
            Check(Inbox(p,_=>null).Receive(wire,1)==DebtReceiveStatus.WaitingForDependency,"Missing user accepted");Empty(p);
            Check(Inbox(p).Receive(wire,2)==DebtReceiveStatus.WaitingForDependency,"Missing metadata accepted");Empty(p);
            Product(p);Check(Inbox(p).Receive(wire,3)==DebtReceiveStatus.WaitingForDependency,"Missing warehouse accepted");Empty(p);
            Warehouse(p);Stocks(p);
            Check(Inbox(p,_=>0).Receive(wire,4)==DebtReceiveStatus.WaitingForDependency,"Invalid mapped user accepted");Empty(p);
            Check(Inbox(p).ReadPending(G(5))!.ReceivedAt==1,"First pending timestamp changed");
            var before=Count(p,"sync_journal");
            Check(Inbox(p).Receive(wire,5)==DebtReceiveStatus.Applied,"Opening not applied");
            Check(Stock(p)==-1 && Convert.ToDouble(Value(p,$"SELECT stock_quantity FROM products WHERE guid='{G(11)}'"))==3,"Repeated rows/negative aggregate wrong");
            Check(Convert.ToInt64(Value(p,$"SELECT updated_at FROM product_stocks WHERE warehouse_guid='{G(12)}'"))==500,"Old sale regressed stock timestamp");
            Check(Count(p,"sale_items")==2 && Count(p,"debt_accounts")==1 && Repo(p).ReadAccounts(G(3)).Single().BalanceMinor==15000,"Ledger/item mismatch");
            Check(Convert.ToInt64(Value(p,"SELECT user_id FROM sales"))==2 && Convert.ToInt64(Value(p,"SELECT payment_type FROM sales"))==3,"Actor mapping/payment lost");
            Check((string?)Value(p,"SELECT product_name FROM sale_items ORDER BY id LIMIT 1")=="Old cable" && (string?)Value(p,"SELECT warehouse_name FROM sale_items ORDER BY id LIMIT 1")=="Old warehouse","Current metadata replaced snapshot");
            Check(Convert.ToDouble(Value(p,"SELECT usd_rate FROM sales"))==12000 && Convert.ToDouble(Value(p,"SELECT tax_amount FROM sales"))==0.6 && Convert.ToDouble(Value(p,"SELECT total_cost FROM sales"))==120,"Historical FX/cost/fee lost");
            Check(Inbox(p).ReadPending(G(5))==null && Inbox(p).ExportApplied(G(5))==wire,"Completion/relay missing");
            Check(Count(p,"sync_journal")==before+5 && Convert.ToInt64(Value(p,"SELECT COUNT(*) FROM sync_journal WHERE kind LIKE 'debt_%' AND acked<>-1"))==0,"Echo/HELD error");
            SqliteConnection.ClearAllPools();var count=Count(p,"sync_journal");
            Check(Inbox(p).Receive(wire,6)==DebtReceiveStatus.AlreadyApplied && Stock(p)==-1 && Count(p,"sync_journal")==count,"Restart duplicate deducted again");
            Sql(p,$"UPDATE product_stocks SET quantity=7 WHERE warehouse_guid='{G(12)}'; UPDATE products SET stock_quantity=11");
            Check(Inbox(p).Receive(wire,7)==DebtReceiveStatus.AlreadyApplied && Stock(p)==7,"Replay used today's balance");
            Reject(()=>new DebtEnvelopeInbox(p,G(1),()=>true,_=>false,_=>2).Receive(wire,7));
            var changed=Packet(cw,ew,sale with{Items=new[]{sale.Items[0] with{ProductName="Changed"},sale.Items[1]}});
            Reject(()=>Inbox(p).Receive(changed,8));
            Sql(p,"UPDATE sale_items SET price_at_sale=101 WHERE id=(SELECT MIN(id) FROM sale_items)");Reject(()=>Inbox(p).Receive(wire,9));Reject(()=>Inbox(p).ExportApplied(G(5)));
            var concurrent=Fresh();Dependencies(concurrent);var results=new DebtReceiveStatus[2];
            Task.WaitAll(Task.Run(()=>results[0]=Inbox(concurrent).Receive(wire,1)),Task.Run(()=>results[1]=Inbox(concurrent).Receive(wire,1)));
            Check(results.Count(x=>x==DebtReceiveStatus.Applied)==1 && results.Count(x=>x==DebtReceiveStatus.AlreadyApplied)==1 && Stock(concurrent)==-1,"Concurrent opening duplicated");
            Sql(concurrent,$"UPDATE sync_journal SET delta=-9 WHERE op_id='{G(13)}'");Reject(()=>Inbox(concurrent).Receive(wire,2));
            foreach(var trigger in new[]{
                "BEFORE INSERT ON sales", "BEFORE INSERT ON sale_items", $"BEFORE INSERT ON sale_items WHEN NEW.guid='{G(20)}'", "BEFORE INSERT ON sync_journal WHEN NEW.kind='debt_stock'",
                "BEFORE UPDATE ON product_stocks", "BEFORE UPDATE ON products", "BEFORE INSERT ON debt_events",
                "BEFORE INSERT ON debt_accounts", "BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%'", "BEFORE DELETE ON debt_sync_inbox"}) {
                var failed=Fresh();Dependencies(failed);
                new DebtEnvelopeInbox(failed,G(1),()=>true,_=>true).Receive(wire,1);
                var journal=Count(failed,"sync_journal");
                Sql(failed,"CREATE TRIGGER fail_boundary "+trigger+" BEGIN SELECT RAISE(ABORT,'Injected failure'); END");
                Reject(()=>Inbox(failed).Receive(wire,2));Empty(failed);
                Check(Stock(failed)==1 && Count(failed,"sync_journal")==journal && Inbox(failed).ReadPending(G(5))!.Reason=="sale_adapter_pending" && Inbox(failed).ExportApplied(G(5))==null,"Failure leaked stock/journal/receipt/inbox");
                Sql(failed,"DROP TRIGGER fail_boundary");Check(Inbox(failed).Receive(wire,3)==DebtReceiveStatus.Applied && Stock(failed)==-1,"Rollback retry failed");
            }
            var collision=Fresh();Dependencies(collision);
            Sql(collision,$"INSERT INTO sync_journal(op_id,kind,entity_guid) VALUES('{G(13)}','stock','other')");Reject(()=>Inbox(collision).Receive(wire,1));Empty(collision);Check(Stock(collision)==1,"Colliding movement changed stock");
            var existing=Fresh();Dependencies(existing);Sql(existing,$"INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at) VALUES('{G(4)}',200,120,3,100)");Reject(()=>Inbox(existing).Receive(wire,1));Check(Count(existing,"debt_accounts")==0 && Stock(existing)==1,"Attached unrelated legacy sale");
            var absentStock=Fresh();Product(absentStock);Warehouse(absentStock);
            Check(Inbox(absentStock).Receive(wire,1)==DebtReceiveStatus.Applied && Stock(absentStock)==-2,"Missing stock row did not start at zero");
            var unsafeDb=Fresh();Dependencies(unsafeDb);
            var unsafeSale=sale with{TotalMinor=199999999999800,Items=sale.Items.Select(i=>i with{Price="999999999999.00000001"}).ToArray()};
            Reject(()=>Inbox(unsafeDb).Receive(Packet(cw,ew,unsafeSale),1));Empty(unsafeDb);Check(Stock(unsafeDb)==1,"Unsafe REAL silently rounded");
            Sql(unsafeDb,$"UPDATE product_stocks SET quantity=9007199254740992 WHERE warehouse_guid='{G(12)}'");
            Reject(()=>Inbox(unsafeDb).Receive(wire,2));Empty(unsafeDb);
            Console.WriteLine("PASS desktop debt sale receiver: actual frozen sale/items/stock/ledger, dependencies, trusted user, exact REAL, negative stock, replay/restart/concurrency, collision/tamper, ten-boundary rollback, HELD");
        } finally {SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
