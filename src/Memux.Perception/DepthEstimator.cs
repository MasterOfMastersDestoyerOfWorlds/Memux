using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Depth estimation using MiDaS model via ONNX Runtime
/// Estimates depth from single RGB image
/// </summary>
public class DepthEstimator : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly bool _useGpu;
    private readonly int _inputWidth = 384;
    private readonly int _inputHeight = 384;
    
    public DepthEstimator(string modelPath, bool useGpu = true)
    {
        _useGpu = useGpu;
        
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"Warning: Depth model not found at {modelPath}");
            Console.WriteLine("Depth estimation will be disabled. Download MiDaS model to enable.");
            return;
        }
        
        try
        {
            var options = new SessionOptions();
            if (useGpu)
            {
                options.AppendExecutionProvider_CUDA();
            }
            
            _session = new InferenceSession(modelPath, options);
            Console.WriteLine($"Depth estimator loaded: {modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load depth model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Estimate depth from RGB image data
    /// Returns normalized depth map (0=near, 1=far)
    /// </summary>
    public float[]? EstimateDepth(byte[] rgbaData, int width, int height)
    {
        if (_session == null)
        {
            return null;
        }
        
        try
        {
            // Preprocess: BGRA to RGB, resize to model input size
            var inputTensor = PreprocessImage(rgbaData, width, height);
            
            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };
            
            using var results = _session.Run(inputs);
            var outputTensor = results.First().AsEnumerable<float>().ToArray();
            
            // Postprocess: resize back to original dimensions
            var depthMap = PostprocessDepth(outputTensor, width, height);
            
            return depthMap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Depth estimation error: {ex.Message}");
            return null;
        }
    }
    
    private DenseTensor<float> PreprocessImage(byte[] bgra, int width, int height)
    {
        // Create tensor with shape [1, 3, inputHeight, inputWidth]
        var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
        
        // Simple bilinear-like downsampling and BGR to RGB conversion
        float scaleX = (float)width / _inputWidth;
        float scaleY = (float)height / _inputHeight;
        
        for (int y = 0; y < _inputHeight; y++)
        {
            for (int x = 0; x < _inputWidth; x++)
            {
                int srcX = (int)(x * scaleX);
                int srcY = (int)(y * scaleY);
                int srcIdx = (srcY * width + srcX) * 4;
                
                if (srcIdx + 2 < bgra.Length)
                {
                    // BGRA to RGB, normalize to [0, 1]
                    tensor[0, 0, y, x] = bgra[srcIdx + 2] / 255.0f; // R
                    tensor[0, 1, y, x] = bgra[srcIdx + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = bgra[srcIdx + 0] / 255.0f; // B
                }
            }
        }
        
        return tensor;
    }
    
    private float[] PostprocessDepth(float[] depth, int targetWidth, int targetHeight)
    {
        // Resize depth map back to original size
        var result = new float[targetWidth * targetHeight];
        
        float scaleX = (float)_inputWidth / targetWidth;
        float scaleY = (float)_inputHeight / targetHeight;
        
        // Find min/max for normalization
        float minDepth = depth.Min();
        float maxDepth = depth.Max();
        float range = maxDepth - minDepth;
        
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int srcX = Math.Min((int)(x * scaleX), _inputWidth - 1);
                int srcY = Math.Min((int)(y * scaleY), _inputHeight - 1);
                int srcIdx = srcY * _inputWidth + srcX;
                
                // Normalize to [0, 1]
                float normalizedDepth = range > 0 ? (depth[srcIdx] - minDepth) / range : 0;
                result[y * targetWidth + x] = normalizedDepth;
            }
        }
        
        return result;
    }
    
    public void Dispose()
    {
        _session?.Dispose();
    }
}

