using PosElectro.Desktop.Models;

static class ReturnAccountingTests
{
    public static void Run()
    {
        void Equal(decimal expected, decimal actual) { if (expected != actual) throw new Exception($"Return: expected {expected}, got {actual}"); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid return accepted"); }
        var empty = new ReturnAccounting.Prior(0, 0, 0);
        var good = ReturnAccounting.Calculate(1, 150000, 120000, empty, 1, true);
        Equal(-2700, 27300 + good.ProfitEffect(0));
        var damaged = ReturnAccounting.Calculate(1, 150000, 120000, empty, 1, false);
        Equal(-122700, 27300 + damaged.ProfitEffect(0));
        Equal(0, 27300 + good.ProfitEffect(2700));
        var prior = empty;
        for (var i = 0; i < 7; i++)
        {
            var part = ReturnAccounting.Calculate(.7m, 100, 33.33m, prior, .1m, i % 2 == 0);
            prior = new(prior.Quantity + part.Quantity, prior.Refund + part.Refund, prior.CostBasis + part.CostBasis);
        }
        Equal(100, prior.Refund); Equal(33.33m, prior.CostBasis);
        Reject(() => ReturnAccounting.Calculate(.7m, 100, 33.33m, prior, .01m, true));
        Reject(() => ReturnAccounting.Calculate(1, 100, 50, empty, 0, true));
        Reject(() => ReturnAccounting.Calculate(1, 100, 50, empty, -1, true));
        Reject(() => ReturnAccounting.Calculate(1, 100, 50, empty, 1.001m, true));
        ReturnAccounting.ValidatePayment(100, 30, 70, 0, 1.8m, 0);
        Reject(() => ReturnAccounting.ValidatePayment(100, 30, 69, 0, 1.8m, 0));
        Reject(() => ReturnAccounting.ValidatePayment(100, 30, 70, 1, 1.8m, 1));
        Reject(() => ReturnAccounting.ValidatePayment(100, 30.001m, 69.999m, 0, 1.8m, 0));
        Console.WriteLine("PASS return accounting: good/damaged profit, fractional cumulative rounding, quantity limits, split payment, actual fee reversal");
    }
}
