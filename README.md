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

### Running

```bash
# TODO: Add main entry point
```

## Key Design Principles

1. **Locality of Concern**: Skills are self-contained with explicit dependencies
2. **Deterministic Generation**: Use templates for skill variations
3. **No Information Hiding**: Skills are readable C# code
4. **Three Times to Tool**: Hand-write first, template after repetition
5. **Generic Core**: No game-specific logic in core modules

## Current Status

Phase 1: Core Infrastructure ✅
- Solution structure created
- PerceptionState and ActionQueue models
- Database schema (SQLite)
- Screen capture (BitBlt)
- Input simulation (keyboard, controller)
- Process manager for launching target applications
- Windows notification system
- Skill library with Roslyn compilation
- ELO ranking system

Phase 2: CV Pipeline (Next)
- Integrate ONNX models
- Depth estimation
- Segmentation
- OCR

## License

MIT License - See LICENSE file for details

## Acknowledgments

- [Voyager](https://github.com/MineDojo/Voyager) for the skill acquisition architecture
- The Everything CLI essay for the behavior-driven skill discovery approach
- [MineDojo](https://github.com/MineDojo/MineDojo) for embodied agent research

