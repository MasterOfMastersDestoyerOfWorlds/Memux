using Memux.Core.Models;
using Memux.Perception;
using Memux.DarkSouls;
using System.Diagnostics;

namespace Memux;

/// <summary>
/// Comprehensive demonstration of Phase 3: CV Pipeline
/// Tests all computer vision components
/// </summary>
public class Phase3Demo
{
    public static async Task RunAsync(
        string? depthModelPath = null,
        string? objectModelPath = null,
        string? objectClassesPath = null,
        string? tessDataPath = null,
        string? gamePath = null,
        bool useGpu = true)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Phase 3: CV Pipeline - Full Demonstration             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // ============================================================
        // Setup: Find game window or use desktop
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Setup: Finding Target Window                                  │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        IntPtr windowHandle = IntPtr.Zero;
        var darkSouls = new DarkSoulsIntegration(gamePath);
        
        if (darkSouls.AttachToGame())
        {
            windowHandle = darkSouls.GetGameWindowHandle();
            Console.WriteLine("✓ Found Dark Souls Remastered window");
        }
        else
        {
            // Fallback: use desktop window for testing
            windowHandle = GetDesktopWindow();
            Console.WriteLine("⚠ Dark Souls not found, using desktop window for testing");
        }
        
        if (windowHandle == IntPtr.Zero)
        {
            Console.WriteLine("✗ Could not find any window to capture");
            return;
        }
        Console.WriteLine();

        // ============================================================
        // Feature 1: Screen Capture
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 1: Screen Capture (BitBlt)                            │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        var screenCapture = new ScreenCapture(windowHandle);
        
        Console.WriteLine("[Test 1] Capturing single frame...");
        var sw = Stopwatch.StartNew();
        var (data, width, height) = screenCapture.CaptureFrame();
        sw.Stop();
        
