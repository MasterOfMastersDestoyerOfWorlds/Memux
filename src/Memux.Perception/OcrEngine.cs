using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Tesseract;
using Memux.Core.Models;

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
    public (List<OcrResult> Results, byte[]? ProcessedImageData, int Width, int Height) ExtractText(byte[] rgbaData, int width, int height)
    {
        if (_engine == null)
            return (new List<OcrResult>(), null, width, height);

        try
        {
            Console.WriteLine($"widht: {width} height: {height}");
            using var img = Pix.Create(width, height, 32);
            System.Runtime.InteropServices.Marshal.Copy(rgbaData, 0, img.GetData().Data, rgbaData.Length);

            using var page = _engine.Process(img, PageSegMode.SparseText);

            var stopwatch = Stopwatch.StartNew();
            var results = new List<OcrResult>();
            using var iter = page.GetIterator();
            iter.Begin();

            stopwatch.Stop();
            Console.WriteLine($"OCR processing took {stopwatch.ElapsedMilliseconds} ms");
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

            // Return the processed image data along with results
            return (results, rgbaData, width, height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OCR error: {ex.Message}");
            return (new List<OcrResult>(), null, width, height);
        }
    }

    /// <summary>
    /// Converts a BGRA image to a 1-bit black and white image.
    /// </summary>
    /// <param name="bgra">The source image data in 32-bit BGRA format.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="threshold">The brightness threshold (0-255). Pixels brighter than this become white.</param>
    /// <param name="invert">Set to true for white text on a dark background.</param>
    /// <returns>A binarized byte array (one byte per pixel, value is 0 or 255).</returns>
    private byte[] Binarize(byte[] bgra, int width, int height, byte threshold = 128, bool invert = false)
    {
        // The output is still a byte per pixel, but the value will only be 0 (black) or 255 (white).
        var binarized = new byte[width * height];

        unsafe
        {
            fixed (byte* src = bgra)
            fixed (byte* dst = binarized)
            {
                byte* s = src;
                byte* d = dst;

                for (int i = 0; i < width * height; i++, s += 4)
                {
                    // First, calculate the grayscale value same as before
                    // Using integer arithmetic can be faster: (77*R + 150*G + 29*B) >> 8
                    byte grayValue = (byte)(0.299f * s[2] + 0.587f * s[1] + 0.114f * s[0]);

                    // Second, apply the threshold to determine black or white.
                    if (invert)
                    {
                        // For inverted text (e.g., white text on black background),
                        // we want the bright text to become black for Tesseract.
                        *d++ = (grayValue > threshold) ? (byte)0 : (byte)255;
                    }
                    else
                    {
                        // For standard text (black text on white background),
                        // we want the dark text to become black.
                        *d++ = (grayValue > threshold) ? (byte)255 : (byte)0;
                    }
                }
            }
        }

        return binarized;
    }
    private byte[] ConvertToGrayscale(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];
        int length = width * height * 4;

        unsafe
        {
            fixed (byte* src = bgra)
            fixed (byte* dst = gray)
            {
                byte* s = src;
                byte* d = dst;

                for (int i = 0; i < width * height; i++, s += 4)
                {
                    // 0.299R + 0.587G + 0.114B
                    *d++ = (byte)(0.299f * s[2] + 0.587f * s[1] + 0.114f * s[0]);
                }
            }
        }

        return gray;
    }

    private byte[] CropRegion(byte[] bgra, int width, int height, int x, int y, int cropWidth, int cropHeight)
    {
        var cropped = new byte[cropWidth * cropHeight * 4];

        unsafe
        {
            fixed (byte* src = bgra)
            fixed (byte* dst = cropped)
            {
                for (int cy = 0; cy < cropHeight; cy++)
                {
                    int srcY = y + cy;
                    if (srcY < 0 || srcY >= height) continue;

                    byte* srcRow = src + (srcY * width + x) * 4;
                    byte* dstRow = dst + cy * cropWidth * 4;

                    int copyWidth = Math.Min(cropWidth, width - x);
                    Buffer.MemoryCopy(srcRow, dstRow, cropWidth * 4, copyWidth * 4);
                }
            }
        }

        return cropped;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
