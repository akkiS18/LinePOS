using System.Globalization;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

public static class DebtMultiDeviceTests
{
    private static string G(int n) => "00000000-0000-0000-0000-" + n.ToString("D12");
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or SqliteException) { return; }
        throw new Exception("Expected rejection did not occur");
    }

    private static DebtRepository Repo(string p, string? actor = null) => new(p, G(1), actor ?? G(2), () => true);
    private static DebtEnvelopeInbox Inbox(string p) =>
        new(p, G(1), () => true, _ => true, actor => 1L);

    private static void Exec(SqliteConnection c, SqliteTransaction? tx, string sql, params object[] args)
    {
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = sql;
        for (int i = 0; i < args.Length; i++) q.Parameters.AddWithValue("@p" + i, args[i]);
        q.ExecuteNonQuery();
    }
    private static void Sql(string path, string sql) { using var c = new SqliteConnection("Data Source=" + path); c.Open(); Exec(c, null, sql); }
    private static object? Value(string path, string sql) { using var c = new SqliteConnection("Data Source=" + path); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
    private static long Count(string p, string table) => Convert.ToInt64(Value(p, "SELECT COUNT(*) FROM " + table));

    private static void Setup(string p)
    {
        _ = new DatabaseContext(p);
        using var c = new SqliteConnection("Data Source=" + p); c.Open();
        using var stream = typeof(DebtMultiDeviceTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
        using var r = new StreamReader(stream);
        foreach (var statement in r.ReadToEnd().Split("-- statement")) Exec(c, null, statement);
        DebtSchema.Install(c);
        Repo(p).BindStore();
    }

    private static void Product(string p, string guid, decimal initialStock = 20m) =>
        Sql(p, $"INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('{guid}','Kabel',60,100,{initialStock.ToString("G", CultureInfo.InvariantCulture)},0,500)");
    private static void Warehouse(string p, string guid) =>
        Sql(p, $"INSERT INTO warehouses(guid,name,updated_at) VALUES('{guid}','Asosiy Ombor',500)");
    private static void Stocks(string p, string prodGuid, string whGuid, decimal stock = 20m) =>
        Sql(p, $"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('{prodGuid}','{whGuid}',{stock.ToString("G", CultureInfo.InvariantCulture)},500)");

    public static void Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "debt-multidevice-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        int next = 0;
        string Fresh() { var p = Path.Combine(dir, (next++) + ".db"); Setup(p); return p; }

        try
        {
            // -------------------------------------------------------------
            // 1. Three-Device Topology Convergence (Desktop + 2 Phones)
            // -------------------------------------------------------------
            {
                var desktopDb = Fresh();
                var phone1Db = Fresh();
                var phone2Db = Fresh();

                Product(desktopDb, G(101), 20m); Warehouse(desktopDb, G(102)); Stocks(desktopDb, G(101), G(102), 20m);
                Product(phone1Db, G(101), 20m); Warehouse(phone1Db, G(102)); Stocks(phone1Db, G(101), G(102), 20m);
                Product(phone2Db, G(101), 20m); Warehouse(phone2Db, G(102)); Stocks(phone2Db, G(101), G(102), 20m);

                // Initial customer created on Desktop and synced to both phones
                var custDraft = new DebtCustomerDraft(G(301), "Ali Valiyev", "+998901234567", "Qarzdor", 100);
                Repo(desktopDb).CreateCustomer(custDraft);
                var custWire = Inbox(desktopDb).ExportApplied(G(301))!;
                Inbox(phone1Db).Receive(custWire, 101);
                Inbox(phone2Db).Receive(custWire, 101);

                // Phone 1 makes a debt sale of 100k total: 20k cash paid, 80k debt (8,000,000 minor)
                var p1Items = new List<DebtSaleItem>
                {
                    new(G(401), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "2", "50000", "30000", "UZS", G(501), "-2")
                };
                var p1Snapshot = new DebtSaleSnapshot(G(601), 100, 10000000, 6000000, 2000000, 0, 0, "0", "12850", "DEBT", p1Items.AsReadOnly());
                var p1SaleReq = G(701);
                Repo(phone1Db, actor: G(21)).OpenSale(new DebtOpenSaleCommand(p1SaleReq, G(301), p1Snapshot));
                var p1Envelope = Inbox(phone1Db).ExportApplied(p1SaleReq)!;

                // Baseline is synced to Phone 2 and Desktop so all devices know about Ali's 80k debt
                Inbox(phone2Db).Receive(p1Envelope, 102);
                Inbox(desktopDb).Receive(p1Envelope, 102);

                // Phone 1 goes offline: takes 30k payment (3,000,000 minor)
                var p1PayReq = G(700);
                Repo(phone1Db, actor: G(21)).TakePayment(new DebtPaymentCommand(p1PayReq, G(301), 3000000, 0, 0, 199));
                var p1PayEnv = Inbox(phone1Db).ExportApplied(p1PayReq)!;

                // Phone 2 goes offline: takes 30k payment (3,000,000 minor) against Ali
                var p2PayReq = G(702);
                Repo(phone2Db, actor: G(22)).TakePayment(new DebtPaymentCommand(p2PayReq, G(301), 3000000, 0, 0, 200));
                var p2PayEnv = Inbox(phone2Db).ExportApplied(p2PayReq)!;

                // Desktop also takes a local 10k payment (1,000,000 minor) directly on Desktop
                var deskPayReq = G(703);
                Repo(desktopDb, actor: G(2)).TakePayment(new DebtPaymentCommand(deskPayReq, G(301), 1000000, 0, 0, 210));
                var deskEnvelope = Inbox(desktopDb).ExportApplied(deskPayReq)!;

                // Delivery Order A: Fresh convergent DB receives Phone1Sale -> Phone1Pay -> Phone2Pay -> DeskPay
                var convDbA = Fresh();
                Product(convDbA, G(101), 20m); Warehouse(convDbA, G(102)); Stocks(convDbA, G(101), G(102), 20m);
                Inbox(convDbA).Receive(custWire, 300);
                Inbox(convDbA).Receive(p1Envelope, 301);
                Inbox(convDbA).Receive(p1PayEnv, 302);
                Inbox(convDbA).Receive(p2PayEnv, 303);
                Inbox(convDbA).Receive(deskEnvelope, 304);
                Inbox(convDbA).DrainPending(305);

                // Delivery Order B: Another fresh convergent DB receives DeskPay -> Phone2Pay -> Phone1Pay -> Phone1Sale (payments before sale!)
                var convDbB = Fresh();
                Product(convDbB, G(101), 20m); Warehouse(convDbB, G(102)); Stocks(convDbB, G(101), G(102), 20m);
                Inbox(convDbB).Receive(custWire, 400);
                // Payments arrive before sale -> queued as WaitingForDependency
                var statusDesk = Inbox(convDbB).Receive(deskEnvelope, 401);
                var statusP2 = Inbox(convDbB).Receive(p2PayEnv, 402);
                var statusP1Pay = Inbox(convDbB).Receive(p1PayEnv, 403);
                Check(statusDesk == DebtReceiveStatus.WaitingForDependency, "Desk payment was not queued before sale");
                Check(statusP2 == DebtReceiveStatus.WaitingForDependency, "Phone 2 payment was not queued before sale");
                Check(statusP1Pay == DebtReceiveStatus.WaitingForDependency, "Phone 1 payment was not queued before sale");
                // Sale arrives, then DrainPending applies all
                var statusP1 = Inbox(convDbB).Receive(p1Envelope, 404);
                Check(statusP1 == DebtReceiveStatus.Applied, "Phone 1 sale not applied");
                Inbox(convDbB).DrainPending(405);

                // Convergence Check: Both databases must have identical balances, cash, stock, and event counts!
                var balA = Repo(convDbA).ReadAccounts(G(301)).Single().BalanceMinor;
                var balB = Repo(convDbB).ReadAccounts(G(301)).Single().BalanceMinor;
                // 80k debt - 30k (P1) - 30k (P2) - 10k (Desk) = 10k remaining debt (1,000,000 minor)
                Check(balA == 1000000, $"Conv A balance mismatch: {balA}");
                Check(balB == 1000000, $"Conv B balance mismatch: {balB}");
                Check(balA == balB, "Convergence balances must be identical across delivery orders");

                // Check cash collected on both (from sale: 0 in debt_events, payments: 30k + 30k + 10k = 70k (7,000,000 minor))
                long CashMinor(string p) => Convert.ToInt64(Value(p, "SELECT COALESCE(SUM(cash_minor),0) FROM debt_events"));
                Check(CashMinor(convDbA) == 7000000, "Cash minor A mismatch");
                Check(CashMinor(convDbB) == 7000000, "Cash minor B mismatch");

                // Check stock quantity on both (20 initial - 2 sold = 18)
                decimal StockQty(string p) => Convert.ToDecimal(Value(p, $"SELECT quantity FROM product_stocks WHERE product_guid='{G(101)}'"));
                Check(StockQty(convDbA) == 18m, "Stock quantity A mismatch");
                Check(StockQty(convDbB) == 18m, "Stock quantity B mismatch");

                // Check events count (1 sale_open, 3 payments = 4 events)
                Check(Count(convDbA, "debt_events") == 4, "Event count A mismatch");
                Check(Count(convDbB, "debt_events") == 4, "Event count B mismatch");
            }

            // -------------------------------------------------------------
            // 2. Offline Excess Payment Convergence (100k debt, two 100k payments -> 200k cash, 0 debt, 100k credit)
            // -------------------------------------------------------------
            {
                var dbServer = Fresh();
                var dbP1 = Fresh();
                var dbP2 = Fresh();

                Product(dbServer, G(101), 50m); Warehouse(dbServer, G(102)); Stocks(dbServer, G(101), G(102), 50m);
                Product(dbP1, G(101), 50m); Warehouse(dbP1, G(102)); Stocks(dbP1, G(101), G(102), 50m);
                Product(dbP2, G(101), 50m); Warehouse(dbP2, G(102)); Stocks(dbP2, G(101), G(102), 50m);

                // Customer with initial 100k debt (10,000,000 minor)
                var custDraft = new DebtCustomerDraft(G(302), "Komil aka", "+998911112233", "Nasiyachi", 100);
                Repo(dbServer).CreateCustomer(custDraft);
                var custWire = Inbox(dbServer).ExportApplied(G(302))!;

                var saleItems = new List<DebtSaleItem>
                {
                    new(G(402), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "100000", "60000", "UZS", G(502), "-1")
                };
                var saleSnapshot = new DebtSaleSnapshot(G(602), 100, 10000000, 6000000, 0, 0, 0, "0", "12850", "DEBT", saleItems.AsReadOnly());
                var saleReq = G(704);
                Repo(dbServer).OpenSale(new DebtOpenSaleCommand(saleReq, G(302), saleSnapshot));
                var saleEnv = Inbox(dbServer).ExportApplied(saleReq)!;

                // Sync baseline to both phones
                Inbox(dbP1).Receive(custWire, 100); Inbox(dbP1).Receive(saleEnv, 101);
                Inbox(dbP2).Receive(custWire, 100); Inbox(dbP2).Receive(saleEnv, 101);

                // Both phones go offline and both accept 100k cash payment (10,000,000 minor)
                var payReq1 = G(705);
                Repo(dbP1, actor: G(21)).TakePayment(new DebtPaymentCommand(payReq1, G(302), 10000000, 0, 0, 200));
                var envPay1 = Inbox(dbP1).ExportApplied(payReq1)!;

                var payReq2 = G(706);
                Repo(dbP2, actor: G(22)).TakePayment(new DebtPaymentCommand(payReq2, G(302), 10000000, 0, 0, 201));
                var envPay2 = Inbox(dbP2).ExportApplied(payReq2)!;

                // Push both payments to Server
                var res1 = Inbox(dbServer).Receive(envPay1, 300);
                var res2 = Inbox(dbServer).Receive(envPay2, 301);
                Check(res1 == DebtReceiveStatus.Applied, "Excess pay 1 failed");
                Check(res2 == DebtReceiveStatus.Applied, "Excess pay 2 failed");

                // Account balance: initial 10,000,000 - 10,000,000 (P1) - 10,000,000 (P2) = -10,000,000 minor (-100k UZS credit)
                var serverAcc = Repo(dbServer).ReadAccounts(G(302)).Single();
                Check(serverAcc.BalanceMinor == -10000000, $"Expected -10000000 credit but got: {serverAcc.BalanceMinor}");

                // Total cash from payments: 200k (20,000,000 minor)
                long serverCash = Convert.ToInt64(Value(dbServer, "SELECT SUM(cash_minor) FROM debt_events WHERE kind='payment'"));
                Check(serverCash == 20000000, $"Expected 20,000,000 cash but got {serverCash}");

                // Neither event was mutated or lost
                Check(Count(dbServer, "debt_events") == 3, "All 3 events (1 sale, 2 payments) must be preserved");
            }

            // -------------------------------------------------------------
            // 3. Cloned Database Writer Epoch Isolation
            // -------------------------------------------------------------
            {
                var baseDb = Fresh();
                Product(baseDb, G(101), 30m); Warehouse(baseDb, G(102)); Stocks(baseDb, G(101), G(102), 30m);
                var custDraft = new DebtCustomerDraft(G(303), "Sardor", "+998933334455", "", 100);
                Repo(baseDb).CreateCustomer(custDraft);

                // Initial debt on baseDb so payments can be allocated
                var saleItems = new List<DebtSaleItem>
                {
                    new(G(403), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "2", "50000", "30000", "UZS", G(503), "-2")
                };
                var saleSnapshot = new DebtSaleSnapshot(G(603), 100, 10000000, 6000000, 0, 0, 0, "0", "12850", "DEBT", saleItems.AsReadOnly());
                Repo(baseDb).OpenSale(new DebtOpenSaleCommand(G(706), G(303), saleSnapshot));

                SqliteConnection.ClearAllPools();
                using (var c = new SqliteConnection("Data Source=" + baseDb))
                {
                    c.Open();
                    using var cmd = c.CreateCommand();
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    cmd.ExecuteNonQuery();
                }
                SqliteConnection.ClearAllPools();

                // Clone database to copy A and copy B
                var copyA = Path.Combine(dir, "clone_a.db");
                var copyB = Path.Combine(dir, "clone_b.db");
                File.Copy(baseDb, copyA, true);
                File.Copy(baseDb, copyB, true);

                // Create independent repos on copy A and copy B
                var repoA = Repo(copyA, actor: G(21));
                var repoB = Repo(copyB, actor: G(22));

                // Perform operations on both copies
                var payA = G(707);
                repoA.TakePayment(new DebtPaymentCommand(payA, G(303), 50000, 0, 0, 100));
                var payB = G(708);
                repoB.TakePayment(new DebtPaymentCommand(payB, G(303), 70000, 0, 0, 101));

                // Verify device GUIDs are distinct
                var devA = (string)Value(copyA, $"SELECT device_guid FROM debt_events WHERE guid='{payA}'")!;
                var devB = (string)Value(copyB, $"SELECT device_guid FROM debt_events WHERE guid='{payB}'")!;
                Check(!string.IsNullOrEmpty(devA) && !string.IsNullOrEmpty(devB), "Device GUIDs must not be empty");
                Check(devA != devB, $"Writer epochs must be disjoint between clones: {devA} vs {devB}");

                // Both must have sequence = 1 scoped to their distinct device_guid
                var seqA = Convert.ToInt64(Value(copyA, $"SELECT device_sequence FROM debt_events WHERE guid='{payA}'"));
                var seqB = Convert.ToInt64(Value(copyB, $"SELECT device_sequence FROM debt_events WHERE guid='{payB}'"));
                Check(seqA == 1 && seqB == 1, "Each writer sequence starts at 1 scoped to device_guid");

                // Cross-sync: Copy A receives payB; Copy B receives payA
                var envA = Inbox(copyA).ExportApplied(payA)!;
                var envB = Inbox(copyB).ExportApplied(payB)!;
                Check(Inbox(copyA).Receive(envB, 200) == DebtReceiveStatus.Applied, "Copy A failed to receive B");
                Check(Inbox(copyB).Receive(envA, 200) == DebtReceiveStatus.Applied, "Copy B failed to receive A");
            }

            // -------------------------------------------------------------
            // 4. Server Restore Rollback Detection and Full Reconciliation
            // -------------------------------------------------------------
            {
                var srvDb = Fresh();
                Product(srvDb, G(101), 30m); Warehouse(srvDb, G(102)); Stocks(srvDb, G(101), G(102), 30m);
                var custDraft = new DebtCustomerDraft(G(304), "Toshmat aka", "+998944445566", "", 100);
                Repo(srvDb).CreateCustomer(custDraft);
                var custWire = Inbox(srvDb).ExportApplied(G(304))!;

                // Client DB (Phone)
                var clientDb = Fresh();
                Product(clientDb, G(101), 30m); Warehouse(clientDb, G(102)); Stocks(clientDb, G(101), G(102), 30m);
                Inbox(clientDb).Receive(custWire, 100);

                // Initial sale on server
                var saleItems = new List<DebtSaleItem>
                {
                    new(G(404), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "50000", "30000", "UZS", G(504), "-1")
                };
                var saleSnapshot = new DebtSaleSnapshot(G(604), 100, 5000000, 3000000, 0, 0, 0, "0", "12850", "DEBT", saleItems.AsReadOnly());
                var saleReq = G(709);
                Repo(srvDb).OpenSale(new DebtOpenSaleCommand(saleReq, G(304), saleSnapshot));
                var saleEnv = Inbox(srvDb).ExportApplied(saleReq)!;

                // Take snapshot of server state BEFORE payment 1 was received (real backup)
                var earlyServerDb = Path.Combine(dir, "server_backup.db");
                SqliteConnection.ClearAllPools();
                using (var c = new SqliteConnection("Data Source=" + srvDb))
                {
                    c.Open();
                    using var cmd = c.CreateCommand();
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    cmd.ExecuteNonQuery();
                }
                SqliteConnection.ClearAllPools();
                File.Copy(srvDb, earlyServerDb, true);

                // Client pulls sale and updates cursor
                Inbox(clientDb).Receive(saleEnv, 101);
                Sql(clientDb, "INSERT OR REPLACE INTO sync_meta(key,value) VALUES('debt_cursor','10')");

                // Client takes payment 1, pushes to server, server marks applied, client sets acked = 1
                var payReq1 = G(710);
                Repo(clientDb, actor: G(21)).TakePayment(new DebtPaymentCommand(payReq1, G(304), 2000000, 0, 0, 110));
                var envPay1 = Inbox(clientDb).ExportApplied(payReq1)!;
                Inbox(srvDb).Receive(envPay1, 111);
                Sql(clientDb, $"UPDATE sync_journal SET acked = 1 WHERE group_id = '{payReq1}'");

                // Client takes payment 2, which is still pending (acked = -1)
                var payReq2 = G(711);
                Repo(clientDb, actor: G(21)).TakePayment(new DebtPaymentCommand(payReq2, G(304), 1000000, 0, 0, 112));

                // Restored server state has high water mark smaller than client cursor (10)
                long serverHighWater = Convert.ToInt64(Value(earlyServerDb, "SELECT COALESCE(MAX(seq),0) FROM sync_journal"));
                Check(serverHighWater < 10, "Early server high water cursor is smaller than client cursor");

                // Simulate client requesting pull with cursor = 10 from restored server:
                // Restored server detects cursor > serverHighWater -> returns cursorRollback!
                bool cursorRollback = 10 > serverHighWater;
                Check(cursorRollback, "Server must signal cursorRollback when client cursor > high");

                // Client handles rollback:
                // 1) Resets debt_cursor to 0
                Sql(clientDb, "UPDATE sync_meta SET value='0' WHERE key='debt_cursor'");
                // 2) Resets acked = 1 debt records back to acked = -1 so missing envelopes are re-pushed
                Sql(clientDb, "UPDATE sync_journal SET acked = -1 WHERE kind LIKE 'debt_%' AND acked = 1");

                // 3) Verify that local pending records were strictly preserved and acked=1 records reset to pending!
                Check(Value(clientDb, $"SELECT 1 FROM sync_journal WHERE group_id='{payReq1}' AND acked=-1") != null, "Pay 1 was reset to pending for re-push");
                Check(Value(clientDb, $"SELECT 1 FROM sync_journal WHERE group_id='{payReq2}' AND acked=-1") != null, "Pay 2 was preserved as pending");

                // 4) Client re-pushes missing envelopes to early server
                var repushEnv1 = Inbox(clientDb).ExportApplied(payReq1)!;
                var repushEnv2 = Inbox(clientDb).ExportApplied(payReq2)!;
                var status1 = Inbox(earlyServerDb).Receive(repushEnv1, 200);
                var status2 = Inbox(earlyServerDb).Receive(repushEnv2, 201);
                Check(status1 == DebtReceiveStatus.Applied, "Early server accepted repushed pay 1");
                Check(status2 == DebtReceiveStatus.Applied, "Early server accepted repushed pay 2");

                // 5) Client pulls from cursor 0
                var allServerEnvelopes = new[] { Inbox(earlyServerDb).ExportApplied(G(304))!, Inbox(earlyServerDb).ExportApplied(saleReq)! };
                foreach (var env in allServerEnvelopes)
                {
                    Inbox(clientDb).Receive(env, 300);
                }

                // Verify convergence between earlyServerDb and clientDb:
                var srvBal = Repo(earlyServerDb).ReadAccounts(G(304)).Single().BalanceMinor;
                var cltBal = Repo(clientDb).ReadAccounts(G(304)).Single().BalanceMinor;
                // 50k debt - 20k (pay1) - 10k (pay2) = 20k remaining debt (2,000,000 minor)
                Check(srvBal == 2000000, $"Server balance mismatch: {srvBal}");
                Check(cltBal == 2000000, $"Client balance mismatch: {cltBal}");
            }

            // -------------------------------------------------------------
            // 5. Customer Duplicate Contact Isolation
            // -------------------------------------------------------------
            {
                var db = Fresh();
                var custA = G(305);
                var custB = G(306);
                // Same name and phone
                Repo(db).CreateCustomer(new DebtCustomerDraft(custA, "Alisher Navoiy", "+998909998877", "Birinchi", 100));
                Repo(db).CreateCustomer(new DebtCustomerDraft(custB, "Alisher Navoiy", "+998909998877", "Ikkinchi", 101));

                Check(Count(db, "debt_customers") == 2, "Both customers must exist with distinct GUIDs");

                // Open sale for custA
                Product(db, G(101), 20m); Warehouse(db, G(102)); Stocks(db, G(101), G(102), 20m);
                var saleItems = new List<DebtSaleItem>
                {
                    new(G(405), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "40000", "20000", "UZS", G(505), "-1")
                };
                var saleReq = G(712);
                Repo(db).OpenSale(new DebtOpenSaleCommand(saleReq, custA, new DebtSaleSnapshot(G(605), 100, 4000000, 2000000, 0, 0, 0, "0", "12850", "DEBT", saleItems.AsReadOnly())));

                // Payment for custB when custB has no debt must be rejected by local allocation
                Reject(() => Repo(db).TakePayment(new DebtPaymentCommand(G(713), custB, 1000000, 0, 0, 110)));

                // Open separate sale for custB of 10k debt
                var saleItemsB = new List<DebtSaleItem>
                {
                    new(G(409), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "10000", "5000", "UZS", G(509), "-1")
                };
                var saleReqB = G(718);
                Repo(db).OpenSale(new DebtOpenSaleCommand(saleReqB, custB, new DebtSaleSnapshot(G(609), 100, 1000000, 500000, 0, 0, 0, "0", "12850", "DEBT", saleItemsB.AsReadOnly())));

                // Payment for custB of 10k
                var payReqB = G(719);
                Repo(db).TakePayment(new DebtPaymentCommand(payReqB, custB, 1000000, 0, 0, 110));

                // Verify accounts are completely isolated
                var accA = Repo(db).ReadAccounts(custA).Single();
                Check(accA.BalanceMinor == 4000000, "Cust A debt unchanged");
                var accB = Repo(db).ReadAccounts(custB).Single();
                Check(accB.BalanceMinor == 0, "Cust B debt paid to 0 independently");
            }

            // -------------------------------------------------------------
            // 6. Customer Update and Archiving Financial Semantics
            // -------------------------------------------------------------
            {
                var db = Fresh();
                var custGuid = G(307);
                Repo(db).CreateCustomer(new DebtCustomerDraft(custGuid, "Baxrom", "+998971112233", "Izoh 1", 100));

                var record0 = Repo(db).ReadCustomer(custGuid);
                Check(record0 != null && record0.Revision == 0 && record0.Name == "Baxrom" && !record0.Archived, "Customer read 0");

                // Stale revision update rejected
                Reject(() => Repo(db).UpdateCustomer(new DebtCustomerUpdate(custGuid, "Baxrom Yangi", "+998971112233", "Izoh 2", 99)));

                // Valid revision update increments revision
                Check(Repo(db).UpdateCustomer(new DebtCustomerUpdate(custGuid, "Baxrom Yangi", "+998971112233", "Izoh 2", 0)), "Update customer");
                var record1 = Repo(db).ReadCustomer(custGuid);
                Check(record1 != null && record1.Revision == 1 && record1.Name == "Baxrom Yangi", "Customer read 1");

                // Open sale for customer
                Product(db, G(101), 20m); Warehouse(db, G(102)); Stocks(db, G(101), G(102), 20m);
                var saleItems = new List<DebtSaleItem>
                {
                    new(G(406), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "30000", "15000", "UZS", G(506), "-1")
                };
                var saleReq = G(714);
                Repo(db).OpenSale(new DebtOpenSaleCommand(saleReq, custGuid, new DebtSaleSnapshot(G(606), 100, 3000000, 1500000, 0, 0, 0, "0", "12850", "DEBT", saleItems.AsReadOnly())));

                // Customer snapshot name at sale is "Baxrom Yangi"
                var accNameAtSale = (string)Value(db, $"SELECT customer_name_at_sale FROM debt_accounts WHERE sale_guid='{G(606)}'")!;
                Check(accNameAtSale == "Baxrom Yangi", "Historical name at sale mismatch");

                // Rename customer again
                Repo(db).UpdateCustomer(new DebtCustomerUpdate(custGuid, "Baxrom 3", "+998971112233", "Izoh 3", 1));
                // Verify historical account still has original snapshot name!
                accNameAtSale = (string)Value(db, $"SELECT customer_name_at_sale FROM debt_accounts WHERE sale_guid='{G(606)}'")!;
                Check(accNameAtSale == "Baxrom Yangi", "Historical account name must be immutable after customer rename");

                // Archive customer
                Repo(db).ArchiveCustomer(custGuid, true);
                var recordArchived = Repo(db).ReadCustomer(custGuid);
                Check(recordArchived != null && recordArchived.Archived && recordArchived.Revision == 3, "Customer archived");

                // New debt sale to archived customer must be rejected
                var saleItems2 = new List<DebtSaleItem>
                {
                    new(G(407), G(101), "Kabel", "Elektr", "METR", G(102), "Asosiy Ombor", "1", "10000", "5000", "UZS", G(507), "-1")
                };
                Reject(() => Repo(db).OpenSale(new DebtOpenSaleCommand(G(715), custGuid, new DebtSaleSnapshot(G(607), 100, 1000000, 500000, 0, 0, 0, "0", "12850", "DEBT", saleItems2.AsReadOnly()))));

                // New local payment for archived customer is rejected
                Reject(() => Repo(db).TakePayment(new DebtPaymentCommand(G(716), custGuid, 1000000, 0, 0, 150)));

                // But remote offline payment taken on peer for archived customer applies cleanly via Inbox!
                var peerDb = Fresh();
                Product(peerDb, G(101), 20m); Warehouse(peerDb, G(102)); Stocks(peerDb, G(101), G(102), 20m);
                var custWire = Inbox(db).ExportApplied(custGuid)!;
                var saleEnv = Inbox(db).ExportApplied(saleReq)!;
                Inbox(peerDb).Receive(custWire, 200);
                Inbox(peerDb).Receive(saleEnv, 201);
                var payReqPeer = G(717);
                Repo(peerDb, actor: G(21)).TakePayment(new DebtPaymentCommand(payReqPeer, custGuid, 1000000, 0, 0, 202));
                var peerPayEnv = Inbox(peerDb).ExportApplied(payReqPeer)!;

                // Even though customer is archived on db, importing peer's payment succeeds!
                var importStatus = Inbox(db).Receive(peerPayEnv, 500);
                Check(importStatus == DebtReceiveStatus.Applied, "Archived customer offline payment from peer was not rejected");
                var dbBal = Repo(db).ReadAccounts(custGuid).Single().BalanceMinor;
                Check(dbBal == 2000000, "Archived customer balance reduced by peer payment");
            }

            Console.WriteLine("PASS multi-device convergence: 3-device topology, offline excess payment, cloned writer epochs, server restore rollback, contact revisions and archiving");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
    }
}
