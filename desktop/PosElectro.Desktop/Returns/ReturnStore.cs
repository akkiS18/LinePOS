using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Returns;

public sealed record ReturnSelection(string SaleItemGuid, decimal Quantity, string WarehouseGuid, bool Resellable);
public sealed record ReturnRequest(string RequestGuid, string SaleGuid, string Reason, decimal CashRefund,
    decimal CardRefund, decimal FeeReversal, List<ReturnSelection> Items);
public sealed record ReturnResult(string Guid, string SaleGuid, decimal Refund, decimal CostReversal, long CreatedAt);

/// <summary>Single LAN authority. All validation, stock, financial and idempotency writes share an immediate transaction.</summary>
public sealed class ReturnStore
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
        Exec(c, tx, @"
CREATE TABLE IF NOT EXISTS returns (
 guid TEXT PRIMARY KEY, sale_guid TEXT NOT NULL, created_at INTEGER NOT NULL,
 operator_guid TEXT NOT NULL, reason TEXT NOT NULL, status TEXT NOT NULL,
 cash_refund REAL NOT NULL, card_refund REAL NOT NULL, fee_reversal REAL NOT NULL,
 request_guid TEXT NOT NULL UNIQUE, authority_guid TEXT NOT NULL,
 request_hash TEXT NOT NULL, result_json TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS returns_sale ON returns(sale_guid);
CREATE TABLE IF NOT EXISTS return_items (
 guid TEXT PRIMARY KEY, return_guid TEXT NOT NULL REFERENCES returns(guid), sale_item_guid TEXT NOT NULL,
 product_guid TEXT NOT NULL, quantity REAL NOT NULL, refund_amount_uzs REAL NOT NULL,
 cost_basis_uzs REAL NOT NULL, cost_reversal_uzs REAL NOT NULL, warehouse_guid TEXT NOT NULL,
 disposition TEXT NOT NULL, original_usd_rate REAL NOT NULL);
CREATE INDEX IF NOT EXISTS returns_line ON return_items(sale_item_guid);
CREATE TABLE IF NOT EXISTS return_quarantine (
 product_guid TEXT NOT NULL, warehouse_guid TEXT NOT NULL, quantity REAL NOT NULL,
 PRIMARY KEY(product_guid,warehouse_guid));");
        tx.Commit();
    }

    // Ordinals refer to immutable original insertion order, never to device-local row IDs.
    public static string LineGuid(string saleGuid, int ordinal) => saleGuid + ":" + ordinal.ToString(CultureInfo.InvariantCulture);

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
        var entries = new List<(ReturnSelection Selected, SaleItem Item, ReturnAccounting.Amounts Amount)>();
        foreach (var selected in canonical.Items)
        {
            var index = sale.Items.FindIndex(i => LineGuid(sale.Guid, sale.Items.IndexOf(i) + 1) == selected.SaleItemGuid);
            if (index < 0) throw new ArgumentException("Asl chek qatori topilmadi");
            var item = sale.Items[index]; var line = financials[index];
            if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM warehouses WHERE guid=@p0 AND is_deleted=0", selected.WarehouseGuid)) != 1)
                throw new ArgumentException("Qabul ombori mavjud emas");
            if (Convert.ToInt64(Scalar(c, tx, "SELECT COUNT(*) FROM products WHERE guid=@p0", item.ProductGuid)) != 1)
                throw new ArgumentException("Mahsulot bog'lanishini tekshiring");
            using var priorCmd = Command(c, tx, "SELECT COALESCE(SUM(quantity),0),COALESCE(SUM(refund_amount_uzs),0),COALESCE(SUM(cost_basis_uzs),0) FROM return_items WHERE sale_item_guid=@p0", selected.SaleItemGuid);
            using var priorReader = priorCmd.ExecuteReader(); priorReader.Read();
            var prior = new ReturnAccounting.Prior(priorReader.GetDecimal(0), priorReader.GetDecimal(1), priorReader.GetDecimal(2));
            var amount = ReturnAccounting.Calculate((decimal)item.Quantity, (decimal)line.TotalPrice, (decimal)line.TotalCost, prior, selected.Quantity, selected.Resellable);
            entries.Add((selected, item, amount));
        }
        var total = entries.Sum(e => e.Amount.Refund); var cost = entries.Sum(e => e.Amount.CostReversal);
        var priorFee = Convert.ToDecimal(Scalar(c, tx, "SELECT COALESCE(SUM(fee_reversal),0) FROM returns WHERE sale_guid=@p0", sale.Guid), CultureInfo.InvariantCulture);
        ReturnAccounting.ValidatePayment(total, request.CashRefund, request.CardRefund, request.FeeReversal, (decimal)sale.TaxAmount, priorFee);
        var guid = System.Guid.NewGuid().ToString(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = new ReturnResult(guid, sale.Guid, total, cost, now);
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
            Id = (long)ir["id"], ProductGuid = Convert.ToString(ir["product_guid"])!,
            Quantity = Convert.ToDouble(ir["quantity"]), PriceAtSale = Convert.ToDouble(ir["price_at_sale"]),
            CostAtSale = Convert.ToDouble(ir["cost_at_sale"]), CostCurrency = Convert.ToString(ir["cost_currency"])!
        });
        return sale;
    }
}
