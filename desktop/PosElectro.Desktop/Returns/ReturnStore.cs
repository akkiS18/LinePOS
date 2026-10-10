using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Returns;

public sealed record ReturnSelection(string SaleItemGuid, decimal Quantity, string WarehouseGuid, bool Resellable);
public sealed record ReturnRequest(string RequestGuid, string SaleGuid, string Reason, decimal CashRefund,
    decimal CardRefund, decimal FeeReversal, List<ReturnSelection> Items);
public sealed record ReturnResult(string Guid, string SaleGuid, decimal Refund, decimal CostReversal, long CreatedAt,
    decimal DebtOffset = 0, string? DebtEventGuid = null);
public sealed record ReturnLine(string Guid, string ProductName, string WarehouseGuid, decimal Sold,
    decimal Returned, decimal Revenue, decimal Cost, decimal Refunded, decimal CostBasis);
public sealed record ReturnQuote(string SaleGuid, List<ReturnLine> Lines, decimal RemainingFee,
    long AccountBalanceMinor = 0, string? DebtAccountGuid = null, string? CustomerGuid = null);

/// <summary>Single LAN authority. All validation, stock, financial and idempotency writes share an immediate transaction.</summary>
public sealed partial class ReturnStore
{
    readonly string path;
    public ReturnStore(string databasePath) { path = databasePath; }
    SqliteConnection Open() { var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); c.Open(); return c; }
    static SqliteCommand Command(SqliteConnection c, SqliteTransaction tx, string sql, params object[] args)
    {
        var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("@p" + i, args[i]);
        return cmd;
    }
    static void Exec(SqliteConnection c, SqliteTransaction tx, string sql, params object[] args)
    { using var cmd = Command(c, tx, sql, args); cmd.ExecuteNonQuery(); }
    static object? Scalar(SqliteConnection c, SqliteTransaction tx, string sql, params object[] args)
    { using var cmd = Command(c, tx, sql, args); return cmd.ExecuteScalar(); }

    public void Install()
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        using var schema = typeof(ReturnStore).Assembly.GetManifestResourceStream("WifiSyncSchema")
            ?? throw new InvalidOperationException("Sinxron sxemasi topilmadi");
        using var schemaReader = new System.IO.StreamReader(schema);
        foreach (var statement in schemaReader.ReadToEnd().Split("-- statement")) Exec(c, tx, statement);
        tx.Commit();
    }

    // Ordinals refer to immutable original insertion order, never to device-local row IDs.
    public static string LineGuid(string saleGuid, int ordinal) => saleGuid + ":" + ordinal.ToString(CultureInfo.InvariantCulture);

    static ReturnAccounting.Prior PriorAmounts(SqliteConnection c, SqliteTransaction tx, string lineGuid)
    {
        using var cmd = Command(c, tx, "SELECT quantity,refund_amount_uzs,cost_basis_uzs FROM return_items WHERE sale_item_guid=@p0", lineGuid);
        using var r = cmd.ExecuteReader(); decimal qty=0, refund=0, cost=0;
        while(r.Read()) { qty += r.GetDecimal(0); refund += r.GetDecimal(1); cost += r.GetDecimal(2); }
        return new(qty,refund,cost);
    }
    public (ReturnRequest? Request, ReturnResult? Result) DesktopDraft(string saleGuid)
    {
        using var c=Open(); using var tx=c.BeginTransaction();
        using var cmd=Command(c,tx,"SELECT payload,result FROM return_drafts WHERE sale_guid=@p0",saleGuid);
        using var r=cmd.ExecuteReader();
        return r.Read() ? (JsonSerializer.Deserialize<ReturnRequest>(r.GetString(0)), r.IsDBNull(1) ? null : JsonSerializer.Deserialize<ReturnResult>(r.GetString(1))) : (null,null);
    }
    public void SaveDesktopDraft(ReturnRequest request, string authority)
    {
        var payload=JsonSerializer.Serialize(request);
        using var c=Open(); using var tx=c.BeginTransaction();
        var existing=Convert.ToString(Scalar(c,tx,"SELECT payload FROM return_drafts WHERE sale_guid=@p0",request.SaleGuid));
        if(!string.IsNullOrEmpty(existing) && existing!=payload) throw new InvalidOperationException("Bu chek uchun oldingi so'rov natijasini avval tekshiring");
        Exec(c,tx,"INSERT OR IGNORE INTO return_drafts(sale_guid,request_guid,authority_guid,payload,state) VALUES(@p0,@p1,@p2,@p3,'submitted')",request.SaleGuid,request.RequestGuid,authority,payload);
        tx.Commit();
    }
    public void FinishDesktopDraft(string requestGuid, ReturnResult? result)
    {
        using var c=Open(); using var tx=c.BeginTransaction();
        if(result==null) Exec(c,tx,"DELETE FROM return_drafts WHERE request_guid=@p0",requestGuid);
        else Exec(c,tx,"UPDATE return_drafts SET result=@p1,state='confirmed' WHERE request_guid=@p0",requestGuid,JsonSerializer.Serialize(result));
        tx.Commit();
    }
    public string OriginalReceipt(string receiptGuid)
    {
        using var c=Open(); using var tx=c.BeginTransaction();
        var original=Convert.ToString(Scalar(c,tx,"SELECT sale_guid FROM returns WHERE guid=@p0",receiptGuid));
        return string.IsNullOrEmpty(original) ? "" : "LP-"+original.Replace("-", "").ToUpperInvariant();
    }
    public string History(string receiptGuid)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        var original = Convert.ToString(Scalar(c, tx, "SELECT sale_guid FROM returns WHERE guid=@p0", receiptGuid));
        var saleGuid = string.IsNullOrEmpty(original) ? receiptGuid : original;
        using var cmd = Command(c, tx, "SELECT guid,created_at,cash_refund+card_refund,reason,status FROM returns WHERE sale_guid=@p0 ORDER BY created_at,guid", saleGuid);
        using var r = cmd.ExecuteReader(); var lines = new List<string>();
        if (!string.IsNullOrEmpty(original)) lines.Add("Asl chek: LP-" + original.Replace("-", "").ToUpperInvariant());
        while (r.Read()) lines.Add($"{(r.GetString(4).StartsWith("reversal:") ? "RV" : "RT")}-{r.GetString(0).Replace("-", "").ToUpperInvariant()} • {DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(1)).LocalDateTime:dd.MM.yyyy HH:mm} • {r.GetDecimal(2):N2} so‘m • {r.GetString(3)}");
        r.Close();
        if (string.IsNullOrEmpty(original) && lines.Count > 0) {
            var sale=ReadSale(c,tx,saleGuid);
            var returned=sale.Items.Select(i=>PriorAmounts(c,tx,i.Guid).Quantity).ToArray();
            lines.Insert(0, returned.All(q=>q==0) ? "Qaytarilmagan (bekor qilingan qaytarishlar bor)" :
                sale.Items.Select((item,i)=>(decimal)item.Quantity==returned[i]).All(v=>v) ? "To‘liq qaytarilgan" : "Qisman qaytarilgan");
        }
        return string.Join(Environment.NewLine, lines);
    }
    public ReturnQuote Quote(string saleGuid)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        var sale = ReadSale(c, tx, saleGuid);
        if ((int)sale.PaymentType >= 6) throw new ArgumentException("Faqat asl savdoni qaytarish mumkin");
        if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM refunds WHERE sale_id=@p0", sale.Id)) > 0)
            throw new ArgumentException("Eski qaytarish yozuvlarini avval tekshiring");
        var financials = SaleAccounting.Lines(sale);
        var lines = new List<ReturnLine>();
        for (var i = 0; i < sale.Items.Count; i++) {
            var item = sale.Items[i];
            var prior = PriorAmounts(c, tx, item.Guid);
            lines.Add(new ReturnLine(item.Guid, item.ProductName, item.WarehouseGuid, (decimal)item.Quantity,
                prior.Quantity, (decimal)financials[i].TotalPrice, (decimal)financials[i].TotalCost, prior.Refund, prior.CostBasis));
        }
        var fee = (decimal)sale.TaxAmount - Convert.ToDecimal(Scalar(c, tx, "SELECT COALESCE(SUM(fee_reversal),0) FROM returns WHERE sale_guid=@p0", saleGuid));
        long accountBalanceMinor = 0; string? debtAccountGuid = null; string? customerGuid = null;
        if (sale.PaymentType == PaymentType.DEBT)
        {
            using var accCmd = Command(c, tx, "SELECT guid, customer_guid, original_debt_minor FROM debt_accounts WHERE sale_guid=@p0", saleGuid);
            using var accR = accCmd.ExecuteReader();
            if (accR.Read())
            {
                debtAccountGuid = accR.GetString(0);
                customerGuid = accR.GetString(1);
                var origDebt = accR.GetInt64(2);
                accR.Close();

                using var linesCmd = Command(c, tx, "SELECT debt_delta_minor FROM debt_event_lines WHERE account_guid=@p0", debtAccountGuid);
                using var linesR = linesCmd.ExecuteReader();
                var deltas = new List<long>();
                while (linesR.Read()) deltas.Add(linesR.GetInt64(0));
                accountBalanceMinor = DebtAccounting.Balance(origDebt, deltas);
            }
        }
        return new ReturnQuote(saleGuid, lines, fee, accountBalanceMinor, debtAccountGuid, customerGuid);
    }

    public ReturnResult Commit(ReturnRequest request, string operatorGuid, string authorityGuid)
    {
        if (!System.Guid.TryParse(request.RequestGuid, out _) || string.IsNullOrWhiteSpace(request.SaleGuid) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 ||
            string.IsNullOrWhiteSpace(operatorGuid) || string.IsNullOrWhiteSpace(authorityGuid) ||
            request.Items == null || request.Items.Count == 0 || request.Items.Count > 1000 ||
            request.Items.Select(i => i.SaleItemGuid).Distinct().Count() != request.Items.Count)
            throw new ArgumentException("Qaytarish so'rovi to'liq emas");
        var canonical = request with { Items = request.Items.OrderBy(i => i.SaleItemGuid, StringComparer.Ordinal).ToList() };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
        using var c = Open(); using var tx = c.BeginTransaction(deferred: false);
        using (var old = Command(c, tx, "SELECT request_hash,result_json FROM returns WHERE request_guid=@p0", request.RequestGuid))
        using (var r = old.ExecuteReader())
        {
            if (r.Read())
            {
                if (r.GetString(0) != hash) throw new ArgumentException("Bir so'rov raqami boshqa ma'lumot bilan qayta yuborildi");
                return JsonSerializer.Deserialize<ReturnResult>(r.GetString(1))!;
            }
        }
        var sale = ReadSale(c, tx, request.SaleGuid);
        if ((int)sale.PaymentType >= 6) throw new ArgumentException("Faqat asl savdoni qaytarish mumkin");
        if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM refunds WHERE sale_id=@p0", sale.Id)) > 0)
            throw new ArgumentException("Ushbu chekda eski qaytarish yozuvlari bor. Avval tarixni tekshiring");
        var financials = SaleAccounting.Lines(sale);
        if (sale.UsdRate <= 0) {
            var usd = sale.Items.Where(i => i.CostCurrency == "USD").Sum(i => i.CostAtSale * i.Quantity);
            var uzs = sale.Items.Where(i => i.CostCurrency != "USD").Sum(i => i.CostAtSale * i.Quantity);
            var inferred = usd > 0 ? (sale.TotalCost - uzs) / usd : 0;
            sale.UsdRate = double.IsFinite(inferred) && inferred > 0 ? inferred : 0;
        }
        var entries = new List<(ReturnSelection Selected, SaleItem Item, ReturnAccounting.Amounts Amount)>();
        foreach (var selected in canonical.Items)
        {
            var index = sale.Items.FindIndex(i => i.Guid == selected.SaleItemGuid);
            if (index < 0) throw new ArgumentException("Asl chek qatori topilmadi");
            var item = sale.Items[index]; var line = financials[index];
            if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM warehouses WHERE guid=@p0 AND is_deleted=0", selected.WarehouseGuid)) != 1)
                throw new ArgumentException("Qabul ombori mavjud emas");
            if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM products WHERE guid=@p0", item.ProductGuid)) != 1)
                throw new ArgumentException("Mahsulot bog'lanishini tekshiring");
            var prior = PriorAmounts(c, tx, selected.SaleItemGuid);
            var amount = ReturnAccounting.Calculate((decimal)item.Quantity, (decimal)line.TotalPrice, (decimal)line.TotalCost, prior, selected.Quantity, selected.Resellable);
            entries.Add((selected, item, amount));
        }
        var total = entries.Sum(e => e.Amount.Refund); var cost = entries.Sum(e => e.Amount.CostReversal);
        var priorFee = Convert.ToDecimal(Scalar(c, tx, "SELECT COALESCE(SUM(fee_reversal),0) FROM returns WHERE sale_guid=@p0", sale.Guid), CultureInfo.InvariantCulture);

        DebtReturnSplit? debtSplit = null;
        string? debtAccountGuid = null;
        string? customerGuid = null;
        string? openingEventGuid = null;
        string? storeGuid = null;
        decimal debtOffset = 0;
        string? debtEventGuid = null;

        if (sale.PaymentType == PaymentType.DEBT)
        {
            using (var accCmd = Command(c, tx, "SELECT guid, customer_guid, store_guid, opening_event_guid, original_debt_minor FROM debt_accounts WHERE sale_guid=@p0", sale.Guid))
            using (var accR = accCmd.ExecuteReader())
            {
                if (accR.Read())
                {
                    debtAccountGuid = accR.GetString(0);
                    customerGuid = accR.GetString(1);
                    storeGuid = accR.GetString(2);
                    openingEventGuid = accR.GetString(3);
                    var origDebt = accR.GetInt64(4);
                    accR.Close();

                    using var linesCmd = Command(c, tx, "SELECT debt_delta_minor FROM debt_event_lines WHERE account_guid=@p0", debtAccountGuid);
                    using var linesR = linesCmd.ExecuteReader();
                    var deltas = new List<long>();
                    while (linesR.Read()) deltas.Add(linesR.GetInt64(0));
                    linesR.Close();

                    var currentBalance = DebtAccounting.Balance(origDebt, deltas);
                    var returnedValueMinor = checked((long)Math.Round(total * 100, MidpointRounding.AwayFromZero));
                    debtSplit = DebtAccounting.SplitReturn(returnedValueMinor, currentBalance);
                    debtOffset = debtSplit.DebtOffsetMinor / 100m;

                    var requestRefundMinor = checked((long)Math.Round((request.CashRefund + request.CardRefund) * 100, MidpointRounding.AwayFromZero));
                    if (debtSplit.RefundMinor != requestRefundMinor)
                        throw new InvalidOperationException("Qarz to'lovi amalga oshirilgani sababli hisob balansi o'zgardi. Qaytarishni qayta hisoblang.");

                    ReturnAccounting.ValidatePayment(debtSplit.RefundMinor / 100m, request.CashRefund, request.CardRefund, request.FeeReversal, (decimal)sale.TaxAmount, priorFee);
                }
                else
                {
                    ReturnAccounting.ValidatePayment(total, request.CashRefund, request.CardRefund, request.FeeReversal, (decimal)sale.TaxAmount, priorFee);
                }
            }
        }
        else
        {
            ReturnAccounting.ValidatePayment(total, request.CashRefund, request.CardRefund, request.FeeReversal, (decimal)sale.TaxAmount, priorFee);
        }

        var guid = System.Guid.NewGuid().ToString(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (debtSplit != null && debtSplit.DebtOffsetMinor > 0)
        {
            debtEventGuid = System.Guid.NewGuid().ToString("D");
            var returnDebtRequestGuid = "return_offset:" + request.RequestGuid;
            var debtSeq = checked(Convert.ToInt64(Scalar(c, tx, "SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0", authorityGuid)) + 1);
            var debtPayload = PosElectro.Desktop.Debt.DebtRepository.Canonical("return_offset", storeGuid!, operatorGuid, returnDebtRequestGuid, customerGuid!, sale.Guid, debtSplit.DebtOffsetMinor.ToString(CultureInfo.InvariantCulture), now.ToString(CultureInfo.InvariantCulture));
            var debtHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(debtPayload))).ToLowerInvariant();
            var cashMinor = -checked((long)Math.Round(request.CashRefund * 100, MidpointRounding.AwayFromZero));
            var cardMinor = -checked((long)Math.Round(request.CardRefund * 100, MidpointRounding.AwayFromZero));
            var feeMinor = -checked((long)Math.Round(request.FeeReversal * 100, MidpointRounding.AwayFromZero));

            Exec(c, tx, @"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid)
VALUES(@p0,@p1,1,'return_offset',@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,NULL,@p13)",
                debtEventGuid, returnDebtRequestGuid, customerGuid!, storeGuid!, operatorGuid, authorityGuid, debtSeq, now,
                debtPayload, debtHash, cashMinor, cardMinor, feeMinor, openingEventGuid!);

            Exec(c, tx, @"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor)
VALUES(@p0,0,@p1,@p2,@p3,@p4)",
                debtEventGuid, debtAccountGuid!, customerGuid!, storeGuid!, -debtSplit.DebtOffsetMinor);
        }

        var actualRefund = request.CashRefund + request.CardRefund;
        var result = new ReturnResult(guid, sale.Guid, actualRefund, cost, now, debtOffset, debtEventGuid);
        Exec(c, tx, @"INSERT INTO returns VALUES(@p0,@p1,@p2,@p3,@p4,'confirmed',@p5,@p6,@p7,@p8,@p9,@p10,@p11)",
            guid, sale.Guid, now, operatorGuid, request.Reason, (double)request.CashRefund, (double)request.CardRefund,
            (double)request.FeeReversal, request.RequestGuid, authorityGuid, hash, JsonSerializer.Serialize(result));
        foreach (var entry in entries)
        {
            var s = entry.Selected; var a = entry.Amount; var item = entry.Item;
            Exec(c, tx, "INSERT INTO return_items VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10)",
                System.Guid.NewGuid().ToString(), guid, s.SaleItemGuid, item.ProductGuid, (double)a.Quantity,
                (double)a.Refund, (double)a.CostBasis, (double)a.CostReversal, s.WarehouseGuid,
                s.Resellable ? "resellable" : "damaged", sale.UsdRate);
            if (s.Resellable)
            {
                Exec(c, tx, @"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3)
ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=quantity+excluded.quantity,updated_at=excluded.updated_at",
                    item.ProductGuid, s.WarehouseGuid, (double)a.Quantity, now);
                Exec(c, tx, "UPDATE products SET stock_quantity=(SELECT COALESCE(SUM(quantity),0) FROM product_stocks WHERE product_guid=@p0) WHERE guid=@p0", item.ProductGuid);
            }
            else Exec(c, tx, @"INSERT INTO return_quarantine VALUES(@p0,@p1,@p2)
ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=quantity+excluded.quantity", item.ProductGuid, s.WarehouseGuid, (double)a.Quantity);
        }
        Exec(c, tx, @"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,created_at,user_id,is_synced,usd_rate)
VALUES(@p0,@p1,@p2,7,@p3,@p4,@p5,0,@p6,1,0,@p7)", guid, -(double)total, -(double)cost,
            -(double)request.CashRefund, -(double)request.CardRefund, -(double)request.FeeReversal, now, sale.UsdRate);
        var receiptId = Convert.ToInt64(Scalar(c, tx, "SELECT last_insert_rowid()"));
        var ordinal = 0;
        foreach (var entry in entries) {
            ordinal++;
            Exec(c, tx, @"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,quantity,price_at_sale,cost_at_sale,cost_currency,warehouse_guid,warehouse_name,category_at_sale,unit_at_sale)
SELECT @p0,@p1,@p2,product_id,product_guid,product_name,@p3,@p4,@p5,'UZS',@p6,(SELECT name FROM warehouses WHERE guid=@p6),category_at_sale,unit_at_sale FROM sale_items WHERE id=@p7",
                LineGuid(guid, ordinal), receiptId, guid, (double)entry.Amount.Quantity,
                -(double)(entry.Amount.Refund / entry.Amount.Quantity), -(double)(entry.Amount.CostReversal / entry.Amount.Quantity),
                entry.Selected.WarehouseGuid, entry.Item.Id);
        }
        // The immutable return event is delivered with the same journal as its stock changes.
        Exec(c, tx, "INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id) VALUES(@p0,'return',@p1,@p2,@p1)",
            System.Guid.NewGuid().ToString(), guid, JsonSerializer.Serialize(result));
        tx.Commit(); return result;
    }

    static Sale ReadSale(SqliteConnection c, SqliteTransaction tx, string guid)
    {
        var sale = new Sale();
        using (var cmd = Command(c, tx, "SELECT * FROM sales WHERE guid=@p0", guid))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read()) throw new ArgumentException("Asl chek topilmadi");
            sale.Id = (long)r["id"]; sale.Guid = guid; sale.TotalAmount = Convert.ToDouble(r["total_amount"]);
            sale.TotalCost = Convert.ToDouble(r["total_cost"]); sale.TaxAmount = Convert.ToDouble(r["tax_amount"]);
            sale.UsdRate = Convert.ToDouble(r["usd_rate"]); sale.PaymentType = (PaymentType)Convert.ToInt32(r["payment_type"]);
        }
        using var items = Command(c, tx, "SELECT * FROM sale_items WHERE sale_id=@p0 ORDER BY id", sale.Id);
        using var ir = items.ExecuteReader();
        while (ir.Read()) sale.Items.Add(new SaleItem {
            Guid = Convert.ToString(ir["guid"])!, Id = (long)ir["id"], ProductGuid = Convert.ToString(ir["product_guid"])!,
            ProductName = Convert.ToString(ir["product_name"])!, WarehouseGuid = Convert.ToString(ir["warehouse_guid"])!,
            Quantity = Convert.ToDouble(ir["quantity"]), PriceAtSale = Convert.ToDouble(ir["price_at_sale"]),
            CostAtSale = Convert.ToDouble(ir["cost_at_sale"]), CostCurrency = Convert.ToString(ir["cost_currency"])!
        });
        return sale;
    }
}
