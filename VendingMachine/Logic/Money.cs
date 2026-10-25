using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VendingMachine.Logic
{
    /// <summary>
    /// Money helpers. All amounts are <see cref="decimal"/> (never float/double),
    /// so 0.5 + 0.5 + 0.5 == 1.5 exactly and "paid == price" comparisons are reliable.
    /// </summary>
    public static class Money
    {
        /// <summary>Formats an amount in a culture-independent way, e.g. "3.50 PLN".</summary>
        public static string Format(decimal amount)
        {
            return amount.ToString("0.00", CultureInfo.InvariantCulture) + " PLN";
        }

        /// <summary>Formats an amount without the currency, e.g. "3.50".</summary>
        public static string FormatPlain(decimal amount)
        {
            return amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Splits an amount into the fewest coins (greedy works for 5 / 2 / 1 / 0.50).
        /// Returns pairs of (coin value, count), largest coin first.
        /// </summary>
        public static List<KeyValuePair<decimal, int>> MakeChange(decimal amount)
        {
            if (amount < 0m)
                throw new ArgumentOutOfRangeException("amount", "Amount cannot be negative.");

            var result = new List<KeyValuePair<decimal, int>>();
            decimal remaining = amount;

            foreach (decimal coin in VendingMachineModel.CoinValues.OrderByDescending(c => c))
            {
                int count = (int)(remaining / coin);
                if (count > 0)
                {
                    result.Add(new KeyValuePair<decimal, int>(coin, count));
                    remaining -= count * coin;
                }
            }

            if (remaining != 0m)
                throw new InvalidOperationException("Amount cannot be paid out with the available coins.");

            return result;
        }

        /// <summary>Human readable coin list, e.g. "2 × 2.00  +  1 × 0.50".</summary>
        public static string Describe(IEnumerable<KeyValuePair<decimal, int>> coins)
        {
            return string.Join("  +  ", coins.Select(c => c.Value + " × " + FormatPlain(c.Key)));
        }
    }
}
