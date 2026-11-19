// UI and unrelated licensing/currency services are deliberately excluded from LAN tests.
using Microsoft.Data.Sqlite;
namespace PosElectro.Desktop.Data {
    public sealed class DatabaseContext {
        public string DatabaseFilePath {get;}
        public DatabaseContext(string path){DatabaseFilePath=path;}
        public int GetActiveProductsCount()=>0;
        public int GetTotalSalesCount()=>0;
        public double GetCardTaxRate()=>1.8;
        public string GetSetting(string key, string defaultValue="")=>defaultValue;
        public void BackupDatabase(string destination) {using var db=new SqliteConnection("Data Source="+DatabaseFilePath);db.Open();using var cmd=db.CreateCommand();cmd.CommandText="VACUUM INTO @destination";cmd.Parameters.AddWithValue("@destination",destination);cmd.ExecuteNonQuery();}
    }
}
namespace PosElectro.Desktop.Services { public sealed class CurrencyService { public double GetCachedUsdRate()=>12850; } }
namespace PosElectro.Desktop.Models { public sealed class Product { } public sealed class Warehouse { } }
// ProductImageSync re-encodes JPEGs with WPF; LAN tests only need the endpoints to exist (images are covered by desktop tests).
namespace PosElectro.Desktop.Sync {
    public sealed class ProductImageSync {
        public const string Capability = "productImagesV1";
        public ProductImageSync(string databasePath, string? imagesDir = null) { }
        public Newtonsoft.Json.Linq.JObject Manifest() => new() { ["images"] = new Newtonsoft.Json.Linq.JArray() };
        public Newtonsoft.Json.Linq.JObject Get(string guid) => new();
        public Newtonsoft.Json.Linq.JObject Put(string guid, string base64) => new() { ["success"] = true };
        public void Delete(string guid) { }
    }
}
