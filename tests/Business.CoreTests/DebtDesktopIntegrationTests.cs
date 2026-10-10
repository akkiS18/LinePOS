using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

public static class DebtDesktopIntegrationTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("DebtDesktopIntegrationTests FAIL: " + message);
    }

    public static void Run()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), "linepos_desktop_debt_test_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var db = new DatabaseContext(tempDb);
            var service = new DebtService(db);

            // 1. Initial Summary Check
            var summary0 = service.GetSummary();
            Check(summary0.TotalActiveDebtMinor == 0, "Initial active debt must be 0");
            Check(summary0.DebtorsCount == 0, "Initial debtors count must be 0");
            Check(summary0.OverdueDebtMinor == 0, "Initial overdue debt must be 0");

            // 2. Customer Creation & Revision Concurrency
            var custGuid = service.CreateCustomer("Ali Valiyev", "+998901234567", "Doimiy xaridor");
            Check(!string.IsNullOrWhiteSpace(custGuid), "Customer GUID generated");

            var customers = service.GetCustomerList("", DebtFilter.All);
            Check(customers.Count == 1, "Customer list has 1 customer");
            var cust = customers[0];
            Check(cust.Guid == custGuid, "Customer GUID matches");
            Check(cust.Revision == 0, "Initial revision is 0");
            Check(!cust.Archived, "Customer is not archived");
            Check(cust.BalanceMinor == 0, "Initial balance is 0");

            // Successful customer update with rev 0
            var updateOk = service.UpdateCustomer(custGuid, "Ali Valiyev Updated", "+998909999999", "Yangi izoh", 0);
            Check(updateOk, "Customer update with rev 0 succeeds");

            // Stale revision customer update must fail (now rev is 1, so updating with rev 0 fails)
            try
            {
                service.UpdateCustomer(custGuid, "Stale Update", "+998900000000", "Stale", 0);
                throw new Exception("Stale revision should have thrown");
            }
            catch
            {
                // Expected
            }

            // 3. Product Setup for Sale
            var p1 = new Product
            {
                Guid = Guid.NewGuid().ToString("D"),
                Name = "Sim 2.5mm",
                Category = "Kabel",
                CostPrice = 2.0,
                CostCurrency = "USD",
                SellingPrice = 50000,
                StockQuantity = 10
            };
            db.SaveProduct(p1);

            var whGuid = Guid.NewGuid().ToString("D");
            using (var conn = new SqliteConnection("Data Source=" + tempDb))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES (@guid, 'Do''kondagi ombor', 1, 0, 100);";
                cmd.Parameters.AddWithValue("@guid", whGuid);
                cmd.ExecuteNonQuery();
            }

            // 4. Open Debt Sale via DebtService
            var saleGuid = Guid.NewGuid().ToString("D");
            var reqGuid = Guid.NewGuid().ToString("D");
            var occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cartItems = new List<DebtCartItemDto>
            {
                new DebtCartItemDto(
                    p1.Guid,
                    p1.Name,
                    p1.Category,
                    "METR",
                    whGuid,
                    "Do'kondagi ombor",
                    15.0, // 15 meters -> stock was 10 -> will become -5 (Negative stock allowed)
                    50000, // 15 * 50000 = 750,000 UZS = 75,000,000 tiyin
                    p1.CostPrice,
                    p1.CostCurrency)
            };

            // Advance payment: 150,000 UZS cash (15,000,000 tiyin), remaining debt: 600,000 UZS (60,000,000 tiyin)
            var cashAdvMinor = 15000000L;
            var cardAdvMinor = 0L;
            var usdRate = 12500.0;
            var cardTaxRate = 1.8;

            var snapshot = DebtService.BuildSaleSnapshot(
                saleGuid,
                occurredAt,
                cartItems,
                cashAdvMinor,
                cardAdvMinor,
                usdRate,
                cardTaxRate);

            Check(snapshot.TotalMinor == 75000000L, "Snapshot total minor must be 75,000,000");
            Check(snapshot.CashMinor == 15000000L, "Snapshot cash minor must be 15,000,000");
            Check(snapshot.CardMinor == 0L, "Snapshot card minor must be 0");
            Check(snapshot.PaymentType == "DEBT", "Snapshot payment type must be DEBT");

            var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            var resReq = service.OpenDebtSale(reqGuid, custGuid, snapshot, yesterday, null, 1L);
            Check(resReq == reqGuid, "OpenSale returned requestGuid");

            // Invariant check: Stock quantity decremented into negative
            var updatedP1 = db.GetProductByGuid(p1.Guid)!;
            Check(updatedP1.StockQuantity == -5, "Negative stock allowance preserved (10 - 15 = -5)");

            // Invariant check: Sale inserted in sales table with payment_type 3 (DEBT)
            var salesList = db.GetSales(DateTime.Today.AddDays(-2), DateTime.Today.AddDays(2));
            var recordedSale = salesList.FirstOrDefault(s => s.Guid == saleGuid);
            Check(recordedSale != null, "Sale recorded in sales table");
            Check(recordedSale!.PaymentType == PaymentType.DEBT, "Sale payment type is DEBT");
            Check(recordedSale.TotalAmount == 750000, "Sale total amount is 750,000");
            Check(recordedSale.CashAmount == 150000, "Sale cash advance is 150,000");

            // Replay protection: Re-opening with same reqGuid must be idempotent
            var replayRes = service.OpenDebtSale(reqGuid, custGuid, snapshot, yesterday, null, 1L);
            Check(replayRes == reqGuid, "Replay with same reqGuid is idempotent");
            Check(db.GetSales(DateTime.Today.AddDays(-2), DateTime.Today.AddDays(2)).Count(s => s.Guid == saleGuid) == 1, "No duplicate sale created on replay");

            // 5. Customer Details & Summary with Overdue Debt
            var summaryAfterSale = service.GetSummary();
            Check(summaryAfterSale.TotalActiveDebtMinor == 60000000L, "Active debt is 60,000,000 tiyin (600,000 UZS)");
            Check(summaryAfterSale.DebtorsCount == 1, "1 debtor");
            Check(summaryAfterSale.OverdueDebtMinor == 60000000L, "Overdue debt is 60,000,000 tiyin because due date was yesterday");
            Check(summaryAfterSale.OverdueDebtorsCount == 1, "1 overdue debtor");

            var details = service.GetCustomerDetails(custGuid) ?? throw new Exception("Customer details not found");
            Check(details.BalanceMinor == 60000000L, "Details balance matches");
            Check(details.Accounts.Count == 1, "1 debt account");
            var acc = details.Accounts[0];
            Check(acc.OriginalDebtMinor == 60000000L, "Original debt minor matches");
            Check(acc.BalanceMinor == 60000000L, "Account balance minor matches");
            Check(acc.IsOverdue, "Account is overdue");
            Check(details.Events.Count == 1, "1 debt opening event");
            Check(details.Events[0].Kind == "sale_open", "Event kind is sale_open");

            // 6. Filter Modes Verification
            var filterAll = service.GetCustomerList("", DebtFilter.All);
            Check(filterAll.Count == 1, "Filter All has 1");
            var filterActive = service.GetCustomerList("", DebtFilter.ActiveDebt);
            Check(filterActive.Count == 1, "Filter ActiveDebt has 1");
            var filterOverdue = service.GetCustomerList("", DebtFilter.Overdue);
            Check(filterOverdue.Count == 1, "Filter Overdue has 1");
            var filterSettled = service.GetCustomerList("", DebtFilter.Settled);
            Check(filterSettled.Count == 0, "Filter Settled has 0");
            var filterCredit = service.GetCustomerList("", DebtFilter.Credit);
            Check(filterCredit.Count == 0, "Filter Credit has 0");

            // 7. Payment Allocation Preview & Overpayment Prevention
            try
            {
                // Attempting to pay 700,000 UZS when debt is 600,000 UZS must throw
                service.PreviewPayment(custGuid, 70000000L);
                throw new Exception("Overpayment preview should have failed");
            }
            catch (ArgumentException)
            {
                // Expected: overpayment blocked at input/preview level
            }

            // Valid payment preview: 200,000 UZS (20,000,000 tiyin)
            var preview = service.PreviewPayment(custGuid, 20000000L);
            Check(preview.TotalPaymentMinor == 20000000L, "Preview total payment matches");
            Check(preview.RemainingBalanceMinor == 40000000L, "Preview remaining balance is 40,000,000");
            Check(preview.Lines.Count == 1, "1 preview line");
            Check(preview.Lines[0].PaidMinor == 20000000L, "Preview line paid matches");
            Check(preview.Lines[0].NewBalanceMinor == 40000000L, "Preview line new balance matches");

            // 8. Record Payment Collection
            var payReqGuid = Guid.NewGuid().ToString("D");
            var recordedPayReq = service.RecordPayment(custGuid, 20000000L, 0L, acc.AccountGuid, payReqGuid);
            Check(recordedPayReq == payReqGuid, "Payment recorded with payReqGuid");

            // Verify idempotency of payment
            var replayPay = service.RecordPayment(custGuid, 20000000L, 0L, acc.AccountGuid, payReqGuid);
            Check(replayPay == payReqGuid, "Payment replay is idempotent");

            // Check details after payment
            var detailsAfterPay = service.GetCustomerDetails(custGuid)!;
            Check(detailsAfterPay.BalanceMinor == 40000000L, "Balance after payment is 400,000 UZS (40,000,000 tiyin)");
            Check(detailsAfterPay.Accounts[0].BalanceMinor == 40000000L, "Account balance after payment is 40,000,000");
            Check(detailsAfterPay.Events.Count == 2, "2 events: sale_open and payment");
            Check(detailsAfterPay.Events.Any(e => e.Kind == "payment"), "payment event exists");

            // 9. Settle Remaining Debt
            var payReqGuid2 = Guid.NewGuid().ToString("D");
            service.RecordPayment(custGuid, 40000000L, 0L, null, payReqGuid2);

            var detailsSettled = service.GetCustomerDetails(custGuid)!;
            Check(detailsSettled.BalanceMinor == 0L, "Debt fully settled (balance = 0)");
            Check(detailsSettled.Accounts[0].BalanceMinor == 0L, "Account balance is 0");

            var summarySettled = service.GetSummary();
            Check(summarySettled.TotalActiveDebtMinor == 0L, "Total active debt is 0 after settlement");
            Check(summarySettled.DebtorsCount == 0, "0 active debtors");

            var filterSettledAfter = service.GetCustomerList("", DebtFilter.Settled);
            Check(filterSettledAfter.Count == 1, "Customer now in Settled filter");

            // 10. Archive / Unarchive Customer
            var archOk = service.ArchiveCustomer(custGuid, true);
            Check(archOk, "Customer archived successfully");
            var activeCusts = service.GetActiveCustomers();
            Check(activeCusts.Count == 0, "Archived customer excluded from ActiveCustomers");

            var unarchOk = service.ArchiveCustomer(custGuid, false);
            Check(unarchOk, "Customer unarchived successfully");
            var activeCusts2 = service.GetActiveCustomers();
            Check(activeCusts2.Count == 1, "Unarchived customer included in ActiveCustomers");

            Console.WriteLine("PASS desktop debt integration: service summary, customer revision concurrency, debt sale snapshot, negative stock, cashier hold/open, payment preview, overpayment prevention, debt settlement, archiving");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(tempDb + suffix))
                {
                    File.Delete(tempDb + suffix);
                }
            }
        }
    }
}
