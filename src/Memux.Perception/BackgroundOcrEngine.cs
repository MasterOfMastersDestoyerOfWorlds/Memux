using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Non-blocking OCR engine that processes frames on a background thread
/// Returns cached results immediately while processing new frames asynchronously
/// </summary>
public class BackgroundOcrEngine : IDisposable
{
    private readonly OcrEngine _ocrEngine;
    private readonly DirtyRegionDetector _dirtyDetector;
    private readonly Thread _workerThread;
    private readonly AutoResetEvent _frameAvailable = new(false);
    private readonly object _lock = new();
    
    private volatile bool _running = true;
    private byte[]? _pendingFrame;
    private int _pendingWidth;
    private int _pendingHeight;
    
    // Cached results (thread-safe access)
    private List<OcrResult> _cachedResults = new();
    private byte[]? _cachedProcessedImage;
    private int _cachedImageWidth;
    private int _cachedImageHeight;

    public BackgroundOcrEngine(string tessDataPath = "./tessdata")
    {
        _ocrEngine = new OcrEngine(tessDataPath);
        _dirtyDetector = new DirtyRegionDetector(threshold: 30, minRegionSize: 10);
        
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "OCR Worker"
        };
        _workerThread.Start();
    }

    /// <summary>
    /// Submit a new frame for processing (non-blocking)
    /// </summary>
    public void SubmitFrame(byte[] rgbaData, int width, int height)
    {
        lock (_lock)
        {
            // Replace pending frame (only keep most recent)
            _pendingFrame = rgbaData;
            _pendingWidth = width;
            _pendingHeight = height;
        }
        
        _frameAvailable.Set();
    }

    /// <summary>
    /// Get cached results immediately (non-blocking)
    /// </summary>
    public (List<OcrResult> Results, byte[]? ProcessedImageData, int Width, int Height) GetResults()
    {
        lock (_lock)
        {
            // Return a copy of the cached results
            return (new List<OcrResult>(_cachedResults), _cachedProcessedImage, _cachedImageWidth, _cachedImageHeight);
        }
    }

    /// <summary>
    /// Reset the detector (e.g., when switching windows)
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _dirtyDetector.Reset();
            _cachedResults.Clear();
            _cachedProcessedImage = null;
        }
    }

    private void WorkerLoop()
    {
        while (_running)
        {
            // Wait for a frame to be available
            _frameAvailable.WaitOne(100);
            
            if (!_running) break;

            byte[]? frameToProcess = null;
            int width = 0, height = 0;

            // Get pending frame
            lock (_lock)
            {
                if (_pendingFrame != null)
                {
                    frameToProcess = _pendingFrame;
                    width = _pendingWidth;
                    height = _pendingHeight;
                    _pendingFrame = null;
                }
            }

            if (frameToProcess == null)
                continue;

            try
            {
                ProcessFrame(frameToProcess, width, height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BackgroundOCR error: {ex.Message}");
            }
        }
    }

    private void ProcessFrame(byte[] frameData, int width, int height)
    {
        // Detect dirty regions
        var dirtyRegions = _dirtyDetector.CompareFrames(frameData, width, height);

        // If nothing changed, no need to process
        if (dirtyRegions.Count == 0)
        {
            return;
        }

        List<OcrResult> previousResults;
        lock (_lock)
        {
            previousResults = new List<OcrResult>(_cachedResults);
        }

        // Expand dirty regions to include overlapping text bounding boxes
        var expandedRegions = _dirtyDetector.ExpandRegionsForBoundingBoxes(dirtyRegions, previousResults);

        // Process each dirty region
        var newRegionResults = new List<OcrResult>();
        var regionBounds = new List<Rectangle>();

        foreach (var region in expandedRegions)
        {
            // Ensure region is within bounds
            var clampedRegion = new Rectangle(
                Math.Max(0, region.X),
                Math.Max(0, region.Y),
                Math.Min(region.Width, width - Math.Max(0, region.X)),
                Math.Min(region.Height, height - Math.Max(0, region.Y))
            );

            if (clampedRegion.Width <= 0 || clampedRegion.Height <= 0)
                continue;

            var regionResults = _ocrEngine.ExtractTextFromRegion(frameData, clampedRegion, width, height);
            newRegionResults.AddRange(regionResults);
            regionBounds.Add(clampedRegion);
        }

        // Merge results: remove old results in dirty regions, add new results
        var mergedResults = new List<OcrResult>();

        // Keep results that are NOT in any dirty region
        foreach (var oldResult in previousResults)
        {
            var resultRect = new Rectangle(
                (int)oldResult.BoundingBox.X,
                (int)oldResult.BoundingBox.Y,
                (int)oldResult.BoundingBox.Width,
                (int)oldResult.BoundingBox.Height
            );

            bool inDirtyRegion = false;
            foreach (var dirtyRegion in expandedRegions)
            {
                if (dirtyRegion.IntersectsWith(resultRect))
                {
                    inDirtyRegion = true;
                    break;
                }
            }

            if (!inDirtyRegion)
            {
                mergedResults.Add(oldResult);
            }
        }

        // Add new results from dirty regions
        mergedResults.AddRange(newRegionResults);

        // Update cached results
        lock (_lock)
        {
            _cachedResults = mergedResults;
            _cachedProcessedImage = frameData;
            _cachedImageWidth = width;
            _cachedImageHeight = height;
        }
    }

    public void Dispose()
    {
        _running = false;
        _frameAvailable.Set();
        
        if (_workerThread.IsAlive)
        {
            _workerThread.Join(1000);
        }
        
        _ocrEngine?.Dispose();
        _frameAvailable?.Dispose();
    }
}

