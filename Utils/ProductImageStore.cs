using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Utils
{
    public static class ProductImageStore
    {
        private static readonly Dictionary<string, Image?> _thumbCache = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);
        private const int MaxImageSide = 1200;

        public static string GetImagesRoot()
        {
            string root = Path.Combine(Database.GetAppDataRoot(), "ProductImages");
            Directory.CreateDirectory(root);
            return root;
        }

        public static string SaveFromSource(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new Exception("Rasm fayli topilmadi.");
            }

            string root = GetImagesRoot();
            string fileName = $"product_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.jpg";
            string targetPath = Path.Combine(root, fileName);

            using Bitmap original = LoadBitmapUnlocked(sourcePath);
            using Bitmap normalized = ResizeIfNeeded(original, MaxImageSide);
            SaveAsJpeg(normalized, targetPath, 86L);

            return fileName;
        }

        public static void DeleteImage(string? imagePath)
        {
            string? absolute = ResolveAbsolutePath(imagePath);
            if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
            {
                return;
            }

            try
            {
                File.Delete(absolute);
            }
            catch
            {
                // Rasm fayli band bo'lsa yoki o'chmasa, asosiy oqimni to'xtatmaymiz.
            }
        }

        public static string? ResolveAbsolutePath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string root = GetImagesRoot();
            string candidate = Path.Combine(root, Path.GetFileName(imagePath));
            string fullRoot = Path.GetFullPath(root);
            string fullCandidate = Path.GetFullPath(candidate);
            if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullCandidate;
        }

        public static Image? TryLoadPreview(string? imagePath, int width = 84, int height = 84)
        {
            string? absolute = ResolveAbsolutePath(imagePath);
            if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
            {
                return null;
            }

            string key = $"{absolute}|{width}x{height}";
            if (_thumbCache.TryGetValue(key, out Image? cached) && cached != null)
            {
                return cached;
            }

            try
            {
                using Bitmap src = LoadBitmapUnlocked(absolute);
                Bitmap preview = ScaleForBox(src, width, height);
                _thumbCache[key] = preview;
                return preview;
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using Image raw = Image.FromStream(fs);
            return new Bitmap(raw);
        }

        private static Bitmap ResizeIfNeeded(Bitmap source, int maxSide)
        {
            int max = Math.Max(source.Width, source.Height);
            if (max <= maxSide)
            {
                return new Bitmap(source);
            }

            double scale = maxSide / (double)max;
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));
            Bitmap result = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using Graphics g = Graphics.FromImage(result);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, w, h));
            return result;
        }

        private static Bitmap ScaleForBox(Bitmap source, int boxW, int boxH)
        {
            Bitmap result = new Bitmap(Math.Max(1, boxW), Math.Max(1, boxH), PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(result);
            g.Clear(Color.Transparent);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;

            double ratio = Math.Min(boxW / (double)Math.Max(1, source.Width), boxH / (double)Math.Max(1, source.Height));
            int w = Math.Max(1, (int)Math.Round(source.Width * ratio));
            int h = Math.Max(1, (int)Math.Round(source.Height * ratio));
            int x = (boxW - w) / 2;
            int y = (boxH - h) / 2;
            g.DrawImage(source, new Rectangle(x, y, w, h));
            return result;
        }

        private static void SaveAsJpeg(Image image, string path, long quality)
        {
            ImageCodecInfo? jpeg = null;
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageDecoders())
            {
                if (string.Equals(codec.FormatDescription, "JPEG", StringComparison.OrdinalIgnoreCase))
                {
                    jpeg = codec;
                    break;
                }
            }

            if (jpeg == null)
            {
                image.Save(path, ImageFormat.Jpeg);
                return;
            }

            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            image.Save(path, jpeg, encoderParams);
        }
    }
}
