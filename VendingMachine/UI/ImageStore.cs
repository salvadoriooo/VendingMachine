using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;

namespace VendingMachine.UI
{
    /// <summary>Loads the product logos that are embedded in the executable.</summary>
    internal static class ImageStore
    {
        private const int MaxSide = 480;

        public static Image Load(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resource == null)
                throw new InvalidOperationException("Embedded image not found: " + fileName);

            using (var stream = assembly.GetManifestResourceStream(resource))
            using (var original = Image.FromStream(stream))
            {
                // Down-scale big sources (the Fanta logo is 2461x2048) so painting stays fast.
                float scale = Math.Min(1f, MaxSide / (float)Math.Max(original.Width, original.Height));
                int w = Math.Max(1, (int)(original.Width * scale)), h = Math.Max(1, (int)(original.Height * scale));
                var bitmap = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);   // logos with transparency get a white backdrop
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, 0, 0, w, h);
                }
                return bitmap;
            }
        }
    }
}
