using System;
using System.Collections.Generic;
using System.Linq;

namespace VendingMachine.Logic
{
    /// <summary>
    /// Pure business logic of the vending machine - no UI code, fully unit-testable.
    /// Flow: select product -> choose payment method -> insert coins / tap card -> pay.
    /// </summary>
    public sealed class VendingMachineModel
    {
        /// <summary>Largest credit the coin mechanism accepts.</summary>
        public const decimal MaxCredit = 50m;

        public static readonly decimal[] CoinValues = { 0.5m, 1m, 2m, 5m };

        private readonly List<Product> products;

        public VendingMachineModel(IEnumerable<Product> products)
        {
            if (products == null) throw new ArgumentNullException("products");
            this.products = products.ToList();
        }

        public event EventHandler Changed;

        public IReadOnlyList<Product> Products { get { return products; } }
        public Product Selected { get; private set; }
        public PaymentMethod Method { get; private set; }

        /// <summary>Money currently inserted (cash mode only).</summary>
        public decimal Credit { get; private set; }

        public decimal Due
        {
            get { return Selected == null ? 0m : Math.Max(0m, Selected.Price - Credit); }
        }

        public bool IsPaid
        {
            get { return Selected != null && Credit >= Selected.Price; }
        }

        public decimal ChangeDue
        {
            get { return IsPaid ? Credit - Selected.Price : 0m; }
        }

        /// <summary>Selects a product. Fails (without changing state) when it is sold out.</summary>
        public bool TrySelect(Product product, out string error)
        {
            error = null;
            if (product == null || !products.Contains(product))
            {
                error = "Unknown product.";
                return false;
            }
            if (product.SoldOut)
            {
                error = product.Name + " is sold out.";
                return false;
            }

            Selected = product;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Chooses cash or card. Switching away from cash returns any inserted coins;
        /// the refunded amount is returned so the UI can show it.
        /// </summary>
        public decimal ChoosePaymentMethod(PaymentMethod method)
        {
            if (Selected == null)
                throw new InvalidOperationException("Select a product first.");

            decimal refund = 0m;
            if (method != PaymentMethod.Cash)
            {
                refund = Credit;
                Credit = 0m;
            }

            Method = method;
            RaiseChanged();
            return refund;
        }

        public bool TryInsertCoin(decimal value, out string error)
        {
            error = null;
            if (Selected == null) { error = "Select a product first."; return false; }
            if (Method != PaymentMethod.Cash) { error = "Choose cash payment first."; return false; }
            if (!CoinValues.Contains(value)) { error = "Coin not accepted."; return false; }
            if (Credit + value > MaxCredit) { error = "Coin slot full (max " + Money.Format(MaxCredit) + ")."; return false; }

            Credit += value;
            RaiseChanged();
            return true;
        }

        /// <summary>Cancels the transaction and returns the inserted money.</summary>
        public decimal Cancel()
        {
            decimal refund = Credit;
            Credit = 0m;
            Selected = null;
            Method = PaymentMethod.None;
            RaiseChanged();
            return refund;
        }

        public SaleResult PayCash(bool donateChange)
        {
            if (Selected == null || Method != PaymentMethod.Cash)
                throw new InvalidOperationException("Cash payment is not active.");
            if (!IsPaid)
                throw new InvalidOperationException("Not enough money inserted.");

            decimal change = ChangeDue;
            var result = new SaleResult
            {
                Product = Selected,
                Method = PaymentMethod.Cash,
                Paid = Credit,
                Change = change,
                ChangeDonated = donateChange && change > 0m,
                ChangeCoins = (donateChange || change == 0m)
                    ? new List<KeyValuePair<decimal, int>>()
                    : Money.MakeChange(change)
            };

            CompleteSale();
            return result;
        }

        public SaleResult PayCard()
        {
            if (Selected == null || Method != PaymentMethod.Card)
                throw new InvalidOperationException("Card payment is not active.");

            var result = new SaleResult
            {
                Product = Selected,
                Method = PaymentMethod.Card,
                Paid = Selected.Price,
                Change = 0m,
                ChangeCoins = new List<KeyValuePair<decimal, int>>()
            };

            CompleteSale();
            return result;
        }

        private void CompleteSale()
        {
            Selected.TakeOne();
            Selected = null;
            Method = PaymentMethod.None;
            Credit = 0m;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
