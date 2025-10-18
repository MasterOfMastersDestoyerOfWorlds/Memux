using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Memux.Core.Models;

namespace Memux.UI;

public static class PerceptionRenderer
{
    public static Bitmap? CreateDepthBitmap(float[]? depth, int width, int height)
    {
        if (depth == null || depth.Length == 0 || width <= 0 || height <= 0) return null;
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int len = width * height;
            var buffer = new byte[len * 4];
            for (int i = 0; i < len; i++)
            {
                float v = Math.Clamp(depth[i], 0f, 1f);
                var (r, g, b) = TurboColormap(v);
                int idx = i * 4;
                buffer[idx + 0] = b; // B
                buffer[idx + 1] = g; // G
                buffer[idx + 2] = r; // R
                buffer[idx + 3] = 255; // A
            }
            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    public static Bitmap? CreateScreenshotBitmap(byte[]? bgra, int width, int height)
    {
        if (bgra == null || bgra.Length == 0 || width <= 0 || height <= 0) return null;
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            // BGRA matches PixelFormat.Format32bppArgb memory layout on little-endian
            Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    public static Bitmap? CreateObjectsOverlay(Bitmap? baseImage, List<DetectedObject>? objects)
    {
        if (baseImage == null) return null;
        var bmp = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);
            if (objects != null)
            {
                using var pen = new Pen(Color.LimeGreen, 2);
                using var font = new Font("Segoe UI", 9, FontStyle.Bold);
                using var bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
                using var textBrush = new SolidBrush(Color.White);
                foreach (var obj in objects)
                {
                    var rect = new RectangleF(obj.BoundingBox.X, obj.BoundingBox.Y, obj.BoundingBox.Width, obj.BoundingBox.Height);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    var label = $"{obj.ClassName} {(obj.Confidence * 100):F0}%";
                    var size = g.MeasureString(label, font);
                    var labelRect = new RectangleF(rect.X, Math.Max(0, rect.Y - size.Height), size.Width + 6, size.Height);
                    g.FillRectangle(bgBrush, labelRect);
                    g.DrawString(label, font, textBrush, labelRect.X + 3, labelRect.Y + 0);
                }
            }
        }
        return bmp;
    }

    public static Bitmap? CreateOcrOverlay(Bitmap? baseImage, List<OcrResult>? ocrResults)
    {
        if (baseImage == null) return null;
        var bmp = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);
            if (ocrResults != null)
            {
                using var pen = new Pen(Color.Red, 2);
                using var font = new Font("Segoe UI", 8, FontStyle.Regular);
                using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
                using var textBrush = new SolidBrush(Color.White);
                foreach (var r in ocrResults)
                {
                    var rect = new RectangleF(r.BoundingBox.X, r.BoundingBox.Y, r.BoundingBox.Width, r.BoundingBox.Height);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    // For readability, draw text in a right-side panel overlay region
                    // Note: Final layout will place text list in UI panel; this keeps overlay minimal
                    var label = r.Text;
                    var size = g.MeasureString(label, font);
                    var labelRect = new RectangleF(Math.Min(baseImage.Width - size.Width - 10, rect.Right + 4), Math.Max(0, rect.Y), size.Width + 6, size.Height);
                    g.FillRectangle(bgBrush, labelRect);
                    g.DrawString(label, font, textBrush, labelRect.X + 3, labelRect.Y);
                }
            }
        }
        return bmp;
    }

    public static Bitmap? CreateObjectsSegmentation(Bitmap? baseImage, int[]? segmentation, List<DetectedObject>? objects, int width, int height)
    {
        if (baseImage == null) return null;
        var bmp = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);
            if (segmentation != null && segmentation.Length == width * height)
            {
                // Overlay semi-transparent class color mask
                using var segBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, width, height);
                var data = segBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var buffer = new byte[width * height * 4];
                    for (int i = 0; i < width * height; i++)
                    {
                        int cls = segmentation[i];
                        var (r, gch, b) = ClassColor(cls);
                        int idx = i * 4;
                        buffer[idx + 0] = b;
                        buffer[idx + 1] = gch;
                        buffer[idx + 2] = r;
                        buffer[idx + 3] = 80; // alpha
                    }
                    Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                }
                finally
                {
                    segBmp.UnlockBits(data);
                }
                g.DrawImage(segBmp, 0, 0, baseImage.Width, baseImage.Height);
            }
            if (objects != null)
            {
                using var pen = new Pen(Color.Yellow, 2);
                using var font = new Font("Segoe UI", 9, FontStyle.Bold);
                using var bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
                using var textBrush = new SolidBrush(Color.White);
                foreach (var obj in objects)
                {
                    var rect = new RectangleF(obj.BoundingBox.X, obj.BoundingBox.Y, obj.BoundingBox.Width, obj.BoundingBox.Height);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    var label = $"{obj.ClassName} {(obj.Confidence * 100):F0}%";
                    var size = g.MeasureString(label, font);
                    var labelRect = new RectangleF(rect.X, Math.Max(0, rect.Y - size.Height), size.Width + 6, size.Height);
                    g.FillRectangle(bgBrush, labelRect);
                    g.DrawString(label, font, textBrush, labelRect.X + 3, labelRect.Y + 0);
                }
            }
        }
        return bmp;
    }

    private static (byte r, byte g, byte b) TurboColormap(float x)
    {
        // Approximate Google Turbo colormap (fast piecewise polynomial)
        // Source: https://ai.googleblog.com/2019/08/turbo-improved-rainbow-colormap-for.html
        // Coefficients approximated for compactness
        double r = 34.61 + x * (1172.33 + x * (-10793.56 + x * (33300.12 + x * (-38394.49 + x * 14825.05))));
        double g = 23.31 + x * (557.33 + x * (1225.70 + x * (-3574.04 + x * (4479.99 + x * -1963.34))));
        double b = 27.2 + x * (321.04 + x * (1536.44 + x * (-5327.09 + x * (6686.02 + x * -2794.82))));
        byte R = (byte)Math.Clamp(r, 0, 255);
        byte G = (byte)Math.Clamp(g, 0, 255);
        byte B = (byte)Math.Clamp(b, 0, 255);
        return (R, G, B);
    }

    private static (byte r, byte g, byte b) ClassColor(int cls)
    {
        // Simple hash-based color
        unchecked
        {
            int h = (int)(cls * 2654435761u);
            byte r = (byte)(h & 0xFF);
            byte g = (byte)((h >> 8) & 0xFF);
            byte b = (byte)((h >> 16) & 0xFF);
            return (r, g, b);
        }
    }
}



