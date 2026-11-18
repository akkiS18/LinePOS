using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Sync;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Tovar rasmlari Wi‑Fi sinxroni: tovar sinxroni (jurnal/revizya) ga tegmasligi shart.</summary>
    public sealed class ProductImageSyncTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-img-" + Guid.NewGuid().ToString("N"));
        private readonly DatabaseContext _db;
        private readonly ProductImageSync _sync;
        private readonly string _guid;

        public ProductImageSyncTests()
        {
            Directory.CreateDirectory(_dir);
            var dbPath = Path.Combine(_dir, "t.db");
            _db = new DatabaseContext(dbPath);
            var p = new Product { Name = "Avtomat 32A", CostPrice = 30000, SellingPrice = 42000, StockQuantity = 5 };
            _db.SaveProduct(p);
            _guid = _db.GetAllProducts().Single().Guid;
            _ = new WifiSyncStore(dbPath); // sinxron triggerlari o'rnatiladi
            _sync = new ProductImageSync(dbPath, Path.Combine(_dir, "product_images"));
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private static byte[] Png(int w, int h)
        {
            var px = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i += 4) { px[i] = 40; px[i + 1] = 100; px[i + 2] = 11; px[i + 3] = (byte)(i % 8 == 0 ? 0 : 255); }
            var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        private long JournalCount()
        {
            using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + _db.DatabaseFilePath);
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sync_journal WHERE kind='product'";
            return (long)cmd.ExecuteScalar()!;
        }

        [Fact]
        public void Put_then_manifest_and_get_roundtrip_without_touching_product_journal()
        {
            var before = JournalCount();
            var bytes = Png(1200, 600);
            var put = _sync.Put(_guid, Convert.ToBase64String(bytes));

            var manifest = _sync.Manifest();
            var entry = (JObject)((JArray)manifest["images"]!).Single();
            Assert.Equal(_guid, (string?)entry["guid"]);
            Assert.Equal((string?)put["hash"], (string?)entry["hash"]);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), (string?)entry["hash"]);
            Assert.Equal(before, JournalCount());

            var got = _sync.Get(_guid);
            var jpeg = Convert.FromBase64String((string)got["data"]!);
            var frame = BitmapDecoder.Create(new MemoryStream(jpeg), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(600, frame.PixelWidth);
            Assert.Equal(300, frame.PixelHeight);
            Assert.Equal(0xFF, jpeg[0]); Assert.Equal(0xD8, jpeg[1]); // JPEG
        }

        [Fact]
        public void Delete_unlinks_image_and_keeps_journal()
        {
            _sync.Put(_guid, Convert.ToBase64String(Png(100, 100)));
            var before = JournalCount();
            _sync.Delete(_guid);
            Assert.Empty((JArray)_sync.Manifest()["images"]!);
            Assert.Equal(before, JournalCount());
        }

        [Fact]
        public void Missing_file_is_reported_separately_not_as_deleted()
        {
            _sync.Put(_guid, Convert.ToBase64String(Png(50, 50)));
            foreach (var f in Directory.GetFiles(Path.Combine(_dir, "product_images"))) File.Delete(f);
            var manifest = _sync.Manifest();
            Assert.Empty((JArray)manifest["images"]!);
            Assert.Equal(_guid, (string?)((JArray)manifest["missing"]!).Single());
        }

        [Fact]
        public void Rejects_garbage_unknown_product_and_bad_guid()
        {
            Assert.Throws<ArgumentException>(() => _sync.Put(_guid, Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })));
            Assert.Throws<ArgumentException>(() => _sync.Put(_guid, "@@not-base64@@"));
            Assert.Throws<ArgumentException>(() => _sync.Put(Guid.NewGuid().ToString(), Convert.ToBase64String(Png(10, 10))));
            Assert.Throws<ArgumentException>(() => _sync.Put("../../x", Convert.ToBase64String(Png(10, 10))));
            Assert.Throws<ArgumentException>(() => _sync.Get(_guid)); // rasm yo'q
        }

        [Fact]
        public void Phone_product_edit_keeps_desktop_image_path()
        {
            _sync.Put(_guid, Convert.ToBase64String(Png(64, 64)));
            var store = new WifiSyncStore(_db.DatabaseFilePath);
            var revision = (long)store.Pull(0)["products"]!.First()["Revision"]!;
            var op = new JObject
            {
                ["id"] = Guid.NewGuid().ToString(), ["kind"] = "product", ["guid"] = _guid, ["baseRevision"] = revision,
                ["data"] = new JObject { ["Guid"] = _guid, ["Name"] = "Avtomat 32A (yangi)", ["CostPrice"] = 30000.0, ["SellingPrice"] = 43000.0,
                    ["MinStockAlert"] = 3.0, ["UnitType"] = 0, ["IsDeleted"] = false, ["Note"] = "", ["Category"] = "Barchasi", ["CostCurrency"] = "UZS", ["UpdatedAt"] = 1L }
            };
            store.Push(new JArray(op));
            Assert.Single((JArray)_sync.Manifest()["images"]!);
        }
    }
}
