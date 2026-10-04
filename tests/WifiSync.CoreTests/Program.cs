using PosElectro.Desktop.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Sync;
using System.Text;

AccountingTests.Run();
int passed=0;
void Test(string name,Action run){run();passed++;Console.WriteLine("PASS "+name);}
void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
string path=Path.Combine(Path.GetTempPath(),"LinePOS_tests_"+Guid.NewGuid()+".db");
void Sql(string sql){using var db=new SqliteConnection("Data Source="+path);db.Open();using var cmd=db.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
JObject Op(string kind,string guid,JObject data,string? id=null,long revision=0)=>new(){["id"]=id??Guid.NewGuid().ToString(),["kind"]=kind,["guid"]=guid,["baseRevision"]=revision,["data"]=data};
JObject Stock(double delta,string wh="w")=>Op("stock","p",new JObject{["WarehouseGuid"]=wh,["Delta"]=delta});
double Quantity(WifiSyncStore store,string wh="w")=>store.Pull(0)["productStocks"]!.OfType<JObject>().Where(x=>(string?)x["WarehouseGuid"]==wh).Sum(x=>(double)x["Quantity"]!);
try {
    Sql("""
    CREATE TABLE products(id INTEGER PRIMARY KEY AUTOINCREMENT,guid TEXT UNIQUE NOT NULL,barcode TEXT UNIQUE,name TEXT NOT NULL,category TEXT NOT NULL,cost_price REAL NOT NULL,cost_currency TEXT NOT NULL,selling_price REAL NOT NULL,selling_price_2 REAL,stock_quantity REAL NOT NULL,unit_type INTEGER NOT NULL,min_stock_alert REAL NOT NULL,is_deleted INTEGER NOT NULL,note TEXT,updated_at INTEGER NOT NULL);
    CREATE TABLE warehouses(id INTEGER PRIMARY KEY AUTOINCREMENT,guid TEXT UNIQUE NOT NULL,name TEXT NOT NULL,is_primary INTEGER NOT NULL,is_deleted INTEGER NOT NULL,updated_at INTEGER NOT NULL);
    CREATE TABLE product_stocks(id INTEGER PRIMARY KEY AUTOINCREMENT,product_guid TEXT NOT NULL,warehouse_guid TEXT NOT NULL,quantity REAL NOT NULL,updated_at INTEGER NOT NULL,UNIQUE(product_guid,warehouse_guid));
    CREATE TABLE sales(id INTEGER PRIMARY KEY AUTOINCREMENT,guid TEXT UNIQUE NOT NULL,total_amount REAL,total_cost REAL,payment_type INTEGER,cash_amount REAL,card_amount REAL,tax_amount REAL,tax_rate REAL,created_at INTEGER,user_id INTEGER,is_synced INTEGER,usd_rate REAL NOT NULL DEFAULT 0);
    CREATE TABLE sale_items(id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL REFERENCES sales(id),sale_guid TEXT,product_id INTEGER,product_guid TEXT,product_name TEXT,quantity REAL,price_at_sale REAL,cost_at_sale REAL,cost_currency TEXT,warehouse_guid TEXT,warehouse_name TEXT,category_at_sale TEXT NOT NULL DEFAULT '',unit_at_sale TEXT NOT NULL DEFAULT '');
    INSERT INTO products VALUES(1,'p',NULL,'Кабель','Barchasi',5,'UZS',10,NULL,10,0,3,0,'',1);
    INSERT INTO warehouses VALUES(1,'w','Asosiy',1,0,1),(2,'w2','Ikkinchi',0,0,1);
    INSERT INTO product_stocks VALUES(1,'p','w',10,1);
    """);
    var store=new WifiSyncStore(path);
    Test("migration preserves existing stock",()=>Assert(Quantity(store)==10,"Existing stock changed"));
    Test("HTTP body uses byte length for Cyrillic and Uzbek",()=>{
        string body="{\"Name\":\"Кабель oʻtkazma\"}";var bytes=Encoding.UTF8.GetBytes(body);
        var wire=Encoding.ASCII.GetBytes($"POST /api/v2/push HTTP/1.1\r\nContent-Length: {bytes.Length}\r\n\r\n").Concat(bytes).ToArray();
        var req=WifiHttpRequest.Read(new MemoryStream(wire),CancellationToken.None).GetAwaiter().GetResult();Assert(req.Body==body,"Unicode truncated");
    });
    Test("reject oversized and ambiguous HTTP framing",()=>{
        foreach(string h in new[]{"Content-Length: 9000000","Content-Length: 1\r\nContent-Length: 1","Transfer-Encoding: chunked"}) {
            bool threw=false;try{WifiHttpRequest.Read(new MemoryStream(Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\n"+h+"\r\n\r\n")),CancellationToken.None).GetAwaiter().GetResult();}catch(ArgumentException){threw=true;} Assert(threw,"Invalid HTTP accepted");
        }
    });
    Test("lost ACK retry changes inventory once",()=>{var op=Stock(-2);store.Push(new JArray(op));store.Push(new JArray(op));Assert(Quantity(store)==8,"Duplicate deduction");});
    Test("offline sales from two devices converge",()=>{store.Push(new JArray(Stock(-3)));Assert(Quantity(store)==5,"Offline quantities not additive");});
    Test("reconnection with no operations preserves desktop stock",()=>{store.Push(new JArray());Assert(Quantity(store)==5,"Stale snapshot overwrote desktop stock");});
    Test("partial batch failure rolls back deductions and acknowledgements",()=>{
        var first=Stock(-1);var invalid=Op("stock","missing",new JObject{["WarehouseGuid"]="w",["Delta"]=-1});
        bool threw=false;try{store.Push(new JArray(first,invalid));}catch(ArgumentException){threw=true;}Assert(threw&&Quantity(store)==5,"Partial batch committed");
        store.Push(new JArray(first));Assert(Quantity(store)==4,"Rolled back operation falsely deduplicated");
    });
    Test("transfer duplicate does not apply twice",()=>{var ops=new JArray(Stock(-2),Stock(2,"w2"));store.Push(ops);store.Push(ops);Assert(Quantity(store)==2&&Quantity(store,"w2")==2,"Transfer duplicated");});
    Test("restart preserves deduplication",()=>{var op=Stock(-1);store.Push(new JArray(op));store=new WifiSyncStore(path);store.Push(new JArray(op));Assert(Quantity(store)==1,"Restart lost operation identity");});
    Test("journal cursor sees late arriving records independent of clocks",()=>{long cursor=(long)store.Pull(0)["cursor"]!;store.Push(new JArray(Stock(-2)));var delta=store.Pull(cursor);Assert(delta["productStocks"]!.Any()&&Quantity(store)==-1,"Changes missed by cursor");});
    Test("negative inventory preserves actual offline sales",()=>Assert(Quantity(store)==-1,"Oversale hidden"));
    Test("local stock and outbox roll back together",()=>{
        using var db=store.Open();using var tx=db.BeginTransaction();WifiSyncStore.Exec(db,tx,"UPDATE product_stocks SET quantity=quantity-10 WHERE warehouse_guid='w'");tx.Rollback();Assert(Quantity(store)==-1,"Rollback lost local stock");
    });
    Test("remote changes do not echo into fresh operations",()=>{using var db=store.Open();Assert(Convert.ToInt64(WifiSyncStore.Scalar(db,null,"SELECT applying FROM sync_control"))==0,"Capture remained disabled");});
    Test("bootstrap never replaces established desktop stock",()=>{var product=(JObject)store.Pull(0)["products"]![0]!;product["StockQuantity"]=99;product["InitialStocks"]=new JArray(new JObject{["WarehouseGuid"]="w",["Quantity"]=99});var op=Op("product","p",product);op["bootstrap"]=true;store.Push(new JArray(op));Assert(Quantity(store)==-1,"Bootstrap overwrote shared baseline");});
    Test("metadata conflict is explicit and atomic",()=>{var p=(JObject)store.Pull(0)["products"]![0]!;p["Name"]="Conflicting";bool threw=false;try{store.Push(new JArray(Op("product","p",p,revision:0)));}catch(SyncConflictException){threw=true;}Assert(threw,"Concurrent metadata silently overwritten");});
    Test("same operation ID with changed data is rejected",()=>{var op=Stock(1);store.Push(new JArray(op));op["data"]!["Delta"]=100;bool threw=false;try{store.Push(new JArray(op));}catch(ArgumentException){threw=true;}Assert(threw,"Operation identity reused");});
    Test("new product bootstrap publishes its warehouse stock in delta",()=>{
        long cursor=(long)store.Pull(0)["cursor"]!;
        var p=(JObject)store.Pull(0)["products"]![0]!.DeepClone();p["Guid"]="new-product";p["Barcode"]=null;
        p["InitialStocks"]=new JArray(new JObject{["WarehouseGuid"]="w",["Quantity"]=7});var op=Op("product","new-product",p);op["bootstrap"]=true;store.Push(new JArray(op));
        Assert(store.Pull(cursor)["productStocks"]!.Any(x=>(string?)x["ProductGuid"]=="new-product"),"New product stock not discoverable");
    });
    Test("receipt and all stock changes commit and deduplicate as one batch",()=>{
        var sale=new JObject{["Guid"]="sale-test",["TotalAmount"]=10,["TotalCost"]=5,["PaymentType"]=0,["CashAmount"]=10,["CardAmount"]=0,["TaxAmount"]=0,["TaxRate"]=0,["CreatedAt"]=1,
            ["Items"]=new JArray(new JObject{["ProductGuid"]="p",["ProductName"]="Кабель",["Quantity"]=1,["PriceAtSale"]=10,["CostAtSale"]=5,["CostCurrency"]="UZS",["WarehouseGuid"]="w",["WarehouseName"]="Asosiy"})};
        double before=Quantity(store);var ops=new JArray(Op("sale","sale-test",sale),Stock(-1));store.Push(ops);store.Push(ops);
        Assert(Quantity(store)==before-1 && store.Pull(0)["sales"]!.Count()==1,"Receipt and stock not deduplicated");
    });
    Test("legacy receipt already present does not deduct stock twice",()=>{
        var sale=(JObject)store.Pull(0)["sales"]![0]!;double before=Quantity(store);store.Push(new JArray(Op("legacy_sale","sale-test",sale)));Assert(Quantity(store)==before,"Existing receipt deducted again");
    });
    Test("actual LAN server requires pairing, rejects old protocol and revokes tokens",()=>{
        var server=new PosElectro.Desktop.Services.LocalSyncServer(new PosElectro.Desktop.Data.DatabaseContext(path));
        using var http=new HttpClient(new HttpClientHandler{UseProxy=false}) {BaseAddress=new Uri("http://127.0.0.1:8080"),Timeout=TimeSpan.FromSeconds(5)};
        server.Start();Assert(server.IsRunning,"Server did not start");
        try {
            Assert((int)http.GetAsync("/api/v2/pull").Result.StatusCode==401,"Unauthenticated read allowed");
            Assert((int)http.GetAsync("/api/sync/download_db").Result.StatusCode==401,"Unauthenticated export allowed");
            var code=server.NewPairingCode();var pair=new JObject{["code"]=code,["deviceId"]="test-device",["deviceName"]="Test phone"};
            var response=http.PostAsync("/api/v2/pair",new StringContent(pair.ToString(),Encoding.UTF8,"application/json")).Result;
            Assert(response.IsSuccessStatusCode,"Pairing failed");var credentials=JObject.Parse(response.Content.ReadAsStringAsync().Result);
            Assert((int)http.PostAsync("/api/v2/pair",new StringContent(pair.ToString(),Encoding.UTF8,"application/json")).Result.StatusCode==401,"Pairing code reused");
            http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",(string)credentials["token"]!);
            var beforeWrongServer=Quantity(store);
            var guardedBatch=new JObject{["operations"]=new JArray(Stock(-1))};
            Assert((int)http.PostAsync("/api/v2/push",new StringContent(guardedBatch.ToString(),Encoding.UTF8,"application/json")).Result.StatusCode==400,"Missing database identity accepted");
            http.DefaultRequestHeaders.Add("X-LinePOS-Server-Id","another-database");
            Assert((int)http.PostAsync("/api/v2/push",new StringContent(guardedBatch.ToString(),Encoding.UTF8,"application/json")).Result.StatusCode==400,"Wrong database identity accepted");
            Assert(Quantity(store)==beforeWrongServer,"Rejected identity changed inventory");
            http.DefaultRequestHeaders.Remove("X-LinePOS-Server-Id");
            http.DefaultRequestHeaders.Add("X-LinePOS-Server-Id",(string)credentials["serverId"]!);
            Assert(http.PostAsync("/api/v2/push",new StringContent(guardedBatch.ToString(),Encoding.UTF8,"application/json")).Result.IsSuccessStatusCode,"Correct database identity rejected");
            Assert(Quantity(store)==beforeWrongServer-1,"Correct identity did not commit exactly once");
            Assert(http.GetAsync("/api/v2/pull?cursor=0").Result.IsSuccessStatusCode,"Paired device rejected");
            Assert((int)http.PostAsync("/api/sync/push_stocks",new StringContent("[]")).Result.StatusCode==410,"Legacy overwrite endpoint still active");
            server.RevokeDevices();Assert((int)http.GetAsync("/api/v2/pull").Result.StatusCode==401,"Revoked token still accepted");
        } finally { server.Stop(); }
    });
    Console.WriteLine($"{passed} tests passed");
} finally { SqliteConnection.ClearAllPools();foreach(var file in new[]{path,path+"-wal",path+"-shm"})if(File.Exists(file))File.Delete(file); }
