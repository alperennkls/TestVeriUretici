using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace TestVeriUretici
{
    /// <summary>
    /// Draws the app icon in code: the tray gets an exact-size icon at any DPI, and build.ps1 asks for
    /// a multi-size .ico to embed in the .exe, so there is no image file to maintain.
    /// </summary>
    internal static class AppIcon
    {
        private static readonly int[] IcoSizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        private static readonly Color Top = Color.FromArgb(59, 130, 246);
        private static readonly Color Bottom = Color.FromArgb(29, 78, 216);

        public static Icon ForTray(int size)
        {
            using (MemoryStream ico = new MemoryStream(BuildIco(new[] { size })))
                return new Icon(ico, size, size);
        }

        public static void SaveIco(string path)
        {
            File.WriteAllBytes(path, BuildIco(IcoSizes));
        }

        /// <summary>Package images named in store\AppxManifest.xml; the name qualifiers are what makepri indexes.</summary>
        public static void SaveStoreAssets(string folder)
        {
            Directory.CreateDirectory(folder);
            SaveScaled(folder, "Square44x44Logo", 44);
            SaveScaled(folder, "Square150x150Logo", 150);
            SaveScaled(folder, "StoreLogo", 50);
            foreach (int size in new[] { 16, 24, 32, 48, 256 })
            {
                SavePng(Path.Combine(folder, "Square44x44Logo.targetsize-" + size + ".png"), size);
                // Unplated: the taskbar and Start list show it without a colored plate behind it
                SavePng(Path.Combine(folder, "Square44x44Logo.targetsize-" + size + "_altform-unplated.png"), size);
            }
        }

        private static void SaveScaled(string folder, string name, int size)
        {
            foreach (int scale in new[] { 100, 125, 150, 200, 400 })
                SavePng(Path.Combine(folder, name + ".scale-" + scale + ".png"),
                    (int)Math.Round(size * scale / 100.0, MidpointRounding.AwayFromZero));
        }

        private static void SavePng(string path, int size)
        {
            using (Bitmap bmp = Draw(size))
                bmp.Save(path, ImageFormat.Png);
        }

        /// <summary>Blue rounded tile with a white "ID".</summary>
        public static Bitmap Draw(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                RectangleF tile = new RectangleF(0, 0, size, size);
                using (GraphicsPath shape = RoundedRect(tile, size * 0.22f))
                using (LinearGradientBrush fill = new LinearGradientBrush(RectangleF.Inflate(tile, 1, 1), Top, Bottom, LinearGradientMode.Vertical))
                    g.FillPath(fill, shape);

                using (FontFamily family = new FontFamily("Segoe UI"))
                using (GraphicsPath label = new GraphicsPath())
                {
                    label.AddString("ID", family, (int)FontStyle.Bold, size * 0.58f, PointF.Empty, StringFormat.GenericTypographic);
                    RectangleF b = label.GetBounds();
                    using (Matrix center = new Matrix())
                    {
                        center.Translate(size / 2f - (b.X + b.Width / 2f), size / 2f - (b.Y + b.Height / 2f));
                        label.Transform(center);
                    }
                    g.FillPath(Brushes.White, label);
                }
            }
            return bmp;
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ICO container: small sizes as 32-bit DIBs (read by every Windows API), 256 px as PNG
        private static byte[] BuildIco(int[] sizes)
        {
            List<byte[]> images = new List<byte[]>();
            foreach (int size in sizes)
                using (Bitmap bmp = Draw(size))
                    images.Add(size >= 256 ? ToPng(bmp) : ToDib(bmp));

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write((short)0);
                w.Write((short)1);                        // type: icon
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dimension = (byte)(sizes[i] >= 256 ? 0 : sizes[i]); // 0 means 256
                    w.Write(dimension);
                    w.Write(dimension);
                    w.Write((byte)0);                     // palette size
                    w.Write((byte)0);                     // reserved
                    w.Write((short)1);                    // planes
                    w.Write((short)32);                   // bits per pixel
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] image in images) w.Write(image);
                w.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] ToDib(Bitmap bmp)
        {
            int width = bmp.Width, height = bmp.Height;
            int maskStride = (width + 31) / 32 * 4;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(40);                              // BITMAPINFOHEADER size
                w.Write(width);
                w.Write(height * 2);                      // color rows + mask rows
                w.Write((short)1);
                w.Write((short)32);
                w.Write(0);                               // BI_RGB
                w.Write(width * height * 4 + maskStride * height);
                w.Write(0L);                              // resolution
                w.Write(0L);                              // palette counts
                for (int y = height - 1; y >= 0; y--)     // rows bottom-up, BGRA
                    for (int x = 0; x < width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        w.Write(new[] { c.B, c.G, c.R, c.A });
                    }
                for (int y = height - 1; y >= 0; y--)     // AND mask, 1 = transparent
                {
                    byte[] row = new byte[maskStride];
                    for (int x = 0; x < width; x++)
                        if (bmp.GetPixel(x, y).A == 0) row[x >> 3] |= (byte)(0x80 >> (x & 7));
                    w.Write(row);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] ToPng(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
