# Memux Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Memux Orchestrator                       │
│                    (Main Autonomous Loop)                       │
└────────────┬──────────────────────────────────┬─────────────────┘
             │                                  │
    ┌────────▼────────┐                ┌───────▼────────┐
    │   Perception    │                │   Curriculum   │
    │   (60 FPS)      │                │  (10s Loop)    │
    └────────┬────────┘                └───────┬────────┘
             │                                  │
    ┌────────▼────────┐                ┌───────▼────────┐
    │ Context Analyzer│                │  Goal Manager  │
    │  (Tags/Hints)   │                │  (Progress)    │
    └────────┬────────┘                └───────┬────────┘
             │                                  │
             └──────────┬──────────────────────┘
                        │
                ┌───────▼────────┐
                │ Skill Selector │
                │ (Small LLM)    │◄──────────┐
                └───────┬────────┘           │
                        │                    │
                ┌───────▼────────┐           │
                │ Skill Library  │           │
                │  (ELO Ranked)  │           │
                └───────┬────────┘           │
                        │                    │
                ┌───────▼────────┐    ┌──────┴────────┐
                │ Action Executor│    │  Composer     │
                │ (Input Sim)    │    │ (Large LLM)   │
                └───────┬────────┘    └──────┬────────┘
                        │                    │
                ┌───────▼────────┐           │
                │  Dark Souls    │           │
                │   Remastered   │           │
                └────────────────┘           │
                                             │
                ┌────────────────────────────▼────┐
                │     Pattern Detector            │
                │  (Action Sequence Analysis)     │
                └────────────────────────┬────────┘
                                         │
                                  ┌──────▼────────┐
                                  │ Notifications │
                                  │  (New Skills) │
                                  └───────────────┘
```

## Data Flow

### 1. Perception → Action Loop (60 FPS)

```
Screen Capture ──→ CV Models ──→ PerceptionState ──→ Context Analysis
                                         │
                                         ├──→ Tag Extraction
                                         ├──→ Object Detection
                                         └──→ Situation Classification
                                                      │
                                                      ▼
                                           Skill Selection (Local LLM)
                                                      │
                                                      ▼
                                              Skill Execution
                                                      │
                                                      ▼
                                          ActionQueue ──→ Input Simulation
                                                      │
                                                      ▼
                                              Game Window
```

### 2. Skill Learning Loop (Continuous)

```
Action Execution ──→ Action Recording ──→ Pattern Detection
                                                   │
                                                   ▼
                                      Meta-Skill Composition
                                                   │
                                                   ▼
                                        Composer (Large LLM)
                                                   │
                                                   ▼
                                          Skill Generation
                                                   │
                                                   ▼
                                         Skill Compilation
                                                   │
                                                   ▼
                                         Add to Library
                                                   │
                                                   ▼
                                           Notification
```

### 3. Curriculum Loop (10 seconds)

```
Current State ──→ Progress Check ──→ Goal Re-evaluation
                                             │
                                             ├──→ Mark Completed
                                             ├──→ Mark Obsolete
                                             └──→ Generate Sub-Goals
                                                       │
                                                       ▼
                                            Composer (Large LLM)
                                                       │
                                                       ▼
                                              New Goals Added
