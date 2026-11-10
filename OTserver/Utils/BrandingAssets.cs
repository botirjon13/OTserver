using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SantexnikaSRM.Utils
{
    public static class BrandingAssets
    {
        private static Image? _cachedLogo;
        private static Icon? _cachedIcon;
        private static readonly Dictionary<string, Image?> _cachedImages = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);

        public static Image? TryLoadLogo()
        {
            if (_cachedLogo != null)
            {
                return _cachedLogo;
            }

            string[] logoNames = new[] { "logotip.png", "logo.png" };
            foreach (string fileName in logoNames)
            {
                foreach (string path in CandidatePaths(fileName))
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using Image img = Image.FromStream(fs);
                        using Bitmap raw = new Bitmap(img);
                        _cachedLogo = TrimLogo(raw);
                        return _cachedLogo;
                    }
                    catch
                    {
                        // Noto'g'ri fayl yoki o'qib bo'lmasa keyingi pathga o'tamiz.
                    }
                }

                Image? embedded = TryLoadEmbeddedImage(fileName);
                if (embedded != null)
                {
                    using Bitmap raw = new Bitmap(embedded);
                    _cachedLogo = TrimLogo(raw);
                    embedded.Dispose();
                    return _cachedLogo;
                }
            }

            return null;
        }

        public static Icon? TryLoadAppIcon()
        {
            if (_cachedIcon != null)
            {
                return _cachedIcon;
            }

            foreach (string path in CandidatePaths("app.ico"))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    _cachedIcon = new Icon(path);
                    return _cachedIcon;
                }
                catch
                {
                    // Ignore va keyingi variantni tekshirish.
                }
            }

            _cachedIcon = TryLoadEmbeddedIcon("app.ico");
            if (_cachedIcon != null)
            {
                return _cachedIcon;
            }

            return null;
        }

        public static Image? TryLoadAssetImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (_cachedImages.TryGetValue(fileName, out Image? cached) && cached != null)
            {
                return cached;
            }

            foreach (string path in CandidatePaths(fileName))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using Image img = Image.FromStream(fs);
                    _cachedImages[fileName] = new Bitmap(img);
                    return _cachedImages[fileName];
                }
                catch
                {
                    // Noto'g'ri fayl yoki o'qib bo'lmasa keyingi pathga o'tamiz.
                }
            }

            Image? embedded = TryLoadEmbeddedImage(fileName);
            if (embedded != null)
            {
                _cachedImages[fileName] = embedded;
                return embedded;
            }

            return null;
        }

        private static Image? TryLoadEmbeddedImage(string fileName)
        {
            try
            {
                Assembly asm = typeof(BrandingAssets).Assembly;
                string? resName = FindEmbeddedResourceName(asm, fileName);
                if (string.IsNullOrWhiteSpace(resName))
                {
                    return null;
                }

                using Stream? stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    return null;
                }

                using Image img = Image.FromStream(stream);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }

        private static Icon? TryLoadEmbeddedIcon(string fileName)
        {
            try
            {
                Assembly asm = typeof(BrandingAssets).Assembly;
                string? resName = FindEmbeddedResourceName(asm, fileName);
                if (string.IsNullOrWhiteSpace(resName))
                {
                    return null;
                }

                using Stream? stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    return null;
                }

                using var icon = new Icon(stream);
                return (Icon)icon.Clone();
            }
            catch
            {
                return null;
            }
        }

        private static string? FindEmbeddedResourceName(Assembly asm, string fileName)
        {
            string suffix = "." + fileName.Replace('\\', '.').Replace('/', '.');
            return asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        public static Control CreateLogoControl(Size size, int radius, string fallbackGlyph)
        {
            Image? logo = TryLoadLogo();
            if (logo != null)
            {
                return new PictureBox
                {
                    Size = size,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Image = logo
                };
            }

            Panel fallback = new Panel
            {
                Size = size,
                BackColor = Color.Transparent
            };
            fallback.Paint += (s, e) =>
            {
                using LinearGradientBrush lg = new LinearGradientBrush(
                    fallback.ClientRectangle,
                    Color.FromArgb(38, 113, 255),
                    Color.FromArgb(78, 66, 235),
                    45f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using GraphicsPath path = RoundedRect(fallback.ClientRectangle, radius);
                e.Graphics.FillPath(lg, path);
                TextRenderer.DrawText(
                    e.Graphics,
                    fallbackGlyph,
                    UiTheme.IconFont(Math.Max(12, size.Width / 3f)),
                    fallback.ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            return fallback;
        }

        private static Bitmap TrimLogo(Bitmap source)
        {
            if (source.Width <= 2 || source.Height <= 2)
            {
                return new Bitmap(source);
            }

            Color bg = source.GetPixel(0, 0);
            int minX = source.Width;
            int minY = source.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color p = source.GetPixel(x, y);
                    if (IsContentPixel(p, bg))
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX <= minX || maxY <= minY)
            {
                return new Bitmap(source);
            }

            int pad = 6;
            minX = Math.Max(0, minX - pad);
            minY = Math.Max(0, minY - pad);
            maxX = Math.Min(source.Width - 1, maxX + pad);
            maxY = Math.Min(source.Height - 1, maxY + pad);

            Rectangle crop = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return source.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        private static bool IsContentPixel(Color p, Color bg)
        {
            if (p.A <= 20)
            {
                return false;
            }

            int dr = p.R - bg.R;
            int dg = p.G - bg.G;
            int db = p.B - bg.B;
            int dist = Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db);
            return dist > 26;
        }

        private static IEnumerable<string> CandidatePaths(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            yield return Path.Combine(baseDir, "Assets", fileName);
            yield return Path.Combine(baseDir, fileName);
            yield return Path.Combine(Application.StartupPath, "Assets", fileName);
            yield return Path.Combine(Application.StartupPath, fileName);
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
