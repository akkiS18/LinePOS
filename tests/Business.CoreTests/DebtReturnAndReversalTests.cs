using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;
using PosElectro.Desktop.Models;
using PosElectro.Desktop.Returns;

static class DebtReturnAndReversalTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "LinePOS_debt_returns_" + Guid.NewGuid() + ".db");
        try
        {
            var db = new DatabaseContext(path);
            var debtService = new DebtService(db);
            var returnStore = new ReturnStore(path);
            returnStore.Install();

            // Setup store & actor in database
            string storeGuid;
            using (var conn = new SqliteConnection("Data Source=" + path))
            {
                conn.Open();
                using var q = conn.CreateCommand();
                q.CommandText = "SELECT store_guid FROM debt_scope WHERE id=1";
                storeGuid = (string)q.ExecuteScalar()!;
            }
            var cashierGuid = "00000000-0000-0000-0000-000000000002";
            var deviceGuid = "00000000-0000-0000-0000-000000000003";

            var whGuid = Guid.NewGuid().ToString("D");
            using (var conn = new SqliteConnection("Data Source=" + path))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES (@guid, 'Asosiy Ombor', 1, 0, 100);";
                cmd.Parameters.AddWithValue("@guid", whGuid);
                cmd.ExecuteNonQuery();
            }

            // 1. Create product & customer
            var product = new Product
            {
                Guid = Guid.NewGuid().ToString("D"),
                Name = "Elektr Dvigatel 100k",
                SellingPrice = 100000,
                CostPrice = 70000,
                CostCurrency = "UZS",
                StockQuantity = 10
            };
            db.SaveProduct(product);

            var customerGuid = debtService.CreateCustomer("Karim Aliyev", "+998901234567", "Nasiya mijoz");
            Check(!string.IsNullOrWhiteSpace(customerGuid), "Customer not created");

            // =========================================================================
            // D13 TEST: 100k sale / 20k paid / 80k debt
            // -------------------------------------------------------------------------
            // We open a 100k debt sale with 20k advance (cash = 20k, debt = 80k).
            // =========================================================================
            var saleGuid = Guid.NewGuid().ToString("D");
            var cartItems = new List<DebtCartItemDto>
            {
                new(product.Guid, product.Name, "Motorlar", "DONA", whGuid, "Asosiy Ombor", 1, 100000, 70000, "UZS")
            };
            var saleSnapshot = DebtService.BuildSaleSnapshot(saleGuid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), cartItems, 2000000, 0, 12800, 0);
            var openSaleReq = Guid.NewGuid().ToString("D");
            var openRes = debtService.OpenDebtSale(openSaleReq, customerGuid, saleSnapshot, "2026-11-01");
            Check(!string.IsNullOrWhiteSpace(openRes), "Open sale failed");

            // Customer balance must be 80,000 UZS (8,000,000 tiyin)
            var customerDetails = debtService.GetCustomerDetails(customerGuid);
            Check(customerDetails!.BalanceMinor == 8000000, $"Initial debt balance mismatch: expected 8000000, got {customerDetails.BalanceMinor}");
            var debtAccount = customerDetails.Accounts.Single(a => a.SaleGuid == saleGuid);
            Check(debtAccount.BalanceMinor == 8000000, "Account balance mismatch");

            // Return Quote test:
            var quote = returnStore.Quote(saleGuid);
            Check(quote.DebtAccountGuid == debtAccount.AccountGuid, "Quote missing DebtAccountGuid");
            Check(quote.CustomerGuid == customerGuid, "Quote missing CustomerGuid");
            Check(quote.AccountBalanceMinor == 8000000, "Quote AccountBalanceMinor mismatch");

            // --- Case A: 30k partial return -> debt 50k, refund 0 ---
            // Let's create a second product and sale for partial return breakdown
            var prodSmall = new Product
            {
                Guid = Guid.NewGuid().ToString("D"),
                Name = "Kabel 10k",
                SellingPrice = 10000,
                CostPrice = 7000,
                CostCurrency = "UZS",
                StockQuantity = 10
            };
            db.SaveProduct(prodSmall);

            var saleGuid100k = Guid.NewGuid().ToString("D");
            var items10 = new List<DebtCartItemDto>
            {
                new(prodSmall.Guid, prodSmall.Name, "Kabel", "METR", whGuid, "Asosiy Ombor", 10, 10000, 7000, "UZS")
            };
            var snap100k = DebtService.BuildSaleSnapshot(saleGuid100k, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), items10, 2000000, 0, 12800, 0);
            debtService.OpenDebtSale(Guid.NewGuid().ToString("D"), customerGuid, snap100k, "2026-11-01");

            var acc100k = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleGuid100k);
            Check(acc100k.BalanceMinor == 8000000, "Account 100k balance should be 80k UZS");

            // Quote 3 items returned (30k return):
            // Debt is 80k. 30k return is less than 80k debt -> debtOffset = 30k, refund = 0.
            var quote100k = returnStore.Quote(saleGuid100k);
            var lineGuid100k = quote100k.Lines[0].Guid;
            var req30k = new ReturnRequest(
                Guid.NewGuid().ToString("D"),
                saleGuid100k,
                "Nuqsonli tovar qaytarildi",
                0, // Cash refund = 0
                0, // Card refund = 0
                0, // Fee reversal = 0
                new() { new(lineGuid100k, 3m, whGuid, true) }
            );

            var res30k = returnStore.Commit(req30k, cashierGuid, deviceGuid);
            Check(res30k.Refund == 0, $"Expected 0 cash refund, got {res30k.Refund}");
            Check(res30k.DebtOffset == 30000, $"Expected 30000 debt offset, got {res30k.DebtOffset}");
            Check(!string.IsNullOrWhiteSpace(res30k.DebtEventGuid), "Return missing DebtEventGuid");

            // Customer details after 30k return: account balance should be 50k (5,000,000 tiyin)
            var accAfter30k = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleGuid100k);
            Check(accAfter30k.BalanceMinor == 5000000, $"Account balance after 30k return should be 50k UZS (5,000,000 tiyin), got {accAfter30k.BalanceMinor}");

            // --- Case B: 90k return from remaining 7 items (70k remaining) ---
            // Remaining items on saleGuid100k is 7 items (value 70k). Current debt is 50k.
            // When all 7 items returned (70k return):
            // Debt balance is 50k -> debtOffset = 50k, refund = 20k!
            var req70k = new ReturnRequest(
                Guid.NewGuid().ToString("D"),
                saleGuid100k,
                "Qolgan tovar qaytarildi",
                20000, // Cash refund = 20,000 UZS
                0,     // Card refund = 0
                0,
                new() { new(lineGuid100k, 7m, whGuid, true) }
            );
            var res70k = returnStore.Commit(req70k, cashierGuid, deviceGuid);
            Check(res70k.Refund == 20000, $"Expected 20,000 cash refund, got {res70k.Refund}");
            Check(res70k.DebtOffset == 50000, $"Expected 50,000 debt offset, got {res70k.DebtOffset}");

            var accAfter70k = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleGuid100k);
            Check(accAfter70k.BalanceMinor == 0, $"Account balance should be settled (0), got {accAfter70k.BalanceMinor}");

            // =========================================================================
            // D14 TEST: Quote and commit race & Late offline payment after return
            // =========================================================================
            // Create a third sale: 50k total, 0 paid, 50k debt (5 items of 10k)
            var saleRaceGuid = Guid.NewGuid().ToString("D");
            var itemsRace = new List<DebtCartItemDto>
            {
                new(prodSmall.Guid, prodSmall.Name, "Kabel", "METR", whGuid, "Asosiy Ombor", 5, 10000, 7000, "UZS")
            };
            var snapRace = DebtService.BuildSaleSnapshot(saleRaceGuid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), itemsRace, 0, 0, 12800, 0);
            var openRaceReq = Guid.NewGuid().ToString("D");
            debtService.OpenDebtSale(openRaceReq, customerGuid, snapRace, "2026-11-01");

            var accRace = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accRace.BalanceMinor == 5000000, "Race sale balance should be 50k");

            // Quote says: return 5 items (50k value). Debt is 50k. Split: offset 50k, refund 0.
            var quoteRace = returnStore.Quote(saleRaceGuid);
            var lineRace = quoteRace.Lines[0].Guid;
            var oldQuoteReq = new ReturnRequest(
                Guid.NewGuid().ToString("D"),
                saleRaceGuid,
                "Race return",
                0, // Cash refund expected 0 based on old quote
                0,
                0,
                new() { new(lineRace, 5m, whGuid, true) }
            );

            // In between Quote and Commit, a payment arrives and is committed: payment of 30k!
            debtService.RecordPayment(customerGuid, 3000000, 0, accRace.AccountGuid);
            var accRaceAfterPayment = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accRaceAfterPayment.BalanceMinor == 2000000, "Balance after payment should be 20k");

            // Now Commit is called with the old quote where cash refund was 0!
            // But with current debt balance of 20k, a 50k return split MUST be: offset 20k, refund 30k!
            // Because requestRefundMinor (0) != split.RefundMinor (30,000,000), it MUST throw InvalidOperationException!
            bool raceDetected = false;
            try
            {
                returnStore.Commit(oldQuoteReq, cashierGuid, deviceGuid);
            }
            catch (InvalidOperationException)
            {
                raceDetected = true;
            }
            Check(raceDetected, "Commit race was not detected! Should throw InvalidOperationException");

            // Ensure no dirty state was committed:
            var accRaceStillClean = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accRaceStillClean.BalanceMinor == 2000000, "Balance mutated despite race rejection");

            // Now commit with the correct split: refund 30k, offset 20k:
            var correctRaceReq = oldQuoteReq with { CashRefund = 30000 };
            var resRaceCommit = returnStore.Commit(correctRaceReq, cashierGuid, deviceGuid);
            Check(resRaceCommit.Refund == 30000 && resRaceCommit.DebtOffset == 20000, "Correct race commit failed");

            // Account is now settled (balance = 0)
            var accRaceSettled = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accRaceSettled.BalanceMinor == 0, "Account should be settled");

            // Late offline payment after return:
            // A mobile device was offline and took a payment of 10,000 UZS (1,000,000 tiyin) before the return occurred.
            // When the mobile device reconnects and pushes its offline payment envelope to desktop,
            // the desktop applies it without failing or mutating the return: creating credit (-1,000,000 tiyin)!
            var phoneDb = Path.Combine(Path.GetTempPath(), "LinePOS_phone_" + Guid.NewGuid() + ".db");
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
                    DebtSchema.Install(c);

                    // Insert product & warehouse in phone db
                    using var pCmd = c.CreateCommand();
                    pCmd.CommandText = "INSERT INTO products(guid,name,cost_price,selling_price,stock_quantity,unit_type,updated_at) VALUES(@g,'Kabel 10k',7000,10000,10,0,100);";
                    pCmd.Parameters.AddWithValue("@g", prodSmall.Guid);
                    pCmd.ExecuteNonQuery();

                    using var wCmd = c.CreateCommand();
                    wCmd.CommandText = "INSERT INTO warehouses(guid,name,updated_at) VALUES(@g,'Asosiy Ombor',100);";
                    wCmd.Parameters.AddWithValue("@g", whGuid);
                    wCmd.ExecuteNonQuery();

                    using var sCmd = c.CreateCommand();
                    sCmd.CommandText = "INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES(@p,@w,10,100);";
                    sCmd.Parameters.AddWithValue("@p", prodSmall.Guid);
                    sCmd.Parameters.AddWithValue("@w", whGuid);
                    sCmd.ExecuteNonQuery();
                }

                var phoneActor = "00000000-0000-0000-0000-000000000004";
                var phoneInbox = new DebtEnvelopeInbox(phoneDb, storeGuid, () => true, _ => true, _ => 1L);
                var phoneRepo = new DebtRepository(phoneDb, storeGuid, phoneActor, () => true);
                phoneRepo.BindStore();

                var deskInbox = new DebtEnvelopeInbox(path, storeGuid, () => true, _ => true, _ => 1L);
                var custWire = deskInbox.ExportApplied(customerGuid);
                Check(custWire != null, "Export custWire failed");
                var saleRaceEnv = deskInbox.ExportApplied(openRaceReq);
                Check(saleRaceEnv != null, "Export saleRaceEnv failed");

                phoneInbox.Receive(custWire!, 100);
                phoneInbox.Receive(saleRaceEnv!, 101);

                // Phone records a 10k payment offline on this account:
                var phonePayReq = Guid.NewGuid().ToString("D");
                phoneRepo.TakePayment(new DebtPaymentCommand(phonePayReq, customerGuid, 1000000, 0, 0, 102, accRace.AccountGuid));
                var phonePayEnv = phoneInbox.ExportApplied(phonePayReq);
                Check(phonePayEnv != null, "Export phonePayEnv failed");

                // Now desktop inbox receives the late offline payment envelope:
                var receiveStatus = deskInbox.Receive(phonePayEnv!, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Check(receiveStatus == DebtReceiveStatus.Applied, $"Expected Applied, got {receiveStatus}");

                // The settled account now reflects the credit (-1,000,000 tiyin):
                var accCreditEarly = debtService.GetCustomerDetails(customerGuid)!.Accounts.Single(a => a.SaleGuid == saleRaceGuid);
                Check(accCreditEarly.BalanceMinor == -1000000, $"Expected credit of -1,000,000 tiyin, got {accCreditEarly.BalanceMinor}");
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
            // D16 TEST: Credit refund & Credit transfer
            // =========================================================================
            // We have source account `accCredit` with -10,000 UZS (-1,000,000 tiyin) credit.
            var accCredit = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accCredit.BalanceMinor == -1000000, $"Expected credit of -1,000,000 tiyin, got {accCredit.BalanceMinor}");

            // 1. Credit refund:
            // Refund 4,000 UZS (400,000 tiyin) cash from the 10k credit:
            var refundReq = Guid.NewGuid().ToString("D");
            var refundGuid = debtService.RefundCredit(customerGuid, accCredit.AccountGuid, 400000, 0, "Mijozga 4k naqd qaytarildi");
            Check(!string.IsNullOrWhiteSpace(refundGuid), "RefundCredit failed");

            // Credit balance should now be -6,000 UZS (-600,000 tiyin):
            var accAfterRefund = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.AccountGuid == accCredit.AccountGuid);
            Check(accAfterRefund.BalanceMinor == -600000, $"Expected -600,000 credit after refund, got {accAfterRefund.BalanceMinor}");

            // Double refund exceeding remaining credit is blocked:
            bool overRefundFailed = false;
            try
            {
                debtService.RefundCredit(customerGuid, accCredit.AccountGuid, 1000000, 0, "Ortiqcha refund");
            }
            catch (Exception)
            {
                overRefundFailed = true;
            }
            Check(overRefundFailed, "Over-refund exceeding credit was not blocked");

            // 2. Credit transfer:
            // We have source account `accCredit` with -6k credit (-600,000 tiyin).
            // We have target account `debtAccount` (from the very first sale) with 80k debt (+8,000,000 tiyin).
            var initialTargetBal = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.AccountGuid == debtAccount.AccountGuid).BalanceMinor;
            Check(initialTargetBal > 0, "Target account must have positive debt");

            // Transfer 6,000 UZS (600,000 tiyin) from source to target:
            var transferGuid = debtService.TransferCredit(customerGuid, accCredit.AccountGuid, debtAccount.AccountGuid, 600000, "Kredit qarzga o'tkazildi");
            Check(!string.IsNullOrWhiteSpace(transferGuid), "TransferCredit failed");

            // Verify source credit reduced from -6k to 0:
            var accSourceAfter = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.AccountGuid == accCredit.AccountGuid);
            Check(accSourceAfter.BalanceMinor == 0, $"Source balance mismatch: expected 0, got {accSourceAfter.BalanceMinor}");

            // Verify target debt reduced by 6k (+600,000 tiyin delta):
            var accTargetAfter = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.AccountGuid == debtAccount.AccountGuid);
            Check(accTargetAfter.BalanceMinor == initialTargetBal - 600000, $"Target balance mismatch: expected {initialTargetBal - 600000}, got {accTargetAfter.BalanceMinor}");

            // Verify that cash and card were NOT moved (credit transfer is purely balance re-allocation):
            var transferEv = debtService.GetCustomerDetails(customerGuid).Events.Single(e => e.Kind == "credit_transfer");
            Check(transferEv.CashMinor == 0 && transferEv.CardMinor == 0, "Credit transfer must have 0 cash and 0 card");
            Check(transferEv.Lines.Count == 2, "Credit transfer must have 2 lines (source +delta, target -delta)");

            // =========================================================================
            // D15 TEST: Return reversal & Payment reversal & Single reversal constraint
            // =========================================================================
            // 1. Return reversal:
            // Reverse resRaceCommit (which had 20k debt offset and 30k cash refund)
            var revReq = new ReturnReversalRequest(
                Guid.NewGuid().ToString("D"),
                resRaceCommit.Guid,
                "Qaytarish noto'g'ri qilingan"
            );
            var revRes = returnStore.Reverse(revReq, cashierGuid, deviceGuid);
            Check(revRes.Refund == -30000, $"Expected refund reversal of -30000, got {revRes.Refund}");

            // Account balance on saleRaceGuid should have restored +20k offset!
            // Balance was 0 (settled by transfer). Adding +20k debt delta makes balance +20k (+2,000,000 tiyin)!
            var accAfterRev = debtService.GetCustomerDetails(customerGuid).Accounts.Single(a => a.SaleGuid == saleRaceGuid);
            Check(accAfterRev.BalanceMinor == 2000000, $"Expected balance +2,000,000 after return reversal, got {accAfterRev.BalanceMinor}");

            // Duplicate return reversal MUST fail:
            bool dupReturnRevFailed = false;
            try
            {
                returnStore.Reverse(revReq with { RequestGuid = Guid.NewGuid().ToString("D") }, cashierGuid, deviceGuid);
            }
            catch (Exception)
            {
                dupReturnRevFailed = true;
            }
            Check(dupReturnRevFailed, "Duplicate return reversal was not blocked");

            // 2. Payment reversal:
            // Find a payment event and reverse it:
            var allEvents = debtService.GetCustomerDetails(customerGuid).Events;
            var paymentEvent = allEvents.First(e => e.Kind == "payment" && e.CanReverse);
            var revPaymentGuid = debtService.ReversePayment(paymentEvent.EventGuid, "To'lov xato kiritildi", paymentEvent.FeeMinor);
            Check(!string.IsNullOrWhiteSpace(revPaymentGuid), "Payment reversal failed");

            // Event is now marked reversed
            var eventsAfterRev = debtService.GetCustomerDetails(customerGuid).Events;
            var revEv = eventsAfterRev.Single(e => e.EventGuid == paymentEvent.EventGuid);
            Check(revEv.IsReversed, "Event isReversed flag not set");
            Check(!revEv.CanReverse, "Event canReverse flag should be false");

            // Duplicate payment reversal MUST fail:
            bool dupPaymentRevFailed = false;
            try
            {
                debtService.ReversePayment(paymentEvent.EventGuid, "Takroriy bekor qilish");
            }
            catch (Exception)
            {
                dupPaymentRevFailed = true;
            }
            Check(dupPaymentRevFailed, "Duplicate payment reversal was not blocked");

            Console.WriteLine("PASS D13-D16 Debt returns, quote race protection, return reversals, payment reversals, credit refunds, credit transfers");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }
    }
}
