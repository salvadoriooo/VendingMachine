using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VendingMachine.UI
{
    internal enum LcdTone { Normal, Warning, Error, Success }

    /// <summary>The green machine display. Messages fade in.</summary>
    internal sealed class LcdDisplay : SmoothControl
    {
        private readonly Timer fade = new Timer { Interval = 20 };
        private string title = "", text = "";
        private LcdTone tone;
        private float alpha = 1f;

        public LcdDisplay()
        {
            Size = new Size(304, 100);
            TabStop = false;
            Surface = Theme.Panel;
            fade.Tick += delegate
            {
                alpha = Math.Min(1f, alpha + 0.12f);
                if (alpha >= 1f) fade.Stop();
                Invalidate();
            };
        }

        public void Show(string title, string text, LcdTone tone)
        {
            if (this.title == title && this.text == text && this.tone == tone) return;
            this.title = title;
            this.text = text;
            this.tone = tone;
            AccessibleName = title + ". " + text;
            alpha = 0.15f;
            fade.Start();
            Invalidate();
        }

        private Color ToneColor
        {
            get
            {
                switch (tone)
                {
                    case LcdTone.Warning: return Theme.Amber;
                    case LcdTone.Error: return Theme.Danger;
                    case LcdTone.Success: return Theme.Success;
                    default: return Theme.LcdText;
                }
            }
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            Gfx.FillRound(g, Color.FromArgb(4, 12, 12), r, 14);
            var screen = RectangleF.Inflate(r, -5, -5);
            using (var path = Gfx.Round(screen, 10))
            using (var lg = new LinearGradientBrush(screen, Color.FromArgb(10, 30, 28), Theme.LcdBg, 90f))
            {
                g.FillPath(lg, path);
                g.SetClip(path);
                using (var scan = new Pen(Color.FromArgb(14, 255, 255, 255), 1f))
                    for (float y = screen.Y; y < screen.Bottom; y += 3f)
                        g.DrawLine(scan, screen.X, y, screen.Right, y);
                g.ResetClip();
            }
            Gfx.DrawRound(g, Color.FromArgb(40, 110, 243, 197), 1f, screen, 10);

            Color c = ToneColor;
            int a = (int)(255 * alpha);

            Gfx.Text(g, title.ToUpperInvariant(), Theme.Mono(8.5f, FontStyle.Bold), Gfx.Alpha(Gfx.Mix(c, Color.Black, 0.35f), a),
                new RectangleF(screen.X + 14, screen.Y + 8, screen.Width - 28, 18));

            var main = new RectangleF(screen.X + 14, screen.Y + 28, screen.Width - 28, screen.Height - 36);
            var font = Theme.Mono(text.Length > 22 ? 12.5f : 15f, FontStyle.Bold);
            using (var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                for (int i = 3; i >= 1; i--)   // cheap glow: the same wrapped text, slightly offset
                {
                    var glow = main;
                    glow.Inflate(i, i);
                    using (var gb = new SolidBrush(Gfx.Alpha(c, (int)(a * 0.07f))))
                        g.DrawString(text, font, gb, glow, fmt);
                }
                using (var brush = new SolidBrush(Gfx.Alpha(c, a)))
                    g.DrawString(text, font, brush, main, fmt);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fade.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Caption + big value (used for "Inserted" and "Due"). Flashes when the value changes.</summary>
    internal sealed class ReadoutBox : SmoothControl
    {
        private readonly Timer flashTimer = new Timer { Interval = 25 };
        private float flash;
        private string value = "0.00 PLN";

        public ReadoutBox()
        {
            Size = new Size(144, 62);
            TabStop = false;
            ValueColor = Theme.Text;
            flashTimer.Tick += delegate
            {
                flash = Math.Max(0f, flash - 0.06f);
                if (flash <= 0f) flashTimer.Stop();
                Invalidate();
            };
        }

        public string Caption { get; set; }
        public Color ValueColor { get; set; }

        public string Value
        {
            get { return value; }
            set
            {
                if (this.value == value) return;
                this.value = value;
                AccessibleName = Caption + " " + value;
                Invalidate();
            }
        }

        public void Flash()
        {
            flash = 1f;
            flashTimer.Start();
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            Gfx.FillRound(g, Gfx.Mix(Theme.Card, Theme.Accent, flash * 0.25f), r, 14);
            Gfx.DrawRound(g, Gfx.Mix(Theme.Line, Theme.Accent, flash), 1f, r, 14);
            Gfx.Text(g, (Caption ?? "").ToUpperInvariant(), Theme.Font(7.5f, FontStyle.Bold), Theme.Muted,
                new RectangleF(r.X + 14, r.Y + 8, r.Width - 20, 14));
            Gfx.Text(g, value, Theme.Font(14f, FontStyle.Bold), Enabled ? Gfx.Mix(ValueColor, Color.White, flash * 0.5f) : Theme.Muted,
                new RectangleF(r.X + 14, r.Y + 24, r.Width - 20, 30));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) flashTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Thin progress bar showing how much of the price has been paid.</summary>
    internal sealed class ProgressStrip : SmoothControl
    {
        private readonly Timer timer = new Timer { Interval = 16 };
        private float shown, target;

        public ProgressStrip()
        {
            Size = new Size(304, 8);
            TabStop = false;
            timer.Tick += delegate
            {
                shown += (target - shown) * 0.2f;
                if (Math.Abs(target - shown) < 0.002f) { shown = target; timer.Stop(); }
                Invalidate();
            };
        }

        /// <summary>0..1</summary>
        public float Progress
        {
            get { return target; }
            set
            {
                target = Math.Max(0f, Math.Min(1f, value));
                timer.Start();
            }
        }

        protected override void Draw(Graphics g)
        {
            var track = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, Theme.Card, track, 4);
            if (shown > 0.001f)
            {
                var fill = new RectangleF(0, 0, Math.Max(Height, (Width - 1) * shown), Height - 1);
                using (var path = Gfx.Round(fill, 4))
                using (var lg = new LinearGradientBrush(fill, Theme.AccentDark, shown >= 1f ? Theme.Success : Theme.Accent, 0f))
                    g.FillPath(lg, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Two-option switch (Cash / Card) with a sliding highlight.</summary>
    internal sealed class SegmentedToggle : SmoothControl
    {
        private readonly string[] items;
        private readonly Timer timer = new Timer { Interval = 16 };
        private int selected = -1;
        private float pos;     // animated highlight position, in item units

        public SegmentedToggle(params string[] items)
        {
            this.items = items;
            Size = new Size(304, 44);
            Cursor = Cursors.Hand;
            TabStop = true;
            timer.Tick += delegate
            {
                pos += (selected - pos) * 0.25f;
                if (Math.Abs(selected - pos) < 0.01f) { pos = selected; timer.Stop(); }
                Invalidate();
            };
        }

        public event EventHandler SelectionChanged;

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                if (selected < 0) pos = value;     // first selection: no slide from nowhere
                selected = value;
                timer.Start();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            int index = (int)(e.X / (Width / (float)items.Length));
            Choose(index);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            if (e.KeyCode == Keys.Left) { Choose(Math.Max(0, (selected < 0 ? 1 : selected) - 1)); e.Handled = true; }
            if (e.KeyCode == Keys.Right) { Choose(Math.Min(items.Length - 1, selected + 1)); e.Handled = true; }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= items.Length || index == selected) return;
            SelectedIndex = index;
            var handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            Gfx.FillRound(g, Theme.Card, r, 14);
            Gfx.DrawRound(g, Theme.Line, 1f, r, 14);

            float w = (r.Width - 8) / items.Length;
            if (selected >= 0)
            {
                var hi = new RectangleF(r.X + 4 + pos * w, r.Y + 4, w, r.Height - 8);
                using (var path = Gfx.Round(hi, 10))
                using (var lg = new LinearGradientBrush(hi, Theme.Accent, Theme.AccentDark, 90f))
                    g.FillPath(lg, path);
            }

            for (int i = 0; i < items.Length; i++)
            {
                var cell = new RectangleF(r.X + 4 + i * w, r.Y + 4, w, r.Height - 8);
                float closeness = 1f - Math.Min(1f, Math.Abs(pos - i));
                Color onHighlight = Color.FromArgb(4, 40, 36);
                Color idle = Enabled ? Theme.Text : Gfx.Alpha(Theme.Muted, 150);
                Color ink = selected < 0 ? idle : Gfx.Mix(idle, onHighlight, closeness);

                // tiny vector icon + label
                var font = Theme.Font(10.5f, FontStyle.Bold);
                SizeF size = g.MeasureString(items[i], font);
                float total = 22f + size.Width;
                float x = cell.X + (cell.Width - total) / 2f;
                float cy = cell.Y + cell.Height / 2f;
                using (var pen = new Pen(ink, 1.8f))
                {
                    if (i == 0)
                        g.DrawEllipse(pen, x, cy - 8, 16, 16);
                    else
                        using (var path = Gfx.Round(new RectangleF(x - 2, cy - 7, 20, 14), 3))
                        {
                            g.DrawPath(pen, path);
                            g.DrawLine(pen, x - 2, cy - 2, x + 18, cy - 2);
                        }
                }
                if (i == 0) Gfx.Text(g, "zł", Theme.Font(7f, FontStyle.Bold), ink, new RectangleF(x, cy - 9, 16, 18), StringAlignment.Center);
                Gfx.Text(g, items[i], font, ink, new RectangleF(x + 24, cell.Y, size.Width + 8, cell.Height));
            }

            if (ShowFocusRing)
                using (var p = new Pen(Theme.Text, 1.5f) { DashStyle = DashStyle.Dash })
                using (var path = Gfx.Round(RectangleF.Inflate(r, 2, 2), 15))
                    g.DrawPath(p, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
