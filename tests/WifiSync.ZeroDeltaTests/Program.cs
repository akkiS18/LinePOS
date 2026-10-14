using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Sync;

if (args.Length != 2) throw new ArgumentException("Usage: desktop.db mobile.db (read-only sources)");
using var desktopSource = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=args[0], Mode=SqliteOpenMode.ReadOnly }.ToString());
using var mobile = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=args[1], Mode=SqliteOpenMode.ReadOnly }.ToString());
desktopSource.Open(); mobile.Open();
var path=Path.Combine(Path.GetTempPath(),"linepos-zero-"+Guid.NewGuid()+".db");
try {
    using (var target=new SqliteConnection("Data Source="+path)) { target.Open();desktopSource.BackupDatabase(target); }
    var store=new WifiSyncStore(path);
    using var db=store.Open();
    object? Scalar(string sql) { using var cmd=db.CreateCommand();cmd.CommandText=sql;return cmd.ExecuteScalar(); }
    string Snapshot() {
        var root=new JObject();
        foreach(var table in new[]{"products","warehouses","product_stocks","sales","sale_items","returns","return_items","debt_accounts","debt_events","debt_event_lines"}) {
            using var cmd=db.CreateCommand();cmd.CommandText=$"SELECT * FROM {table} ORDER BY rowid";
            using var rows=cmd.ExecuteReader();var list=new JArray();
            while(rows.Read()) {var row=new JArray();for(int i=0;i<rows.FieldCount;i++)row.Add(rows.IsDBNull(i)?JValue.CreateNull():JToken.FromObject(rows.GetValue(i)));list.Add(row);}
            root[table]=list;
        }
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }
    void Check(bool ok,string label) { if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label); }
    var ops=new JArray();
    using(var cmd=mobile.CreateCommand()) {
        cmd.CommandText="SELECT payload FROM sync_journal WHERE acked=0 ORDER BY seq";
        using var rows=cmd.ExecuteReader();while(rows.Read())ops.Add(JObject.Parse(rows.GetString(0)));
    }
    Check(ops.Count==7 && ops.All(x=>(string?)x["kind"]=="stock" && (double)x["data"]!["Delta"]! == 0),"reported seven zero-delta operations present");
    var before=Snapshot();var journalBefore=Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sync_journal"));
    var first=store.Push(ops);
    Check(first["accepted"]!.Count()==7 && Snapshot()==before,"seven operations ACK without financial/stock changes");
    Check(Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sync_journal"))==journalBefore+7,"operation IDs durably journaled");
    var replay=store.Push(ops);
    Check(JToken.DeepEquals(first,replay) && Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sync_journal"))==journalBefore+7 && Snapshot()==before,"retry is idempotent");
    var changed=(JObject)ops[0].DeepClone();changed["data"]!["Delta"]=1;
    bool rejected=false;try {store.Push(new JArray(changed));}catch(ArgumentException){rejected=true;}
    Check(rejected && Snapshot()==before,"same ID with changed payload rejected");
    var bad=(JObject)ops[0].DeepClone();bad["id"]=Guid.NewGuid().ToString();bad["guid"]="missing-product";bad["data"]!["Delta"]=1;
    rejected=false;try{store.Push(new JArray(bad));}catch(ArgumentException){rejected=true;}
    Check(rejected && Snapshot()==before,"nonzero missing product rejected");
    var valid=(JObject)ops[0].DeepClone();valid["id"]=Guid.NewGuid().ToString();valid["data"]!["Delta"]=-1;
    var count=Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sync_journal"));
    rejected=false;try{store.Push(new JArray(valid,bad));}catch(ArgumentException){rejected=true;}
    Check(rejected && Snapshot()==before && Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sync_journal"))==count,"invalid batch rolls back preceding stock change");
    var pull=store.Pull(0);
    var localReturns=new HashSet<string>();
    using(var cmd=mobile.CreateCommand()) {cmd.CommandText="SELECT guid FROM returns";using var rows=cmd.ExecuteReader();while(rows.Read())localReturns.Add(rows.GetString(0));}
    Check(pull["returns"]!.Count()==3 && pull["returns"]!.Count(x=>!localReturns.Contains((string)x["guid"]!))==1,"pull contains missing return");
    Check(pull["sales"]!.Any(s=>(string?)s["Guid"]=="64f35e30-0c91-496f-83f6-6b4f810e83b4"),"pull contains return receipt");
} finally { SqliteConnection.ClearAllPools();if(File.Exists(path))File.Delete(path); }
