using System;

namespace VendingMachine.Logic
{
    public enum ProductCategory { Drink, Snack }

    public sealed class Product
    {
        public Product(string code, string name, string detail, ProductCategory category,
                       decimal price, int stock, string imageKey)
        {
            if (price <= 0m) throw new ArgumentOutOfRangeException("price");
            if (stock < 0) throw new ArgumentOutOfRangeException("stock");

            Code = code;
            Name = name;
            Detail = detail;
            Category = category;
            Price = price;
            Stock = stock;
            Capacity = stock;
            ImageKey = imageKey;
        }

        /// <summary>Slot code shown on the machine, e.g. "A1".</summary>
        public string Code { get; private set; }
        public string Name { get; private set; }
        public string Detail { get; private set; }
        public ProductCategory Category { get; private set; }
        public decimal Price { get; private set; }
        public string ImageKey { get; private set; }

        /// <summary>Items currently in the slot.</summary>
        public int Stock { get; private set; }

        /// <summary>Items the slot holds when full (used to draw the stock indicator).</summary>
        public int Capacity { get; private set; }

        public bool SoldOut { get { return Stock <= 0; } }

        internal void TakeOne()
        {
            if (SoldOut) throw new InvalidOperationException(Name + " is sold out.");
            Stock--;
        }
    }
}
