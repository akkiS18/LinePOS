using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Debt;

public sealed record DebtCartItemDto(
    string ProductGuid,
    string ProductName,
    string? Category,
    string UnitDisplay,
    string WarehouseGuid,
    string WarehouseName,
    double Quantity,
    double PriceAtSale,
    double CostPrice,
    string CostCurrency);

public enum DebtFilter
{
    All = 0,
    ActiveDebt = 1,
    Overdue = 2,
    Settled = 3,
    Credit = 4
}

public sealed record DebtSummaryDto(
    long TotalActiveDebtMinor,
    int DebtorsCount,
    long OverdueDebtMinor,
    int OverdueDebtorsCount,
    long TotalCreditMinor);

public sealed record DebtCustomerItemDto(
    string Guid,
    string Name,
    string Phone,
    string Note,
    bool Archived,
    long Revision,
    long CreatedAt,
    long BalanceMinor,
    long OriginalDebtMinor,
    long TotalPaidMinor,
    bool HasOverdue,
    string? EarliestDueDate,
    int ActiveAccountsCount)
{
    public string CustomerGuid => Guid;
    public string FullName => Name;
    public bool IsArchived => Archived;
    public double BalanceUz => BalanceMinor / 100.0;
    public string BalanceDisplay => BalanceMinor > 0
        ? $"{BalanceUz:N0} so'm"
        : BalanceMinor < 0
            ? $"+{Math.Abs(BalanceUz):N0} so'm (Haqdor)"
            : "0 so'm (Qarz yo'q)";
    public string PhoneDisplay => string.IsNullOrWhiteSpace(Phone) ? "Telefon kiritilmagan" : Phone;
    public string StatusBadge => Archived
        ? "Arxivlangan"
        : HasOverdue
            ? "⚠️ Muddati o'tgan"
            : BalanceMinor > 0
                ? "🔴 Qarzdor"
                : BalanceMinor < 0
                    ? "🟢 Haqdor"
                    : "⚪ Yopilgan";
}

public sealed record DebtAccountItemDto(
    string AccountGuid,
    string SaleGuid,
    string ReceiptNumber,
    long CreatedAt,
    long OriginalDebtMinor,
    long BalanceMinor,
    string? DueDate,
    bool IsOverdue,
    string CustomerNameAtSale)
{
    public double OriginalDebtUz => OriginalDebtMinor / 100.0;
    public double BalanceUz => BalanceMinor / 100.0;
    public string BalanceDisplay => $"{BalanceUz:N0} so'm";
    public string CreatedAtDisplay => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    public string DueDateDisplay => string.IsNullOrWhiteSpace(DueDate) ? "Muddatsiz" : DueDate;
    public string StatusText => BalanceMinor <= 0
        ? "✅ To'liq yopilgan"
        : IsOverdue
            ? "⚠️ Muddati o'tgan"
            : "⏳ Faol nasiya";
}

public sealed record DebtEventItemDto(
    string EventGuid,
    string RequestGuid,
    string Kind,
    long OccurredAt,
    long CashMinor,
    long CardMinor,
    long FeeMinor,
    string DeviceGuid,
    List<DebtEventLineDto> Lines)
{
    public string OccurredAtDisplay => DateTimeOffset.FromUnixTimeMilliseconds(OccurredAt).LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    public string KindDisplay => Kind switch
    {
        "sale_open" => "🛒 Nasiya savdo",
        "payment" => "💰 Qarz to'lovi",
        "return_offset" => "↩️ Qaytarish hisobidan",
        "credit_refund" => "💸 Kredit qaytarish",
        "credit_transfer" => "🔁 Kredit o'tkazish",
        _ => Kind
    };
    public double TotalPaymentUz => (CashMinor + CardMinor) / 100.0;
    public string AmountDisplay => TotalPaymentUz > 0 ? $"{TotalPaymentUz:N0} so'm" : "-";
}

public sealed record DebtEventLineDto(
    string AccountGuid,
    long DebtDeltaMinor)
{
    public double DeltaUz => DebtDeltaMinor / 100.0;
    public string DeltaDisplay => DebtDeltaMinor < 0
        ? $"-{Math.Abs(DeltaUz):N0} so'm"
        : $"+{DeltaUz:N0} so'm";
}

