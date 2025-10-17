# CV Model Setup Guide

This guide explains how to download and set up the computer vision models required for Phase 3 (CV Pipeline).

## Overview

Memux uses three types of CV models via ONNX Runtime:

1. **MiDaS** - Depth estimation (monocular depth from single RGB image)
2. **YOLOv8** - Object detection (bounding boxes + class labels)
3. **Tesseract** - OCR (text extraction)

## Quick Setup

```bash
# Create models directory
mkdir models
cd models

# Create tessdata directory
mkdir tessdata
```

## 1. MiDaS Depth Estimation

### Download Model

**Option A: MiDaS Small (Recommended for real-time)**
- Size: ~30 MB
- Speed: Fast (~20ms on GPU)
- Accuracy: Good

```bash
# Download MiDaS Small ONNX model
curl -L -o models/midas_small.onnx https://github.com/isl-org/MiDaS/releases/download/v3_1/midas_v21_small_256.onnx
```

**Option B: MiDaS Large (Best accuracy)**
- Size: ~300 MB
- Speed: Slower (~50ms on GPU)
- Accuracy: Excellent

```bash
# Download MiDaS Large ONNX model
curl -L -o models/midas_large.onnx https://github.com/isl-org/MiDaS/releases/download/v3_1/dpt_beit_large_512.onnx
```

### Convert PyTorch to ONNX (Alternative)

If you need to convert the PyTorch model yourself:

```python
import torch
import torch.onnx

# Load MiDaS model
model = torch.hub.load("intel-isl/MiDaS", "MiDaS_small")
model.eval()

# Export to ONNX
dummy_input = torch.randn(1, 3, 384, 384)
torch.onnx.export(
    model,
    dummy_input,
    "models/midas_small.onnx",
    input_names=['input'],
    output_names=['output'],
    opset_version=12
)
```

## 2. YOLOv8 Object Detection

### Download Model

**Option A: YOLOv8n (Nano - Recommended for real-time)**
- Size: ~6 MB
- Speed: Very Fast (~5ms on GPU)
- Accuracy: Good

```bash
# Download YOLOv8n ONNX model
curl -L -o models/yolov8n.onnx https://github.com/ultralytics/assets/releases/download/v0.0.0/yolov8n.onnx

# Download COCO class names
curl -L -o models/coco_classes.txt https://raw.githubusercontent.com/pjreddie/darknet/master/data/coco.names
```

**Option B: YOLOv8s (Small - Balanced)**
- Size: ~22 MB
- Speed: Fast (~10ms on GPU)
- Accuracy: Very Good

```bash
curl -L -o models/yolov8s.onnx https://github.com/ultralytics/assets/releases/download/v0.0.0/yolov8s.onnx
```

**Option C: YOLOv8m (Medium - Best accuracy)**
- Size: ~52 MB
- Speed: Moderate (~15ms on GPU)
- Accuracy: Excellent

```bash
curl -L -o models/yolov8m.onnx https://github.com/ultralytics/assets/releases/download/v0.0.0/yolov8m.onnx
```

### Export YOLOv8 from Ultralytics (Alternative)

```python
from ultralytics import YOLO

# Load YOLOv8 model
model = YOLO('yolov8n.pt')

# Export to ONNX
model.export(format='onnx', simplify=True)
```

### COCO Classes File

Create `models/coco_classes.txt` with 80 COCO object classes:

```
person
bicycle
car
motorcycle
airplane
bus
train
truck
boat
traffic light
fire hydrant
stop sign
parking meter
bench
bird
cat
dog
horse
sheep
cow
elephant
bear
zebra
giraffe
backpack
umbrella
handbag
tie
suitcase
frisbee
skis
snowboard
sports ball
kite
baseball bat
baseball glove
skateboard
surfboard
tennis racket
bottle
wine glass
cup
fork
knife
spoon
bowl
banana
apple
sandwich
orange
broccoli
carrot
hot dog
pizza
donut
cake
chair
couch
potted plant
bed
dining table
toilet
tv
laptop
mouse
remote
keyboard
cell phone
microwave
oven
toaster
sink
refrigerator
book
clock
vase
scissors
teddy bear
hair drier
toothbrush
```

## 3. Tesseract OCR

### Download Tessdata

**Fast Model (Recommended)**
```bash
# Download English language data (fast)
curl -L -o tessdata/eng.traineddata https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata
```

**Best Model (Higher accuracy, slower)**
```bash
# Download English language data (best)
curl -L -o tessdata/eng.traineddata https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata
```

### Additional Languages

If you need other languages:

```bash
# Japanese
curl -L -o tessdata/jpn.traineddata https://github.com/tesseract-ocr/tessdata_fast/raw/main/jpn.traineddata

# Chinese (Simplified)
curl -L -o tessdata/chi_sim.traineddata https://github.com/tesseract-ocr/tessdata_fast/raw/main/chi_sim.traineddata

# Spanish
curl -L -o tessdata/spa.traineddata https://github.com/tesseract-ocr/tessdata_fast/raw/main/spa.traineddata
```

