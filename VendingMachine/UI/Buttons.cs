using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VendingMachine.Logic;

namespace VendingMachine.UI
{
    /// <summary>A round coin the user "inserts" by clicking.</summary>
    internal sealed class CoinButton : SmoothControl
    {
        private readonly decimal value;
        private readonly Color ring, center;

        public CoinButton(decimal value)
        {
            this.value = value;
            Size = new Size(64, 64);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = Money.Format(value) + " coin";

            // roughly the look of Polish coins: 0.50 brass, 1 silver, 2 silver ring + brass centre, 5 brass ring + silver centre
            if (value < 1m) { ring = Color.FromArgb(214, 160, 70); center = Color.FromArgb(226, 178, 92); }
            else if (value == 1m) { ring = Color.FromArgb(196, 202, 214); center = Color.FromArgb(214, 219, 230); }
            else if (value == 2m) { ring = Color.FromArgb(196, 202, 214); center = Color.FromArgb(222, 176, 88); }
            else { ring = Color.FromArgb(222, 176, 88); center = Color.FromArgb(200, 206, 218); }
        }

        public decimal Value { get { return value; } }

        protected override void Draw(Graphics g)
        {
            float scale = (Pressed && Enabled) ? 0.93f : 1f;
            float d = (Math.Min(Width, Height) - 6) * scale;
            var rect = new RectangleF((Width - d) / 2f, (Height - d) / 2f - 1f, d, d);

            Color outer = Enabled ? ring : Color.FromArgb(70, 76, 98);
            Color inner = Enabled ? center : Color.FromArgb(58, 64, 86);

            using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(shadow, rect.X, rect.Y + 4, rect.Width, rect.Height);

            if (Hover && Enabled)
                using (var glow = new Pen(Gfx.Alpha(Theme.Accent, 150), 3f))
                    g.DrawEllipse(glow, RectangleF.Inflate(rect, 3, 3));

            using (var lg = new LinearGradientBrush(rect, Gfx.Lighten(outer, 0.3f), Gfx.Darken(outer, 0.25f), 55f))
                g.FillEllipse(lg, rect);

            var inset = RectangleF.Inflate(rect, -d * 0.13f, -d * 0.13f);
            using (var lg = new LinearGradientBrush(inset, Gfx.Darken(inner, 0.18f), Gfx.Lighten(inner, 0.2f), 55f))
                g.FillEllipse(lg, inset);
            using (var edge = new Pen(Gfx.Alpha(Color.Black, 70), 1.2f))
                g.DrawEllipse(edge, inset);

            Color ink = Enabled ? Color.FromArgb(52, 40, 18) : Color.FromArgb(120, 126, 148);
            string label = value < 1m ? "0.50" : ((int)value).ToString();
            Gfx.Text(g, label, Theme.Font(value < 1m ? 11f : 14f, FontStyle.Bold), ink,
                new RectangleF(inset.X, inset.Y - 3, inset.Width, inset.Height), StringAlignment.Center);
            Gfx.Text(g, "PLN", Theme.Font(6.5f, FontStyle.Bold), ink,
                new RectangleF(inset.X, inset.Y + inset.Height * 0.52f, inset.Width, 12), StringAlignment.Center);

            if (ShowFocusRing)
                using (var p = new Pen(Theme.Text, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawEllipse(p, RectangleF.Inflate(rect, 5, 5));
        }
    }

    internal enum ButtonStyle { Primary, Secondary, Danger }

    /// <summary>Flat rounded button with hover / pressed / disabled states and an optional attention pulse.</summary>
    internal sealed class RoundedButton : SmoothControl
    {
        private readonly Timer pulseTimer = new Timer { Interval = 40 };
        private float phase;
        private bool pulse;

        public RoundedButton()
        {
            Size = new Size(200, 48);
            Cursor = Cursors.Hand;
            TabStop = true;
            Style = ButtonStyle.Primary;
            pulseTimer.Tick += delegate { phase += 0.07f; Invalidate(); };
        }

        public ButtonStyle Style { get; set; }

        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; Invalidate(); }
        }

        /// <summary>Makes the button glow to draw attention (e.g. when payment is ready).</summary>
        public bool Pulse
        {
            get { return pulse; }
            set
            {
                if (pulse == value) return;
                pulse = value;
                pulseTimer.Enabled = value;
                Invalidate();
            }
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(3, 3, Width - 7, Height - 7);
            Color baseColor, textColor;
            switch (Style)
            {
                case ButtonStyle.Danger: baseColor = Theme.Danger; textColor = Color.FromArgb(40, 12, 12); break;
                case ButtonStyle.Secondary: baseColor = Theme.Card; textColor = Theme.Text; break;
                default: baseColor = Theme.Accent; textColor = Color.FromArgb(4, 40, 36); break;
            }

            if (!Enabled)
            {
                Gfx.FillRound(g, Gfx.Mix(Theme.Panel, Theme.Card, 0.6f), r, 14);
                Gfx.DrawRound(g, Theme.Line, 1f, r, 14);
                Gfx.Text(g, Text, Theme.Font(11f, FontStyle.Bold), Gfx.Alpha(Theme.Muted, 160), r, StringAlignment.Center);
                return;
            }

            if (pulse)
            {
                float k = (float)(Math.Sin(phase) * 0.5 + 0.5);
                for (int i = 3; i >= 1; i--)
                    Gfx.DrawRound(g, Gfx.Alpha(baseColor, (int)(28 * k * (4 - i))), 2f, RectangleF.Inflate(r, i * 2f, i * 2f), 14 + i * 2);
            }

            Color top = Hover ? Gfx.Lighten(baseColor, 0.12f) : baseColor;
            if (Pressed) top = Gfx.Darken(baseColor, 0.15f);
            using (var path = Gfx.Round(r, 14))
            using (var lg = new LinearGradientBrush(r, top, Gfx.Darken(top, Style == ButtonStyle.Secondary ? 0.1f : 0.18f), 90f))
                g.FillPath(lg, path);
            if (Style == ButtonStyle.Secondary) Gfx.DrawRound(g, Hover ? Theme.Accent : Theme.Line, 1.2f, r, 14);

            var textRect = r;
            if (Pressed) textRect.Offset(0, 1);
            Gfx.Text(g, Text, Theme.Font(11f, FontStyle.Bold), textColor, textRect, StringAlignment.Center);

            if (ShowFocusRing)
                using (var p = new Pen(Theme.Text, 1.5f) { DashStyle = DashStyle.Dash })
                using (var path = Gfx.Round(RectangleF.Inflate(r, 3, 3), 16))
                    g.DrawPath(p, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) pulseTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
