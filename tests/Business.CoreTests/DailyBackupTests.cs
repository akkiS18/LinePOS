using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

public static class DailyBackupTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static long Count(string dbPath, string sql)
    {
        using var c = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static Sale NewSale(Product p) => new Sale
    {
        Guid = Guid.NewGuid().ToString(), CreatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds(), TotalAmount = 9500, TotalCost = 7000, CashAmount = 9500,
        Items = new() { new SaleItem { ProductId = p.Id, ProductGuid = p.Guid, ProductName = p.Name, Quantity = 1, PriceAtSale = 9500, CostAtSale = 7000, WarehouseGuid = "main-default-warehouse" } }
    };

    public static void Run()
    {
        Console.WriteLine("RUNNING DailyBackupTests...");
        var dir = Path.Combine(Path.GetTempPath(), "LinePOS_daily_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "pos_desktop.db");
        string? restoreTemp = null;
        try
        {
            var db = new DatabaseContext(path);
            // Ilovada Wi-Fi sinxron sxemasini WifiSyncStore o'rnatadi; tiklash sync_meta jadvaliga yozadi
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var stream = typeof(DailyBackupTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
                foreach (var statement in new StreamReader(stream).ReadToEnd().Split("-- statement"))
                {
                    if (string.IsNullOrWhiteSpace(statement)) continue;
                    using var cmd = conn.CreateCommand(); cmd.CommandText = statement; cmd.ExecuteNonQuery();
                }
            }
            var product = new Product { Guid = Guid.NewGuid().ToString(), Name = "LED lampa", Category = "Elektr", CostPrice = 7000, SellingPrice = 9500, StockQuantity = 100 };
            db.SaveProduct(product);
            db.InsertSale(NewSale(product));

            // 1) Bugungi zaxira yaratiladi, ikkinchi chaqiruv yangisini yaratmaydi
            var first = db.DailyBackup();
            Check(first != null && File.Exists(first) && first.EndsWith(".zip"), "Daily backup not created");
            Check(Path.GetFileName(first!) == $"LinePOS_Kunlik_{DateTime.Now:yyyyMMdd}.zip", "Unexpected daily backup name");
            Check(db.DailyBackup() == null, "Second backup on the same day must be skipped");

            // 2) Faqat oxirgi 14 kunlik saqlanadi; uzilishdan qolgan vaqtinchalik fayllar tozalanadi
            for (int i = 1; i <= 20; i++)
                File.WriteAllText(Path.Combine(db.BackupsDirectory, $"LinePOS_Kunlik_{DateTime.Now.AddDays(-i):yyyyMMdd}.zip"), "old");
            File.WriteAllText(Path.Combine(db.BackupsDirectory, "daily_tmp_leftover.db"), "x");
            File.Delete(first!);
            var second = db.DailyBackup();
            var kept = Directory.GetFiles(db.BackupsDirectory, "LinePOS_Kunlik_*.zip").Select(Path.GetFileName).OrderByDescending(x => x).ToList();
            Check(second != null && kept.Count == DatabaseContext.DailyBackupKeepDays, $"Expected 14 daily backups, got {kept.Count}");
            Check(kept[0] == Path.GetFileName(second) && kept[^1] == $"LinePOS_Kunlik_{DateTime.Now.AddDays(-13):yyyyMMdd}.zip", "Wrong backups pruned");
            Check(!File.Exists(Path.Combine(db.BackupsDirectory, "daily_tmp_leftover.db")), "Stale temp file not cleaned");
            Check(!Directory.GetFiles(db.BackupsDirectory, "*.tmp").Any(), "Temp zip left behind");

            // 3) Zip zaxiradan tiklash: yaxlit baza, keyingi savdo bekor bo'ladi
            db.InsertSale(NewSale(product));
            Check(db.GetTotalSalesCount() == 2, "Setup: second sale missing");
            var (restorePath, isTemp) = DatabaseContext.PrepareBackupForRestore(second!);
            restoreTemp = restorePath;
            Check(isTemp && File.Exists(restorePath), "Zip was not extracted");
            Check(Count(restorePath, "SELECT COUNT(*) FROM sales") == 1, "Backup content wrong");
            db.RestoreDatabase(restorePath);
            Check(db.GetTotalSalesCount() == 1, "Restore from daily zip failed");
            var (same, sameTemp) = DatabaseContext.PrepareBackupForRestore(path);
            Check(same == path && !sameTemp, ".db path must pass through unchanged");

            Console.WriteLine("PASS daily backup: one zip per day, keeps 14, cleans temp files, restores from zip");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (restoreTemp != null && File.Exists(restoreTemp)) File.Delete(restoreTemp); } catch { }
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
