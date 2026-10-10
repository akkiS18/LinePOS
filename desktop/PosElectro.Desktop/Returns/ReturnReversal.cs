using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Returns;
public sealed record ReturnReversalRequest(string RequestGuid, string ReturnGuid, string Reason);

public sealed partial class ReturnStore
{
    public ReturnResult Reverse(ReturnReversalRequest request, string operatorGuid, string authorityGuid)
    {
        if (!Guid.TryParse(request.RequestGuid, out _) || !Guid.TryParse(request.ReturnGuid, out _) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 ||
            string.IsNullOrWhiteSpace(operatorGuid) || string.IsNullOrWhiteSpace(authorityGuid))
            throw new ArgumentException("Bekor qilish sababi va so'rov raqami kerak");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
        using var c = Open(); using var tx = c.BeginTransaction(deferred: false);
        using (var cmd = Command(c, tx, "SELECT request_hash,result_json FROM returns WHERE request_guid=@p0", request.RequestGuid))
        using (var r = cmd.ExecuteReader()) {
            if(r.Read()) { if(r.GetString(0) != hash) throw new ArgumentException("So'rov boshqa ma'lumot bilan yuborildi");
                return JsonSerializer.Deserialize<ReturnResult>(r.GetString(1))!; }
        }
        if (Scalar(c,tx,"SELECT guid FROM returns WHERE status=@p0", "reversal:" + request.ReturnGuid) != null)
            throw new ArgumentException("Bu qaytarish allaqachon bekor qilingan");
        string saleGuid; decimal refund, cost; double rate;
        using (var cmd = Command(c,tx,@"SELECT r.sale_guid,r.cash_refund+r.card_refund,s.total_cost,s.usd_rate
FROM returns r JOIN sales s ON s.guid=r.guid WHERE r.guid=@p0 AND r.status='confirmed'",request.ReturnGuid))
        using (var r = cmd.ExecuteReader()) {
            if(!r.Read()) throw new ArgumentException("Tasdiqlangan qaytarish topilmadi");
            saleGuid=r.GetString(0); refund=r.GetDecimal(1); cost=-r.GetDecimal(2); rate=r.GetDouble(3);
        }
        var guid=Guid.NewGuid().ToString(); var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ReturnResult? origResult = null;
        try {
            var origResultJson = Convert.ToString(Scalar(c, tx, "SELECT result_json FROM returns WHERE guid=@p0", request.ReturnGuid));
            if (!string.IsNullOrEmpty(origResultJson))
                origResult = JsonSerializer.Deserialize<ReturnResult>(origResultJson);
        } catch { }

        if (origResult?.DebtEventGuid != null)
        {
            var debtOffsetGuid = origResult.DebtEventGuid;
            if (Scalar(c, tx, "SELECT 1 FROM debt_events WHERE kind='return_reversal' AND reference_guid=@p0", debtOffsetGuid) != null)
                throw new ArgumentException("Bu qaytarish allaqachon bekor qilingan");

            string custGuid, strGuid; long origCash, origCard, origFee;
            using (var dCmd = Command(c, tx, "SELECT customer_guid, store_guid, cash_minor, card_minor, fee_minor FROM debt_events WHERE guid=@p0", debtOffsetGuid))
            using (var dR = dCmd.ExecuteReader())
            {
                if (!dR.Read()) throw new ArgumentException("Asl qaytarish qarz yozuvi topilmadi");
                custGuid = dR.GetString(0); strGuid = dR.GetString(1);
                origCash = dR.GetInt64(2); origCard = dR.GetInt64(3); origFee = dR.GetInt64(4);
            }

            string accGuid; long origDelta;
            using (var lCmd = Command(c, tx, "SELECT account_guid, debt_delta_minor FROM debt_event_lines WHERE event_guid=@p0", debtOffsetGuid))
            using (var lR = lCmd.ExecuteReader())
            {
                if (!lR.Read()) throw new ArgumentException("Asl qaytarish qarz qatori topilmadi");
                accGuid = lR.GetString(0); origDelta = lR.GetInt64(1);
            }

            var revDebtEventGuid = Guid.NewGuid().ToString("D");
            var revDebtRequestGuid = "return_reversal:" + request.RequestGuid;
            var revDebtSeq = checked(Convert.ToInt64(Scalar(c, tx, "SELECT COALESCE(MAX(device_sequence),0) FROM debt_events WHERE device_guid=@p0", authorityGuid)) + 1);
            var revPayload = PosElectro.Desktop.Debt.DebtRepository.Canonical("return_reversal", strGuid, operatorGuid, revDebtRequestGuid, custGuid, debtOffsetGuid, now.ToString(CultureInfo.InvariantCulture), request.Reason);
            var revHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revPayload))).ToLowerInvariant();

            Exec(c, tx, @"INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid)
VALUES(@p0,@p1,1,'return_reversal',@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,NULL,@p13)",
                revDebtEventGuid, revDebtRequestGuid, custGuid, strGuid, operatorGuid, authorityGuid, revDebtSeq, now,
                revPayload, revHash, -origCash, -origCard, -origFee, debtOffsetGuid);

            Exec(c, tx, @"INSERT INTO debt_event_lines(event_guid,line_index,account_guid,customer_guid,store_guid,debt_delta_minor)
VALUES(@p0,0,@p1,@p2,@p3,@p4)",
                revDebtEventGuid, accGuid, custGuid, strGuid, -origDelta);
        }

