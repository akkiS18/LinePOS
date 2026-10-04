using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PosElectro.Desktop.Sync;

// All remote writes, deduplication and journal entries share one SQLite transaction.
public sealed class WifiSyncStore
{
    private readonly string connectionString;
    public WifiSyncStore(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, DefaultTimeout = 15 }.ToString();
        using var db = Open();
        using var tx = db.BeginTransaction();
        if (Convert.ToInt32(Scalar(db, tx, "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='guid'")) == 0)
            Exec(db, tx, "ALTER TABLE sale_items ADD COLUMN guid TEXT NOT NULL DEFAULT ''");
        Exec(db, tx, "UPDATE sale_items SET guid=(SELECT guid FROM sales WHERE id=sale_items.sale_id)||':'||(SELECT COUNT(*) FROM sale_items previous WHERE previous.sale_id=sale_items.sale_id AND previous.id<=sale_items.id) WHERE guid=''");
        using var schema = typeof(WifiSyncStore).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
        using var reader = new StreamReader(schema);
        foreach (var statement in reader.ReadToEnd().Split("-- statement")) Exec(db, tx, statement);
        Exec(db, tx, "CREATE TABLE IF NOT EXISTS sync_devices (token_hash TEXT PRIMARY KEY, device_id TEXT NOT NULL, device_name TEXT NOT NULL)");
        Exec(db, tx, "INSERT OR IGNORE INTO sync_meta(key,value) VALUES('server_id',@value)", ("@value", Guid.NewGuid().ToString()));
        tx.Commit();
    }
    public SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public string ServerId { get { using var db = Open(); return (string)Scalar(db, null, "SELECT value FROM sync_meta WHERE key='server_id'")!; } }
    internal static void Exec(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { using var cmd = Command(db, tx, sql, args); cmd.ExecuteNonQuery(); }
    internal static object? Scalar(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { using var cmd = Command(db, tx, sql, args); return cmd.ExecuteScalar(); }
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { var cmd=db.CreateCommand(); cmd.Transaction=tx; cmd.CommandText=sql; foreach(var (key,value) in args)cmd.Parameters.AddWithValue(key,value??DBNull.Value); return cmd; }
    public static readonly Dictionary<string,string> Fields = new()
    {
        ["id"]="Id",["guid"]="Guid",["barcode"]="Barcode",["name"]="Name",["category"]="Category",["cost_price"]="CostPrice",["cost_currency"]="CostCurrency",["selling_price"]="SellingPrice",["selling_price_2"]="SellingPrice2",["stock_quantity"]="StockQuantity",["unit_type"]="UnitType",["min_stock_alert"]="MinStockAlert",["is_deleted"]="IsDeleted",["note"]="Note",["updated_at"]="UpdatedAt",["is_primary"]="IsPrimary",["product_guid"]="ProductGuid",["warehouse_guid"]="WarehouseGuid",["quantity"]="Quantity",["sale_id"]="SaleId",["sale_guid"]="SaleGuid",["product_id"]="ProductId",["product_name"]="ProductName",["price_at_sale"]="PriceAtSale",["cost_at_sale"]="CostAtSale",["warehouse_name"]="WarehouseName",["total_amount"]="TotalAmount",["total_cost"]="TotalCost",["usd_rate"]="UsdRate",["category_at_sale"]="CategoryAtSale",["unit_at_sale"]="UnitAtSale",["payment_type"]="PaymentType",["cash_amount"]="CashAmount",["card_amount"]="CardAmount",["tax_amount"]="TaxAmount",["tax_rate"]="TaxRate",["created_at"]="CreatedAt",["user_id"]="UserId",["is_synced"]="IsSynced"
    };
    private static JArray Rows(SqliteConnection db, SqliteTransaction tx, string sql, params (string,object?)[] args)
    {
        using var cmd=Command(db,tx,sql,args); using var reader=cmd.ExecuteReader(); var result=new JArray();
        while(reader.Read()) { var row=new JObject(); for(int i=0;i<reader.FieldCount;i++) {var key=reader.GetName(i); if(!Fields.TryGetValue(key,out var prop))continue; row[prop]=reader.IsDBNull(i)?JValue.CreateNull():key is "is_deleted" or "is_primary" or "is_synced"?new JValue(reader.GetInt64(i)!=0):JToken.FromObject(reader.GetValue(i));} result.Add(row); }
        return result;
    }
    private static long Revision(SqliteConnection db, SqliteTransaction tx, string kind, string guid) => Convert.ToInt64(Scalar(db,tx,"SELECT COALESCE(MAX(seq),0) FROM sync_journal WHERE kind=@kind AND entity_guid=@guid",("@kind",kind),("@guid",guid)));
    public JObject Pull(long since)
    {
        using var db=Open(); using var tx=db.BeginTransaction();
        long high=Convert.ToInt64(Scalar(db,tx,"SELECT COALESCE(MAX(seq),0) FROM sync_journal"));
        if(since<0 || since>high)throw new InvalidOperationException("Sinxron kursori noto'g'ri. Kompyuter bazasi tiklangan bo'lishi mumkin; qayta ulash kerak.");
        var result=new JObject { ["protocol"]=2,["serverId"]=ServerId,["cursor"]=high };
        foreach(var (kind,table,key) in new[]{("product","products","guid"),("warehouse","warehouses","guid"),("sale","sales","guid"),("stock","product_stocks","product_guid")})
        {
            var rows=Rows(db,tx,$"SELECT * FROM {table} WHERE @since=0 OR {key} IN (SELECT entity_guid FROM sync_journal WHERE seq>@since AND seq<=@high AND kind=@kind)",("@since",since),("@high",high),("@kind",kind));
            foreach(JObject row in rows) { if(kind=="sale")row["Items"]=Rows(db,tx,"SELECT * FROM sale_items WHERE sale_id=@id ORDER BY id",("@id",(long)row["Id"]!)); if(kind is "product" or "warehouse")row["Revision"]=Revision(db,tx,kind,(string)row["Guid"]!); }
            if(kind=="stock")
            {
                using var deleted=Command(db,tx,"SELECT DISTINCT entity_guid,warehouse_guid FROM sync_journal j WHERE kind='stock' AND warehouse_guid<>'' AND (@since=0 OR seq>@since) AND seq<=@high AND NOT EXISTS(SELECT 1 FROM product_stocks s WHERE s.product_guid=j.entity_guid AND s.warehouse_guid=j.warehouse_guid)",("@since",since),("@high",high));
                using var reader=deleted.ExecuteReader();while(reader.Read())rows.Add(new JObject{["ProductGuid"]=reader.GetString(0),["WarehouseGuid"]=reader.GetString(1),["Quantity"]=0,["UpdatedAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
            }
            result[table=="product_stocks"?"productStocks":table]=rows;
        }
        // Return records are immutable and their line/stock effects share this snapshot.
        result["returns"] = RawRows(db,tx,"SELECT * FROM returns");
        result["returnItems"] = RawRows(db,tx,"SELECT * FROM return_items");
        result["returnQuarantine"] = RawRows(db,tx,"SELECT * FROM return_quarantine");
        tx.Commit(); return result;
    }
    private static JArray RawRows(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        using var cmd=Command(db,tx,sql); using var reader=cmd.ExecuteReader(); var rows=new JArray();
        while(reader.Read()) { var row=new JObject(); for(var i=0;i<reader.FieldCount;i++) row[reader.GetName(i)]=reader.IsDBNull(i)?JValue.CreateNull():JToken.FromObject(reader.GetValue(i)); rows.Add(row); }
        return rows;
    }
    public JObject Push(JArray operations)
    {
        if(operations.Count>500)throw new ArgumentException("Bitta so'rovda 500 dan ortiq amal bo'lishi mumkin emas.");
        using var db=Open(); using var tx=db.BeginTransaction(); Exec(db,tx,"UPDATE sync_control SET applying=1 WHERE id=1");
        var accepted=new JArray();
        foreach(JObject op in operations)
        {
            string id=Required(op,"id"), kind=Required(op,"kind"), guid=Required(op,"guid");
            var previous=Scalar(db,tx,"SELECT seq FROM sync_journal WHERE op_id=@id",("@id",id));
            if(previous!=null) { var stored=(string?)Scalar(db,tx,"SELECT payload FROM sync_journal WHERE op_id=@id",("@id",id)); if(stored!=op.ToString(Formatting.None))throw new ArgumentException("Amal IDsi boshqa ma'lumot bilan qayta yuborildi."); accepted.Add(new JObject{["id"]=id,["revision"]=Convert.ToInt64(previous),["kind"]=kind,["guid"]=guid}); continue; }
            var data=(JObject?)op["data"] ?? throw new ArgumentException("Amal ma'lumoti yo'q.");
            long revision=Revision(db,tx,kind,guid), expected=(long?)op["baseRevision"]??0;
            bool bootstrap=(bool?)op["bootstrap"]??false;
            if(kind is "product" or "warehouse")
            {
                string table=kind=="product"?"products":"warehouses";
                bool exists=Scalar(db,tx,$"SELECT 1 FROM {table} WHERE guid=@guid",("@guid",guid))!=null;
                if(!bootstrap && exists && expected!=revision)throw new SyncConflictException(kind,guid,revision,"Tovar yoki ombor boshqa qurilmada tahrirlangan. Qaysi tahrirni saqlashni tanlang.");
                if(!bootstrap || !exists)
                {
                    if((string?)data["Guid"]!=guid)throw new ArgumentException("GUID mos emas.");
                    Required(data,"Name");
                    if(kind=="product") { foreach(var field in new[]{"CostPrice","SellingPrice","MinStockAlert"}) { double value=(double?)data[field]??throw new ArgumentException($"{field} yo‘q."); if(!double.IsFinite(value)||value<0)throw new ArgumentException($"{field} noto‘g‘ri."); } if((int?)data["UnitType"] is not (0 or 1 or 2))throw new ArgumentException("O‘lchov birligi noto‘g‘ri."); }
                    if(kind=="product" && data["Barcode"] is JValue { Type:JTokenType.String } barcode && !string.IsNullOrWhiteSpace((string?)barcode)) { var duplicate=Scalar(db,tx,"SELECT guid FROM products WHERE barcode=@barcode AND guid<>@guid",("@barcode",(string?)barcode),("@guid",guid)); if(duplicate!=null)throw new ArgumentException("Bu shtrix-kod kompyuterda boshqa GUID bilan mavjud. Bazalarni qo‘lda moslashtirish kerak; savdolar navbatda saqlanadi."); }
                    Upsert(db,tx,table,data);
                    if(kind=="product" && bootstrap && !exists && data["InitialStocks"] is JArray initial)
                        foreach(JObject stock in initial) { Stock(db,tx,guid,Required(stock,"WarehouseGuid"),(double)stock["Quantity"]!); Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,acked) VALUES(lower(hex(randomblob(16))),'stock',@guid,1)",("@guid",guid)); }
                }
            }
            else if(kind=="stock") Stock(db,tx,guid,Required(data,"WarehouseGuid"),(double?)data["Delta"]??throw new ArgumentException("Delta yo'q."));
            else if(kind is "sale" or "legacy_sale") InsertSale(db,tx,data,guid,kind=="legacy_sale");
            else throw new ArgumentException("Noma'lum amal turi.");
            Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,acked) VALUES(@id,@kind,@guid,@payload,1)",("@id",id),("@kind",kind=="legacy_sale"?"sale":kind),("@guid",guid),("@payload",op.ToString(Formatting.None)));
            long seq=Convert.ToInt64(Scalar(db,tx,"SELECT last_insert_rowid()"));
            accepted.Add(new JObject{["id"]=id,["revision"]=seq,["kind"]=kind,["guid"]=guid});
        }
        Exec(db,tx,"UPDATE sync_control SET applying=0 WHERE id=1"); tx.Commit(); return new JObject{["accepted"]=accepted};
    }
    public static string Required(JObject obj,string key) { string? value=(string?)obj[key]; if(string.IsNullOrWhiteSpace(value)||value.Length>256)throw new ArgumentException($"{key} noto'g'ri."); return value; }
    private static void Stock(SqliteConnection db,SqliteTransaction tx,string guid,string warehouse,double delta)
    {
        if(!double.IsFinite(delta))throw new ArgumentException("Qoldiq soni noto'g'ri.");
        if(Scalar(db,tx,"SELECT 1 FROM products WHERE guid=@guid",("@guid",guid))==null || Scalar(db,tx,"SELECT 1 FROM warehouses WHERE guid=@guid",("@guid",warehouse))==null)throw new ArgumentException("Tovar yoki ombor topilmadi; navbat saqlanadi.");
        Exec(db,tx,"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@guid,@wh,@delta,@now) ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=quantity+excluded.quantity,updated_at=excluded.updated_at",("@guid",guid),("@wh",warehouse),("@delta",delta),("@now",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        Exec(db,tx,"UPDATE products SET stock_quantity=(SELECT COALESCE(SUM(quantity),0) FROM product_stocks WHERE product_guid=@guid) WHERE guid=@guid",("@guid",guid));
    }
    private static void Upsert(SqliteConnection db,SqliteTransaction tx,string table,JObject data)
    {
        var columns=table=="products"?new[]{"guid","barcode","name","category","cost_price","cost_currency","selling_price","selling_price_2","unit_type","min_stock_alert","is_deleted","note","updated_at"}:new[]{"guid","name","is_primary","is_deleted","updated_at"};
        var args=columns.Select(c=>("@"+c,Value(data,Fields[c]))).ToArray();
        // Aggregate stock is maintained only by inventory operations, never metadata edits.
        var names=string.Join(",",columns); var values=string.Join(",",columns.Select(c=>"@"+c));
        Exec(db,tx,$"INSERT INTO {table}({names}{(table=="products"?",stock_quantity":"")}) VALUES({values}{(table=="products"?",0":"")}) ON CONFLICT(guid) DO UPDATE SET {string.Join(",",columns.Where(c=>c!="guid").Select(c=>$"{c}=excluded.{c}"))}",args);
    }
    private static object? Value(JObject data,string name) {var value=data[name]; if(value==null||value.Type==JTokenType.Null)return null; if(value.Type==JTokenType.Boolean)return (bool)value?1:0; return ((JValue)value).Value;}
    private static void InsertSale(SqliteConnection db,SqliteTransaction tx,JObject data,string guid,bool legacy)
    {
        if((string?)data["Guid"]!=guid)throw new ArgumentException("Chek GUIDsi mos emas.");
        if ((int?)data["PaymentType"] >= 7) throw new ArgumentException("Qaytarish faqat vakolatli lokal serverda yaratiladi.");
        if(Scalar(db,tx,"SELECT 1 FROM sales WHERE guid=@guid",("@guid",guid))!=null)return;
        var columns=new[]{"guid","total_amount","total_cost","payment_type","cash_amount","card_amount","tax_amount","tax_rate","created_at","usd_rate"};
        Exec(db,tx,$"INSERT INTO sales({string.Join(",",columns)},user_id,is_synced) VALUES({string.Join(",",columns.Select(c=>"@"+c))},1,1)",columns.Select(c=>("@"+c,Value(data,Fields[c]) ?? (c=="usd_rate" ? (object)0.0 : null))).ToArray());
        long saleId=Convert.ToInt64(Scalar(db,tx,"SELECT last_insert_rowid()"));
        var items=data["Items"]?.DeepClone() as JArray??throw new ArgumentException("Chek tovarlari yo'q.");
        if(items.Count==0)throw new ArgumentException("Bo'sh chek.");
        var ordinal = 0;
        foreach(JObject item in items)
        {
            ordinal++;
            if (string.IsNullOrWhiteSpace((string?)item["Guid"])) item["Guid"] = guid + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var product=Required(item,"ProductGuid"); double qty=(double?)item["Quantity"]??0; if(!double.IsFinite(qty)||qty<=0)throw new ArgumentException("Savdo miqdori noto'g'ri.");
            var productId=Scalar(db,tx,"SELECT id FROM products WHERE guid=@guid",("@guid",product))??throw new ArgumentException("Chekdagi tovar topilmadi.");
            var itemColumns=new[]{"guid","product_guid","product_name","quantity","price_at_sale","cost_at_sale","cost_currency","warehouse_guid","warehouse_name","category_at_sale","unit_at_sale"};
            var args=itemColumns.Select(c=>("@"+c,Value(item,Fields[c]) ?? (c is "category_at_sale" or "unit_at_sale" ? (object)"" : null))).Concat(new[]{("@sale",(object?)saleId),("@sg",guid),("@pid",productId)}).ToArray();
            Exec(db,tx,$"INSERT INTO sale_items(sale_id,sale_guid,product_id,{string.Join(",",itemColumns)}) VALUES(@sale,@sg,@pid,{string.Join(",",itemColumns.Select(c=>"@"+c))})",args);
            if(legacy) { Stock(db,tx,product,(string?)item["WarehouseGuid"] is {Length:>0} wh?wh:"main-default-warehouse",-qty); Exec(db,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,acked) VALUES(lower(hex(randomblob(16))),'stock',@guid,1)",("@guid",product)); }
        }
    }
}
public sealed class SyncConflictException : Exception
{
    public string Kind {get;} public string Guid {get;} public long Revision {get;}
    public SyncConflictException(string kind,string guid,long revision,string message):base(message){Kind=kind;Guid=guid;Revision=revision;}
}
