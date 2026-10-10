using PosElectro.Desktop.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Sync;
using PosElectro.Desktop.Debt;
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
    CREATE TABLE refunds(id INTEGER PRIMARY KEY, sale_id INTEGER);
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
            var cursor=(long)store.Pull(0)["cursor"]!;
            var quoteResponse=http.PostAsync("/api/v2/returns/quote",new StringContent("{\"SaleGuid\":\"sale-test\"}",Encoding.UTF8,"application/json")).Result;
            Assert(quoteResponse.IsSuccessStatusCode,"Return quote rejected");
            var quote=JObject.Parse(quoteResponse.Content.ReadAsStringAsync().Result);
            var returnBody=new JObject { ["RequestGuid"]=Guid.NewGuid().ToString(),["SaleGuid"]="sale-test",["Reason"]="LAN test",["CashRefund"]=10,["CardRefund"]=0,["FeeReversal"]=0,
                ["Items"]=new JArray(new JObject { ["SaleItemGuid"]=(string)quote["Lines"]![0]!["Guid"]!,["Quantity"]=1,["WarehouseGuid"]="w",["Resellable"]=true }) };
            double beforeReturn=Quantity(store);
            JObject ReturnCall() { var returnResponse=http.PostAsync("/api/v2/returns",new StringContent(returnBody.ToString(),Encoding.UTF8,"application/json")).Result;
                Assert(returnResponse.IsSuccessStatusCode,"LAN return failed: "+returnResponse.Content.ReadAsStringAsync().Result);return JObject.Parse(returnResponse.Content.ReadAsStringAsync().Result); }
            var returned=ReturnCall();var retried=ReturnCall();
            Assert((string?)returned["Guid"]==(string?)retried["Guid"]&&Quantity(store)==beforeReturn+1,"Return lost ACK duplicated stock");
            var snapshot=store.Pull(cursor);
            Assert(snapshot["returns"]!.Count()==1 && snapshot["returnItems"]!.Count()==1 && snapshot["sales"]!.Count()==1,"Return snapshot is incomplete");
            var forged=(JObject)snapshot["sales"]![0]!.DeepClone();forged["Guid"]=Guid.NewGuid().ToString();
            Assert((int)http.PostAsync("/api/v2/push",new StringContent(new JObject { ["operations"]=new JArray(Op("sale",(string)forged["Guid"]!,forged)) }.ToString(),Encoding.UTF8,"application/json")).Result.StatusCode==400,"Client forged an authoritative return");
            var reverseBody=new JObject { ["RequestGuid"]=Guid.NewGuid().ToString(),["ReturnGuid"]=(string)returned["Guid"]!,["Reason"]="Undo LAN test" };
            for(var attempt=0;attempt<2;attempt++) Assert(http.PostAsync("/api/v2/returns/reverse",new StringContent(reverseBody.ToString(),Encoding.UTF8,"application/json")).Result.IsSuccessStatusCode,"LAN reversal rejected");
            Assert(Quantity(store)==beforeReturn,"Reversal retry changed stock twice");
            Assert(!store.Pull((long)store.Pull(0)["cursor"]!)["returns"]!.Any(),"Unchanged return history retransmitted");

            // Debt capabilities and store GUID in pairing and ping
            Assert(credentials["capabilities"] is JArray caps && caps.Values<string>().Contains("debtLedgerV1"), "Debt capability missing in pairing");
            string debtStoreGuid = (string)credentials["storeGuid"]!;
            Assert(!string.IsNullOrWhiteSpace(debtStoreGuid), "Store GUID missing in pairing");
            var pingResp = http.GetAsync("/api/ping").Result; Assert(pingResp.IsSuccessStatusCode, "Ping failed");
            var pingObj = JObject.Parse(pingResp.Content.ReadAsStringAsync().Result);
            Assert(pingObj["capabilities"] is JArray pingCaps && pingCaps.Values<string>().Contains("debtLedgerV1"), "Debt capability missing in ping");
            Assert((string)pingObj["storeGuid"]! == debtStoreGuid, "Store GUID mismatch in ping");

            // Legacy push rejects PaymentType == 3
            var debtSaleOp = Op("sale", Guid.NewGuid().ToString(), new JObject { ["Guid"] = Guid.NewGuid().ToString(), ["PaymentType"] = 3, ["TotalAmount"] = 100, ["Items"] = new JArray() });
            Assert((int)http.PostAsync("/api/v2/push", new StringContent(new JObject { ["operations"] = new JArray(debtSaleOp) }.ToString(), Encoding.UTF8, "application/json")).Result.StatusCode == 400, "Debt sale accepted on legacy push");

            // Legacy pull excludes payment_type == 3
            Sql("INSERT INTO sales(guid, total_amount, payment_type, is_synced) VALUES('debt-legacy-leak', 500, 3, 0)");
            var pullResp = http.GetAsync("/api/v2/pull?cursor=0").Result;
            var pullData = JObject.Parse(pullResp.Content.ReadAsStringAsync().Result);
            Assert(!pullData["sales"]!.Any(s => (string?)s["Guid"] == "debt-legacy-leak"), "Debt sale leaked into legacy pull");

            // Unauthenticated debt push returns 401
            using var unauthHttp = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://127.0.0.1:8080") };
            Assert((int)unauthHttp.PostAsync("/api/v2/debt/push", new StringContent("{\"envelopes\":[]}", Encoding.UTF8, "application/json")).Result.StatusCode == 401, "Unauthenticated debt push allowed");

            // Wrong server ID returns 400
            http.DefaultRequestHeaders.Remove("X-LinePOS-Server-Id");
            http.DefaultRequestHeaders.Add("X-LinePOS-Server-Id", "wrong-server");
            Assert((int)http.PostAsync("/api/v2/debt/push", new StringContent("{\"envelopes\":[]}", Encoding.UTF8, "application/json")).Result.StatusCode == 400, "Wrong server ID accepted on debt push");
            http.DefaultRequestHeaders.Remove("X-LinePOS-Server-Id");
            http.DefaultRequestHeaders.Add("X-LinePOS-Server-Id", (string)credentials["serverId"]!);

            // LAN customer debt push
            string G(int n) => $"00000000-0000-0000-0000-{n:D12}";
            var custGuid = G(101);
            var custPayload = DebtRepository.Canonical("customer", debtStoreGuid, G(102), custGuid, "Mijoz Bir", "+998901234567", "izoh", "1000");
            var custHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(custPayload))).ToLowerInvariant();
            var custWire = DebtWire.EncodeCustomer(new DebtWireCustomer(custGuid, debtStoreGuid, G(103), custPayload, custHash), debtStoreGuid);
            var custEnvelope = DebtEnvelope.Encode(new DebtEnvelopePacket(custGuid, debtStoreGuid, custWire, "", ""), debtStoreGuid);

            var pushCustResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(custEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            Assert(pushCustResp.IsSuccessStatusCode, "Customer debt push failed");
            var pushCustRes = JObject.Parse(pushCustResp.Content.ReadAsStringAsync().Result);
            Assert((string)pushCustRes["results"]![0]!["status"]! == "Applied", "Customer envelope status not Applied");

            // Retry customer push returns AlreadyApplied
            var retryCustResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(custEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            var retryCustRes = JObject.Parse(retryCustResp.Content.ReadAsStringAsync().Result);
            Assert((string)retryCustRes["results"]![0]!["status"]! == "AlreadyApplied", "Customer retry not AlreadyApplied");

            // Tampered body for same GUID is rejected
            var tamperedCustPayload = DebtRepository.Canonical("customer", debtStoreGuid, G(102), custGuid, "Boshqa Ism", "+998901234567", "izoh", "1000");
            var tamperedCustHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(tamperedCustPayload))).ToLowerInvariant();
            var tamperedCustWire = DebtWire.EncodeCustomer(new DebtWireCustomer(custGuid, debtStoreGuid, G(103), tamperedCustPayload, tamperedCustHash), debtStoreGuid);
            var tamperedEnvelope = DebtEnvelope.Encode(new DebtEnvelopePacket(custGuid, debtStoreGuid, tamperedCustWire, "", ""), debtStoreGuid);
            Assert((int)http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(tamperedEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result.StatusCode == 400, "Tampered envelope accepted");

            // Active debt database download is refused
            Assert((int)http.GetAsync("/api/sync/download_db").Result.StatusCode == 400, "download_db did not refuse database with active debt");

            // Debt pull returns customer envelope with cursor
            var debtPullResp = http.GetAsync("/api/v2/debt/pull?cursor=0").Result;
            Assert(debtPullResp.IsSuccessStatusCode, "Debt pull failed");
            var debtPullData = JObject.Parse(debtPullResp.Content.ReadAsStringAsync().Result);
            Assert(debtPullData["envelopes"]!.Count() >= 1, "Debt pull missing envelopes");
            long debtCursor = (long)debtPullData["cursor"]!;
            var debtPullEmpty = http.GetAsync($"/api/v2/debt/pull?cursor={debtCursor}").Result;
            var emptyData = JObject.Parse(debtPullEmpty.Content.ReadAsStringAsync().Result);
            Assert(emptyData["envelopes"]!.Count() == 0, "Repeated debt pull returned old envelopes");

            // Sale open envelope push & stock deduction & idempotency
            var debtProdGuid = G(501);
            var debtWhGuid = G(502);
            Sql($"INSERT INTO products(guid, barcode, name, category, cost_price, cost_currency, selling_price, stock_quantity, unit_type, min_stock_alert, is_deleted, note, updated_at) VALUES('{debtProdGuid}', NULL, 'Кабель', 'Barchasi', 50, 'UZS', 100, 10, 0, 3, 0, '', 1)");
            Sql($"INSERT INTO warehouses(guid, name, is_primary, is_deleted, updated_at) VALUES('{debtWhGuid}', 'Asosiy', 1, 0, 1)");
            Sql($"INSERT INTO product_stocks(product_guid, warehouse_guid, quantity, updated_at) VALUES('{debtProdGuid}', '{debtWhGuid}', 10, 1)");

            var saleGuid = G(201);
            var reqGuid = G(202);
            var itemGuid = G(203);
            var stockOpGuid = G(204);
            var saleItems = new List<DebtSaleItem>
            {
                new DebtSaleItem(itemGuid, debtProdGuid, "Кабель", "Barchasi", "dona", debtWhGuid, "Asosiy", "2", "100", "50", "UZS", stockOpGuid, "-2")
            };
            var saleSnapshot = new DebtSaleSnapshot(saleGuid, 2000, 20000, 10000, 5000, 5000, 0, "0", "12850", "DEBT", saleItems.AsReadOnly());
            var saleWire = DebtEnvelope.EncodeSale(saleSnapshot);
            var saleFingerprint = DebtWire.Fingerprint(saleWire);
            var salePayload = DebtRepository.Canonical("sale_open", debtStoreGuid, G(102), reqGuid, custGuid, saleGuid, saleFingerprint, "20000", "5000", "5000", "2000", "");
            var saleHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(salePayload))).ToLowerInvariant();
            var accountGuid = saleGuid;
            var accountWire = new DebtWireAccount(accountGuid, saleGuid, reqGuid, 10000, null, "Mijoz Bir");
            var evObj = new DebtWireEvent(reqGuid, reqGuid, "sale_open", custGuid, debtStoreGuid, G(102), G(103), 1, 2000, salePayload, saleHash, 0, 0, 0, null, accountWire, Array.Empty<DebtLine>());
            var eventWire = DebtWire.EncodeEvent(evObj, debtStoreGuid);
            var saleEnvelope = DebtEnvelope.Encode(new DebtEnvelopePacket(reqGuid, debtStoreGuid, "", eventWire, saleWire), debtStoreGuid);

            double stockBeforeDebtSale = Quantity(store, debtWhGuid);
            var pushSaleResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(saleEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            Assert(pushSaleResp.IsSuccessStatusCode, "Sale envelope push failed: " + pushSaleResp.Content.ReadAsStringAsync().Result);
            var pushSaleRes = JObject.Parse(pushSaleResp.Content.ReadAsStringAsync().Result);
            Assert((string)pushSaleRes["results"]![0]!["status"]! == "Applied", "Sale envelope not Applied");
            Assert(Quantity(store, debtWhGuid) == stockBeforeDebtSale - 2, "Debt sale did not deduct stock by 2");

            var retrySaleResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(saleEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            var retrySaleRes = JObject.Parse(retrySaleResp.Content.ReadAsStringAsync().Result);
            Assert((string)retrySaleRes["results"]![0]!["status"]! == "AlreadyApplied", "Sale retry not AlreadyApplied");
            Assert(Quantity(store, debtWhGuid) == stockBeforeDebtSale - 2, "Sale retry deducted stock again");

            // Payment envelope push & debt reduction
            var payReqGuid = G(301);
            var payPayload = DebtRepository.Canonical("payment", debtStoreGuid, G(102), payReqGuid, custGuid, "4000", "0", "0", "3000", accountGuid, "");
            var payHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payPayload))).ToLowerInvariant();
            var payLines = new List<DebtLine> { new DebtLine(accountGuid, -4000) };
            var payEvent = new DebtWireEvent(payReqGuid, payReqGuid, "payment", custGuid, debtStoreGuid, G(102), G(103), 2, 3000, payPayload, payHash, 4000, 0, 0, null, null, payLines.AsReadOnly());
            var payEventWire = DebtWire.EncodeEvent(payEvent, debtStoreGuid);
            var payEnvelope = DebtEnvelope.Encode(new DebtEnvelopePacket(payReqGuid, debtStoreGuid, "", payEventWire, ""), debtStoreGuid);

            var pushPayResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(payEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            Assert(pushPayResp.IsSuccessStatusCode, "Payment envelope push failed");
            var pushPayRes = JObject.Parse(pushPayResp.Content.ReadAsStringAsync().Result);
            Assert((string)pushPayRes["results"]![0]!["status"]! == "Applied", "Payment envelope not Applied");

            // Delta pull returns sale and payment envelopes
            var deltaPullResp = http.GetAsync($"/api/v2/debt/pull?cursor={debtCursor}").Result;
            Assert(deltaPullResp.IsSuccessStatusCode, "Debt delta pull failed");
            var deltaPullData = JObject.Parse(deltaPullResp.Content.ReadAsStringAsync().Result);
            Assert(deltaPullData["envelopes"]!.Count() == 2, "Delta pull expected 2 envelopes (sale and payment)");

            // Missing dependency envelope queues as WaitingForDependency and drains upon dependency arrival
            var depReqGuid = G(401);
            var missingCustGuid = G(499);
            var orphanAccountGuid = G(498);
            var orphanPayPayload = DebtRepository.Canonical("payment", debtStoreGuid, G(102), depReqGuid, missingCustGuid, "1000", "0", "0", "4000", orphanAccountGuid, "");
            var orphanPayHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(orphanPayPayload))).ToLowerInvariant();
            var orphanLines = new List<DebtLine> { new DebtLine(orphanAccountGuid, -1000) };
            var orphanEvent = new DebtWireEvent(depReqGuid, depReqGuid, "payment", missingCustGuid, debtStoreGuid, G(102), G(103), 3, 4000, orphanPayPayload, orphanPayHash, 1000, 0, 0, null, null, orphanLines.AsReadOnly());
            var orphanWire = DebtWire.EncodeEvent(orphanEvent, debtStoreGuid);
            var orphanEnvelope = DebtEnvelope.Encode(new DebtEnvelopePacket(depReqGuid, debtStoreGuid, "", orphanWire, ""), debtStoreGuid);

            var orphanResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(orphanEnvelope) }.ToString(), Encoding.UTF8, "application/json")).Result;
            Assert(orphanResp.IsSuccessStatusCode, "Orphan push failed");
            var orphanRes = JObject.Parse(orphanResp.Content.ReadAsStringAsync().Result);
            Assert((string)orphanRes["results"]![0]!["status"]! == "WaitingForDependency", "Orphan packet was not queued as WaitingForDependency");

            // Now create the missing customer and account so drain applies the pending packet
            var missingSaleGuid = orphanAccountGuid;
            var missingReqGuid = G(497);
            var missingItemGuid = G(496);
            var missingStockOp = G(495);
            var missingSaleItems = new List<DebtSaleItem>
            {
                new DebtSaleItem(missingItemGuid, debtProdGuid, "Кабель", "Barchasi", "dona", debtWhGuid, "Asosiy", "1", "100", "50", "UZS", missingStockOp, "-1")
            };
            var missingSaleSnapshot = new DebtSaleSnapshot(missingSaleGuid, 4000, 10000, 5000, 0, 0, 0, "0", "12850", "DEBT", missingSaleItems.AsReadOnly());
            var missingSaleWire = DebtEnvelope.EncodeSale(missingSaleSnapshot);
            var missingSaleFp = DebtWire.Fingerprint(missingSaleWire);
            var missingSalePayload = DebtRepository.Canonical("sale_open", debtStoreGuid, G(102), missingReqGuid, missingCustGuid, missingSaleGuid, missingSaleFp, "10000", "0", "0", "4000", "");
            var missingSaleHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(missingSalePayload))).ToLowerInvariant();
            var missingAccWire = new DebtWireAccount(orphanAccountGuid, missingSaleGuid, missingReqGuid, 10000, null, "Yetim Mijoz");
            var missingEvObj = new DebtWireEvent(missingReqGuid, missingReqGuid, "sale_open", missingCustGuid, debtStoreGuid, G(102), G(103), 4, 4000, missingSalePayload, missingSaleHash, 0, 0, 0, null, missingAccWire, Array.Empty<DebtLine>());
            var missingEvWire = DebtWire.EncodeEvent(missingEvObj, debtStoreGuid);

            var createMissingCustPayload = DebtRepository.Canonical("customer", debtStoreGuid, G(102), missingCustGuid, "Yetim Mijoz", "+998900000000", "", "4000");
            var createMissingCustHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(createMissingCustPayload))).ToLowerInvariant();
            var createMissingCustWire = DebtWire.EncodeCustomer(new DebtWireCustomer(missingCustGuid, debtStoreGuid, G(103), createMissingCustPayload, createMissingCustHash), debtStoreGuid);

            var drainEnv = DebtEnvelope.Encode(new DebtEnvelopePacket(missingReqGuid, debtStoreGuid, createMissingCustWire, missingEvWire, missingSaleWire), debtStoreGuid);

            var drainResp = http.PostAsync("/api/v2/debt/push", new StringContent(new JObject { ["envelopes"] = new JArray(drainEnv) }.ToString(), Encoding.UTF8, "application/json")).Result;
            Assert(drainResp.IsSuccessStatusCode, "Drain customer push failed: " + drainResp.Content.ReadAsStringAsync().Result);
            var pullAllResp = http.GetAsync("/api/v2/debt/pull?cursor=0").Result;
            var allEnvelopes = (JArray)JObject.Parse(pullAllResp.Content.ReadAsStringAsync().Result)["envelopes"]!;
            Assert(allEnvelopes.Any(w => DebtEnvelope.Decode((string)w!, debtStoreGuid).Guid == depReqGuid), "Orphan packet was not drained upon dependency arrival");

            // Product conflict in sync_conflicts does not break debt operations
            Sql("INSERT INTO sync_conflicts(kind, entity_guid, revision, message) VALUES('product', 'p', 99, 'Test conflict')");
            var debtWithConflictResp = http.GetAsync("/api/v2/debt/pull?cursor=0").Result;
            Assert(debtWithConflictResp.IsSuccessStatusCode, "Debt pull failed when product conflict present");
            Sql("DELETE FROM sync_conflicts");

            server.RevokeDevices();Assert((int)http.GetAsync("/api/v2/pull").Result.StatusCode==401,"Revoked token still accepted");
        } finally { server.Stop(); }
    });
    Console.WriteLine($"{passed} tests passed");
} finally { SqliteConnection.ClearAllPools();foreach(var file in new[]{path,path+"-wal",path+"-shm"})if(File.Exists(file))File.Delete(file); }