        var result=new ReturnResult(guid,saleGuid,-refund,-cost,now,origResult!=null?-origResult.DebtOffset:0);
        Exec(c,tx,@"INSERT INTO returns(guid,sale_guid,created_at,operator_guid,reason,status,cash_refund,card_refund,fee_reversal,request_guid,authority_guid,request_hash,result_json)
SELECT @p0,sale_guid,@p1,@p2,@p3,@p4,-cash_refund,-card_refund,-fee_reversal,@p5,@p6,@p7,@p8 FROM returns WHERE guid=@p9",
            guid,now,operatorGuid,request.Reason,"reversal:"+request.ReturnGuid,request.RequestGuid,authorityGuid,hash,JsonSerializer.Serialize(result),request.ReturnGuid);
        var stocks=new List<(string Product,string Warehouse,double Quantity,string Disposition)>();
        using(var cmd=Command(c,tx,"SELECT product_guid,warehouse_guid,quantity,disposition FROM return_items WHERE return_guid=@p0",request.ReturnGuid))
        using(var r=cmd.ExecuteReader()) while(r.Read()) stocks.Add((r.GetString(0),r.GetString(1),r.GetDouble(2),r.GetString(3)));
        foreach(var stock in stocks) {
            if(stock.Disposition=="resellable") {
                Exec(c,tx,@"INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p0,@p1,@p2,@p3)
ON CONFLICT(product_guid,warehouse_guid) DO UPDATE SET quantity=quantity+excluded.quantity,updated_at=excluded.updated_at",stock.Product,stock.Warehouse,-stock.Quantity,now);
                Exec(c,tx,"UPDATE products SET stock_quantity=(SELECT COALESCE(SUM(quantity),0) FROM product_stocks WHERE product_guid=@p0) WHERE guid=@p0",stock.Product);
            } else {
                var quantity=Convert.ToDouble(Scalar(c,tx,"SELECT quantity FROM return_quarantine WHERE product_guid=@p0 AND warehouse_guid=@p1",stock.Product,stock.Warehouse));
                if(quantity + 1e-9 < stock.Quantity) throw new ArgumentException("Nuqsonli qoldiqni avval tekshiring");
                Exec(c,tx,"UPDATE return_quarantine SET quantity=quantity-@p2 WHERE product_guid=@p0 AND warehouse_guid=@p1",stock.Product,stock.Warehouse,stock.Quantity);
            }
        }
        Exec(c,tx,@"INSERT INTO return_items SELECT @p0||':'||guid,@p0,sale_item_guid,product_guid,-quantity,-refund_amount_uzs,-cost_basis_uzs,-cost_reversal_uzs,warehouse_guid,disposition,original_usd_rate FROM return_items WHERE return_guid=@p1",guid,request.ReturnGuid);
        Exec(c,tx,@"INSERT INTO sales(guid,total_amount,total_cost,payment_type,cash_amount,card_amount,tax_amount,tax_rate,created_at,user_id,is_synced,usd_rate)
SELECT @p0,-total_amount,-total_cost,8,-cash_amount,-card_amount,-tax_amount,0,@p1,1,0,usd_rate FROM sales WHERE guid=@p2",guid,now,request.ReturnGuid);
        var id=Convert.ToInt64(Scalar(c,tx,"SELECT last_insert_rowid()"));
        Exec(c,tx,@"INSERT INTO sale_items(guid,sale_id,sale_guid,product_id,product_guid,product_name,quantity,price_at_sale,cost_at_sale,cost_currency,warehouse_guid,warehouse_name,category_at_sale,unit_at_sale)
SELECT @p0||':'||guid,@p1,@p0,product_id,product_guid,product_name,quantity,-price_at_sale,-cost_at_sale,cost_currency,warehouse_guid,warehouse_name,category_at_sale,unit_at_sale FROM sale_items WHERE sale_guid=@p2",guid,id,request.ReturnGuid);
        Exec(c,tx,"INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id) VALUES(@p0,'return',@p1,@p2,@p1)",Guid.NewGuid().ToString(),guid,JsonSerializer.Serialize(result));
        tx.Commit(); return result;
    }
}
