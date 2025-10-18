using Xunit;
using Memux.Core.Models;
using Memux.Perception;
using System.Runtime.InteropServices;

namespace Memux.Tests;

public class Phase3Tests
{
    [Fact]
    public void ScreenCapture_Initialize_Success()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        
        // Act
        var capture = new ScreenCapture(desktopHandle);
        
        // Assert
        Assert.NotNull(capture);
    }
    
    [Fact]
    public void ScreenCapture_CaptureFrame_ReturnsData()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        
        // Act
        var (data, width, height) = capture.CaptureFrame();
        
        // Assert
        Assert.NotNull(data);
        Assert.True(width > 0, "Width should be positive");
        Assert.True(height > 0, "Height should be positive");
        Assert.Equal(width * height * 4, data.Length); // BGRA format
    }
    
    [Fact]
    public void ScreenCapture_MultipleCapturesConsistent()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        
        // Act
        var (data1, width1, height1) = capture.CaptureFrame();
        var (data2, width2, height2) = capture.CaptureFrame();
        
        // Assert
        Assert.Equal(width1, width2);
        Assert.Equal(height1, height2);
        Assert.Equal(data1.Length, data2.Length);
    }
    
    [Fact]
    public void ScreenCapture_PerformanceTest_100Frames()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < 100; i++)
        {
            capture.CaptureFrame();
        }
        sw.Stop();
        
        // Assert
        double avgMs = sw.ElapsedMilliseconds / 100.0;
        Assert.True(avgMs < 50, $"Average capture time {avgMs:F2}ms should be less than 50ms");
    }
    
    [Fact]
    public void DepthEstimator_InitializeWithoutModel_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = "nonexistent_model.onnx";
        
        // Act
        var estimator = new DepthEstimator(nonExistentPath, useGpu: false);
        
        // Assert
        Assert.NotNull(estimator);
        
        // Should return null when model not loaded
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        var (data, width, height) = capture.CaptureFrame();
        
        var depthMap = estimator.EstimateDepth(data, width, height);
        Assert.Null(depthMap);
    }
    
    [Fact]
    public void ObjectDetector_InitializeWithoutModel_HandlesGracefully()
    {
        // Arrange
        var nonExistentModelPath = "nonexistent_yolo.onnx";
        var nonExistentClassesPath = "nonexistent_classes.txt";
        
        // Act
        var detector = new ObjectDetector(nonExistentModelPath, nonExistentClassesPath, useGpu: false);
        
        // Assert
        Assert.NotNull(detector);
        
        // Should return empty list when model not loaded
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        var (data, width, height) = capture.CaptureFrame();
        
        var objects = detector.DetectObjects(data, width, height);
        Assert.NotNull(objects);
        Assert.Empty(objects);
    }
    
    [Fact]
    public void OcrEngine_InitializeWithoutTessdata_HandlesGracefully()
    {
        // Arrange
        var nonExistentPath = "nonexistent_tessdata";
        
        // Act
        var ocrEngine = new OcrEngine(nonExistentPath);
        
        // Assert
        Assert.NotNull(ocrEngine);
        
        // Should return empty list when tessdata not found
        var desktopHandle = GetDesktopWindow();
        var capture = new ScreenCapture(desktopHandle);
        var (data, width, height) = capture.CaptureFrame();
        
        var (results, processedImage, imgWidth, imgHeight) = ocrEngine.ExtractText(data, width, height);
        Assert.NotNull(results);
        Assert.Empty(results);
    }
    
    [Fact]
    public void PerceptionPipeline_InitializeWithoutModels_Success()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        
        // Act
        var pipeline = new PerceptionPipeline(
            desktopHandle,
            depthModelPath: null,
            objectDetectionModelPath: null,
            objectDetectionClassesPath: null,
            tessDataPath: null,
            useGpu: false
        );
        
        // Assert
        Assert.NotNull(pipeline);
    }
    
    [Fact]
    public void PerceptionPipeline_CaptureQuick_Success()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var pipeline = new PerceptionPipeline(
            desktopHandle,
            depthModelPath: null,
            objectDetectionModelPath: null,
            objectDetectionClassesPath: null,
            tessDataPath: null,
            useGpu: false
        );
        
        // Act
        var state = pipeline.CaptureQuick();
        
        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.ScreenData);
        Assert.True(state.Width > 0);
        Assert.True(state.Height > 0);
        Assert.True(state.Timestamp <= DateTime.UtcNow);
    }
    
    [Fact]
    public void PerceptionPipeline_CaptureAndProcess_WithoutModels_Success()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var pipeline = new PerceptionPipeline(
            desktopHandle,
            depthModelPath: null,
            objectDetectionModelPath: null,
            objectDetectionClassesPath: null,
            tessDataPath: null,
            useGpu: false
        );
        
        // Act
        var state = pipeline.CaptureAndProcess();
        
        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.ScreenData);
        Assert.True(state.Width > 0);
        Assert.True(state.Height > 0);
        // Without models, these should be null or empty
        Assert.Null(state.DepthMap);
        Assert.True(state.DetectedObjects == null || state.DetectedObjects.Count == 0);
        Assert.True(state.OcrResults == null || state.OcrResults.Count == 0);
    }
    
    [Fact]
    public void PerceptionPipeline_MultipleCaptures_Consistent()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var pipeline = new PerceptionPipeline(
            desktopHandle,
            depthModelPath: null,
            objectDetectionModelPath: null,
            objectDetectionClassesPath: null,
            tessDataPath: null,
            useGpu: false
        );
        
        // Act
        var state1 = pipeline.CaptureQuick();
        System.Threading.Thread.Sleep(10); // Small delay
        var state2 = pipeline.CaptureQuick();
        
        // Assert
        Assert.Equal(state1.Width, state2.Width);
        Assert.Equal(state1.Height, state2.Height);
        Assert.True(state2.Timestamp >= state1.Timestamp);
    }
    
    [Fact]
    public void PerceptionPipeline_PerformanceTest_QuickCapture()
    {
        // Arrange
        var desktopHandle = GetDesktopWindow();
        var pipeline = new PerceptionPipeline(
            desktopHandle,
            depthModelPath: null,
            objectDetectionModelPath: null,
            objectDetectionClassesPath: null,
            tessDataPath: null,
            useGpu: false
        );
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < 100; i++)
        {
            pipeline.CaptureQuick();
        }
        sw.Stop();
        
        // Assert
        double avgMs = sw.ElapsedMilliseconds / 100.0;
        Assert.True(avgMs < 50, $"Average quick capture time {avgMs:F2}ms should be less than 50ms");
    }
    
    [Fact]
    public void PerceptionState_Initialize_DefaultValues()
    {
        // Arrange & Act
        var state = new PerceptionState();
        
        // Assert
        // ContextHints may be null by default, initialize if needed
        Assert.Equal(0, state.Width);
        Assert.Equal(0, state.Height);
    }
    
    [Fact]
    public void DetectedObject_Properties_WorkCorrectly()
    {
        // Arrange & Act
        var obj = new DetectedObject
        {
            ClassName = "test_object",
            Confidence = 0.95f,
            BoundingBox = new BoundingBox { X = 100, Y = 200, Width = 50, Height = 75 }
        };
        
        // Assert
        Assert.Equal("test_object", obj.ClassName);
        Assert.Equal(0.95f, obj.Confidence);
        Assert.NotNull(obj.BoundingBox);
        Assert.Equal(100, obj.BoundingBox.X);
    }
    
    [Fact]
    public void OcrResult_Properties_WorkCorrectly()
    {
        // Arrange & Act
        var result = new OcrResult
        {
            Text = "Sample Text",
            Confidence = 0.85f,
            BoundingBox = new BoundingBox { X = 10, Y = 20, Width = 100, Height = 30 }
        };
        
        // Assert
        Assert.Equal("Sample Text", result.Text);
        Assert.Equal(0.85f, result.Confidence);
        Assert.NotNull(result.BoundingBox);
    }
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}

