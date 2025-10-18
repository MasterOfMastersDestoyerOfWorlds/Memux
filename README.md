# Memux — Voyager-Inspired Skill Acquisition for Dark Souls

Memux is an autonomous skill acquisition system inspired by Voyager (MineDojo). It learns skills through observation and execution, using a perception pipeline (screen capture + CV), a local selector for real-time decisions, and a large-model composer for new skill generation.

## What You Get (High-Level)

- Perception: Screen capture, optional depth, object, and text signals
- Selection: Fast local selector with caching and rule fallback
- Skills: Executable, ranked skills with runtime compilation
- Composer: LLM-based skill generation and debugging
- Curriculum: Goal generation and progress tracking

All components are designed to be game-agnostic; `Memux.DarkSouls` provides the thin integration layer.

## Current Status (Phases 1–4 Implemented)

- Core models, database, execution, and notifications
- CV pipeline (MiDaS/YOLO/Tesseract via ONNX Runtime, GPU optional)
- Local selection with caching; rule-based fallback for reliability
- LLM composition (HTTP client abstraction + templated prompts)

These provide a complete loop for perception → selection → execution, and an offline path for generating new skills.

## Quick Start

1) Build
```bash
dotnet build
```

2) Optional: Download minimal CV models (Windows PowerShell)
```powershell
New-Item -ItemType Directory -Force -Path "models\tessdata"
Invoke-WebRequest -Uri "https://github.com/isl-org/MiDaS/releases/download/v3_1/midas_v21_small_256.onnx" -OutFile "models\midas_small.onnx"
Invoke-WebRequest -Uri "https://github.com/ultralytics/assets/releases/download/v0.0.0/yolov8n.onnx" -OutFile "models\yolov8n.onnx"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/pjreddie/darknet/master/data/coco.names" -OutFile "models\coco_classes.txt"
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata" -OutFile "models\tessdata\eng.traineddata"
```

3) Run demos (pick what you need)
```bash
# LLM (composer) demo
dotnet run --project src/Memux.App -- --phase2-demo

# CV pipeline demo (works without models; richer with models)
dotnet run --project src/Memux.App -- --phase3-demo \
  --depth-model models/midas_small.onnx \
  --object-model models/yolov8n.onnx \
  --object-classes models/coco_classes.txt \
  --tess-data models/tessdata

# Skill selection demo (local selector; optional GGUF model)
dotnet run --project src/Memux.App -- --phase4-demo
```

4) Optional: Local LLM for selection (GGUF via LLamaSharp)
```bash
dotnet run --project src/Memux.App -- --phase4-demo \
  --llm-model models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf
```

## Architecture (Overview)

```
Perception ──→ Context ──→ Selection ──→ Execution
              └──────────── Composer ──→ New Skills
```

- Perception: Screen + optional depth/objects/text → unified state
- Selection: Local model + rules + cache → sub‑16ms path
- Execution: Action queues simulate inputs
- Composer: Large model generates/repairs skills offline
- Curriculum: Goals drive exploration and acquisition

## Roadmap to “Past the First Level”

See TODO.md for a Voyager‑style checklist that takes the agent from boot to exiting the Undead Asylum (or equivalent “first level” milestone) and onward.

## License & Credits

- License: MIT
- Inspiration: Voyager (MineDojo)
- Techniques: Everything CLI, ELO‑ranked skills, templated codegen