## Directory Structure

After setup, your directory should look like this:

```
Memux/
├── models/
│   ├── midas_small.onnx          # Depth estimation model
│   ├── yolov8n.onnx              # Object detection model
│   ├── coco_classes.txt          # YOLO class names
│   └── tessdata/
│       └── eng.traineddata       # OCR language data
```

## GPU Support

### CUDA (NVIDIA)

For GPU acceleration, install CUDA Toolkit:

1. Download CUDA Toolkit 11.8 or 12.x: https://developer.nvidia.com/cuda-downloads
2. Install cuDNN 8.x: https://developer.nvidia.com/cudnn
3. Ensure ONNX Runtime GPU package is installed (already in project dependencies)

The application will automatically use GPU if available.

### CPU Fallback

If no GPU is available, models will run on CPU (slower but functional).

Performance comparison:
- **GPU (RTX 3060)**: ~30ms total (depth + detection + OCR)
- **CPU (i7-10700K)**: ~200ms total

## Testing the Setup

### Test Individual Models

```bash
# Run Phase 3 demo (without models - will show warnings)
dotnet run --project src/Memux.App -- --phase3-demo

# Run with models
dotnet run --project src/Memux.App -- --phase3-demo \
  --depth-model models/midas_small.onnx \
  --object-model models/yolov8n.onnx \
  --object-classes models/coco_classes.txt \
  --tess-data models/tessdata
```

### Verify GPU Usage

Check ONNX Runtime logs for GPU initialization:
```
Depth estimator loaded: models/midas_small.onnx (GPU: CUDA)
Object detector loaded: models/yolov8n.onnx (GPU: CUDA)
OCR engine loaded (CPU: Tesseract)
```

## Troubleshooting

### "Model not found" Warning

- Verify file paths are correct
- Ensure models are downloaded completely
- Check file permissions

### GPU Not Detected

- Install NVIDIA CUDA Toolkit and cuDNN
- Update GPU drivers
- Verify CUDA installation: `nvcc --version`
- Check GPU availability: `nvidia-smi`

### Slow Performance

- Use smaller models (MiDaS Small, YOLOv8n)
- Enable GPU acceleration
- Reduce image resolution
- Process every Nth frame instead of every frame

### High Memory Usage

- Use quantized models (INT8 instead of FP32)
- Reduce batch size
- Process images at lower resolution
- Limit concurrent CV operations

## Model Alternatives

### Depth Estimation
- **DPT** (Vision Transformer-based): Better accuracy, slower
- **Monodepth2**: Lightweight alternative
- **LeReS**: High-resolution depth

### Object Detection
- **YOLOv5**: Older but well-tested
- **YOLOv7**: Good balance
- **EfficientDet**: Mobile-friendly
- **Faster R-CNN**: Highest accuracy, slowest

### OCR
- **EasyOCR**: Better for non-English text
- **PaddleOCR**: Fast and accurate
- **TrOCR**: Transformer-based, best for handwriting

## Performance Optimization

### Model Quantization

Convert FP32 models to INT8 for faster inference:

```python
import onnx
from onnxruntime.quantization import quantize_dynamic

# Quantize YOLO model
quantize_dynamic(
    model_input='yolov8n.onnx',
    model_output='yolov8n_int8.onnx',
    weight_type='int8'
)
```

### TensorRT Optimization (NVIDIA only)

For maximum GPU performance:

```bash
# Convert ONNX to TensorRT engine
trtexec --onnx=yolov8n.onnx --saveEngine=yolov8n.trt --fp16
```

## Model License Information

- **MiDaS**: MIT License
- **YOLOv8**: AGPL-3.0 License (use Ultralytics commercial license for proprietary use)
- **Tesseract**: Apache 2.0 License

Always review and comply with model licenses for your use case.

## Quick Start Script (Windows PowerShell)

```powershell
# Create directories
New-Item -ItemType Directory -Force -Path "models\tessdata"

# Download models
Invoke-WebRequest -Uri "https://github.com/isl-org/MiDaS/releases/download/v3_1/midas_v21_small_256.onnx" -OutFile "models\midas_small.onnx"
Invoke-WebRequest -Uri "https://github.com/ultralytics/assets/releases/download/v0.0.0/yolov8n.onnx" -OutFile "models\yolov8n.onnx"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/pjreddie/darknet/master/data/coco.names" -OutFile "models\coco_classes.txt"
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata" -OutFile "models\tessdata\eng.traineddata"

Write-Host "Models downloaded successfully!"
```

## Next Steps

After setting up models:

1. Run Phase 3 demo to test CV pipeline
2. Tune confidence thresholds for your game
3. Profile performance and optimize if needed
4. Integrate CV output into skill selection

For more information, see `PHASE3_COMPLETE.md`.

---

**Last Updated:** October 17, 2025  
**Memux Version:** Phase 3 (CV Pipeline)

