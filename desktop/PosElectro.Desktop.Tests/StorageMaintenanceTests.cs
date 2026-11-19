using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Services;
using PosElectro.Desktop.Sync;
using Xunit;

namespace PosElectro.Desktop.Tests
{
    /// <summary>Yillar davomida hajm va tezlik: chek indekslari, jurnal payloadini tozalash, rasmlarni ixcham saqlash.</summary>
    public sealed class StorageMaintenanceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "linepos-storage-" + Guid.NewGuid().ToString("N"));
        private readonly string _dbPath;

        public StorageMaintenanceTests()
        {
            Directory.CreateDirectory(_dir);
            _dbPath = Path.Combine(_dir, "t.db");
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private string Plan(string sql)
        {
            using var c = new SqliteConnection("Data Source=" + _dbPath); c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
            var plan = ""; using (var r = cmd.ExecuteReader()) while (r.Read()) plan += r.GetString(3) + ";";
            return plan;
        }

        [Fact]
        public void SaleItemLookups_UseIndexes()
        {
            _ = new DatabaseContext(_dbPath);
            Assert.Contains("idx_sale_items_sale", Plan("SELECT * FROM sale_items WHERE sale_id = 5 ORDER BY id"));
            Assert.Contains("idx_sale_items_sale_guid", Plan("SELECT * FROM sale_items WHERE sale_guid = 'x'"));
        }

        [Fact]
        public void CompactSyncJournal_StripsOnlyOldReplicationPayloads()
        {
            var db = new DatabaseContext(_dbPath);
            _ = new WifiSyncStore(_dbPath);
            const int extra = 50;
            using (var c = new SqliteConnection("Data Source=" + _dbPath))
            {
                c.Open();
                using var tx = c.BeginTransaction();
                using var cmd = c.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id,acked) VALUES($id,$kind,$g,$p,'',1)";
                var id = cmd.Parameters.Add("$id", SqliteType.Text); var kind = cmd.Parameters.Add("$kind", SqliteType.Text);
                var g = cmd.Parameters.Add("$g", SqliteType.Text); cmd.Parameters.AddWithValue("$p", "{\"big\":\"payload\"}");
                // Eng eskisi: qarz yozuvi — hech qachon tozalanmasligi kerak
                id.Value = "debt:old"; kind.Value = "debt_event"; g.Value = Guid.NewGuid().ToString(); cmd.ExecuteNonQuery();
                for (int i = 0; i < DatabaseContext.JournalPayloadKeepRows + extra; i++)
                {
                    id.Value = "op" + i; kind.Value = i % 2 == 0 ? "sale" : "stock"; g.Value = Guid.NewGuid().ToString(); cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            var stripped = db.CompactSyncJournal();
            Assert.Equal(extra, stripped); // eng yangi 20000 tadan tashqaridagi sale/stock qatorlar; debt_event tegilmaydi

            using var check = new SqliteConnection("Data Source=" + _dbPath); check.Open();
            long Count(string sql) { using var q = check.CreateCommand(); q.CommandText = sql; return Convert.ToInt64(q.ExecuteScalar()); }
            Assert.Equal(1, Count("SELECT COUNT(*) FROM sync_journal WHERE op_id='debt:old' AND payload IS NOT NULL"));
            Assert.Equal(DatabaseContext.JournalPayloadKeepRows + extra + 1, Count("SELECT COUNT(*) FROM sync_journal WHERE op_id LIKE 'op%' OR op_id='debt:old'"));
            Assert.Equal(1, Count($"SELECT COUNT(*) FROM sync_journal WHERE op_id='op{DatabaseContext.JournalPayloadKeepRows + extra - 1}' AND payload IS NOT NULL"));
            Assert.Equal(0, db.CompactSyncJournal()); // ikkinchi marta: ish yo'q
        }

        // Shovqinli piksellar: haqiqiy foto kabi yomon siqiladi (gradient PNG juda kichik chiqib, testni aldaydi)
        private static BitmapSource Photo(int w, int h)
        {
            var px = new byte[w * h * 3];
            new Random(42).NextBytes(px);
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, px, w * 3);
        }

        private static void Save(BitmapEncoder enc, BitmapSource src, string path)
        {
            enc.Frames.Add(BitmapFrame.Create(src));
            using var fs = File.Create(path); enc.Save(fs);
        }

        [Fact]
        public void LargePhoto_IsShrunkToCompactJpeg()
        {
            var src = Path.Combine(_dir, "big.png");
            Save(new PngBitmapEncoder(), Photo(4000, 3000), src);
            var result = ProductImageFiles.SaveFromFile(src, Path.Combine(_dir, "prod_x"));

            Assert.EndsWith(".jpg", result);
            var frame = BitmapDecoder.Create(new Uri(result), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(ProductImageFiles.MaxSide, Math.Max(frame.PixelWidth, frame.PixelHeight));
            Assert.Equal(960, frame.PixelHeight); // 4:3 nisbat saqlanadi
            Assert.True(new FileInfo(result).Length < new FileInfo(src).Length / 4);
        }

        [Fact]
        public void SmallJpeg_IsKeptAsIs()
        {
            var src = Path.Combine(_dir, "small.jpg");
            Save(new JpegBitmapEncoder { QualityLevel = 80 }, Photo(400, 300), src);
            var result = ProductImageFiles.SaveFromFile(src, Path.Combine(_dir, "prod_y"));
            Assert.Equal(File.ReadAllBytes(src), File.ReadAllBytes(result));
        }

        [Fact]
        public void UnreadableFile_IsCopiedUnchanged()
        {
            var src = Path.Combine(_dir, "photo.webp");
            File.WriteAllBytes(src, new byte[] { 1, 2, 3, 4 });
            var result = ProductImageFiles.SaveFromFile(src, Path.Combine(_dir, "prod_z"));
            Assert.EndsWith(".webp", result);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(result));
        }
    }
}