public sealed record DebtCustomerDetailDto(
    DebtCustomerRecord Customer,
    long BalanceMinor,
    IReadOnlyList<DebtAccountItemDto> Accounts,
    IReadOnlyList<DebtEventItemDto> Events);

public sealed record DebtPaymentAllocationPreview(
    long TotalPaymentMinor,
    long RemainingBalanceMinor,
    IReadOnlyList<DebtAllocationLinePreview> Lines);

public sealed record DebtAllocationLinePreview(
    string AccountGuid,
    string ReceiptNumber,
    long PreviousBalanceMinor,
    long PaidMinor,
    long NewBalanceMinor);

public sealed class DebtService
{
    private readonly DatabaseContext _db;
    private readonly DebtRepository _repo;
    private readonly string _storeGuid;
    private readonly string _hostActorGuid;

    public DebtService(DatabaseContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        var dbPath = _db.DatabaseFilePath;

        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            InstallSyncSchema(conn);
            DebtSchema.Install(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS sync_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT store_guid FROM debt_scope WHERE id=1";
            var existingStore = (string?)cmd.ExecuteScalar();
            if (string.IsNullOrWhiteSpace(existingStore))
            {
                cmd.CommandText = "SELECT value FROM sync_meta WHERE key='server_id'";
                existingStore = (string?)cmd.ExecuteScalar();
                if (string.IsNullOrWhiteSpace(existingStore))
                {
                    existingStore = Guid.NewGuid().ToString("D");
                    cmd.CommandText = "INSERT OR REPLACE INTO sync_meta(key,value) VALUES('server_id',@s)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@s", existingStore);
                    cmd.ExecuteNonQuery();
                }
                cmd.CommandText = "INSERT OR IGNORE INTO debt_scope(id,store_guid) VALUES(1,@id)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", existingStore);
                cmd.ExecuteNonQuery();
            }
            _storeGuid = existingStore;

