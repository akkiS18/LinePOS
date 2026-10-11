using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

static class DebtStage1AccountingTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "debt-stage1-" + Guid.NewGuid() + ".db");
        try
        {
            var db = new DatabaseContext(path);
            var service = new DebtService(db);
            db.SetCardTaxRate(1.8);
            db.SetSetting("last_usd_rate", "12000");
            using var c = new SqliteConnection("Data Source=" + path); c.Open();
            var product = Guid.NewGuid().ToString(); var warehouse = Guid.NewGuid().ToString();
            using (var q = c.CreateCommand())
            {
                q.CommandText = $"INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES('{product}','Cable',50000,100000,10,0,100); INSERT INTO warehouses(guid,name,updated_at) VALUES('{warehouse}','Main',100); INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('{product}','{warehouse}',10,100)";
                q.ExecuteNonQuery();
            }
            var customer = service.CreateCustomer("Fee test", "", "");
            var sale = DebtService.BuildSaleSnapshot(Guid.NewGuid().ToString(), 1000,
                new[] { new DebtCartItemDto(product,"Cable","Cable","DONA",warehouse,"Main",1,100000,50000,"UZS") }, 0,0,12000,0);
            service.OpenDebtSale(Guid.NewGuid().ToString(),customer,sale);
            var request = Guid.NewGuid().ToString();
            service.RecordPayment(customer,4000000,6000000,requestGuid:request,occurredAt:2000);
            var paid = DebtReportProjection.Query(c,2000,3000);
            Check(paid.NetDebtCollectedTotalUz == 100000, "Mixed payment cashflow");
            Check(paid.NetDebtFeeUz == 1080 && Math.Abs(paid.NetDebtFeeUsd!.Value - .09) < 1e-12, "Frozen card fee and FX");
            Check(paid.ClosingDebtMinor == 0, "Fee must not reduce the debt payment");
            db.SetCardTaxRate(3); db.SetSetting("last_usd_rate","24000");
            Check(service.RecordPayment(customer,4000000,6000000,requestGuid:request) == request, "Retry after settings change");
            Check(DebtReportProjection.Query(c,2000,3000).NetDebtCollectedTotalUz == 100000, "Retry duplicated money");
            bool rejected = false;
            try { service.RecordPayment(customer,4000001,6000000,requestGuid:request); } catch (ArgumentException) { rejected=true; }
            Check(rejected,"Changed retry amount must fail");
            service.Repository.ReversePayment(new(Guid.NewGuid().ToString(),request,"Correction",54000,3000));
            var reversed = DebtReportProjection.Query(c,2000,4000);
            Check(reversed.NetDebtCollectedTotalUz == 0, "Reversal must cancel cashflow, not double it");
            Check(reversed.NetDebtFeeUz == 540 && Math.Abs(reversed.NetDebtFeeUsd!.Value - .045) < 1e-12,"Unrefunded fee remains expense");
            Check(reversed.ClosingDebtMinor == 10000000,"Reversal restores debt");
            var reversalOnly = DebtReportProjection.Query(c,3000,4000);
            Check(reversalOnly.NetDebtCollectedTotalUz == -100000 && reversalOnly.NetDebtFeeUz == -540,"Reversal-only date window signs");
            Check(DebtReportProjection.Query(c,2000,3000).NetDebtCollectedTotalUz == 100000,"Exclusive end date");
            service.RecordPayment(customer,10000000,0,requestGuid:Guid.NewGuid().ToString(),occurredAt:4000);
            Check(DebtReportProjection.Query(c,4000,5000).NetDebtFeeUz == 0,"Cash has no card commission");
            // Legacy nonzero fees without FX must never borrow today's rate.
            var legacyCustomer = service.CreateCustomer("Legacy fee", "", "");
            var legacySale = DebtService.BuildSaleSnapshot(Guid.NewGuid().ToString(),1000,
                new[] { new DebtCartItemDto(product,"Cable","Cable","DONA",warehouse,"Main",1,100000,50000,"UZS") },0,0,12000,0);
            service.OpenDebtSale(Guid.NewGuid().ToString(),legacyCustomer,legacySale);
            var legacyRequest = Guid.NewGuid().ToString();
            service.Repository.TakePayment(new(legacyRequest,legacyCustomer,0,10000000,100000,6000));
            Check(DebtReportProjection.Query(c,6000,7000).NetDebtFeeUsd == null,"Missing historical FX is unknown");
            service.Repository.ReversePayment(new(Guid.NewGuid().ToString(),legacyRequest,"Full refund",100000,7000));
            var full = DebtReportProjection.Query(c,6000,8000);
            Check(full.NetDebtCollectedTotalUz == 0 && full.NetDebtFeeUz == 0,"Full reversal cancels both cashflow and fee");
            Check(DebtService.CalculatePaymentFee(25,2m)==1,"Half tiyin rounds up");
            Check(DebtService.CalculatePaymentFee(long.MaxValue,100m)==long.MaxValue,"Large valid fee does not overflow");
            Console.WriteLine("PASS debt stage 1: configured fees, historic FX, retries, mixed/cash payments, signed reversal, date boundaries, rounding");
        }
        finally { SqliteConnection.ClearAllPools(); foreach(var suffix in new[]{"","-wal","-shm"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
    }
}
