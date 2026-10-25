using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VendingMachine.Logic;

namespace VendingMachine.UI
{
    /// <summary>
    /// Replaces the old plain MessageBox: asks whether the change should be donated to support Ukraine.
    /// Returns DialogResult.Yes (donate) or DialogResult.No (keep the change).
    /// </summary>
    internal sealed class DonationDialog : Form
    {
        private readonly decimal change;

        public DonationDialog(decimal change)
        {
            this.change = change;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 290);
            BackColor = Theme.Panel;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "Your change";

            var donate = new RoundedButton { Text = "Donate " + Money.Format(change), Location = new Point(28, 214), Size = new Size(230, 50), Surface = Theme.Panel };
            var keep = new RoundedButton { Text = "Keep my change", Style = ButtonStyle.Secondary, Location = new Point(270, 214), Size = new Size(162, 50), Surface = Theme.Panel };
            donate.Click += delegate { DialogResult = DialogResult.Yes; };
            keep.Click += delegate { DialogResult = DialogResult.No; };
            Controls.Add(donate);
            Controls.Add(keep);

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.No; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            using (var pen = new Pen(Theme.Line, 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // heart in the colours of the Ukrainian flag
            float cx = 230, cy = 70, s = 30;
            using (var heart = new GraphicsPath())
            {
                heart.AddBezier(cx, cy + s * 0.9f, cx - s * 1.25f, cy - s * 0.05f, cx - s * 0.65f, cy - s * 1.05f, cx, cy - s * 0.35f);
                heart.AddBezier(cx, cy - s * 0.35f, cx + s * 0.65f, cy - s * 1.05f, cx + s * 1.25f, cy - s * 0.05f, cx, cy + s * 0.9f);
                heart.CloseFigure();
                var bounds = heart.GetBounds();
                using (var glow = new Pen(Color.FromArgb(40, 255, 215, 0), 10f) { LineJoin = LineJoin.Round })
                    g.DrawPath(glow, heart);
                using (var lg = new LinearGradientBrush(bounds, Color.FromArgb(0, 87, 183), Color.FromArgb(255, 215, 0), 90f))
                {
                    lg.InterpolationColors = new ColorBlend(4)
                    {
                        Colors = new[] { Color.FromArgb(0, 87, 183), Color.FromArgb(0, 87, 183), Color.FromArgb(255, 215, 0), Color.FromArgb(255, 215, 0) },
                        Positions = new[] { 0f, 0.5f, 0.5f, 1f }
                    };
                    g.FillPath(lg, heart);
                }
            }

            Gfx.Text(g, "Round up for a good cause?", Theme.Font(16f, FontStyle.Bold), Theme.Text,
                new RectangleF(20, 112, Width - 40, 34), StringAlignment.Center);
            Gfx.Text(g, "Your change is " + Money.Format(change) + ".", Theme.Font(10.5f), Theme.Text,
                new RectangleF(20, 150, Width - 40, 24), StringAlignment.Center);
            Gfx.Text(g, "Would you like to donate it to support Ukraine?", Theme.Font(10.5f), Theme.Muted,
                new RectangleF(20, 174, Width - 40, 24), StringAlignment.Center);
        }
    }
}
