using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace VendingMachine.UI
{
    /// <summary>Colour palette and fonts shared by every custom control.</summary>
    internal static class Theme
    {
        public static readonly Color BgTop = Color.FromArgb(10, 13, 22);
        public static readonly Color BgBottom = Color.FromArgb(24, 29, 49);

        public static readonly Color Panel = Color.FromArgb(24, 28, 42);
        public static readonly Color Card = Color.FromArgb(33, 38, 58);
        public static readonly Color CardHover = Color.FromArgb(43, 50, 76);
        public static readonly Color Line = Color.FromArgb(54, 61, 90);

        public static readonly Color Accent = Color.FromArgb(45, 212, 191);
        public static readonly Color AccentDark = Color.FromArgb(15, 150, 138);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Danger = Color.FromArgb(248, 113, 113);
        public static readonly Color Success = Color.FromArgb(74, 222, 128);

        public static readonly Color Text = Color.FromArgb(236, 240, 247);
        public static readonly Color Muted = Color.FromArgb(138, 148, 174);

        public static readonly Color LcdBg = Color.FromArgb(7, 22, 21);
        public static readonly Color LcdText = Color.FromArgb(110, 243, 197);
        public static readonly Color LcdDim = Color.FromArgb(56, 128, 106);

        private const string Family = "Segoe UI";
        private const string MonoFamily = "Consolas";
        private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return Get(Family, size, style);
        }

        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
        {
            return Get(MonoFamily, size, style);
        }

        private static Font Get(string family, float size, FontStyle style)
        {
            string key = family + "|" + size + "|" + (int)style;
            Font f;
            if (!cache.TryGetValue(key, out f))
            {
                f = new Font(family, size, style, GraphicsUnit.Point);
                cache[key] = f;
            }
            return f;
        }
    }

    /// <summary>Small GDI+ helpers (rounded rectangles, colour mixing, image fitting).</summary>
    internal static class Gfx
    {
        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d < 1f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color color, RectangleF r, float radius)
        {
            using (var path = Round(r, radius))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        public static void DrawRound(Graphics g, Color color, float width, RectangleF r, float radius)
        {
            using (var path = Round(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, path);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, float t) { return Mix(c, Color.White, t); }
        public static Color Darken(Color c, float t) { return Mix(c, Color.Black, t); }
        public static Color Alpha(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c); }

        /// <summary>Draws the image so it fully covers <paramref name="dest"/> (centre-cropped).</summary>
        public static void DrawCover(Graphics g, Image img, RectangleF dest)
        {
            float scale = Math.Max(dest.Width / img.Width, dest.Height / img.Height);
            float w = img.Width * scale, h = img.Height * scale;
            g.DrawImage(img, new RectangleF(dest.X + (dest.Width - w) / 2f, dest.Y + (dest.Height - h) / 2f, w, h));
        }

        /// <summary>
        /// Fits the whole logo inside <paramref name="size"/> and extends its edge pixels into the
        /// leftover area, so nothing is cropped and there are no visible letterbox bars.
        /// </summary>
        public static Bitmap ComposeContain(Image img, Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min(size.Width / (float)img.Width, size.Height / (float)img.Height);
                float dw = img.Width * scale, dh = img.Height * scale;
                float dx = (size.Width - dw) / 2f, dy = (size.Height - dh) / 2f;

                if (dx > 0.5f)
                {
                    g.DrawImage(img, new RectangleF(0, dy, dx + 1, dh), new RectangleF(0, 0, 1, img.Height), GraphicsUnit.Pixel);
                    g.DrawImage(img, new RectangleF(dx + dw - 1, dy, dx + 1, dh), new RectangleF(img.Width - 1, 0, 1, img.Height), GraphicsUnit.Pixel);
                }
                if (dy > 0.5f)
                {
                    g.DrawImage(img, new RectangleF(dx, 0, dw, dy + 1), new RectangleF(0, 0, img.Width, 1), GraphicsUnit.Pixel);
                    g.DrawImage(img, new RectangleF(dx, dy + dh - 1, dw, dy + 1), new RectangleF(0, img.Height - 1, img.Width, 1), GraphicsUnit.Pixel);
                }
                g.DrawImage(img, new RectangleF(dx, dy, dw, dh));
            }
            return bmp;
        }

        public static void Text(Graphics g, string text, Font font, Color color, RectangleF bounds,
                                StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center)
        {
            using (var brush = new SolidBrush(color))
            using (var fmt = new StringFormat())
            {
                fmt.Alignment = h;
                fmt.LineAlignment = v;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, font, brush, bounds, fmt);
            }
        }

        public static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
    }
}
