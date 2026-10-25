using System;
using System.Collections.Generic;
using System.Linq;
using VendingMachine.Logic;

// Dependency-free test runner (no NuGet needed).
// Build & run:  mcs -out:LogicTests.exe LogicTests.cs ../VendingMachine/Logic/*.cs && mono LogicTests.exe
internal static class LogicTests
{
    private static int passed, failed;

    private static void Check(string name, bool condition)
    {
        if (condition) { passed++; Console.WriteLine("  PASS  " + name); }
        else { failed++; Console.WriteLine("  FAIL  " + name); }
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    private static VendingMachineModel NewMachine()
    {
        return new VendingMachineModel(Catalog.Create());
    }

    private static void Insert(VendingMachineModel m, params decimal[] coins)
    {
        string err;
        foreach (var c in coins) m.TryInsertCoin(c, out err);
    }

    private static int Main()
    {
        string err;

        Console.WriteLine("Money");
        Check("Format uses invariant culture", Money.Format(3.5m) == "3.50 PLN");
        Check("Change 4.50 = 2+2+0.50",
            Money.Describe(Money.MakeChange(4.5m)) == "2 × 2.00  +  1 × 0.50");
        Check("Change 0 is empty", Money.MakeChange(0m).Count == 0);
        Check("Change 8.00 = 5+2+1",
            Money.MakeChange(8m).Sum(c => c.Key * c.Value) == 8m && Money.MakeChange(8m).Count == 3);
        Check("Change 0.30 is impossible", Throws<InvalidOperationException>(() => Money.MakeChange(0.3m)));

        Console.WriteLine("Selecting and paying with cash");
        var m = NewMachine();
        var fanta = m.Products[0];
        Check("Select Fanta", m.TrySelect(fanta, out err));
        Check("Cannot insert coin before choosing cash", !m.TryInsertCoin(1m, out err));
        m.ChoosePaymentMethod(PaymentMethod.Cash);
        Check("Reject unknown coin", !m.TryInsertCoin(0.2m, out err));
        Insert(m, 0.5m, 1m, 2m);                     // 3.50 - the FIRST coin must count (old bug)
        Check("First coin is counted (3.50 inserted)", m.Credit == 3.5m);
        Check("Exact payment is accepted", m.IsPaid && m.ChangeDue == 0m);
        var sale = m.PayCash(false);
        Check("Exact payment: no change", sale.Change == 0m && sale.ChangeCoins.Count == 0);
        Check("Stock decreased", fanta.Stock == fanta.Capacity - 1);
        Check("State fully reset after sale (old bug: credit leaked)",
            m.Credit == 0m && m.Selected == null && m.Method == PaymentMethod.None);

        Console.WriteLine("Overpaying and change");
        m.TrySelect(m.Products[3], out err);          // Twix 2.50
        m.ChoosePaymentMethod(PaymentMethod.Cash);
        Insert(m, 5m);
        Check("Change due is 2.50", m.ChangeDue == 2.5m);
        sale = m.PayCash(false);
        Check("Change returned as coins", sale.Change == 2.5m && !sale.ChangeDonated
            && sale.ChangeCoins.Sum(c => c.Key * c.Value) == 2.5m);

        m.TrySelect(m.Products[4], out err);          // Snickers
        m.ChoosePaymentMethod(PaymentMethod.Cash);
        Insert(m, 5m);
        sale = m.PayCash(true);
        Check("Donated change is not returned", sale.ChangeDonated && sale.ChangeCoins.Count == 0 && sale.Change == 2.5m);

        Console.WriteLine("Invalid operations are blocked (old bugs: crashes)");
        m.TrySelect(m.Products[1], out err);
        m.ChoosePaymentMethod(PaymentMethod.Cash);
        Check("Paying with no money throws a controlled error",
            Throws<InvalidOperationException>(() => m.PayCash(false)));
        Insert(m, 1m);
        Check("Underpaying is rejected", Throws<InvalidOperationException>(() => m.PayCash(false)));
        Check("Due amount is 2.50", m.Due == 2.5m);

        Console.WriteLine("Refunds");
        decimal refund = m.Cancel();
        Check("Cancel refunds inserted coins", refund == 1m && m.Credit == 0m && m.Selected == null);
        m.TrySelect(m.Products[1], out err);
        m.ChoosePaymentMethod(PaymentMethod.Cash);
        Insert(m, 2m, 1m);
        refund = m.ChoosePaymentMethod(PaymentMethod.Card);
        Check("Switching cash -> card refunds coins", refund == 3m && m.Credit == 0m);
        Check("Cannot insert coins in card mode", !m.TryInsertCoin(1m, out err));

        Console.WriteLine("Card payment");
        sale = m.PayCard();
        Check("Card pays exact price, no change", sale.Paid == 3.5m && sale.Change == 0m && sale.Method == PaymentMethod.Card);
        Check("PayCard without selection throws", Throws<InvalidOperationException>(() => m.PayCard()));

        Console.WriteLine("Stock");
        var m2 = NewMachine();
        var cola = m2.Products[2];
        for (int i = 0; i < cola.Capacity; i++)
        {
            m2.TrySelect(cola, out err);
            m2.ChoosePaymentMethod(PaymentMethod.Card);
            m2.PayCard();
        }
        Check("Product sells out", cola.SoldOut);
        Check("Sold-out product cannot be selected", !m2.TrySelect(cola, out err) && err.Contains("sold out"));

        Console.WriteLine("Limits");
        var m3 = NewMachine();
        m3.TrySelect(m3.Products[0], out err);
        m3.ChoosePaymentMethod(PaymentMethod.Cash);
        for (int i = 0; i < 10; i++) m3.TryInsertCoin(5m, out err);      // 50.00 = max
        Check("Credit capped at 50.00", m3.Credit == 50m && !m3.TryInsertCoin(0.5m, out err));

        Console.WriteLine("Decimal precision (float would fail here)");
        var m4 = NewMachine();
        m4.TrySelect(m4.Products[3], out err);        // 2.50
        m4.ChoosePaymentMethod(PaymentMethod.Cash);
        for (int i = 0; i < 5; i++) m4.TryInsertCoin(0.5m, out err);
        Check("5 x 0.50 equals price exactly", m4.IsPaid && m4.ChangeDue == 0m);

        Console.WriteLine();
        Console.WriteLine("Result: {0} passed, {1} failed", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
