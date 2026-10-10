using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

public static class DebtBackupRestoreTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static SqliteConnection OpenConn(string dbPath)
    {
        var c = new SqliteConnection($"Data Source={dbPath}");
        c.Open();
        return c;
    }

    private static void InstallWifiSync(SqliteConnection conn)
    {
        using var stream = typeof(DebtBackupRestoreTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
        using var reader = new StreamReader(stream);
        foreach (var statement in reader.ReadToEnd().Split("-- statement"))
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = statement;
            cmd.ExecuteNonQuery();
        }
    }

    public static void Run()
    {
        Console.WriteLine("RUNNING DebtBackupRestoreTests (Qism 8: D21-D23, recovery, corruption protection)...");
        Test_D21_D22_PaymentOnlyDay_BackupRestore();
        Test_D23_ServerRestore_Triggers_New_Epoch_And_Reconciliation();
        Test_Corrupt_And_Invalid_Backup_Rejected_Safely();
        Test_Recovery_Backup_Created_Before_Restore();
        Console.WriteLine("ALL QISM 8 BACKUP & RESTORE TESTS PASSED SUCCESSFULLY!");
    }

    private static void Test_D21_D22_PaymentOnlyDay_BackupRestore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LinePOS_backup_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "main_pos.db");
        var backupPath = Path.Combine(tempDir, "export_backup.db");

        try
        {
            var db = new DatabaseContext(dbPath);
            var storeGuid = Guid.NewGuid().ToString("D");
            var actorGuid = Guid.NewGuid().ToString("D");
            var prodGuid = Guid.NewGuid().ToString("D");
            var whGuid = Guid.NewGuid().ToString("D");

            var prod = new Product
            {
                Guid = prodGuid,
                Name = "Kabel",
                Category = "Cable",
                CostPrice = 40000,
                CostCurrency = "UZS",
                SellingPrice = 50000,
                StockQuantity = 10
            };
            db.SaveProduct(prod);

            using (var conn = OpenConn(dbPath))
            {
                InstallWifiSync(conn);
                DebtSchema.Install(conn);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO debt_scope(id, store_guid) VALUES(1, $st); " +
                                 "INSERT OR REPLACE INTO warehouses(guid, name, is_primary, is_deleted, updated_at) VALUES($wh, 'Main', 1, 0, 100); " +
                                 "INSERT OR REPLACE INTO product_stocks(product_guid, warehouse_guid, quantity, updated_at) VALUES($pr, $wh, 10, 100);";
                cmd.Parameters.AddWithValue("$st", storeGuid);
                cmd.Parameters.AddWithValue("$wh", whGuid);
                cmd.Parameters.AddWithValue("$pr", prodGuid);
                cmd.ExecuteNonQuery();
            }

            var repo = new DebtRepository(dbPath, storeGuid, actorGuid, () => true);

            // 1. Create a customer and open a debt sale
            var custGuid = Guid.NewGuid().ToString("D");
            repo.CreateCustomer(new DebtCustomerDraft(custGuid, "Anvar Test", "+998901234567", "Nasiya mijozi", 1700000000000L));

            var saleReq = Guid.NewGuid().ToString("D");
            var saleGuid = Guid.NewGuid().ToString("D");
            var saleSnapshot = new DebtSaleSnapshot(
                saleGuid,
                1700000010000L,
                50000000L,  // 500,000 UZS total
                40000000L,  // 400,000 UZS cost
                0L,         // cash
                0L,         // card
                0L,         // fee
                "0",
                "12850",
                "DEBT",
                new[]
                {
                    new DebtSaleItem(
                        Guid.NewGuid().ToString("D"),
                        prodGuid,
                        "Kabel",
                        "Cable",
                        "METR",
                        whGuid,
                        "Main",
                        "10",
                        "50000",
                        "40000",
                        "UZS",
                        Guid.NewGuid().ToString("D"),
                        "-10"
                    )
                }
            );

            repo.OpenSale(new DebtOpenSaleCommand(
                RequestGuid: saleReq,
                CustomerGuid: custGuid,
                Sale: saleSnapshot,
                DueDate: "2030-01-01",
                NewCustomer: null,
                UserId: 1L
            ));

            // 2. Next day: PAYMENT ONLY DAY (No sales on this day)
            var payReq = Guid.NewGuid().ToString("D");
            repo.TakePayment(new DebtPaymentCommand(
                RequestGuid: payReq,
                CustomerGuid: custGuid,
                CashMinor: 20000000L, // 200,000 UZS
                CardMinor: 0L,
                FeeMinor: 0L,
                OccurredAt: 1700086400000L // Next day
            ));

            // Verify debt balance is 300,000 UZS (30,000,000 tiyin)
            var accountsBefore = repo.ReadAccounts(custGuid);
            Check(accountsBefore.Single().BalanceMinor == 30000000L, $"Expected active debt 300,000 UZS, got {accountsBefore.Single().BalanceMinor}");

            // 3. Verify unsent local event exists (sync_journal acked = -1) created by TakePayment
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sync_journal WHERE acked = -1 AND group_id = $grp;";
                cmd.Parameters.AddWithValue("$grp", payReq);
                var countBefore = Convert.ToInt64(cmd.ExecuteScalar());
                Check(countBefore == 1, $"TakePayment must naturally produce an outbox sync_journal record with acked=-1, got {countBefore}");
            }

            // 4. Add missing-dependency inbox entry (debt_sync_inbox)
            var pendingReq = Guid.NewGuid().ToString("D");
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO debt_sync_inbox(packet_guid, store_guid, payload, received_at, error) " +
                                 "VALUES($req, $st, '{\"kind\":\"debt_payment\",\"pending\":true}', 1700086410000, 'Missing opening sale');";
                cmd.Parameters.AddWithValue("$req", pendingReq);
                cmd.Parameters.AddWithValue("$st", storeGuid);
                cmd.ExecuteNonQuery();
            }

            // 5. Add a large applied envelope body (>2MiB) into sync_meta
            var largeReq = Guid.NewGuid().ToString("D");
            var largePayload = new StringBuilder(2_200_000);
            largePayload.Append("{\"large_envelope\":\"");
            while (largePayload.Length < 2_150_000)
            {
                largePayload.Append("abcdefghijklmnopqrstuvwxyz0123456789ABCDEF");
            }
            largePayload.Append("\"}");
            var largeBodyString = largePayload.ToString();

            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO sync_meta(key, value) VALUES($k, $v);";
                cmd.Parameters.AddWithValue("$k", $"debt_envelope_v1:{largeReq}");
                cmd.Parameters.AddWithValue("$v", largeBodyString);
                cmd.ExecuteNonQuery();
            }

            // 6. Perform BackupDatabase
            db.BackupDatabase(backupPath);
            Check(File.Exists(backupPath), "Backup file must exist");
            var backupSize = new FileInfo(backupPath).Length;
            Check(backupSize > 2_000_000, $"Backup file must be > 2MB (actual: {backupSize})");

            // 7. Clear connection pools and test RestoreDatabase
            SqliteConnection.ClearAllPools();
            db.RestoreDatabase(backupPath);

            // 8. Verify restored state:
            // a) Balance and customer details
            var repoRestored = new DebtRepository(dbPath, storeGuid, actorGuid, () => true);
            var accountsAfter = repoRestored.ReadAccounts(custGuid);
            Check(accountsAfter.Single().BalanceMinor == 30000000L, $"Restored debt must match 300,000 UZS, got {accountsAfter.Single().BalanceMinor}");

            // b) Integrity & Foreign Keys
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var intCheck = Convert.ToString(cmd.ExecuteScalar());
                Check(intCheck == "ok", $"Integrity check failed: {intCheck}");

                cmd.CommandText = "PRAGMA foreign_key_check;";
                using var reader = cmd.ExecuteReader();
                Check(!reader.Read(), "Restored database must have 0 foreign key violations");
            }

            // c) Unsent local event (sync_journal acked = -1) preserved
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sync_journal WHERE acked = -1 AND group_id = $grp;";
                cmd.Parameters.AddWithValue("$grp", payReq);
                var unsentCount = Convert.ToInt64(cmd.ExecuteScalar());
                Check(unsentCount == 1, $"Unsent local event must be preserved with acked=-1, got {unsentCount}");
            }

            // d) Missing dependency inbox item preserved
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT error, payload FROM debt_sync_inbox WHERE packet_guid = $req;";
                cmd.Parameters.AddWithValue("$req", pendingReq);
                using var reader = cmd.ExecuteReader();
                Check(reader.Read(), "Pending inbox item must exist");
                Check(reader.GetString(0) == "Missing opening sale", "Pending item error preserved");
                Check(reader.GetString(1).Contains("pending"), "Pending item payload preserved");
            }

            // e) >2MiB envelope body in sync_meta preserved bit-for-bit
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM sync_meta WHERE key = $k;";
                cmd.Parameters.AddWithValue("$k", $"debt_envelope_v1:{largeReq}");
                var restoredLargeBody = Convert.ToString(cmd.ExecuteScalar());
                Check(restoredLargeBody == largeBodyString, "Large >2MiB envelope body must match exactly");
            }

            // f) Next operation uses fresh writer epoch, starts sequence from 1 (no collision)
            var nextPayReq = Guid.NewGuid().ToString("D");
            repoRestored.TakePayment(new DebtPaymentCommand(
                RequestGuid: nextPayReq,
                CustomerGuid: custGuid,
                CashMinor: 10000000L, // 100,000 UZS
                CardMinor: 0L,
                FeeMinor: 0L,
                OccurredAt: 1700086500000L
            ));

            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT device_sequence FROM debt_events WHERE guid = $g;";
                cmd.Parameters.AddWithValue("$g", nextPayReq);
                var seq = Convert.ToInt64(cmd.ExecuteScalar());
                Check(seq == 1L, $"Next payment after restore must have sequence 1 with its new writer epoch, got {seq}");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void Test_D23_ServerRestore_Triggers_New_Epoch_And_Reconciliation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LinePOS_server_restore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "server.db");
        var backupPath = Path.Combine(tempDir, "server_snapshot.db");

        try
        {
            var db = new DatabaseContext(dbPath);
            var initialEpoch = Guid.NewGuid().ToString("D");

            using (var conn = OpenConn(dbPath))
            {
                InstallWifiSync(conn);
                DebtSchema.Install(conn);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO sync_meta(key, value) VALUES('debt_history_epoch', $ep);";
                cmd.Parameters.AddWithValue("$ep", initialEpoch);
                cmd.ExecuteNonQuery();
            }

            // Create backup with initial epoch
            db.BackupDatabase(backupPath);

            // Restore from backup
            SqliteConnection.ClearAllPools();
            db.RestoreDatabase(backupPath);

            // Verify that RestoreDatabase assigned a brand new debt_history_epoch
            using (var conn = OpenConn(dbPath))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM sync_meta WHERE key='debt_history_epoch';";
                var newEpoch = Convert.ToString(cmd.ExecuteScalar());
                Check(!string.IsNullOrEmpty(newEpoch), "Restored server must have debt_history_epoch");
                Check(newEpoch != initialEpoch, "Restored server must regenerate a new history epoch to trigger client reconciliation");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void Test_Corrupt_And_Invalid_Backup_Rejected_Safely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LinePOS_corrupt_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "live.db");
        var emptyFile = Path.Combine(tempDir, "empty.db");
        var garbageFile = Path.Combine(tempDir, "garbage.db");
        var badFkFile = Path.Combine(tempDir, "bad_fk.db");

        File.WriteAllBytes(emptyFile, Array.Empty<byte>());
        File.WriteAllText(garbageFile, "NOT A VALID SQLITE DATABASE FILE HEADER!!");

        try
        {
            var db = new DatabaseContext(dbPath);
            var storeGuid = Guid.NewGuid().ToString("D");
            var custGuid = Guid.NewGuid().ToString("D");

            using (var conn = OpenConn(dbPath))
            {
                InstallWifiSync(conn);
                DebtSchema.Install(conn);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO debt_scope(id, store_guid) VALUES(1, $st);";
                cmd.Parameters.AddWithValue("$st", storeGuid);
                cmd.ExecuteNonQuery();
            }

            // Seed live database with valid data
            var repo = new DebtRepository(dbPath, storeGuid, Guid.NewGuid().ToString("D"), () => true);
            repo.CreateCustomer(new DebtCustomerDraft(custGuid, "Tirik Mijoz", "+998901112233", "Active", 1700000000000L));

            // Create bad_fk.db: database with foreign key violation
            using (var badConn = new SqliteConnection($"Data Source={badFkFile}"))
            {
                badConn.Open();
                using var cmd = badConn.CreateCommand();
                cmd.CommandText = "PRAGMA foreign_keys = OFF;" +
                                 "CREATE TABLE parent(id INTEGER PRIMARY KEY);" +
                                 "CREATE TABLE child(id INTEGER PRIMARY KEY, p_id INTEGER REFERENCES parent(id));" +
                                 "INSERT INTO child(id, p_id) VALUES(1, 999);"; // 999 does not exist in parent!
                cmd.ExecuteNonQuery();
            }

            // Test 1: Empty file rejected
            bool emptyThrew = false;
            try
            {
                db.RestoreDatabase(emptyFile);
            }
            catch (InvalidOperationException)
            {
                emptyThrew = true;
            }
            Check(emptyThrew, "RestoreDatabase must throw on empty backup file");

            // Test 2: Garbage file rejected
            bool garbageThrew = false;
            try
            {
                db.RestoreDatabase(garbageFile);
            }
            catch (Exception)
            {
                garbageThrew = true;
            }
            Check(garbageThrew, "RestoreDatabase must throw on corrupted SQLite file");

            // Test 3: Broken foreign keys rejected
            bool fkThrew = false;
            try
            {
                db.RestoreDatabase(badFkFile);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Foreign Key"))
            {
                fkThrew = true;
            }
            Check(fkThrew, "RestoreDatabase must throw on foreign key violations");

            // Test 4: Live database was NOT modified or corrupted
            SqliteConnection.ClearAllPools();
            var repoAfter = new DebtRepository(dbPath, storeGuid, Guid.NewGuid().ToString("D"), () => true);
            var cust = repoAfter.ReadCustomer(custGuid);
            Check(cust != null && cust.Name == "Tirik Mijoz", "Live database must remain fully intact after rejected restores");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void Test_Recovery_Backup_Created_Before_Restore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LinePOS_recovery_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "live.db");
        var targetBackup = Path.Combine(tempDir, "valid_backup.db");

        try
        {
            var db = new DatabaseContext(dbPath);
            var storeGuid = Guid.NewGuid().ToString("D");

            using (var conn = OpenConn(dbPath))
            {
                InstallWifiSync(conn);
                DebtSchema.Install(conn);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO debt_scope(id, store_guid) VALUES(1, $st);";
                cmd.Parameters.AddWithValue("$st", storeGuid);
                cmd.ExecuteNonQuery();
            }

            var repo = new DebtRepository(dbPath, storeGuid, Guid.NewGuid().ToString("D"), () => true);
            repo.CreateCustomer(new DebtCustomerDraft(Guid.NewGuid().ToString("D"), "Mijoz 1", "+998901234567", "", 1700000000000L));

            // Create target backup to restore from
            db.BackupDatabase(targetBackup);

            // Add new data to live database
            repo.CreateCustomer(new DebtCustomerDraft(Guid.NewGuid().ToString("D"), "Mijoz 2", "+998909876543", "", 1700000000000L));

            // Perform restore
            SqliteConnection.ClearAllPools();
            db.RestoreDatabase(targetBackup);

            // Verify that a before_restore backup was created in Backups folder
            var backupDir = Path.Combine(tempDir, "Backups");
            Check(Directory.Exists(backupDir), "Backups directory must exist");
            var beforeRestoreFiles = Directory.GetFiles(backupDir, "*before_restore*.db");
            Check(beforeRestoreFiles.Length > 0, "A recovery backup (before_restore) must be created before restoring");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
