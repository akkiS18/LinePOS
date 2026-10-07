using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PosElectro.Desktop.Debt;

static long N(JsonNode n, string key) => n[key]!.GetValue<long>();
static string S(JsonNode n, string key) => n[key]!.GetValue<string>();
static DebtAccount Account(JsonNode n) => new(S(n,"id"), S(n,"sale"), S(n,"customer"), N(n,"at"), N(n,"balance"));
static DebtAccount[] Accounts(JsonNode n) => n["accounts"]!.AsArray().Select(a => Account(a!)).ToArray();
static JsonArray Lines(IEnumerable<DebtLine> lines) => new(lines.Select(l => (JsonNode)new JsonArray(JsonValue.Create(l.AccountGuid), JsonValue.Create(l.DeltaMinor))).ToArray());
static JsonObject Effect(DebtEffect e) => new() { ["lines"] = Lines(e.Lines), ["cash"] = e.CashMinor, ["card"] = e.CardMinor, ["fee"] = e.FeeExpenseMinor, ["revenue"] = e.RevenueMinor };
static DebtEffect Payment(JsonNode n) => DebtAccounting.Payment(N(n,"cash"), N(n,"card"), N(n,"fee"),
    n["allocations"]!.AsArray().Select(a => new DebtAllocation(a![0]!.GetValue<string>(), a[1]!.GetValue<long>())));
static JsonNode Execute(JsonNode n)
{
    switch (S(n,"op"))
    {
        case "parse": return JsonValue.Create(DebtAccounting.ParseUzs(S(n,"text")))!;
        case "sale": return JsonValue.Create(DebtAccounting.NewDebt(N(n,"total"), N(n,"cash"), N(n,"card")))!;
        case "balance": return JsonValue.Create(DebtAccounting.Balance(N(n,"original"), n["deltas"]!.AsArray().Select(d => d!.GetValue<long>())))!;
        case "totals":
            var t = DebtAccounting.Totals(Accounts(n)); return new JsonArray(JsonValue.Create(t.ReceivableMinor), JsonValue.Create(t.CreditMinor));
        case "allocate":
            return new JsonArray(DebtAccounting.Allocate(N(n,"amount"), S(n,"customer"), Accounts(n), n["target"]?.GetValue<string>())
                .Select(a => (JsonNode)new JsonArray(JsonValue.Create(a.AccountGuid), JsonValue.Create(a.AmountMinor))).ToArray());
        case "payment": return Effect(Payment(n));
        case "reverse": return Effect(DebtAccounting.ReversePayment(Payment(n), N(n,"refundedFee")));
        case "return":
            var r = DebtAccounting.SplitReturn(N(n,"value"), N(n,"balance")); return new JsonArray(JsonValue.Create(r.DebtOffsetMinor), JsonValue.Create(r.RefundMinor));
        case "transfer": return Effect(DebtAccounting.TransferCredit(Account(n["source"]!), Account(n["target"]!), N(n,"amount")));
        case "refund": return Effect(DebtAccounting.RefundCredit(Account(n["account"]!), N(n,"cash"), N(n,"card")));
        default: throw new InvalidOperationException("Unknown fixture operation");
    }
}

if (args.Length != 2 && args.Length != 4) throw new ArgumentException("Usage: fixtures.json result.json");
if(args.Length==4) WireTests.Run(args[2],args[3]);
var fixtures = JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray();
var output = new JsonObject();
foreach (var fixture in fixtures)
{
    var f = fixture!; JsonNode actual;
    try { actual = Execute(f); }
    catch (OverflowException) { actual = new JsonObject { ["error"] = "overflow" }; }
    catch (ArgumentException) { actual = new JsonObject { ["error"] = "invalid" }; }
    if (!JsonNode.DeepEquals(actual, f["expected"])) throw new Exception($"{S(f,"id")}: expected {f["expected"]}, actual {actual}");
    output.Add(S(f,"id"), actual);
}
File.WriteAllText(args[1], output.ToJsonString());
Console.WriteLine($"C#: {fixtures.Count} shared debt fixtures passed");
