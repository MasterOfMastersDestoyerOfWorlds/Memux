using Tesseract;
using Memux.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Memux.Perception;

/// <summary>
/// Text extraction using Tesseract OCR
/// Extracts text from screen regions
/// </summary>
public class OcrEngine : IDisposable
{
    private TesseractEngine? _engine;
    private readonly string _tessDataPath;
    
    public OcrEngine(string tessDataPath = "./tessdata")
    {
        _tessDataPath = tessDataPath;
        
        if (!Directory.Exists(tessDataPath))
        {
            Console.WriteLine($"Warning: Tesseract data not found at {tessDataPath}");
            Console.WriteLine("OCR will be disabled. Download tessdata to enable.");
            return;
        }
        
        try
        {
            _engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            Console.WriteLine("OCR engine loaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load OCR engine: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Extract text from entire image
    /// </summary>
    public List<OcrResult> ExtractText(byte[] rgbaData, int width, int height)
    {
        if (_engine == null)
        {
            return new List<OcrResult>();
        }
        
        try
        {
            // Convert BGRA to grayscale for better OCR
            var grayData = ConvertToGrayscale(rgbaData, width, height);
            
            // Create a temporary PNG file and load it
            var tempFile = Path.GetTempFileName() + ".png";
            try
            {
                CreateGrayscalePng(grayData, width, height, tempFile);
                using var img = Pix.LoadFromFile(tempFile);
                using var page = _engine.Process(img);
                
                var results = new List<OcrResult>();
                
                // Get text with bounding boxes
                using var iter = page.GetIterator();
                iter.Begin();
                
                do
                {
                    if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
                    {
                        string word = iter.GetText(PageIteratorLevel.Word);
                        float confidence = iter.GetConfidence(PageIteratorLevel.Word) / 100f;
                        
                        if (confidence > 0.5f && !string.IsNullOrWhiteSpace(word))
                        {
                            results.Add(new OcrResult
                            {
                                Text = word.Trim(),
                                Confidence = confidence,
                                BoundingBox = new BoundingBox
                                {
                                    X = bounds.X1,
                                    Y = bounds.Y1,
                                    Width = bounds.X2 - bounds.X1,
                                    Height = bounds.Y2 - bounds.Y1
                                }
                            });
                        }
                    }
                } while (iter.Next(PageIteratorLevel.Word));
                
                return results;
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OCR error: {ex.Message}");
            return new List<OcrResult>();
        }
    }
    
    /// <summary>
    /// Extract text from specific region
    /// </summary>
    public string? ExtractTextFromRegion(byte[] rgbaData, int width, int height, int x, int y, int regionWidth, int regionHeight)
    {
        if (_engine == null)
        {
            return null;
        }
        
        try
        {
            // Crop to region
            var regionData = CropRegion(rgbaData, width, height, x, y, regionWidth, regionHeight);
            var grayData = ConvertToGrayscale(regionData, regionWidth, regionHeight);
            
            // Create a temporary PNG file and load it
            var tempFile = Path.GetTempFileName() + ".png";
            try
            {
                CreateGrayscalePng(grayData, regionWidth, regionHeight, tempFile);
                using var img = Pix.LoadFromFile(tempFile);
                using var page = _engine.Process(img);
                
                return page.GetText();
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OCR region error: {ex.Message}");
            return null;
        }
    }
    
    private byte[] ConvertToGrayscale(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];
        
        for (int i = 0; i < width * height; i++)
        {
            int idx = i * 4;
            if (idx + 2 < bgra.Length)
            {
                // Grayscale conversion: 0.299*R + 0.587*G + 0.114*B
                gray[i] = (byte)(0.299 * bgra[idx + 2] + 0.587 * bgra[idx + 1] + 0.114 * bgra[idx]);
            }
        }
        
        return gray;
    }
    
    private byte[] CropRegion(byte[] bgra, int width, int height, int x, int y, int cropWidth, int cropHeight)
    {
        var cropped = new byte[cropWidth * cropHeight * 4];
        
        for (int cy = 0; cy < cropHeight; cy++)
        {
            for (int cx = 0; cx < cropWidth; cx++)
            {
                int srcX = x + cx;
                int srcY = y + cy;
                
                if (srcX >= 0 && srcX < width && srcY >= 0 && srcY < height)
                {
                    int srcIdx = (srcY * width + srcX) * 4;
                    int dstIdx = (cy * cropWidth + cx) * 4;
                    
                    Array.Copy(bgra, srcIdx, cropped, dstIdx, 4);
                }
            }
        }
        
        return cropped;
    }
    
    private void CreateGrayscalePng(byte[] grayData, int width, int height, string filePath)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
        
        // Set up grayscale palette
        var palette = bitmap.Palette;
        for (int i = 0; i < 256; i++)
        {
            palette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
        }
        bitmap.Palette = palette;
        
        // Lock bitmap data and copy grayscale data
        var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
        
        try
        {
            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ptr[y * bitmapData.Stride + x] = grayData[y * width + x];
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
        
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }
    
    public void Dispose()
    {
        _engine?.Dispose();
    }
}