        Console.WriteLine($"✓ Captured frame: {width}x{height}");
        Console.WriteLine($"  Data size: {data.Length / 1024 / 1024:F2} MB");
        Console.WriteLine($"  Capture time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
        
        Console.WriteLine("[Test 2] Testing capture performance (100 frames)...");
        sw.Restart();
        int captureCount = 100;
        for (int i = 0; i < captureCount; i++)
        {
            screenCapture.CaptureFrame();
        }
        sw.Stop();
        double avgCaptureMs = sw.ElapsedMilliseconds / (double)captureCount;
        double captureFps = 1000.0 / avgCaptureMs;
        
        Console.WriteLine($"✓ Average capture time: {avgCaptureMs:F2}ms");
        Console.WriteLine($"  Potential FPS: {captureFps:F1}");
        Console.WriteLine();

        // ============================================================
        // Feature 2: Depth Estimation (MiDaS)
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 2: Depth Estimation (MiDaS via ONNX)                  │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        DepthEstimator? depthEstimator = null;
        if (!string.IsNullOrEmpty(depthModelPath))
        {
            Console.WriteLine($"[Step 1] Loading depth model from: {depthModelPath}");
            Console.WriteLine($"  GPU acceleration: {(useGpu ? "Enabled" : "Disabled")}");
            
            depthEstimator = new DepthEstimator(depthModelPath, useGpu);
            
            if (depthEstimator != null)
            {
                Console.WriteLine("[Step 2] Running depth estimation...");
                sw.Restart();
                var depthMap = depthEstimator.EstimateDepth(data, width, height);
                sw.Stop();
                
                if (depthMap != null)
                {
                    Console.WriteLine($"✓ Depth estimation complete");
                    Console.WriteLine($"  Depth map size: {depthMap.Length} pixels");
                    Console.WriteLine($"  Processing time: {sw.ElapsedMilliseconds}ms");
                    
                    // Calculate statistics
                    float minDepth = depthMap.Min();
                    float maxDepth = depthMap.Max();
                    float avgDepth = depthMap.Average();
                    
                    Console.WriteLine($"  Depth range: {minDepth:F3} to {maxDepth:F3}");
                    Console.WriteLine($"  Average depth: {avgDepth:F3}");
                }
                else
                {
                    Console.WriteLine("✗ Depth estimation failed");
                }
            }
        }
        else
        {
            Console.WriteLine("⚠ No depth model specified (use --depth-model argument)");
            Console.WriteLine("  Depth estimation will be skipped.");
        }
        Console.WriteLine();

        // ============================================================
        // Feature 3: Object Detection (YOLO)
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 3: Object Detection (YOLOv8 via ONNX)                 │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        ObjectDetector? objectDetector = null;
        if (!string.IsNullOrEmpty(objectModelPath) && !string.IsNullOrEmpty(objectClassesPath))
        {
            Console.WriteLine($"[Step 1] Loading object detection model");
            Console.WriteLine($"  Model: {objectModelPath}");
            Console.WriteLine($"  Classes: {objectClassesPath}");
            Console.WriteLine($"  GPU acceleration: {(useGpu ? "Enabled" : "Disabled")}");
            
            objectDetector = new ObjectDetector(objectModelPath, objectClassesPath, useGpu);
            
            Console.WriteLine("[Step 2] Running object detection...");
            sw.Restart();
            var detectedObjects = objectDetector.DetectObjects(data, width, height, 0.5f);
            sw.Stop();
            
            Console.WriteLine($"✓ Object detection complete");
            Console.WriteLine($"  Detected objects: {detectedObjects.Count}");
            Console.WriteLine($"  Processing time: {sw.ElapsedMilliseconds}ms");
            
            if (detectedObjects.Any())
            {
                Console.WriteLine($"  Top detections:");
                foreach (var obj in detectedObjects.OrderByDescending(o => o.Confidence).Take(5))
                {
                    Console.WriteLine($"    - {obj.ClassName}: {obj.Confidence:P0} at ({obj.BoundingBox.X:F0}, {obj.BoundingBox.Y:F0})");
                }
            }
        }
        else
        {
            Console.WriteLine("⚠ No object detection model specified");
            Console.WriteLine("  Use --object-model and --object-classes arguments");
        }
        Console.WriteLine();

        // ============================================================
        // Feature 4: OCR (Tesseract)
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 4: OCR Text Extraction (Tesseract)                    │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        OcrEngine? ocrEngine = null;
        if (!string.IsNullOrEmpty(tessDataPath))
        {
            Console.WriteLine($"[Step 1] Loading OCR engine from: {tessDataPath}");
            
            ocrEngine = new OcrEngine(tessDataPath);
            
            Console.WriteLine("[Step 2] Running OCR...");
            sw.Restart();
            var ocrResults = ocrEngine.ExtractText(data, width, height);
            sw.Stop();
            
            Console.WriteLine($"✓ OCR complete");
            Console.WriteLine($"  Extracted words: {ocrResults.Count}");
            Console.WriteLine($"  Processing time: {sw.ElapsedMilliseconds}ms");
            
            if (ocrResults.Any())
            {
                Console.WriteLine($"  Sample text (high confidence):");
                foreach (var result in ocrResults.OrderByDescending(r => r.Confidence).Take(10))
                {
                    Console.WriteLine($"    - \"{result.Text}\" ({result.Confidence:P0})");
                }
            }
        }
        else
        {
            Console.WriteLine("⚠ No tessdata path specified (use --tess-data argument)");
            Console.WriteLine("  OCR will be skipped.");
        }
        Console.WriteLine();

        // ============================================================
        // Feature 5: Full Pipeline Performance
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 5: Full Pipeline Integration                          │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("[Test] Running full perception pipeline...");
        var pipeline = new PerceptionPipeline(
            windowHandle,
            depthModelPath,
            objectModelPath,
            objectClassesPath,
            tessDataPath,
            useGpu
        );
        
        Console.WriteLine("  Processing 30 frames to measure performance...");
        var frameTimes = new List<long>();
        
        for (int i = 0; i < 30; i++)
        {
            sw.Restart();
            var state = pipeline.CaptureAndProcess();
            sw.Stop();
            frameTimes.Add(sw.ElapsedMilliseconds);
            
            if (i % 10 == 0)
            {
                Console.Write(".");
            }
        }
        Console.WriteLine(" Done!");
        Console.WriteLine();
        
        // Calculate statistics
        double avgFrameTime = frameTimes.Average();
        double maxFrameTime = frameTimes.Max();
        double minFrameTime = frameTimes.Min();
        double fps = 1000.0 / avgFrameTime;
        
        Console.WriteLine($"✓ Pipeline performance:");
        Console.WriteLine($"  Average frame time: {avgFrameTime:F2}ms ({fps:F1} FPS)");
        Console.WriteLine($"  Min frame time: {minFrameTime}ms");
        Console.WriteLine($"  Max frame time: {maxFrameTime}ms");
        Console.WriteLine($"  Target 60 FPS (16.67ms): {(avgFrameTime <= 16.67 ? "✓ Achieved" : "✗ Not achieved")}");
        Console.WriteLine();

        // ============================================================
        // Feature 6: PerceptionState Population
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 6: PerceptionState Population                         │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("[Test] Capturing and analyzing perception state...");
        var finalState = pipeline.CaptureAndProcess();
        
        Console.WriteLine($"✓ PerceptionState populated:");
        Console.WriteLine($"  Timestamp: {finalState.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"  Resolution: {finalState.Width}x{finalState.Height}");
        Console.WriteLine($"  Screen data: {(finalState.ScreenData != null ? $"{finalState.ScreenData.Length / 1024 / 1024:F2} MB" : "null")}");
        Console.WriteLine($"  Depth map: {(finalState.DepthMap != null ? $"{finalState.DepthMap.Length} pixels" : "null")}");
        Console.WriteLine($"  Detected objects: {finalState.DetectedObjects?.Count ?? 0}");
        Console.WriteLine($"  OCR results: {finalState.OcrResults?.Count ?? 0}");
        Console.WriteLine();

        // ============================================================
        // Feature 7: Real-time Demo (Optional)
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 7: Real-time Capture Demo                             │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("Run real-time capture for 5 seconds? (Y/N)");
        var key = Console.ReadKey(true);
        
        if (key.Key == ConsoleKey.Y)
        {
            Console.WriteLine("Running real-time capture... (Press any key to stop)");
            Console.WriteLine();
            
            var startTime = DateTime.UtcNow;
            int frameCount = 0;
            var cancellation = new CancellationTokenSource();
            
            _ = Task.Run(() =>
            {
                Console.ReadKey(true);
                cancellation.Cancel();
            });
            
            while (!cancellation.Token.IsCancellationRequested && 
                   (DateTime.UtcNow - startTime).TotalSeconds < 5)
            {
                var state = pipeline.CaptureAndProcess();
                frameCount++;
                
                // Display live stats
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"Frame {frameCount} | Objects: {state.DetectedObjects?.Count ?? 0} | Text: {state.OcrResults?.Count ?? 0}   ");
                
                await Task.Delay(16); // ~60 FPS target
            }
            
            Console.WriteLine();
            Console.WriteLine($"✓ Captured {frameCount} frames in {(DateTime.UtcNow - startTime).TotalSeconds:F1}s");
            Console.WriteLine($"  Average FPS: {frameCount / (DateTime.UtcNow - startTime).TotalSeconds:F1}");
        }
        else
        {
            Console.WriteLine("Skipping real-time demo.");
        }
        Console.WriteLine();

        // Cleanup
        pipeline.Dispose();
        depthEstimator?.Dispose();
        objectDetector?.Dispose();
        ocrEngine?.Dispose();

        // ============================================================
        // Summary
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Phase 3 Summary                                                │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("Phase 3 Components Demonstrated:");
        Console.WriteLine($"  ✓ Screen Capture (BitBlt)");
        Console.WriteLine($"  {(depthEstimator != null ? "✓" : "⚠")} Depth Estimation (MiDaS)");
        Console.WriteLine($"  {(objectDetector != null ? "✓" : "⚠")} Object Detection (YOLO)");
        Console.WriteLine($"  {(ocrEngine != null ? "✓" : "⚠")} OCR (Tesseract)");
        Console.WriteLine($"  ✓ PerceptionPipeline coordination");
        Console.WriteLine($"  ✓ Parallel CV processing");
        Console.WriteLine($"  ✓ PerceptionState population");
        Console.WriteLine();
        
        if (depthEstimator == null || objectDetector == null || ocrEngine == null)
        {
            Console.WriteLine("⚠ Some CV models were not loaded.");
            Console.WriteLine("  See MODEL_SETUP.md for instructions on downloading models.");
            Console.WriteLine();
        }
        
        Console.WriteLine("Performance Summary:");
        Console.WriteLine($"  Screen Capture: {avgCaptureMs:F2}ms ({captureFps:F1} FPS)");
        Console.WriteLine($"  Full Pipeline: {avgFrameTime:F2}ms ({fps:F1} FPS)");
        string fpsStatus = avgFrameTime <= 16.67 ? "✓ Achieved" : $"✗ {(16.67 - avgFrameTime):F2}ms too slow";
        Console.WriteLine($"  60 FPS Target: {fpsStatus}");
        Console.WriteLine();
        
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            Phase 3: CV Pipeline - COMPLETE ✓                  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}

