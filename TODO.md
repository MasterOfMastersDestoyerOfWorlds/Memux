# TODO — Voyager-Style Plan for Program Learning

Goal: Autonomous agent learns and executes programs through observation, skill acquisition, and goal-driven curriculum. This plan uses Dark Souls Remastered as an example target program.

## 0) Environment & Tooling
- [x] Confirm target program path and launch reliably (generalized program registry)
- [x] Detect and focus game window (HWND) before input
- [x] Run as admin for SendInput permissions
- [ ] Set graphics to windowed 1080p; disable frame caps/overlays
- [x] have unit tests run as part of the build pipeline
- [x] Focused capture: capture only the focused program window; re-acquire HWND if it changes
- [x] Stamp `FocusedProgram` in `PerceptionState`; selector respects program scoping
- [x] UI: display current focused program; allow switching focus from Programs panel
- [ ] Persistence: store and restore last focused program on startup
- [x] UI focus guard: keep viewer always-on-top and reassert focus (prevent child processes from stealing activation)

## 1) Perception Baseline (Works Without Models)
- [ ] Verify BitBlt capture loop at ≥60 FPS (quick mode)
- [ ] Implement lightweight HUD heuristics (health bar pixel scan)
- [ ] Add color-threshold regions for "You Died"/menu prompts
- [ ] Wire minimal PerceptionState (screen only) into selection
- [x] Add Windows.Graphics.Capture window capture path (prefer for focused HWND)
- [x] D3D11 staging copy → BGRA byte[] for pipeline integration
- [x] Unit tests for Graphics Capture to prevent regressions
- [x] Add FPS display to perception viewer for depth/objects/OCR
- [x] OCR pane: split 50/50 with bounding box image (left) and spatial text visualization (right)
- [x] Remove OCR model status from model panel
- [x] Spatial text rendering scaled to bounding boxes
- [ ] Handle cloaked/minimized windows (DWMWA_CLOAKED): pause capture until visible

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
- [x] Rule-based fallback: tag/goal overlap + ELO weighting
- [x] Cache last-N contexts (TTL ~5s) for <1ms hot path
- [x] Candidate filtering (top-20 by relevance)
- [ ] Minimal local LLM prompt (optional); GGUF model switch
- [x] Program scoping: use `program:<id>` for app-specific skills; allow generic (`vision`, `input`, `generic`)

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
- [x] Demo E: Program focus & skill scoping (switch programs, verify filtering)

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