            cmd.CommandText = "SELECT value FROM sync_meta WHERE key='debt_host_actor_guid'";
            cmd.Parameters.Clear();
            var existingActor = (string?)cmd.ExecuteScalar();
            if (string.IsNullOrWhiteSpace(existingActor))
            {
                existingActor = Guid.NewGuid().ToString("D");
                cmd.CommandText = "INSERT OR REPLACE INTO sync_meta(key,value) VALUES('debt_host_actor_guid',@actor)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@actor", existingActor);
                cmd.ExecuteNonQuery();
            }
            _hostActorGuid = existingActor;
        }

        _repo = new DebtRepository(dbPath, _storeGuid, _hostActorGuid, () => true);
        _repo.BindStore();
    }

    public string StoreGuid => _storeGuid;
    public string HostActorGuid => _hostActorGuid;
    public DebtRepository Repository => _repo;

    public static string FormatDecimal(decimal value)
    {
        var s = value.ToString("0.########", CultureInfo.InvariantCulture);
        if (s.Contains('.'))
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }
        return string.IsNullOrEmpty(s) ? "0" : s;
    }

    public DebtSummaryDto GetSummary()
    {
        using var conn = new SqliteConnection($"Data Source={_db.DatabaseFilePath}");
        conn.Open();

        var customers = GetCustomerList(null, DebtFilter.All);
        long totalDebt = 0;
        int debtorsCount = 0;
        long overdueDebt = 0;
        int overdueDebtorsCount = 0;
        long totalCredit = 0;

        foreach (var c in customers)
        {
            if (c.BalanceMinor > 0)
            {
                totalDebt += c.BalanceMinor;
                debtorsCount++;
                if (c.HasOverdue)
                {
                    overdueDebtorsCount++;
                }
            }
            else if (c.BalanceMinor < 0)
            {
                totalCredit += Math.Abs(c.BalanceMinor);
            }
        }

        // Calculate exact overdue debt sum across all overdue accounts
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.guid, a.original_debt_minor, a.due_date,
                   COALESCE((SELECT SUM(l.debt_delta_minor) FROM debt_event_lines l WHERE l.account_guid=a.guid), 0) as delta
            FROM debt_accounts a
            WHERE a.store_guid=@store AND a.due_date IS NOT NULL AND a.due_date < @today";
        cmd.Parameters.AddWithValue("@store", _storeGuid);
        cmd.Parameters.AddWithValue("@today", today);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var orig = reader.GetInt64(1);
            var delta = reader.GetInt64(3);
            var balance = orig + delta;
            if (balance > 0)
            {
                overdueDebt += balance;
            }
        }

        return new DebtSummaryDto(totalDebt, debtorsCount, overdueDebt, overdueDebtorsCount, totalCredit);
    }

    public List<DebtCustomerItemDto> GetCustomerList(string? searchQuery, DebtFilter filter)
    {
        using var conn = new SqliteConnection($"Data Source={_db.DatabaseFilePath}");
        conn.Open();

        var list = new List<DebtCustomerItemDto>();
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var query = @"
            SELECT c.guid, c.name, c.phone, c.note, c.archived, c.revision, c.created_at,
                   COALESCE((
                       SELECT SUM(a.original_debt_minor)
                       FROM debt_accounts a
                       WHERE a.customer_guid = c.guid AND a.store_guid = c.store_guid
                   ), 0) AS total_original_debt,
                   COALESCE((
                       SELECT SUM(l.debt_delta_minor)
                       FROM debt_event_lines l
                       WHERE l.customer_guid = c.guid AND l.store_guid = c.store_guid
                   ), 0) AS total_deltas,
                   (
                       SELECT MIN(a.due_date)
                       FROM debt_accounts a
                       WHERE a.customer_guid = c.guid AND a.store_guid = c.store_guid
                         AND a.due_date IS NOT NULL
                         AND (a.original_debt_minor + COALESCE((SELECT SUM(l2.debt_delta_minor) FROM debt_event_lines l2 WHERE l2.account_guid = a.guid), 0)) > 0
                   ) AS earliest_due,
                   (
                       SELECT COUNT(*)
                       FROM debt_accounts a
                       WHERE a.customer_guid = c.guid AND a.store_guid = c.store_guid
                         AND (a.original_debt_minor + COALESCE((SELECT SUM(l2.debt_delta_minor) FROM debt_event_lines l2 WHERE l2.account_guid = a.guid), 0)) > 0
                   ) AS active_accounts_count,
                   (
                       SELECT COUNT(*)
                       FROM debt_accounts a
                       WHERE a.customer_guid = c.guid AND a.store_guid = c.store_guid
                         AND a.due_date IS NOT NULL AND a.due_date < @today
                         AND (a.original_debt_minor + COALESCE((SELECT SUM(l2.debt_delta_minor) FROM debt_event_lines l2 WHERE l2.account_guid = a.guid), 0)) > 0
                   ) AS overdue_accounts_count
            FROM debt_customers c
            WHERE c.store_guid = @store";

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query += " AND (c.name LIKE @search OR c.phone LIKE @search)";
        }

        query += " ORDER BY c.archived ASC, (total_original_debt + total_deltas) DESC, c.name ASC";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@store", _storeGuid);
        cmd.Parameters.AddWithValue("@today", today);
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            cmd.Parameters.AddWithValue("@search", $"%{searchQuery.Trim()}%");
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var guid = reader.GetString(0);
            var name = reader.GetString(1);
            var phone = reader.GetString(2);
            var note = reader.GetString(3);
            var archived = reader.GetInt64(4) != 0;
            var revision = reader.GetInt64(5);
            var createdAt = reader.GetInt64(6);
            var originalDebt = reader.GetInt64(7);
            var deltas = reader.GetInt64(8);
            var earliestDue = reader.IsDBNull(9) ? null : reader.GetString(9);
            var activeAccountsCount = Convert.ToInt32(reader.GetInt64(10));
            var overdueAccountsCount = Convert.ToInt32(reader.GetInt64(11));

            var balance = originalDebt + deltas;
            var totalPaid = Math.Max(0, originalDebt - Math.Max(0, balance));
            var hasOverdue = overdueAccountsCount > 0;

            var item = new DebtCustomerItemDto(
                guid, name, phone, note, archived, revision, createdAt,
                balance, originalDebt, totalPaid, hasOverdue, earliestDue, activeAccountsCount);

            // Filter logic
            bool matchesFilter = filter switch
            {
                DebtFilter.All => true,
                DebtFilter.ActiveDebt => balance > 0,
                DebtFilter.Overdue => hasOverdue && balance > 0,
                DebtFilter.Settled => balance == 0,
                DebtFilter.Credit => balance < 0,
                _ => true
            };

            if (matchesFilter)
            {
                list.Add(item);
            }
        }

        return list;
    }

    public DebtCustomerDetailDto? GetCustomerDetails(string customerGuid)
    {
        var customer = _repo.ReadCustomer(customerGuid);
        if (customer == null) return null;

        using var conn = new SqliteConnection($"Data Source={_db.DatabaseFilePath}");
        conn.Open();

        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // 1. Accounts
        var accounts = new List<DebtAccountItemDto>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT a.guid, a.sale_guid, s.id, a.created_at_epoch, a.original_debt_minor, a.due_date, a.customer_name_at_sale,
                       COALESCE((SELECT SUM(debt_delta_minor) FROM debt_event_lines WHERE account_guid=a.guid), 0) as deltas
                FROM (
                    SELECT guid, sale_guid, customer_guid, store_guid, original_debt_minor, due_date, customer_name_at_sale,
                           (SELECT occurred_at FROM debt_events WHERE guid=debt_accounts.opening_event_guid) as created_at_epoch
                    FROM debt_accounts
                    WHERE customer_guid=@cust AND store_guid=@store
                ) a
                JOIN sales s ON s.guid=a.sale_guid
                ORDER BY a.created_at_epoch DESC";
            cmd.Parameters.AddWithValue("@cust", customerGuid);
            cmd.Parameters.AddWithValue("@store", _storeGuid);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var aGuid = reader.GetString(0);
                var saleGuid = reader.GetString(1);
                var saleReceipt = "LP-" + saleGuid.Replace("-", "").ToUpperInvariant();
                var createdAt = reader.GetInt64(3);
                var origDebt = reader.GetInt64(4);
                var dueDate = reader.IsDBNull(5) ? null : reader.GetString(5);
                var custNameAtSale = reader.GetString(6);
                var deltas = reader.GetInt64(7);
                var balance = origDebt + deltas;
                var isOverdue = dueDate != null && string.CompareOrdinal(dueDate, today) < 0 && balance > 0;

                accounts.Add(new DebtAccountItemDto(
                    aGuid, saleGuid, saleReceipt, createdAt, origDebt, balance, dueDate, isOverdue, custNameAtSale));
            }
        }

        // 2. Events & Lines
        var events = new List<DebtEventItemDto>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT guid, request_guid, kind, occurred_at, cash_minor, card_minor, fee_minor, device_guid
                FROM debt_events
                WHERE customer_guid=@cust AND store_guid=@store
                ORDER BY occurred_at DESC";
            cmd.Parameters.AddWithValue("@cust", customerGuid);
            cmd.Parameters.AddWithValue("@store", _storeGuid);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var evGuid = reader.GetString(0);
                var reqGuid = reader.GetString(1);
                var kind = reader.GetString(2);
                var occurredAt = reader.GetInt64(3);
                var cash = reader.GetInt64(4);
                var card = reader.GetInt64(5);
                var fee = reader.GetInt64(6);
                var dev = reader.GetString(7);

                events.Add(new DebtEventItemDto(
                    evGuid, reqGuid, kind, occurredAt, cash, card, fee, dev, new List<DebtEventLineDto>()));
            }
        }

        // Populate event lines
        foreach (var ev in events)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT account_guid, debt_delta_minor FROM debt_event_lines WHERE event_guid=@ev ORDER BY line_index ASC";
            cmd.Parameters.AddWithValue("@ev", ev.EventGuid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ev.Lines.Add(new DebtEventLineDto(reader.GetString(0), reader.GetInt64(1)));
            }
        }

        long totalBalance = accounts.Sum(a => a.BalanceMinor);

        return new DebtCustomerDetailDto(customer, totalBalance, accounts, events);
    }

    public List<DebtCustomerItemDto> GetActiveCustomers()
    {
        return GetCustomerList(null, DebtFilter.All).Where(c => !c.Archived).ToList();
    }

    public string CreateCustomer(string name, string phone, string note)
    {
        var guid = Guid.NewGuid().ToString("D");
        var draft = new DebtCustomerDraft(guid, name?.Trim() ?? "", phone?.Trim() ?? "", note?.Trim() ?? "", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return _repo.CreateCustomer(draft);
    }

    public bool UpdateCustomer(string guid, string name, string phone, string note, long revision)
    {
        var update = new DebtCustomerUpdate(guid, name?.Trim() ?? "", phone?.Trim() ?? "", note?.Trim() ?? "", revision);
        return _repo.UpdateCustomer(update);
    }

    public bool ArchiveCustomer(string guid, bool archive)
    {
        return _repo.ArchiveCustomer(guid, archive);
    }

    public DebtPaymentAllocationPreview PreviewPayment(string customerGuid, long totalPaymentMinor, string? targetAccountGuid = null)
    {
        if (totalPaymentMinor <= 0)
        {
            throw new ArgumentException("To'lov summasi 0 dan katta bo'lishi kerak.");
        }

        var accounts = _repo.ReadAccounts(customerGuid);
        var activeAccounts = accounts.Where(a => a.BalanceMinor > 0).ToList();
        var totalOutstanding = activeAccounts.Sum(a => a.BalanceMinor);

        if (totalPaymentMinor > totalOutstanding)
        {
            throw new ArgumentException($"To'lov summasi jami qarzdan ({totalOutstanding / 100.0:N0} so'm) ko'p bo'lishi mumkin emas.");
        }

        var allocations = DebtAccounting.Allocate(totalPaymentMinor, customerGuid, accounts, targetAccountGuid);

        var previewLines = new List<DebtAllocationLinePreview>();
        foreach (var alloc in allocations)
        {
            var acc = accounts.First(a => a.AccountGuid == alloc.AccountGuid);
            var paid = alloc.AmountMinor;
            var newBal = acc.BalanceMinor - paid;
            var receipt = "LP-" + acc.SaleGuid.Replace("-", "").ToUpperInvariant();
            previewLines.Add(new DebtAllocationLinePreview(alloc.AccountGuid, receipt, acc.BalanceMinor, paid, newBal));
        }

        var newTotalRemaining = totalOutstanding - totalPaymentMinor;
        return new DebtPaymentAllocationPreview(totalPaymentMinor, newTotalRemaining, previewLines);
    }

    public long? GetDebtEventOccurredAt(string requestGuid)
    {
        if (string.IsNullOrWhiteSpace(requestGuid)) return null;
        using var conn = new SqliteConnection($"Data Source={_db.DatabaseFilePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT occurred_at FROM debt_events WHERE request_guid = @r LIMIT 1";
        cmd.Parameters.AddWithValue("@r", requestGuid);
        var res = cmd.ExecuteScalar();
        if (res != null && res != DBNull.Value)
        {
            return Convert.ToInt64(res);
        }
        return null;
    }

    public string RecordPayment(
        string customerGuid,
        long cashMinor,
        long cardMinor,
        string? targetAccountGuid = null,
        string? requestGuid = null,
        long? occurredAt = null)
    {
        var req = requestGuid ?? Guid.NewGuid().ToString("D");
        long timestamp = occurredAt ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (occurredAt == null && !string.IsNullOrEmpty(requestGuid))
        {
            var existingAt = GetDebtEventOccurredAt(req);
            if (existingAt != null)
            {
                timestamp = existingAt.Value;
            }
        }

        var cmd = new DebtPaymentCommand(
            req,
            customerGuid,
            cashMinor,
            cardMinor,
            0,
            timestamp,
            targetAccountGuid);

        return _repo.TakePayment(cmd);
    }

    public static DebtSaleSnapshot BuildSaleSnapshot(
        string saleGuid,
        long occurredAt,
        IReadOnlyList<DebtCartItemDto> items,
        long cashMinor,
        long cardMinor,
        double usdRate,
        double cardTaxRate)
    {
        if (items.Count == 0) throw new ArgumentException("Savatchada tovar yo'q");

        var saleItems = new List<DebtSaleItem>();
        BigInteger totalRevenueUnits = 0;
        BigInteger totalCostUnits = 0;
        var scale = BigInteger.Pow(10, 8);
        var fxUnits = DecimalUnits(FormatDecimal((decimal)usdRate));

        foreach (var item in items)
        {
            var itemGuid = Guid.NewGuid().ToString("D");
            var prodGuid = item.ProductGuid;
            var whGuid = item.WarehouseGuid;
            var opGuid = Guid.NewGuid().ToString("D");

            var qDecimal = (decimal)item.Quantity;
            var pDecimal = (decimal)item.PriceAtSale;
            var cDecimal = (decimal)item.CostPrice;

            var qStr = FormatDecimal(qDecimal);
            var pStr = FormatDecimal(pDecimal);
            var cStr = FormatDecimal(cDecimal);
            var deltaStr = "-" + qStr;

            var qUnits = DecimalUnits(qStr);
            var pUnits = DecimalUnits(pStr);
            var cUnits = DecimalUnits(cStr);

            totalRevenueUnits += qUnits * pUnits;
            totalCostUnits += qUnits * cUnits * (item.CostCurrency == "USD" ? fxUnits : scale);

            saleItems.Add(new DebtSaleItem(
                itemGuid,
                prodGuid,
                item.ProductName,
                item.Category ?? "",
                item.UnitDisplay,
                whGuid,
                item.WarehouseName,
                qStr,
                pStr,
                cStr,
                item.CostCurrency,
                opGuid,
                deltaStr));
        }

        var totalMinor = Rounded(totalRevenueUnits * 100, scale * scale);
        var costMinor = Rounded(totalCostUnits * 100, scale * scale * scale);

        var feeRateStr = FormatDecimal((decimal)cardTaxRate);
        var feeRateUnits = DecimalUnits(feeRateStr);
        var feeMinor = Rounded((BigInteger)cardMinor * feeRateUnits, 100 * scale);

        return new DebtSaleSnapshot(
            saleGuid,
            occurredAt,
            totalMinor,
            costMinor,
            cashMinor,
            cardMinor,
            feeMinor,
            feeRateStr,
            FormatDecimal((decimal)usdRate),
            "DEBT",
            saleItems);
    }

    private static BigInteger DecimalUnits(string s)
    {
        var scale = BigInteger.Pow(10, 8);
        var p = s.Split('.');
        return BigInteger.Parse(p[0], CultureInfo.InvariantCulture) * scale +
            (p.Length == 1 ? BigInteger.Zero : BigInteger.Parse(p[1].PadRight(8, '0'), CultureInfo.InvariantCulture));
    }

    private static long Rounded(BigInteger value, BigInteger divisor)
    {
        var n = (value + divisor / 2) / divisor;
        return (long)n;
    }

    public string OpenDebtSale(
        string requestGuid,
        string customerGuid,
        DebtSaleSnapshot saleSnapshot,
        string? dueDate = null,
        DebtCustomerDraft? newCustomer = null,
        long? userId = 1L)
    {
        var cmd = new DebtOpenSaleCommand(requestGuid, customerGuid, saleSnapshot, dueDate, newCustomer, userId);
        return _repo.OpenSale(cmd);
    }

    private static void InstallSyncSchema(SqliteConnection conn)
    {
        var asm = typeof(DebtService).Assembly;
        var stream = asm.GetManifestResourceStream("WifiSyncSchema");
        if (stream != null)
        {
            using var reader = new System.IO.StreamReader(stream);
            var sql = reader.ReadToEnd();
            foreach (var stmt in sql.Split("-- statement", StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = stmt.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = trimmed;
                try { cmd.ExecuteNonQuery(); } catch { }
            }
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS sync_control (id INTEGER PRIMARY KEY CHECK(id=1), applying INTEGER NOT NULL DEFAULT 0, current_group TEXT NOT NULL DEFAULT '');
                INSERT OR IGNORE INTO sync_control(id,applying) VALUES(1,0);
                CREATE TABLE IF NOT EXISTS sync_journal (seq INTEGER PRIMARY KEY AUTOINCREMENT, op_id TEXT NOT NULL UNIQUE, kind TEXT NOT NULL, entity_guid TEXT NOT NULL, warehouse_guid TEXT NOT NULL DEFAULT '', delta REAL NOT NULL DEFAULT 0, base_revision INTEGER NOT NULL DEFAULT 0, payload TEXT, group_id TEXT NOT NULL DEFAULT '', acked INTEGER NOT NULL DEFAULT 0);
                CREATE TABLE IF NOT EXISTS sync_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
            cmd.ExecuteNonQuery();
        }
    }
}
