# Getting Started with Memux

## Quick Start

Memux is now successfully compiled and ready for testing! Here's how to get started.

### 1. Prerequisites

- .NET 8 SDK
- Windows 10/11
- Dark Souls Remastered (or any target application)
- OpenAI API key (optional, for LLM features)

### 2. Set Up Your Environment

```bash
# Set your OpenAI API key
$env:OPENAI_API_KEY = "your-api-key-here"

# Or pass it as command line argument
cd C:\Code\Memux
dotnet run --project src\Memux.Core -- --api-key your-api-key-here
```

### 3. Basic Usage Example

```csharp
using Memux.Core;
using Memux.Core.Database;
using Memux.Skills;
using Memux.UI;
using Memux.DarkSouls;

// Initialize database
var db = new MemuxDatabase("memux.db");

// Initialize skill library
var skillLibrary = new SkillLibrary("memux.db");

// Create seed skills (basic primitives)
await skillLibrary.CreateSeedSkillsAsync();

// Initialize notification system
var notificationManager = new NotificationManager();

// Launch Dark Souls
var darkSouls = new DarkSoulsIntegration();
bool launched = darkSouls.LaunchGame(); // or AttachToGame()

if (launched)
{
    // Get window handle
    var windowHandle = darkSouls.GetGameWindowHandle();
    
    // TODO: Start perception loop
    // TODO: Start skill selection loop
    // TODO: Start curriculum loop
}
```

### 4. Adding Your First Custom Skill

```csharp
// Add a dodge skill
await skillLibrary.AddSkillAsync(
    name: "QuickDodge",
    code: @"
        // Check if enemy is close
        if (state.DetectedObjects != null && 
            state.DetectedObjects.Any(o => o.ClassName.Contains(""enemy"")))
        {
            // Dodge away from enemy
            queue.AddButtonPress(""B"", 50);
            queue.AddStickMovement(""LEFT"", 0, -1.0f, 200);
        }
    ",
    tags: new List<string> { "combat", "dodge", "defensive", "enemy-aware" }
);

// The skill is now available in the library!
```

### 5. Using Code Generation Templates

```csharp
using Memux.CodeGen;

var templateEngine = new SkillTemplateEngine();

// Generate all dodge direction variations
var dodgeVariations = templateEngine.GenerateVariations("DirectionalDodge");

foreach (var (name, code, tags) in dodgeVariations)
{
    await skillLibrary.AddSkillAsync(name, code, tags);
    Console.WriteLine($"Generated skill: {name}");
}
```

### 6. Testing Skills

```csharp
using Memux.Actions;
using Memux.Perception;

// Create action executor
var executor = new ActionExecutor();

// Get a skill
var dodgeSkill = await skillLibrary.GetSkillAsync("skill-id");

if (dodgeSkill?.Execute != null)
{
    // Create mock perception state
    var state = new PerceptionState
    {
        Timestamp = DateTime.UtcNow,
        Width = 1920,
        Height = 1080
    };
    
    // Execute skill
    var actions = dodgeSkill.Execute(state);
    var result = await executor.ExecuteAsync(actions, state);
    
    if (result.Success)
    {
        Console.WriteLine($"Skill executed in {result.ExecutionTimeMs}ms");
        skillLibrary.RecordUsage(dodgeSkill.Id, true, result.ExecutionTimeMs);
    }
}
```

### 7. Using LLM for Skill Generation

```csharp
using Memux.Composer;

var composer = new ComposerAgent("your-api-key", "gpt-4");

// Generate a new skill from natural language
var result = await composer.GenerateSkillAsync(
    goalDescription: "Create a skill that attacks when an enemy is in range",
    availableSkills: await skillLibrary.GetTopSkillsAsync(10)
);

// Add the generated skill
var newSkill = await skillLibrary.AddSkillAsync(
    result.SkillName,
    result.SkillCode,
    result.Tags
);

// Show notification
notificationManager.ShowSkillNotification(
    result.SkillName,
    $"New skill created: {string.Join(", ", result.Tags)}"
);
```

