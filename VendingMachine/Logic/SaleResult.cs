using System.Collections.Generic;

namespace VendingMachine.Logic
{
    public enum PaymentMethod { None, Cash, Card }

    /// <summary>Everything the UI needs to know about a completed purchase.</summary>
    public sealed class SaleResult
    {
        public Product Product { get; internal set; }
        public PaymentMethod Method { get; internal set; }
        public decimal Paid { get; internal set; }

        /// <summary>Total change due (also set when it was donated).</summary>
        public decimal Change { get; internal set; }

        public bool ChangeDonated { get; internal set; }

        /// <summary>Coins that physically drop into the tray (empty when donated or no change).</summary>
        public List<KeyValuePair<decimal, int>> ChangeCoins { get; internal set; }
    }
}
