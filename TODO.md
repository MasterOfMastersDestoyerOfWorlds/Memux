# TODO — Voyager-Style Plan to Clear the First Level

Goal: Autonomous agent learns and executes a minimal curriculum to get past the first level (e.g., Undead Asylum) in Dark Souls Remastered.

## 0) Environment & Tooling
- [ ] Confirm Dark Souls Remastered path and launch reliably
- [ ] Detect and focus game window (HWND) before input
- [ ] Run as admin for SendInput permissions
- [ ] Set graphics to windowed 1080p; disable frame caps/overlays
- [ ] have unit tests un as part of the build pipeline

## 1) Perception Baseline (Works Without Models)
- [ ] Verify BitBlt capture loop at ≥60 FPS (quick mode)
- [ ] Implement lightweight HUD heuristics (health bar pixel scan)
- [ ] Add color-threshold regions for “You Died”/menu prompts
- [ ] Wire minimal PerceptionState (screen only) into selection

## 2) Optional CV Signals (Upside Only)
- [ ] Depth: MiDaS small (models/midas_small.onnx) — normalize [0,1]
- [ ] Objects: YOLOv8n (yolov8n.onnx + coco names) — enemy/door heuristics
- [ ] OCR: Tesseract “eng” — prompt text (e.g., “A: OK”, “Bonfire”)
- [ ] Gate each model behind feature flags; degrade gracefully

## 3) Seed Skill Library (Deterministic, Minimal)
- [ ] Movement: MoveForward, TurnLeft, TurnRight, StepBack
- [ ] Interaction: Interact (A), Dodge (B), LightAttack (RB)
- [ ] Camera control: CenterCamera, SmallLeft/Right nudges
- [ ] Compose “TraverseFog” (interact + short walk)
- [ ] Compose “OpenDoor” (interact + wait-for-animation)

## 4) Selection Loop (Sub‑16ms Path)
- [ ] Rule-based fallback: tag/goal overlap + ELO weighting
- [ ] Cache last-N contexts (TTL ~5s) for <1ms hot path
- [ ] Candidate filtering (top-20 by relevance)
- [ ] Minimal local LLM prompt (optional); GGUF model switch

## 5) Curriculum (Voyager-Style, Small Steps)
- [ ] Define milestone goals:
  - [ ] G1: Exit cell (OpenDoor)
  - [ ] G2: Reach bonfire (MoveForward + Interact)
  - [ ] G3: Acquire weapon (navigate + Interact)
  - [ ] G4: Traverse fog (TraverseFog)
  - [ ] G5: Defeat asylum demon (dodge + light attack loop)
  - [ ] G6: Trigger exit cutscene (interact)
- [ ] 10-second re-eval: mark stuck → generate sub-goals
- [ ] Telemetry: success/failure per goal; ELO updates

## 6) Waypointing & Simple Navigation
- [ ] Define screen-space beacons (bonfire/orange fog/door colors)
- [ ] Implement “head towards beacon” with small turns + forward
- [ ] Add fallbacks: timeouts → re-center camera, try alternate turn

## 7) Combat Micro-Loops (Asylum Demon MVP)
- [ ] Enemy presence heuristic (size/motion or YOLO label)
- [ ] “Approach until near” (depth or size threshold)
- [ ] Loop: RollDodge → LightAttack → Retreat (timed)
- [ ] Abort conditions: hp low, stuck timer, lost sight → reset camera

## 8) Robustness & Recovery
- [ ] Detect loading/menu states (OCR/regions); pause actions
- [ ] On death: wait → respawn ritual (Interact if needed) → resume Gx
- [ ] Stuck detector: no translation for T ms → camera sweep + retry path

## 9) Demos & Checkpoints
- [ ] Demo A: Navigation to bonfire (no CV models)
- [ ] Demo B: Traverse first fog gate
- [ ] Demo C: Asylum demon avoidance (survive 30s)
- [ ] Demo D: Asylum demon defeat (scripted micro-loop)

## 10) Data & Evaluation
- [ ] Log action sequences, outcomes, timings
- [ ] Produce per-goal success rates and time-to-complete
- [ ] Record frames around failures for analysis

## 11) Stretch (Quality Boosters)
- [ ] Non‑max suppression on detections
- [ ] Quantized models (INT8) / TensorRT for speed
- [ ] Skill embeddings for semantic retrieval
- [ ] Async CV/update loop with Nth‑frame processing

## Checklists by Milestone

### Milestone A — Exit Cell
- [ ] Locate door region
- [ ] Interact
- [ ] Advance 2–3 meters forward

### Milestone B — First Bonfire
- [ ] Headings: maintain forward vector (camera recenters)
- [ ] Detect bonfire text or beacon color
- [ ] Interact to rest (optional)

### Milestone C — First Fog Gate
- [ ] Detect fog visual signature (bright white/yellow region)
- [ ] Interact to traverse; short walk forward

### Milestone D — Asylum Demon
- [ ] Enter arena; center camera
- [ ] Maintain safe distance; roll on wind-up
- [ ] Attack windows: 1–2 light attacks, then evade
- [ ] Repeat until cutscene/exit triggers


