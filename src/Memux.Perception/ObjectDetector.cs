using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Object detection using YOLO model via ONNX Runtime
/// Detects objects in image and returns bounding boxes with class labels
/// </summary>
public class ObjectDetector : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly string[] _classNames;
    private readonly int _inputSize = 640;
    
    public ObjectDetector(string modelPath, string classNamesPath, bool useGpu = true)
    {
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"Warning: Object detection model not found at {modelPath}");
            Console.WriteLine("Object detection will be disabled. Download YOLO model to enable.");
            _classNames = Array.Empty<string>();
            return;
        }
        
        // Load class names
        if (File.Exists(classNamesPath))
        {
            _classNames = File.ReadAllLines(classNamesPath);
        }
        else
        {
            Console.WriteLine($"Warning: Class names file not found at {classNamesPath}");
            _classNames = new[] { "object" }; // Generic fallback
        }
        
        try
        {
            var options = new SessionOptions();
            if (useGpu)
            {
                options.AppendExecutionProvider_CUDA();
            }
            
            _session = new InferenceSession(modelPath, options);
            Console.WriteLine($"Object detector loaded: {modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load object detection model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Detect objects in image
    /// Returns list of detected objects with bounding boxes and confidence
    /// </summary>
    public List<DetectedObject> DetectObjects(byte[] rgbaData, int width, int height, float confidenceThreshold = 0.5f)
    {
        if (_session == null)
        {
            return new List<DetectedObject>();
        }
        
        try
        {
            // Preprocess image
            var inputTensor = PreprocessImage(rgbaData, width, height);
            
            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };
            
            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();
            
            // Postprocess results
            var detections = PostprocessResults(output, width, height, confidenceThreshold);
            
            return detections;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Object detection error: {ex.Message}");
            return new List<DetectedObject>();
        }
    }
    
    private DenseTensor<float> PreprocessImage(byte[] bgra, int width, int height)
    {
        // Create tensor with shape [1, 3, inputSize, inputSize]
        var tensor = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });
        
        float scale = Math.Min((float)_inputSize / width, (float)_inputSize / height);
        int newWidth = (int)(width * scale);
        int newHeight = (int)(height * scale);
        
        // Center the image
        int offsetX = (_inputSize - newWidth) / 2;
        int offsetY = (_inputSize - newHeight) / 2;
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int srcX = (int)(x / scale);
                int srcY = (int)(y / scale);
                int srcIdx = (srcY * width + srcX) * 4;
                
                int dstY = y + offsetY;
                int dstX = x + offsetX;
                
                if (srcIdx + 2 < bgra.Length && dstY < _inputSize && dstX < _inputSize)
                {
                    // BGRA to RGB, normalize to [0, 1]
                    tensor[0, 0, dstY, dstX] = bgra[srcIdx + 2] / 255.0f; // R
                    tensor[0, 1, dstY, dstX] = bgra[srcIdx + 1] / 255.0f; // G
                    tensor[0, 2, dstY, dstX] = bgra[srcIdx + 0] / 255.0f; // B
                }
            }
        }
        
        return tensor;
    }
    
    private List<DetectedObject> PostprocessResults(float[] output, int imgWidth, int imgHeight, float threshold)
    {
        var detections = new List<DetectedObject>();
        
        // YOLOv8 output format: [batch, num_predictions, 4 + num_classes]
        // For simplicity, we'll parse basic bounding boxes
        // Note: Actual YOLO postprocessing is more complex with NMS
        
        int numPredictions = output.Length / (4 + _classNames.Length);
        int stride = 4 + _classNames.Length;
        
        for (int i = 0; i < numPredictions && i * stride < output.Length; i++)
        {
            int offset = i * stride;
            
            // Get class with highest confidence
            float maxConf = 0;
            int maxClassIdx = 0;
            
            for (int c = 0; c < _classNames.Length && offset + 4 + c < output.Length; c++)
            {
                float conf = output[offset + 4 + c];
                if (conf > maxConf)
                {
                    maxConf = conf;
                    maxClassIdx = c;
                }
            }
            
            if (maxConf > threshold)
            {
                // Bounding box (center x, center y, width, height)
                float cx = output[offset + 0];
                float cy = output[offset + 1];
                float w = output[offset + 2];
                float h = output[offset + 3];
                
                // Convert to (x, y, width, height) in original image coordinates
                float x = (cx - w / 2) * imgWidth / _inputSize;
                float y = (cy - h / 2) * imgHeight / _inputSize;
                w = w * imgWidth / _inputSize;
                h = h * imgHeight / _inputSize;
                
                detections.Add(new DetectedObject
                {
                    ClassName = maxClassIdx < _classNames.Length ? _classNames[maxClassIdx] : "unknown",
                    Confidence = maxConf,
                    BoundingBox = new BoundingBox
                    {
                        X = x,
                        Y = y,
                        Width = w,
                        Height = h
                    }
                });
            }
        }
        
        return detections;
    }
    
    public void Dispose()
    {
        _session?.Dispose();
    }
}

