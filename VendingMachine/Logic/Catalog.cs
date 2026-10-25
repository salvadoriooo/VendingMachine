using System.Collections.Generic;

namespace VendingMachine.Logic
{
    /// <summary>Single source of truth for products and prices (the old UI had prices hard-coded twice).</summary>
    public static class Catalog
    {
        public static List<Product> Create()
        {
            const int stock = 5;
            return new List<Product>
            {
                new Product("A1", "Fanta",    "0.33 l",        ProductCategory.Drink, 3.5m, stock, "fantalogo.jpg"),
                new Product("A2", "Sprite",   "0.33 l",        ProductCategory.Drink, 3.5m, stock, "spritelogo.png"),
                new Product("A3", "Cola",     "0.33 l",        ProductCategory.Drink, 3.5m, stock, "colalogo.png"),
                new Product("B1", "Twix",     "Chocolate bar", ProductCategory.Snack, 2.5m, stock, "twixlogo.png"),
                new Product("B2", "Snickers", "Chocolate bar", ProductCategory.Snack, 2.5m, stock, "snickerslogo.jpeg"),
                new Product("B3", "Mars",     "Chocolate bar", ProductCategory.Snack, 2.5m, stock, "marslogo.jpg"),
            };
        }
    }
}