```

## Component Details

### Memux.Core
```
┌─────────────────────────────────────┐
│        MemuxDatabase (SQLite)       │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ skills                      │   │
│  │  - code (C# functions)      │   │
│  │  - dependencies (JSON)      │   │
│  │  - elo_rating (double)      │   │
│  │  - tags (JSON array)        │   │
│  │  - code_location (string)   │   │
│  │  - usage stats              │   │
│  └─────────────────────────────┘   │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ skill_usage_history         │   │
│  │ goals                       │   │
│  │ action_sequences            │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.Perception
```
┌─────────────────────────────────────┐
│      ScreenCapture (BitBlt)         │
│             ↓                       │
│  ┌─────────────────────────────┐   │
│  │  PerceptionState            │   │
│  │   - ScreenData (BGRA)       │   │
│  │   - DepthMap (float[])      │   │
│  │   - DetectedObjects (list)  │   │
│  │   - OcrResults (list)       │   │
│  │   - ContextHints (dict)     │   │
│  └─────────────────────────────┘   │
│             ↓                       │
│    CV Models (ONNX Runtime)         │
│     - MiDaS (depth)                 │
│     - YOLO (objects)                │
│     - Tesseract (OCR)               │
└─────────────────────────────────────┘
```

### Memux.Skills
```
┌─────────────────────────────────────┐
│       Skill (Executable Code)       │
│                                     │
│  Func<PerceptionState, ActionQueue> │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ Code (string)               │   │
│  │  - C# function              │   │
│  │  - Reads PerceptionState    │   │
│  │  - Returns ActionQueue      │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ SkillCompiler (Roslyn)      │   │
│  │  - Parse C# code            │   │
│  │  - Compile to delegate      │   │
│  │  - Cache compiled result    │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ SkillLibrary                │   │
│  │  - Store in database        │   │
│  │  - Track ELO rating         │   │
│  │  - Record usage stats       │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.Actions
```
┌─────────────────────────────────────┐
│        ActionQueue                  │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ Actions:                    │   │
│  │  - KeyPress                 │   │
│  │  - ButtonPress              │   │
│  │  - StickMovement            │   │
│  │  - Wait                     │   │
│  │  - PerceptionCheck          │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ ActionExecutor              │   │
│  │  - KeyboardInput (SendInput)│   │
│  │  - ControllerInput (XInput) │   │
│  │  - Timing & delays          │   │
│  │  - Action recording         │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│        Game Window                  │
└─────────────────────────────────────┘
```

### Memux.Composer
```
┌─────────────────────────────────────┐
│      ComposerAgent (Large LLM)      │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ ILlmClient                  │   │
│  │  - OpenAI HTTP client       │   │
│  │  - Abstract interface       │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ Operations:                 │   │
│  │  - GenerateSkill()          │   │
│  │  - ComposeMetaSkill()       │   │
│  │  - DebugSkill()             │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ PromptTemplates             │   │
│  │  - System prompts           │   │
│  │  - User prompt generation   │   │
│  │  - Response parsing         │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.Selection
```
┌─────────────────────────────────────┐
│     ContextAnalyzer                 │
│                                     │
│  PerceptionState ──→ ContextInfo    │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ Extracts:                   │   │
│  │  - IsInCombat               │   │
│  │  - IsInMenu                 │   │
│  │  - IsInDialog               │   │
│  │  - HasNearbyObstacle        │   │
│  │  - DetectedObjects          │   │
│  │  - Tags                     │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ LocalLLMSelector            │   │
│  │  - Phi-3 / Llama 3.2 (3B)   │   │
│  │  - Tags → Top N skills      │   │
│  │  - Target: <16ms            │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.Curriculum
```
┌─────────────────────────────────────┐
│     CurriculumAgent                 │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ Goal Generation             │   │
│  │  - GenerateInitialGoals()   │   │
│  │  - LLM-based                │   │
│  │  - Progressive difficulty   │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ 10-Second Timer             │   │
│  │  - Re-evaluate all goals    │   │
│  │  - Check progress           │   │
│  │  - Mark obsolete if stuck   │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ Events:                     │   │
│  │  - GoalCreated              │   │
│  │  - GoalCompleted            │   │
│  │  - GoalFailed               │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.CodeGen
```
┌─────────────────────────────────────┐
│     SkillTemplateEngine             │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ Template Definition         │   │
│  │  - CodeTemplate (string)    │   │
│  │  - Parameters (dict)        │   │
│  │  - TagTemplate (list)       │   │
│  │  - NameTemplate (string)    │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ GenerateVariations()        │   │
│  │  - Cartesian product of     │   │
│  │    parameter values         │   │
│  │  - String substitution      │   │
│  │  - Return (name, code, tags)│   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ Built-in Templates:         │   │
│  │  - DirectionalDodge         │   │
│  │  - DirectionalMovement      │   │
│  │  - KeyPress                 │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

### Memux.UI
```
┌─────────────────────────────────────┐
│     NotificationManager             │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ NotificationForm            │   │
│  │  - Steam-like popup         │   │
│  │  - Bottom-right corner      │   │
│  │  - Auto-dismiss (5s fade)   │   │
│  │  - Click to dismiss         │   │
│  └─────────────────────────────┘   │
│              ↓                      │
│  ┌─────────────────────────────┐   │
│  │ Shows:                      │   │
│  │  - New skill discovered     │   │
│  │  - Skill name               │   │
│  │  - Description/tags         │   │
│  └─────────────────────────────┘   │
└─────────────────────────────────────┘
```

## Design Patterns

### 1. Strategy Pattern
- `ILlmClient` - Swap LLM implementations (OpenAI, Anthropic, local)

### 2. Observer Pattern
- `CurriculumAgent` events (GoalCreated, GoalCompleted, GoalFailed)
- `ActionExecutor` events (ActionSequenceRecorded)

### 3. Template Method
- `SkillTemplateEngine` - Generate variations from base template

### 4. Facade Pattern
- `MemuxOrchestrator` - Simplifies interaction with all subsystems

### 5. Repository Pattern
- `SkillLibrary` - Abstracts database operations

## Key Decisions

### Why Two LLMs?

```
Large LLM (GPT-4)              Small LLM (Phi-3)
- Skill generation             - Skill selection
- Meta-skill composition       - Context → Tags → Skills
- Debugging                    - <16ms per frame
- ~2-5 seconds                 - Quantized, local
- Accurate, creative           - Fast, specialized
```

### Why ELO Ranking?

```
Traditional Scoring:
  Total Success / Total Usage = 75%

ELO Ranking:
  Accounts for:
  - Recency of success
  - Difficulty of situation
  - Competition with other skills
  - Dynamic adjustment
```

### Why SQLite?

```
Advantages:
- Single file database
- No server required
- ACID transactions
- Fast queries
- Portable
- Embedded in app

Alternatives Considered:
- JSON files (slow queries)
- PostgreSQL (overkill)
- In-memory only (not persistent)
```

## Performance Targets

| Component | Target | Notes |
|-----------|--------|-------|
| Perception | 60 FPS | 16.67ms per frame |
| CV Processing | <10ms | Depth + Objects + OCR |
| Skill Selection | <16ms | Local LLM inference |
| Skill Execution | Variable | Depends on action queue |
| Compilation | <100ms | Roslyn caching helps |
| Database Query | <1ms | Indexed by ELO, tags |

## Security Considerations

- Skills execute arbitrary C# code ⚠️
  - Roslyn compilation in sandboxed context
  - No file system access by default
  - No network access by default
  - User should review generated skills

- API Keys stored in environment
  - Not in source code
  - Not in database
  - User responsibility

- Input simulation requires admin
  - SendInput needs elevated privileges
  - Consider security implications

## Extensibility Points

1. **New Games** - Swap `Memux.DarkSouls` module
2. **New LLMs** - Implement `ILlmClient` interface
3. **New CV Models** - Add to `Memux.Perception`
4. **New Templates** - Register in `SkillTemplateEngine`
5. **New Actions** - Extend `ActionQueue` and `ActionExecutor`
6. **New Context Hints** - Extend `ContextAnalyzer`

---

*This architecture balances research flexibility with practical implementation*

