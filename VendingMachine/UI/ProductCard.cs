using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VendingMachine.Logic;

namespace VendingMachine.UI
{
    /// <summary>One slot of the machine: logo, code, name, price and a stock indicator.</summary>
    internal sealed class ProductCard : SmoothControl
    {
        private readonly Product product;
        private readonly Image logo;
        private bool selected;

        public ProductCard(Product product, Image logo)
        {
            this.product = product;
            this.logo = logo == null ? null : Gfx.ComposeContain(logo, new Size(344, 192));
            Size = new Size(200, 200);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = product.Name + " " + Money.Format(product.Price);
        }

        public Product Product { get { return product; } }

        public bool Selected
        {
            get { return selected; }
            set { if (selected != value) { selected = value; Invalidate(); } }
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(4, 4, Width - 9, Height - 9);
            bool soldOut = product.SoldOut;

            Color fill = Theme.Card;
            if (!soldOut && Pressed) fill = Gfx.Darken(Theme.Card, 0.2f);
            else if (!soldOut && Hover) fill = Theme.CardHover;

            if (selected)
            {
                // soft glow around the selected card
                for (int i = 4; i >= 1; i--)
                {
                    var glow = RectangleF.Inflate(r, i * 1.5f, i * 1.5f);
                    Gfx.DrawRound(g, Gfx.Alpha(Theme.Accent, 22 * (5 - i)), 2f, glow, 16 + i);
                }
            }

            Gfx.FillRound(g, fill, r, 16);
            if (selected) Gfx.DrawRound(g, Theme.Accent, 2.5f, r, 16);
            else Gfx.DrawRound(g, Hover && !soldOut ? Gfx.Alpha(Theme.Accent, 120) : Theme.Line, 1f, r, 16);

            // product image (rounded)
            var imgRect = new RectangleF(r.X + 10, r.Y + 10, r.Width - 20, 96);
            using (var clip = Gfx.Round(imgRect, 11))
            {
                g.SetClip(clip);
                if (logo != null) Gfx.DrawCover(g, logo, imgRect);
                if (soldOut)
                    using (var dim = new SolidBrush(Color.FromArgb(185, 12, 14, 24)))
                        g.FillRectangle(dim, imgRect);
                g.ResetClip();
            }

            // slot code badge
            var badge = new RectangleF(imgRect.X + 8, imgRect.Y + 8, 34, 22);
            Gfx.FillRound(g, Color.FromArgb(170, 10, 12, 20), badge, 8);
            Gfx.Text(g, product.Code, Theme.Font(9f, FontStyle.Bold), Color.White, badge, StringAlignment.Center);

            if (soldOut)
            {
                var state = g.Save();
                g.TranslateTransform(imgRect.X + imgRect.Width / 2, imgRect.Y + imgRect.Height / 2);
                g.RotateTransform(-10f);
                var tag = new RectangleF(-60, -15, 120, 30);
                Gfx.DrawRound(g, Theme.Danger, 2f, tag, 8);
                Gfx.Text(g, "SOLD OUT", Theme.Font(12f, FontStyle.Bold), Theme.Danger, tag, StringAlignment.Center);
                g.Restore(state);
            }

            // name + detail
            Color nameColor = soldOut ? Theme.Muted : Theme.Text;
            Gfx.Text(g, product.Name, Theme.Font(12.5f, FontStyle.Bold), nameColor,
                new RectangleF(r.X + 14, r.Y + 112, r.Width - 28, 24));
            Gfx.Text(g, product.Detail, Theme.Font(8.5f), Theme.Muted,
                new RectangleF(r.X + 14, r.Y + 135, r.Width - 28, 16));

            // price pill
            string price = Money.Format(product.Price);
            var pill = new RectangleF(r.X + 12, r.Bottom - 34, 92, 24);
            Color priceColor = soldOut ? Theme.Muted : Theme.Amber;
            Gfx.FillRound(g, Gfx.Alpha(priceColor, 30), pill, 12);
            Gfx.Text(g, price, Theme.Font(10.5f, FontStyle.Bold), priceColor, pill, StringAlignment.Center);

            // stock pips (one per item the slot can hold)
            int pips = Math.Max(1, product.Capacity);
            float pip = 9f, gap = 4f;
            float total = pips * pip + (pips - 1) * gap;
            float x = r.Right - 14 - total, y = r.Bottom - 22;
            for (int i = 0; i < pips; i++)
            {
                var dot = new RectangleF(x + i * (pip + gap), y, pip, pip);
                if (i < product.Stock)
                    using (var b = new SolidBrush(product.Stock <= 1 ? Theme.Amber : Theme.Accent)) g.FillEllipse(b, dot);
                else
                    using (var p = new Pen(Theme.Line, 1.4f)) g.DrawEllipse(p, dot);
            }

            if (ShowFocusRing)
                using (var p = new Pen(Theme.Text, 1.5f) { DashStyle = DashStyle.Dash })
                using (var path = Gfx.Round(RectangleF.Inflate(r, 2, 2), 18))
                    g.DrawPath(p, path);
        }
    }
}
