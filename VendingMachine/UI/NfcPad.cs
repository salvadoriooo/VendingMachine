using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using VendingMachine.Logic;

namespace VendingMachine.UI
{
    internal enum NfcState { Idle, Processing, Approved }

    /// <summary>Contactless reader: pulsing rings while idle, spinner while authorizing, tick when approved.</summary>
    internal sealed class NfcPad : SmoothControl
    {
        private readonly Timer timer = new Timer { Interval = 33 };
        private float phase;
        private NfcState state = NfcState.Idle;

        public NfcPad()
        {
            Size = new Size(304, 170);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = "Contactless card reader";
            timer.Tick += delegate { phase += 0.018f; Invalidate(); };
        }

        public NfcState State
        {
            get { return state; }
            set
            {
                state = value;
                Cursor = value == NfcState.Idle ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            timer.Enabled = Visible;
            base.OnVisibleChanged(e);
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Gfx.Round(r, 18))
            using (var lg = new LinearGradientBrush(r, Hover && state == NfcState.Idle ? Theme.CardHover : Theme.Card, Gfx.Darken(Theme.Card, 0.25f), 90f))
                g.FillPath(lg, path);
            Gfx.DrawRound(g, state == NfcState.Approved ? Theme.Success : Hover && state == NfcState.Idle ? Theme.Accent : Theme.Line, 1.4f, r, 18);

            float cx = Width / 2f, cy = 62f;
            Color tone = state == NfcState.Approved ? Theme.Success : Theme.Accent;

            if (state != NfcState.Approved)
            {
                for (int k = 0; k < 3; k++)
                {
                    float p = (phase * (state == NfcState.Processing ? 2f : 1f) + k / 3f) % 1f;
                    float rad = 22f + p * 40f;
                    using (var pen = new Pen(Gfx.Alpha(tone, (int)(130 * (1f - p))), 2f))
                        g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                }
            }

            // disc behind the icon
            using (var b = new SolidBrush(Gfx.Alpha(tone, 40)))
                g.FillEllipse(b, cx - 26, cy - 26, 52, 52);

            if (state == NfcState.Approved)
            {
                using (var pen = new Pen(Theme.Success, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] { new PointF(cx - 12, cy + 1), new PointF(cx - 3, cy + 10), new PointF(cx + 13, cy - 9) });
            }
            else if (state == NfcState.Processing)
            {
                using (var pen = new Pen(Theme.Accent, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, cx - 16, cy - 16, 32, 32, phase * 720f, 100);
            }
            else
            {
                // contactless symbol: dot + three arcs
                using (var pen = new Pen(Theme.Accent, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        float rad = 7f + i * 7f;
                        g.DrawArc(pen, cx - 14 - rad, cy - rad, rad * 2, rad * 2, -45, 90);
                    }
                }
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillEllipse(b, cx - 18, cy - 3.5f, 7, 7);
            }

            string title = state == NfcState.Idle ? "Tap to pay" : state == NfcState.Processing ? "Authorizing…" : "Approved";
            string sub = state == NfcState.Idle ? "Click the reader to simulate a contactless card"
                       : state == NfcState.Processing ? "Please keep your card near the reader"
                       : "Payment accepted";
            Gfx.Text(g, title, Theme.Font(12.5f, FontStyle.Bold), Theme.Text, new RectangleF(0, 112, Width, 22), StringAlignment.Center);
            Gfx.Text(g, sub, Theme.Font(8.5f), Theme.Muted, new RectangleF(8, 134, Width - 16, 18), StringAlignment.Center);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>The collection slot at the bottom: the product drops in with a bounce, change coins appear beside it.</summary>
    internal sealed class DispenserTray : SmoothControl
    {
        private readonly Timer timer = new Timer { Interval = 16 };
        private readonly SortedDictionary<decimal, int> held = new SortedDictionary<decimal, int>();
        private Image item;
        private string itemName;
        private List<KeyValuePair<decimal, int>> coins;
        private string coinCaption;
        private float drop = 1f;
        private int ticks;

        public DispenserTray()
        {
            Size = new Size(688, 76);
            Cursor = Cursors.Default;
            TabStop = true;
            AccessibleName = "Collection tray";
            timer.Tick += delegate
            {
                ticks++;
                drop = Math.Min(1f, ticks / 55f);
                if (drop >= 1f) timer.Stop();
                Invalidate();
            };
        }

        public event EventHandler Collected;

        public bool HasContent { get { return item != null || (coins != null && coins.Count > 0); } }
        private bool Animating { get { return timer.Enabled; } }

        public void Drop(Image image, string name)
        {
            item = image == null ? null : Gfx.ComposeContain(image, new Size(104, 104));
            itemName = name;
            ticks = 0;
            drop = 0f;
            timer.Start();
            UpdateCursor();
            Invalidate();
        }

        /// <summary>Adds coins to the tray (several refunds / change drops accumulate until collected).</summary>
        public void AddCoins(string caption, List<KeyValuePair<decimal, int>> value)
        {
            foreach (var coin in value)
            {
                int existing;
                held.TryGetValue(coin.Key, out existing);
                held[coin.Key] = existing + coin.Value;
            }
            coins = held.OrderByDescending(c => c.Key).ToList();
            coinCaption = caption + " " + Money.Format(held.Sum(c => c.Key * c.Value));
            UpdateCursor();
            Invalidate();
        }

        public void Clear()
        {
            item = null;
            itemName = null;
            coins = null;
            held.Clear();
            coinCaption = null;
            timer.Stop();
            drop = 1f;
            UpdateCursor();
            Invalidate();
        }

        private void UpdateCursor() { Cursor = HasContent ? Cursors.Hand : Cursors.Default; }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (HasContent && !Animating)
            {
                Clear();
                var handler = Collected;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void Draw(Graphics g)
        {
            var slot = new RectangleF(2, 2, Width - 5, Height - 5);
            using (var path = Gfx.Round(slot, 16))
            using (var lg = new LinearGradientBrush(slot, Color.FromArgb(2, 4, 9), Color.FromArgb(16, 20, 33), 90f))
                g.FillPath(lg, path);
            Gfx.DrawRound(g, Theme.Line, 1f, slot, 16);

            using (var path = Gfx.Round(slot, 16))
            {
                g.SetClip(path);

                if (!HasContent)
                {
                    Gfx.Text(g, "COLLECTION TRAY", Theme.Font(8.5f, FontStyle.Bold), Gfx.Alpha(Theme.Muted, 190),
                        new RectangleF(slot.X, slot.Y, slot.Width, slot.Height), StringAlignment.Center);
                    using (var pen = new Pen(Gfx.Alpha(Theme.Muted, 130), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        float cx = slot.X + slot.Width / 2f - 90f, cy = slot.Y + slot.Height / 2f;
                        g.DrawLines(pen, new[] { new PointF(cx - 6, cy - 3), new PointF(cx, cy + 3), new PointF(cx + 6, cy - 3) });
                    }
                }

                if (item != null)
                {
                    float size = 52f;
                    float restY = slot.Bottom - size - 8f;
                    float y = -size + (restY + size) * Gfx.EaseOutBounce(drop);
                    var thumb = new RectangleF(slot.X + 18, y, size, size);
                    using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                        g.FillEllipse(shadow, thumb.X + 4, slot.Bottom - 10, size - 8, 6);
                    using (var tp = Gfx.Round(thumb, 10))
                    {
                        g.SetClip(tp, CombineMode.Intersect);
                        Gfx.DrawCover(g, item, thumb);
                        g.SetClip(path);
                    }
                    Gfx.DrawRound(g, Gfx.Alpha(Color.White, 60), 1f, thumb, 10);

                    if (drop >= 1f)
                    {
                        Gfx.Text(g, itemName, Theme.Font(11.5f, FontStyle.Bold), Theme.Text,
                            new RectangleF(thumb.Right + 14, slot.Y + 10, 220, 24));
                        Gfx.Text(g, "Click to collect", Theme.Font(8.5f), Theme.Accent,
                            new RectangleF(thumb.Right + 14, slot.Y + 34, 220, 20));
                    }
                }

                if (coins != null && coins.Count > 0)
                {
                    float x = slot.Right - 16f;
                    var font = Theme.Font(9.5f, FontStyle.Bold);
                    for (int i = coins.Count - 1; i >= 0; i--)
                    {
                        string chip = coins[i].Value + " × " + Money.FormatPlain(coins[i].Key);
                        float w = g.MeasureString(chip, font).Width + 16f;
                        x -= w;
                        var rect = new RectangleF(x, slot.Y + slot.Height / 2f - 2, w, 24);
                        Gfx.FillRound(g, Gfx.Alpha(Theme.Amber, 36), rect, 12);
                        Gfx.Text(g, chip, font, Theme.Amber, rect, StringAlignment.Center);
                        x -= 6f;
                    }
                    Gfx.Text(g, (coinCaption ?? "CHANGE").ToUpperInvariant(), Theme.Font(8f, FontStyle.Bold), Theme.Muted,
                        new RectangleF(x, slot.Y + 8, slot.Right - x - 16, 18), StringAlignment.Far);
                }

                g.ResetClip();
            }

            if (ShowFocusRing)
                using (var p = new Pen(Theme.Text, 1.5f) { DashStyle = DashStyle.Dash })
                using (var path = Gfx.Round(RectangleF.Inflate(slot, 2, 2), 18))
                    g.DrawPath(p, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
