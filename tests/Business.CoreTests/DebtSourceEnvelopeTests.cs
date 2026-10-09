using System.Globalization;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

static class DebtSourceEnvelopeTests
{
    static string G(int n) => "00000000-0000-0000-0000-" + n.ToString("D12");
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Reject(Action action) {
        try { action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or UnauthorizedAccessException or SqliteException) { return; }
        throw new Exception("Expected rejection did not occur");
    }
    static DebtRepository Repo(string p) => new(p, G(1), G(2), () => true);
    static DebtEnvelopeInbox Inbox(string p, Func<string, long?>? map = null) =>
        new(p, G(1), () => true, _ => true, map ?? (actor => actor == G(2) ? 1 : null));
    static void Exec(SqliteConnection c, SqliteTransaction? tx, string sql, params object[] args) {
        using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = sql;
        for (int i = 0; i < args.Length; i++) q.Parameters.AddWithValue("@p" + i, args[i]);
        q.ExecuteNonQuery();
    }
    static void Sql(string path, string sql) { using var c = new SqliteConnection("Data Source=" + path); c.Open(); Exec(c, null, sql); }
    static object? Value(string path, string sql) { using var c = new SqliteConnection("Data Source=" + path); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar(); }
    static long Count(string p, string table) => Convert.ToInt64(Value(p, "SELECT COUNT(*) FROM " + table));
    static void Setup(string p) {
        _ = new DatabaseContext(p);
        using var c = new SqliteConnection("Data Source=" + p); c.Open();
        using var stream = typeof(DebtSourceEnvelopeTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
        using var r = new StreamReader(stream);
        foreach (var statement in r.ReadToEnd().Split("-- statement")) Exec(c, null, statement);
        Repo(p).BindStore();
    }
    static void Product(string p, string guid, decimal initialStock = 10m) =>
        Sql(p, $"INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('{guid}','Initial Product',60,100,{initialStock.ToString("G", CultureInfo.InvariantCulture)},0,500)");
    static void Warehouse(string p, string guid) =>
        Sql(p, $"INSERT INTO warehouses(guid,name,updated_at) VALUES('{guid}','Main Warehouse',500)");
    static void Stocks(string p, string prodGuid, string whGuid, decimal stock = 10m) =>
        Sql(p, $"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('{prodGuid}','{whGuid}',{stock.ToString("G", CultureInfo.InvariantCulture)},500)");

    public static void Run() {
        var dir = Path.Combine(Path.GetTempPath(), "debt-source-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        int next = 0;
        string Fresh() { var p = Path.Combine(dir, (next++) + ".db"); Setup(p); return p; }

        try {
            // 1. Full cycle: Local Customer + Sale + Payment commit -> Export -> Peer Receive -> Source Echo
            var db1 = Fresh();
            var db2 = Fresh();
            Product(db1, G(11), 10m); Warehouse(db1, G(12)); Stocks(db1, G(11), G(12), 10m);
            Product(db2, G(11), 10m); Warehouse(db2, G(12)); Stocks(db2, G(11), G(12), 10m);

            var repo1 = Repo(db1);
            var inbox1 = Inbox(db1);
            var inbox2 = Inbox(db2);

            // Customer
            var custDraft = new DebtCustomerDraft(G(3), "Hasan Aka", "+998901112233", "doimiy", 100);
            Check(repo1.CreateCustomer(custDraft) == G(3), "Customer creation");
            var custWire = inbox1.ExportApplied(G(3));
            Check(custWire != null && custWire.Length > 0, "Export applied customer");
            Check(inbox2.Receive(custWire!, 101) == DebtReceiveStatus.Applied, "Peer receive customer");
            Check(inbox1.Receive(custWire!, 102) == DebtReceiveStatus.AlreadyApplied, "Source echo customer");
            Check(Count(db1, "debt_customers") == 1, "Duplicate customer on source");
            Check(Count(db2, "debt_customers") == 1, "Customer applied on peer");

            // Sale snapshot & command
            var saleSnapshot = new DebtSaleSnapshot(G(4), 200, 20000, 12000, 2000, 3000, 60, "2", "12000", "DEBT", new[] {
                new DebtSaleItem(G(10), G(11), "Old cable", "Category", "METR", G(12), "Main Warehouse", "1.25", "100", "0.005", "USD", G(13), "-1.25"),
                new DebtSaleItem(G(20), G(11), "Old cable", "Category", "METR", G(12), "Main Warehouse", "0.75", "100", "60", "UZS", G(23), "-0.75")
            });
            var openCmd = new DebtOpenSaleCommand(G(5), G(3), saleSnapshot, "2030-01-01", null, 1);
            Check(repo1.OpenSale(openCmd) == G(5), "Local open sale");

            // Verify local DB 1 mutations
            Check(Count(db1, "sales") == 1, "Local sale created");
            Check(Count(db1, "sale_items") == 2, "Local sale items created");
            Check(Convert.ToInt64(Value(db1, "SELECT payment_type FROM sales WHERE guid='" + G(4) + "'")) == 3, "Desktop payment type 3");
            Check(Convert.ToDouble(Value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "' AND warehouse_guid='" + G(12) + "'")) == 8.0, "Stock deducted on source");
            Check(Convert.ToDouble(Value(db1, "SELECT stock_quantity FROM products WHERE guid='" + G(11) + "'")) == 8.0, "Total stock deducted on source");
            Check(Count(db1, "debt_events") == 1, "Sale open event created");
            Check(repo1.ReadAccounts(G(3)).Single().BalanceMinor == 15000, "Initial debt account balance 15000");
            Check(Convert.ToInt64(Value(db1, "SELECT COUNT(*) FROM sync_journal WHERE group_id='" + G(5) + "' AND acked<>-1")) == 0, "All entries held in journal");

            // Export from DB 1 and Receive in DB 2
            var saleWire = inbox1.ExportApplied(G(5));
            Check(saleWire != null && saleWire.Length > 0, "Export applied sale envelope");
            Check(inbox2.Receive(saleWire!, 201) == DebtReceiveStatus.Applied, "Peer receive sale envelope");

            // Verify peer DB 2 mutations
            Check(Count(db2, "sales") == 1, "Peer sale created");
            Check(Count(db2, "sale_items") == 2, "Peer sale items created");
            Check(Convert.ToDouble(Value(db2, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "' AND warehouse_guid='" + G(12) + "'")) == 8.0, "Stock deducted on peer");
            Check(Inbox(db2).ReadPending(G(5)) == null, "Peer inbox cleared pending");
            Check(new DebtRepository(db2, G(1), G(2), () => true).ReadAccounts(G(3)).Single().BalanceMinor == 15000, "Peer account balance 15000");

            // Source echo
            Check(inbox1.Receive(saleWire!, 202) == DebtReceiveStatus.AlreadyApplied, "Source echo sale envelope");
            Check(Convert.ToDouble(Value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "' AND warehouse_guid='" + G(12) + "'")) == 8.0, "Stock unchanged on source echo");
            Check(Count(db1, "sales") == 1, "Sales count unchanged on source echo");

            // Source replay & conflict
            Check(repo1.OpenSale(openCmd) == G(5), "Source replay same body");
            Check(Convert.ToDouble(Value(db1, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "' AND warehouse_guid='" + G(12) + "'")) == 8.0, "Stock unchanged on source replay");
            Reject(() => repo1.OpenSale(openCmd with { DueDate = "2031-01-01" })); // Altered command rejected

            // Payment
            var payCmd = new DebtPaymentCommand(G(6), G(3), 5000, 0, 0, 300);
            Check(repo1.TakePayment(payCmd) == G(6), "Local take payment");
            Check(repo1.ReadAccounts(G(3)).Single().BalanceMinor == 10000, "Source balance after payment 10000");

            var payWire = inbox1.ExportApplied(G(6));
            Check(payWire != null && payWire.Length > 0, "Export applied payment envelope");
            Check(inbox2.Receive(payWire!, 301) == DebtReceiveStatus.Applied, "Peer receive payment envelope");
            Check(new DebtRepository(db2, G(1), G(2), () => true).ReadAccounts(G(3)).Single().BalanceMinor == 10000, "Peer balance after payment 10000");

            // Payment source echo
            Check(inbox1.Receive(payWire!, 302) == DebtReceiveStatus.AlreadyApplied, "Source echo payment envelope");
            Check(repo1.ReadAccounts(G(3)).Single().BalanceMinor == 10000, "Source balance unchanged on payment echo");

            // 2. Inline Customer creation inside OpenSale
            var db3 = Fresh(); var db4 = Fresh();
            Product(db3, G(11), 10m); Warehouse(db3, G(12)); Stocks(db3, G(11), G(12), 10m);
            Product(db4, G(11), 10m); Warehouse(db4, G(12)); Stocks(db4, G(11), G(12), 10m);
            var repo3 = Repo(db3); var inbox3 = Inbox(db3); var inbox4 = Inbox(db4);

            var inlineCustomer = new DebtCustomerDraft(G(30), "Inline Xaridor", "+998909999999", "inline", 400);
            var inlineSaleSnapshot = new DebtSaleSnapshot(G(40), 400, 20000, 12000, 2000, 3000, 60, "2", "12000", "DEBT", new[] {
                new DebtSaleItem(G(110), G(11), "Old cable", "Category", "METR", G(12), "Main Warehouse", "2", "100", "60", "UZS", G(123), "-2")
            });
            var inlineOpenCmd = new DebtOpenSaleCommand(G(50), G(30), inlineSaleSnapshot, "2030-01-01", inlineCustomer, 1);
            Check(repo3.OpenSale(inlineOpenCmd) == G(50), "Inline customer open sale");

            var inlineWire = inbox3.ExportApplied(G(50));
            Check(inlineWire != null && inlineWire.Length > 0, "Export inline envelope");
            Check(inbox4.Receive(inlineWire!, 401) == DebtReceiveStatus.Applied, "Peer receive inline envelope");
            Check(Count(db4, "debt_customers") == 1, "Peer created customer inline");
            Check(Count(db4, "sales") == 1, "Peer created sale inline");
            Check(inbox3.Receive(inlineWire!, 402) == DebtReceiveStatus.AlreadyApplied, "Source echo inline envelope");

            // 3. Concurrent double submit
            var db5 = Fresh();
            Product(db5, G(11), 20m); Warehouse(db5, G(12)); Stocks(db5, G(11), G(12), 20m);
            var repo5 = Repo(db5);
            repo5.CreateCustomer(new DebtCustomerDraft(G(31), "Concurrent Customer", "", "", 500));
            var concSale = new DebtSaleSnapshot(G(41), 500, 10000, 6000, 1000, 0, 0, "0", "12000", "DEBT", new[] {
                new DebtSaleItem(G(111), G(11), "Cable", "Category", "METR", G(12), "Warehouse", "1", "100", "60", "UZS", G(124), "-1")
            });
            var concCmd = new DebtOpenSaleCommand(G(51), G(31), concSale, null, null, 1);
            Task.WaitAll(
                Task.Run(() => repo5.OpenSale(concCmd)),
                Task.Run(() => repo5.OpenSale(concCmd))
            );
            Check(Count(db5, "sales") == 1, "Concurrent submit produced exactly one sale");
            Check(Convert.ToDouble(Value(db5, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "'")) == 19.0, "Stock decremented exactly once in concurrent submit");

            // 4. Historical metadata immutability: mutating customer/product after sale doesn't change frozen envelope
            Sql(db5, $"UPDATE debt_customers SET name='Mutated Customer Name', note='Mutated Note' WHERE guid='{G(31)}'");
            Sql(db5, $"UPDATE products SET name='Mutated Product Name', selling_price=99999 WHERE guid='{G(11)}'");
            var frozenWire = Inbox(db5).ExportApplied(G(51));
            Check(frozenWire != null, "Frozen wire exported after DB metadata changes");
            var decodedPacket = DebtEnvelope.Decode(frozenWire!, G(1));
            var decodedSale = DebtEnvelope.DecodeSale(decodedPacket.SaleWire);
            Check(decodedSale.Items[0].ProductName == "Cable", "Frozen sale retains original product name");
            Check(decodedSale.Items[0].Price == "100", "Frozen sale retains original price");
            Check(repo5.OpenSale(concCmd) == G(51), "Replay still works after contact/product mutation");

            // 5. Full rollback on failure
            var db6 = Fresh();
            Product(db6, G(11), 10m); Warehouse(db6, G(12)); Stocks(db6, G(11), G(12), 10m);
            var repo6 = Repo(db6);
            repo6.CreateCustomer(new DebtCustomerDraft(G(32), "Rollback Customer", "", "", 600));
            // Trigger failure right when sync_meta attempts to insert debt_envelope_v1
            Sql(db6, "CREATE TRIGGER fail_envelope BEFORE INSERT ON sync_meta WHEN NEW.key LIKE 'debt_envelope_v1:%' BEGIN SELECT RAISE(ABORT,'Injected envelope failure'); END;");
            var failSale = new DebtSaleSnapshot(G(42), 600, 10000, 6000, 1000, 0, 0, "0", "12000", "DEBT", new[] {
                new DebtSaleItem(G(112), G(11), "Cable", "Category", "METR", G(12), "Warehouse", "1", "100", "60", "UZS", G(125), "-1")
            });
            var failCmd = new DebtOpenSaleCommand(G(52), G(32), failSale, null, null, 1);
            Reject(() => repo6.OpenSale(failCmd));
            Check(Count(db6, "sales") == 0, "Sales table rolled back");
            Check(Count(db6, "sale_items") == 0, "Sale items table rolled back");
            Check(Count(db6, "debt_events") == 0, "Debt events table rolled back");
            Check(Count(db6, "debt_accounts") == 0, "Debt accounts table rolled back");
            Check(Count(db6, "debt_command_receipts") == 0, "Command receipts table rolled back");
            Check(Convert.ToInt64(Value(db6, "SELECT COUNT(*) FROM sync_journal WHERE group_id='" + G(52) + "'")) == 0, "Sync journal rolled back");
            Check(Convert.ToDouble(Value(db6, "SELECT quantity FROM product_stocks WHERE product_guid='" + G(11) + "'")) == 10.0, "Product stock untouched on rollback");
            Check((string)Value(db6, "SELECT current_group FROM sync_control")! == "", "Current group reset on rollback");

            // 6. Preflight rejections before commit (size, items, non-roundtripping decimal / precision loss)
            var db7 = Fresh();
            Product(db7, G(11), 10m); Warehouse(db7, G(12)); Stocks(db7, G(11), G(12), 10m);
            var repo7 = Repo(db7);
            repo7.CreateCustomer(new DebtCustomerDraft(G(33), "Preflight Customer", "", "", 700));

            // Oversized items (> 1000)
            var itemsList = new List<DebtSaleItem>();
            for (int i = 0; i < 1001; i++) {
                itemsList.Add(new DebtSaleItem(G(2000 + i), G(11), "C", "C", "U", G(12), "W", "1", "1", "1", "UZS", G(4000 + i), "-1"));
            }
            var tooManyItems = new DebtSaleSnapshot(G(43), 700, 100100, 100100, 0, 0, 0, "0", "12000", "DEBT", itemsList.AsReadOnly());
            Reject(() => repo7.OpenSale(new DebtOpenSaleCommand(G(53), G(33), tooManyItems, null, null, 1)));
            Check(Count(db7, "sales") == 0, "No sales written after item count rejection");

            // Precision loss / non-roundtripping decimal rejected before commit
            var unsafeSale = concSale with {
                TotalMinor = 999999999999000001L,
                Items = new[] {
                    new DebtSaleItem(G(113), G(11), "Cable", "Category", "METR", G(12), "Warehouse", "1", "999999999999.00000001", "60", "UZS", G(126), "-1")
                }
            };
            Reject(() => repo7.OpenSale(new DebtOpenSaleCommand(G(54), G(33), unsafeSale, null, null, 1)));
            Check(Count(db7, "sales") == 0, "No sales written after precision loss rejection");
            Reject(() => DebtSaleReceiver.Real(1000000000000000001m));

            Console.WriteLine("PASS debt source envelope: local commit, export, peer receive, source echo, replay, rollback, preflight");
        } finally {
            SqliteConnection.ClearAllPools();
            Directory.Delete(dir, true);
        }
    }
}
