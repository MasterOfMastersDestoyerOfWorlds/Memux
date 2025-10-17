# Memux - General Skill Acquisition System

Memux is a skill acquisition system inspired by [Voyager](https://github.com/MineDojo/Voyager) and the Everything CLI philosophy. It learns skills through observation and execution, using computer vision for perception and a two-tier LLM architecture for skill selection and composition.

## Architecture

Memux combines:

- **Bottom-up skill discovery** from observed behavior patterns
- **Top-down goal-driven curriculum** for autonomous learning
- **Two-tier LLM system**: small local LLM for real-time skill selection, large LLM for skill composition
- **ELO ranking** to surface the most useful skills
- **Database persistence** following the Everything CLI schema
- **Windows notifications** for new skill discoveries

## Project Structure

```
src/
├── Memux.Core/         # Core data models and database
├── Memux.Perception/   # Screen capture and CV (depth, OCR, segmentation)
├── Memux.Actions/      # Input simulation (keyboard, controller)
├── Memux.Skills/       # Skill library, compiler, ELO system
├── Memux.Selection/    # Local LLM for skill selection
├── Memux.Composer/     # Large LLM for skill composition
├── Memux.Curriculum/   # Goal generation and progress tracking
├── Memux.CodeGen/      # Deterministic skill templates
├── Memux.DarkSouls/    # Dark Souls Remastered integration
└── Memux.UI/           # Notification system
```

## Technology Stack

- **Language**: C# (.NET 8)
- **CV Inference**: ONNX Runtime (MiDaS, YOLO, Tesseract)
- **Compilation**: Roslyn (Microsoft.CodeAnalysis)
- **LLM APIs**: OpenAI, Anthropic
- **Local LLM**: LLamaSharp (llama.cpp bindings)
- **Input Simulation**: Windows SendInput API, XInput
- **Persistence**: SQLite

## Getting Started

### Prerequisites

- .NET 8 SDK
- Windows 10/11 (for input simulation and screen capture)
- Dark Souls Remastered (for testing)
- OpenAI or Anthropic API key

### Building

```bash
dotnet build
```

### Running Tests

```bash
# Run all 45 unit tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

See `TESTS.md` for complete test documentation.

### Running

```bash
# Set your OpenAI API key
export OPENAI_API_KEY="sk-..."  # Linux/Mac
$env:OPENAI_API_KEY = "sk-..."  # Windows PowerShell

# Run the basic demo
dotnet run --project src/Memux.App -- --demo

# Run the comprehensive Phase 2 demo (LLM features)
dotnet run --project src/Memux.App -- --phase2-demo

# Run the comprehensive Phase 3 demo (CV pipeline)
dotnet run --project src/Memux.App -- --phase3-demo

# Run the comprehensive Phase 4 demo (Skill selection)
dotnet run --project src/Memux.App -- --phase4-demo

# Run Phase 4 with local LLM (optional)
dotnet run --project src/Memux.App -- --phase4-demo \
  --llm-model models/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf

# Run with CV models (see MODEL_SETUP.md for setup)
dotnet run --project src/Memux.App -- --phase3-demo \
  --depth-model models/midas_small.onnx \
  --object-model models/yolov8n.onnx \
  --object-classes models/coco_classes.txt \
  --tess-data models/tessdata

# Run full autonomous mode (requires Dark Souls)
dotnet run --project src/Memux.App -- --game "C:\Path\To\DarkSoulsRemastered.exe"
```

## Key Design Principles

1. **Locality of Concern**: Skills are self-contained with explicit dependencies
2. **Deterministic Generation**: Use templates for skill variations
3. **No Information Hiding**: Skills are readable C# code
4. **Three Times to Tool**: Hand-write first, template after repetition
5. **Generic Core**: No game-specific logic in core modules

## Current Status

**Phase 1: Core Infrastructure** ✅ COMPLETE
- Solution structure created
- PerceptionState and ActionQueue models
- Database schema (SQLite)
- Screen capture (BitBlt)
- Input simulation (keyboard, controller)
- Process manager for launching target applications
- Windows notification system
- Skill library with Roslyn compilation
- ELO ranking system

**Phase 2: LLM Integration** ✅ COMPLETE
- ILlmClient abstraction (OpenAI HTTP client)
- ComposerAgent for skill generation
- Voyager-style prompt templates
- CurriculumAgent with 10-second re-evaluation
- SkillTemplateEngine for deterministic generation
- Meta-skill composition
- Skill debugging feedback loop
- Comprehensive demo (see `PHASE2_COMPLETE.md`)

**Phase 3: CV Pipeline** ✅ COMPLETE
- ONNX Runtime integration
- MiDaS depth estimation (monocular depth)
- YOLO object detection (bounding boxes + classes)
- Tesseract OCR (text extraction)
- Parallel CV processing
- GPU acceleration (CUDA)
- Comprehensive demo (see `PHASE3_COMPLETE.md`)
- Model setup guide (see `MODEL_SETUP.md`)

**Phase 4: Skill Selection** ✅ COMPLETE
- ContextAnalyzer for extracting high-level context
- Local LLM integration (LLamaSharp with GGUF models)
- Rule-based fallback selector
- SkillCache for performance optimization
- <16ms selection target (achieved)
- Comprehensive demo (see `PHASE4_COMPLETE.md`)

See `STATUS.md` for detailed progress tracking.

## License

MIT License - See LICENSE file for details

## Acknowledgments

- [Voyager](https://github.com/MineDojo/Voyager) for the skill acquisition architecture
- The Everything CLI essay for the behavior-driven skill discovery approach
- [MineDojo](https://github.com/MineDojo/MineDojo) for embodied agent research

