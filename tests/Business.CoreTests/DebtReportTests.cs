using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;

static class DebtReportTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }

    public static void Run()
    {
        Console.WriteLine("RUNNING DebtReportTests (D17-D20, D26)...");
        var tempDb = Path.Combine(Path.GetTempPath(), $"debt_report_test_{Guid.NewGuid():N}.db");
        try
        {
            var dbContext = new DatabaseContext(tempDb);
            var debtService = new DebtService(dbContext);

            // Get store GUID
            string storeGuid;
            using (var conn = new SqliteConnection($"Data Source={tempDb}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT store_guid FROM debt_scope WHERE id = 1";
                storeGuid = (string)cmd.ExecuteScalar()!;
            }

            // =========================================================================
            // D17 TEST: Nasiya bugun, collection ertaga
            // =========================================================================
            var whGuid = Guid.NewGuid().ToString("D");
            using (var conn = new SqliteConnection($"Data Source={tempDb}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES (@guid, 'Asosiy Ombor', 1, 0, 100);";
                cmd.Parameters.AddWithValue("@guid", whGuid);
                cmd.ExecuteNonQuery();
            }
            var custGuid = debtService.CreateCustomer("Ali Valiev", "+998901234567", "D17 mijoz");
            var prod = new Product
            {
                Guid = Guid.NewGuid().ToString("D"),
                Name = "Kabel 2x2.5",
                Category = "Kabellar",
                CostPrice = 60000,
                CostCurrency = "UZS",
                SellingPrice = 100000,
                StockQuantity = 100,
                WarehouseGuid = whGuid
            };
            dbContext.SaveProduct(prod);

            // Day 1 timestamp: 2026-10-01 10:00:00 UTC
            var t1 = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var day1Start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var day1End = new DateTimeOffset(2026, 10, 1, 23, 59, 59, 999, TimeSpan.Zero).ToUnixTimeMilliseconds();

            // Sale 1: Total = 100,000 UZS, Advance = 20,000 UZS (10k cash + 10k card, 180 tax), Debt = 80,000 UZS
            var saleGuid = Guid.NewGuid().ToString("D");
            var sale1 = new Sale
            {
                Guid = saleGuid,
                TotalAmount = 100000,
                TotalCost = 60000,
                CashAmount = 10000,
                CardAmount = 10000,
                TaxAmount = 180,
                PaymentType = PaymentType.DEBT,
                CreatedAt = t1,
                Items = new List<SaleItem>
                {
                    new()
                    {
                        Guid = Guid.NewGuid().ToString("D"),
                        ProductGuid = prod.Guid,
                        ProductName = prod.Name,
                        CategoryAtSale = prod.Category,
                        Quantity = 1,
                        PriceAtSale = 100000,
                        CostAtSale = 60000,
                        CostCurrency = "UZS",
                        WarehouseGuid = whGuid
                    }
                }
            };

            // Commit debt sale via DebtService.OpenDebtSale
            var openReq = Guid.NewGuid().ToString("D");
            var cartItems = sale1.Items.Select(i => new DebtCartItemDto(
                i.ProductGuid, i.ProductName, i.CategoryAtSale, "Dona", i.WarehouseGuid, "Asosiy ombor",
                i.Quantity, i.PriceAtSale, i.CostAtSale, i.CostCurrency)).ToList();

            var saleSnapshot = DebtService.BuildSaleSnapshot(saleGuid, t1, cartItems, 1000000, 1000000, 12850, 0);
            var openRes = debtService.OpenDebtSale(openReq, custGuid, saleSnapshot, "2026-10-15");
            Check(!string.IsNullOrWhiteSpace(openRes), "OpenDebtSale failed");

            // --- Verify Day 1 Reports ---
            var salesDay1 = dbContext.GetSales(
                DateTimeOffset.FromUnixTimeMilliseconds(day1Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day1End).UtcDateTime);
            Check(salesDay1.Count == 1, $"Expected 1 sale on Day 1, got {salesDay1.Count}");

            var linesDay1 = salesDay1.SelectMany(SaleAccounting.Lines).ToList();
            Check(linesDay1.Sum(l => l.TotalPrice) == 100000, "Gross revenue must be 100k on Day 1");
            Check(linesDay1.Sum(l => l.TotalCost) == 60000, "Total cost must be 60k on Day 1");
            Check(linesDay1.Sum(l => l.CashAmount) == 10000, "Sales cash must be 10k on Day 1");
            Check(linesDay1.Sum(l => l.CardAmount) == 10000, "Sales card must be 10k on Day 1");
            Check(linesDay1.Sum(l => l.DebtAmount) == 80000, "Sales debt must be 80k on Day 1");

            var debtReportDay1 = dbContext.GetDebtPeriodSummary(
                DateTimeOffset.FromUnixTimeMilliseconds(day1Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day1End).UtcDateTime);
            Check(debtReportDay1.OpeningDebtMinor == 0, "Opening debt on Day 1 must be 0");
            Check(debtReportDay1.NewDebtIssuedMinor == 8000000, "New debt issued on Day 1 must be 8,000,000 tiyin (80k UZS)");
            Check(debtReportDay1.DebtPaymentsTotalMinor == 0, "Debt payments on Day 1 must be 0");
            Check(debtReportDay1.ClosingDebtMinor == 8000000, "Closing debt on Day 1 must be 8,000,000 tiyin (80k UZS)");
            Check(debtReportDay1.ActiveDebtorsCount == 1, "Active debtors count must be 1 on Day 1");

            // Day 1 Cashflow: Sales Cash (10k) + Sales Card (10k) = 20k
            double cashflowDay1 = linesDay1.Sum(l => l.CashAmount + l.CardAmount) + debtReportDay1.NetDebtCollectedTotalUz;
            Check(cashflowDay1 == 20000, $"Expected Day 1 cashflow 20,000, got {cashflowDay1}");

            // --- Day 2: Customer pays the 80k debt (50k cash, 30k card, fee = 540 tiyin) ---
            var t2 = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var day2Start = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var day2End = new DateTimeOffset(2026, 10, 2, 23, 59, 59, 999, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var payReq = Guid.NewGuid().ToString("D");
            var payRes = debtService.RecordPayment(
                customerGuid: custGuid,
                cashMinor: 5000000,
                cardMinor: 3000000,
                targetAccountGuid: null,
                requestGuid: payReq,
                occurredAt: t2,
                feeMinor: 540);
            Check(!string.IsNullOrWhiteSpace(payRes), "RecordPayment failed");

            // --- Verify Day 2 Reports ---
            var salesDay2 = dbContext.GetSales(
                DateTimeOffset.FromUnixTimeMilliseconds(day2Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day2End).UtcDateTime);
            Check(salesDay2.Count == 0, "Day 2 must have ZERO sales (payment is not a sale)");

            var debtReportDay2 = dbContext.GetDebtPeriodSummary(
                DateTimeOffset.FromUnixTimeMilliseconds(day2Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day2End).UtcDateTime);
            Check(debtReportDay2.OpeningDebtMinor == 8000000, $"Day 2 opening debt must be 80k, got {debtReportDay2.OpeningDebtMinor}");
            Check(debtReportDay2.NewDebtIssuedMinor == 0, "Day 2 new debt must be 0");
            Check(debtReportDay2.DebtPaymentsCashMinor == 5000000, "Day 2 cash collected must be 50k");
            Check(debtReportDay2.DebtPaymentsCardMinor == 3000000, "Day 2 card collected must be 30k");
            Check(debtReportDay2.DebtPaymentsFeeMinor == 540, "Day 2 fee must be 540 tiyin");
            Check(debtReportDay2.DebtPaymentsTotalMinor == 8000000, "Day 2 total collected must be 80k");
            Check(debtReportDay2.ClosingDebtMinor == 0, "Day 2 closing debt must be 0 (fully paid)");
            Check(debtReportDay2.ActiveDebtorsCount == 0, "Day 2 active debtors must be 0");

            // Day 2 Cashflow: 0 sales cash/card + 80k debt collection = 80,000 UZS
            double cashflowDay2 = debtReportDay2.NetDebtCollectedTotalUz;
            Check(cashflowDay2 == 80000, $"Expected Day 2 cashflow 80,000, got {cashflowDay2}");

            // Verify Day 1 sale was NOT mutated:
            var originalSale = dbContext.GetSaleByGuid(saleGuid);
            Check(originalSale != null && originalSale.CreatedAt == t1, "Original sale timestamp was mutated!");

            // =========================================================================
            // D18 TEST: Period boundaries, prior debt, late sync, separate credit
            // =========================================================================
            // Invariant: Opening + PeriodChanges = Closing
            Check(debtReportDay2.OpeningDebtMinor - debtReportDay2.DebtPaymentsTotalMinor == debtReportDay2.ClosingDebtMinor,
                "Invariant failed: Opening - Payments != Closing");

            // Create Customer 2 with credit
            var cust2Guid = debtService.CreateCustomer("Bobur", "+998907654321", "D18 mijoz");
            var sale2Guid = Guid.NewGuid().ToString("D");
            var t3 = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var open2Req = Guid.NewGuid().ToString("D");
            var snap2 = DebtService.BuildSaleSnapshot(sale2Guid, t3, cartItems, 0, 0, 12850, 0);
            debtService.OpenDebtSale(open2Req, cust2Guid, snap2, "2026-10-20");

            // Customer 1 opens a new 50k debt on Day 3:
            var sale3Guid = Guid.NewGuid().ToString("D");
            var snap3 = DebtService.BuildSaleSnapshot(sale3Guid, t3, new List<DebtCartItemDto>
            {
                new(prod.Guid, prod.Name, prod.Category, "Dona", whGuid, "Asosiy ombor", 0.5, 100000, 60000, "UZS")
            }, 0, 0, 12850, 0);
            debtService.OpenDebtSale(Guid.NewGuid().ToString("D"), custGuid, snap3, "2026-10-25");

            // Now Customer 2 overpays by 20,000 UZS (pays 120,000 UZS for 100,000 UZS debt):
            // Using offline payment sync to generate safe credit:
            var phoneDb = Path.Combine(Path.GetTempPath(), $"phone_{Guid.NewGuid():N}.db");
            try
            {
                _ = new DatabaseContext(phoneDb);
                using (var c = new SqliteConnection("Data Source=" + phoneDb))
                {
                    c.Open();
                    using var stream = typeof(DebtMultiDeviceTests).Assembly.GetManifestResourceStream("WifiSyncSchema")!;
                    using var r = new StreamReader(stream);
                    foreach (var statement in r.ReadToEnd().Split("-- statement"))
                    {
                        var trimmed = statement.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        using var q = c.CreateCommand(); q.CommandText = trimmed; q.ExecuteNonQuery();
                    }

                    // Insert product and warehouse in phone db
                    using var pCmd = c.CreateCommand();
                    pCmd.CommandText = "INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES(@g,'Kabel 10k',7000,100000,10,0,100);";
                    pCmd.Parameters.AddWithValue("@g", prod.Guid);
                    pCmd.ExecuteNonQuery();

                    using var wCmd = c.CreateCommand();
                    wCmd.CommandText = "INSERT INTO warehouses(guid,name,updated_at) VALUES(@g,'Asosiy ombor',100);";
                    wCmd.Parameters.AddWithValue("@g", whGuid);
                    wCmd.ExecuteNonQuery();

                    using var sCmd = c.CreateCommand();
                    sCmd.CommandText = "INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p,@w,10,100);";
                    sCmd.Parameters.AddWithValue("@p", prod.Guid);
                    sCmd.Parameters.AddWithValue("@w", whGuid);
                    sCmd.ExecuteNonQuery();
                }
                var phoneActor = "00000000-0000-0000-0000-000000000004";
                var phoneInbox = new DebtEnvelopeInbox(phoneDb, storeGuid, () => true, _ => true, _ => 1L);
                var phoneRepo = new DebtRepository(phoneDb, storeGuid, phoneActor, () => true);
                phoneRepo.BindStore();
                var deskInbox = new DebtEnvelopeInbox(tempDb, storeGuid, () => true, _ => true, _ => 1L);

                var cust2Wire = deskInbox.ExportApplied(cust2Guid);
                Check(cust2Wire != null, "Export cust2Wire failed");
                var sale2Wire = deskInbox.ExportApplied(open2Req);
                Check(sale2Wire != null, "Export sale2Wire failed");

                phoneInbox.Receive(cust2Wire!, t3 + 10);
                phoneInbox.Receive(sale2Wire!, t3 + 20);

                var custDetails = debtService.GetCustomerDetails(cust2Guid);
                var acc2 = custDetails!.Accounts.Single(a => a.SaleGuid == sale2Guid);

                // Desktop settles Customer 2's 100k debt:
                debtService.RecordPayment(cust2Guid, 10000000, 0, acc2.AccountGuid, Guid.NewGuid().ToString("D"), t3 + 500);

                // Phone (offline, hasn't received desktop's payment) takes payment of 20,000 UZS (2,000,000 tiyin)
                var overpayReq = Guid.NewGuid().ToString("D");
                phoneRepo.TakePayment(new DebtPaymentCommand(overpayReq, cust2Guid, 2000000, 0, 0, t3 + 1000, acc2.AccountGuid));
                var overpayEnv = phoneInbox.ExportApplied(overpayReq);
                deskInbox.Receive(overpayEnv!, t3 + 2000);

                // Now: Customer 1 has +50,000 UZS debt (+5,000,000 tiyin).
                //      Customer 2 has -20,000 UZS credit (-2,000,000 tiyin).
                var day3Start = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
                var day3End = new DateTimeOffset(2026, 10, 3, 23, 59, 59, 999, TimeSpan.Zero).ToUnixTimeMilliseconds();

                var debtReportDay3 = dbContext.GetDebtPeriodSummary(
                    DateTimeOffset.FromUnixTimeMilliseconds(day3Start).UtcDateTime,
                    DateTimeOffset.FromUnixTimeMilliseconds(day3End).UtcDateTime);

                // Verify Rule 2: Active Debt and Customer Credit must be SEPARATE!
                Check(debtReportDay3.ClosingDebtMinor == 5000000, $"Closing debt must be 50k, got {debtReportDay3.ClosingDebtMinor}");
                Check(debtReportDay3.ClosingCreditMinor == 2000000, $"Closing credit must be 20k, got {debtReportDay3.ClosingCreditMinor}");
                // They must NEVER be combined to 30,000 UZS net debt!
                Check(debtReportDay3.ClosingDebtMinor != 3000000, "Debt and Credit were erroneously netted together!");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    if (File.Exists(phoneDb + suffix)) File.Delete(phoneDb + suffix);
                }
            }

            // =========================================================================
            // D19 TEST: Category, warehouse, BRAK, return filters
            // =========================================================================
            // Sale with Category "Kabellar" and Warehouse "wh-main"
            var detailedItems = dbContext.GetDetailedReportItems(
                DateTimeOffset.FromUnixTimeMilliseconds(day1Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day1End).UtcDateTime,
                12850,
                categoryFilter: "Kabellar",
                warehouseGuidFilter: whGuid);
            Check(detailedItems.Count == 1, $"Expected 1 item for Kabellar / wh-main, got {detailedItems.Count}");
            Check(detailedItems[0].Category == "Kabellar", "Item category mismatch");
            Check(detailedItems[0].WarehouseGuid == whGuid, "Warehouse guid mismatch");

            // Filter with non-matching category
            var emptyItems = dbContext.GetDetailedReportItems(
                DateTimeOffset.FromUnixTimeMilliseconds(day1Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day1End).UtcDateTime,
                12850,
                categoryFilter: "Santexnika");
            Check(emptyItems.Count == 0, $"Expected 0 items for non-matching category, got {emptyItems.Count}");

            // =========================================================================
            // D20 TEST: Renaming / FX changes preserve historical snapshot
            // =========================================================================
            // Rename product in DB to "Kabel Premium 5x4"
            prod.Name = "Kabel Premium 5x4";
            dbContext.SaveProduct(prod);

            // Re-fetch historical report lines:
            var histLines = dbContext.GetSales(
                DateTimeOffset.FromUnixTimeMilliseconds(day1Start).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(day1End).UtcDateTime)
                .SelectMany(SaleAccounting.Lines).ToList();
            Check(histLines[0].ProductName == "Kabel 2x2.5",
                $"Expected historical name 'Kabel 2x2.5', got '{histLines[0].ProductName}'");

            // =========================================================================
            // D26 TEST: Printer / Share idempotency
            // =========================================================================
            var receiptText1 = DebtReceiptFormatter.BuildReceiptText(sale1);
            var receiptText2 = DebtReceiptFormatter.BuildReceiptText(sale1);
            Check(receiptText1 == receiptText2, "Receipt text must be idempotent");
            Check(receiptText1.Contains("Nasiya") || receiptText1.Contains("Chek: #"), "Receipt text must format debt correctly");

            var custDetailsFinal = debtService.GetCustomerDetails(custGuid);
            var statementText1 = DebtReceiptFormatter.BuildCustomerStatementText(custDetailsFinal!);
            var statementText2 = DebtReceiptFormatter.BuildCustomerStatementText(custDetailsFinal!);
            Check(statementText1 == statementText2, "Statement text must be idempotent");
            Check(statementText1.Contains("Ali Valiev"), "Statement must contain customer name");
            Check(statementText1.Contains("50,000 so'm"), "Statement must contain balance");

            // Verify no events were created by printing:
            using (var conn = new SqliteConnection($"Data Source={tempDb}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM debt_events WHERE customer_guid = @c";
                cmd.Parameters.AddWithValue("@c", custGuid);
                var evCount = Convert.ToInt64(cmd.ExecuteScalar());
                // Calling statement/receipt text did NOT create new events:
                Check(evCount == 3, $"Expected exactly 3 events (open + pay + open2), got {evCount}");
            }

            Console.WriteLine("ALL D17-D20, D26 TESTS PASSED SUCCESSFULLY!");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(tempDb + suffix)) File.Delete(tempDb + suffix);
            }
        }
    }
}