### 8. Setting Up the Curriculum

```csharp
using Memux.Curriculum;

var curriculum = new CurriculumAgent("your-api-key");

// Subscribe to goal events
curriculum.GoalCreated += (s, e) => 
    Console.WriteLine($"New goal: {e.Goal.Description}");
curriculum.GoalCompleted += (s, e) => 
    Console.WriteLine($"Completed: {e.Goal.Description}");

// Generate initial goals
var goals = await curriculum.GenerateInitialGoalsAsync(
    "Playing Dark Souls Remastered from the start"
);

// Goals will be re-evaluated every 10 seconds automatically
```

### 9. Run the Phase 2 Demo

To see all Phase 2 features in action:

```bash
# Set your API key
$env:OPENAI_API_KEY = "sk-..."

# Run comprehensive Phase 2 demo
dotnet run --project src\Memux.App -- --phase2-demo

# Or with explicit API key
dotnet run --project src\Memux.App -- --phase2-demo --api-key sk-...
```

The demo will showcase:
- LLM skill generation
- Template-based skill variations
- Meta-skill composition
- Curriculum goal generation
- 10-second re-evaluation timer
- Skill debugging feedback loop

### 10. Run the Phase 3 Demo

To test the computer vision pipeline:

```bash
# Test screen capture only (no models needed)
dotnet run --project src\Memux.App -- --phase3-demo

# Test with full CV pipeline (after downloading models)
dotnet run --project src\Memux.App -- --phase3-demo \
  --depth-model models/midas_small.onnx \
  --object-model models/yolov8n.onnx \
  --object-classes models/coco_classes.txt \
  --tess-data models/tessdata
```

The demo will showcase:
- Screen capture performance
- Depth estimation (MiDaS)
- Object detection (YOLO)
- OCR text extraction (Tesseract)
- Full pipeline integration
- Real-time capture mode

**See `MODEL_SETUP.md` for instructions on downloading CV models.**

## Current Status

### ✅ Implemented (Phase 1, 2 & 3)

- Core data structures (PerceptionState, ActionQueue, Goal)
- SQLite database with skill storage
- Screen capture (BitBlt)
- **Depth estimation (MiDaS via ONNX)**
- **Object detection (YOLO via ONNX)**
- **OCR text extraction (Tesseract)**
- **Full CV pipeline with parallel processing**
- Input simulation (keyboard, controller)
- Process manager for launching applications
- Skill library with Roslyn compilation
- ELO ranking system
- Windows notification system
- LLM integration (OpenAI via HTTP)
- ComposerAgent for skill generation and debugging
- Curriculum agent with 10-second re-evaluation
- Code generation template engine
- Dark Souls integration stub
- Comprehensive demos for Phase 2 and Phase 3

### 🚧 TODO (Phase 4+)

- Local LLM integration (LLamaSharp) for skill selection
- Pattern detection for automatic skill composition
- Skill selection caching
- Full autonomous loop orchestration
- Testing and debugging tools

## Next Steps

1. Download ONNX models for CV pipeline (MiDaS, YOLO, Tesseract)
2. Implement perception loop
3. Test skill execution with real game input
4. Fine-tune ELO ranking parameters
5. Test curriculum goal generation
6. Implement pattern detection for meta-skills

## Troubleshooting

### Build Warnings

- **SixLabors.ImageSharp vulnerabilities**: Known issue, not critical for development. Will be fixed in future updates.

### Runtime Issues

- **No window found**: Make sure the target game is running before launching Memux
- **Input not working**: Memux needs to be run as administrator for input simulation
- **API errors**: Check that your OpenAI API key is valid and has sufficient credits

## Contributing

This is a research project exploring skill acquisition systems. Contributions welcome!

## License

MIT License - See LICENSE file for details

