using Memux.Core.Models;
using Memux.Core;

namespace Memux.Perception;

/// <summary>
/// Coordinates all perception components
/// Manages frame capture and CV processing
/// </summary>
public class PerceptionPipeline : IDisposable
{
    private readonly ScreenCapture _screenCapture;
    private readonly DepthEstimator? _depthEstimator;
    private readonly ObjectDetector? _objectDetector;
    private readonly OcrEngine? _ocrEngine;
    
    private DateTime _lastCaptureTime;
    private int _frameCount;
    
    public PerceptionPipeline(
        IntPtr windowHandle,
        string? depthModelPath = null,
        string? objectDetectionModelPath = null,
        string? objectDetectionClassesPath = null,
        string? tessDataPath = null,
        bool useGpu = true)
    {
        _screenCapture = new ScreenCapture(windowHandle);
        
        // Initialize CV models if paths provided
        if (!string.IsNullOrEmpty(depthModelPath))
        {
            _depthEstimator = new DepthEstimator(depthModelPath, useGpu);
        }
        
        if (!string.IsNullOrEmpty(objectDetectionModelPath) && !string.IsNullOrEmpty(objectDetectionClassesPath))
        {
            _objectDetector = new ObjectDetector(objectDetectionModelPath, objectDetectionClassesPath, useGpu);
        }
        
        // Initialize OCR engine - use provided path or default to ./tessdata
        string ocrPath = !string.IsNullOrEmpty(tessDataPath) ? tessDataPath : "./tessdata";
        _ocrEngine = new OcrEngine(ocrPath);
    }
    
    /// <summary>
    /// Capture current frame and run full perception pipeline
    /// </summary>
    public PerceptionState CaptureAndProcess()
    {
        var state = new PerceptionState
        {
            Timestamp = DateTime.UtcNow
        };
        
        try
        {
            // Capture screen
            var (data, width, height) = _screenCapture.CaptureFrame();
            state.ScreenData = data;
            state.Width = width;
            state.Height = height;
            
            // Run CV models in parallel for speed
            var tasks = new List<Task>();
            
            // Depth estimation
            if (_depthEstimator != null)
            {
                tasks.Add(Task.Run(() =>
                {
                    state.DepthMap = _depthEstimator.EstimateDepth(data, width, height);
                }));
            }
            
            // Object detection
            if (_objectDetector != null)
            {
                tasks.Add(Task.Run(() =>
                {
                    state.DetectedObjects = _objectDetector.DetectObjects(data, width, height);
                }));
            }
            
            // OCR (most expensive, run last)
            if (_ocrEngine != null)
            {
                tasks.Add(Task.Run(() =>
                {
                    state.OcrResults = _ocrEngine.ExtractText(data, width, height);
                }));
            }
            
            // Wait for all CV tasks to complete
            Task.WaitAll(tasks.ToArray());
            
            _frameCount++;
            var now = DateTime.UtcNow;
            if ((now - _lastCaptureTime).TotalSeconds >= 1.0)
            {
                _frameCount = 0;
                _lastCaptureTime = now;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Perception pipeline error: {ex.Message}");
        }
        
        return state;
    }
    
    /// <summary>
    /// Quick capture without expensive CV (for low-latency scenarios)
    /// </summary>
    public PerceptionState CaptureQuick()
    {
        try
        {
            var (data, width, height) = _screenCapture.CaptureFrame();
            return new PerceptionState
            {
                Timestamp = DateTime.UtcNow,
                ScreenData = data,
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Perception quick capture error: {ex.Message}");
            return new PerceptionState
            {
                Timestamp = DateTime.UtcNow,
                ScreenData = Array.Empty<byte>(),
                Width = 0,
                Height = 0
            };
        }
    }
    
    public void Dispose()
    {
        _depthEstimator?.Dispose();
        _objectDetector?.Dispose();
        _ocrEngine?.Dispose();
    }

    /// <summary>
    /// Update the underlying capture window handle when it changes
    /// </summary>
    public void UpdateWindowHandle(IntPtr windowHandle)
    {
        _screenCapture.SetWindowHandle(windowHandle);
    }
}

