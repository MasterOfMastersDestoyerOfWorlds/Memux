using Memux.Core;
using Memux.Core.Database;
using Memux.Skills;
using Memux.Actions;
using Memux.Perception;
using Memux.Selection;
using Memux.Composer;
using Memux.Curriculum;
// using Memux.UI;
using Memux.DarkSouls;
using Memux.CodeGen;

namespace Memux;

/// <summary>
/// Main entry point for Memux
/// Demonstrates the full autonomous learning loop
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Memux: General Skill Acquisition System ===");
        Console.WriteLine();
        
        // Parse command line arguments
        string? gamePath = null;
        string? apiKey = null;
        string dbPath = "memux.db";
        bool demo = false;
        bool phase2Demo = false;
        bool phase3Demo = false;
        bool phase4Demo = false;
        string? depthModel = null;
        string? objectModel = null;
        string? objectClasses = null;
        string? tessData = null;
        string? llmModel = null;
        bool useGpu = true;
        string? steamPath = null;
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--game" && i + 1 < args.Length)
            {
                gamePath = args[i + 1];
            }
            else if (args[i] == "--api-key" && i + 1 < args.Length)
            {
                apiKey = args[i + 1];
            }
            else if (args[i] == "--db" && i + 1 < args.Length)
            {
                dbPath = args[i + 1];
            }
            else if (args[i] == "--demo")
            {
                demo = true;
            }
            else if (args[i] == "--phase2-demo")
            {
                phase2Demo = true;
            }
            else if (args[i] == "--phase3-demo")
            {
                phase3Demo = true;
            }
            else if (args[i] == "--phase4-demo")
            {
                phase4Demo = true;
            }
            else if (args[i] == "--depth-model" && i + 1 < args.Length)
            {
                depthModel = args[i + 1];
            }
            else if (args[i] == "--object-model" && i + 1 < args.Length)
            {
                objectModel = args[i + 1];
            }
            else if (args[i] == "--object-classes" && i + 1 < args.Length)
            {
                objectClasses = args[i + 1];
            }
            else if (args[i] == "--tess-data" && i + 1 < args.Length)
            {
                tessData = args[i + 1];
            }
            else if (args[i] == "--llm-model" && i + 1 < args.Length)
            {
                llmModel = args[i + 1];
            }
            else if (args[i] == "--no-gpu")
            {
                useGpu = false;
            }
            else if (args[i] == "--steam" && i + 1 < args.Length)
            {
                steamPath = args[i + 1];
            }
        }
        
        // Check for API key in environment if not provided
        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (phase4Demo)
        {
            await Memux.App.Phase4Demo.RunAsync(llmModel);
            return;
        }
        
        if (phase3Demo)
        {
            await Phase3Demo.RunAsync(depthModel, objectModel, objectClasses, tessData, gamePath, useGpu);
            return;
        }
        
        if (phase2Demo)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("ERROR: Phase 2 demo requires OpenAI API key.");
                Console.WriteLine("Set OPENAI_API_KEY environment variable or use --api-key argument.");
                return;
            }
            await Phase2Demo.RunAsync(apiKey, dbPath);
            return;
        }
        
        if (demo)
        {
            await RunDemoAsync(dbPath, apiKey);
            return;
        }
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("WARNING: No OpenAI API key provided.");
            Console.WriteLine("LLM features will be disabled.");
            Console.WriteLine("Set OPENAI_API_KEY environment variable or use --api-key argument.");
            Console.WriteLine();
        }
        
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine("Initializing Memux...");
        Console.WriteLine();
        
        // Initialize system
        await RunFullSystemAsync(dbPath, apiKey, gamePath, steamPath);
    }
    
    static async Task RunDemoAsync(string dbPath, string? apiKey)
    {
        Console.WriteLine("=== Running Memux Demo ===");
        Console.WriteLine();
        
        // 1. Initialize skill library
        Console.WriteLine("1. Initializing skill library...");
        var skillLibrary = new SkillLibrary(dbPath);
        await skillLibrary.CreateSeedSkillsAsync();
        
        // 2. Show seed skills
        Console.WriteLine("\n2. Seed skills created:");
        var skills = await skillLibrary.GetTopSkillsAsync(10);
        foreach (var skill in skills)
        {
            Console.WriteLine($"   - {skill.GetDescription()}");
        }
        
        // 3. Generate skills from templates
        Console.WriteLine("\n3. Generating skills from templates...");
        var templateEngine = new SkillTemplateEngine();
        var dodgeVariations = templateEngine.GenerateVariations("DirectionalDodge");
        
        foreach (var (name, code, tags) in dodgeVariations.Take(4))
        {
            await skillLibrary.AddSkillAsync(name, code, tags);
            Console.WriteLine($"   - Generated: {name}");
        }
        
        // 4. Test skill execution
        Console.WriteLine("\n4. Testing skill execution...");
        var testSkill = await skillLibrary.GetTopSkillsAsync(1);
        if (testSkill.Any() && testSkill[0].Execute != null)
        {
            var state = new Memux.Core.Models.PerceptionState
            {
                Timestamp = DateTime.UtcNow,
                Width = 1920,
                Height = 1080
            };
            
            var actions = testSkill[0].Execute!(state);
            Console.WriteLine($"   - Skill '{testSkill[0].Name}' generated actions: {actions.ToCompactString()}");
        }
        
        // 5. Test LLM skill generation (if API key provided)
        if (!string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("\n5. Testing LLM skill generation...");
            var composer = new ComposerAgent(apiKey);
            
            try
            {
                var result = await composer.GenerateSkillAsync(
                    "Create a skill that moves forward for 2 seconds",
                    await skillLibrary.GetTopSkillsAsync(5)
                );
                
                Console.WriteLine($"   - Generated skill: {result.SkillName}");
                Console.WriteLine($"   - Tags: {string.Join(", ", result.Tags)}");
                Console.WriteLine($"   - Code preview: {result.SkillCode.Substring(0, Math.Min(100, result.SkillCode.Length))}...");
                
                // Add to library
                await skillLibrary.AddSkillAsync(result.SkillName, result.SkillCode, result.Tags);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   - LLM generation failed: {ex.Message}");
            }
        }
        
        // 6. Show final stats
        Console.WriteLine("\n6. Final Statistics:");
        var allSkills = await skillLibrary.GetTopSkillsAsync(100);
        Console.WriteLine($"   - Total skills: {allSkills.Count}");
        Console.WriteLine($"   - Average ELO: {allSkills.Average(s => s.EloRating):F2}");
        
        Console.WriteLine("\n=== Demo Complete ===");
        Console.WriteLine("Run without --demo flag to start full autonomous mode.");
    }
    
    static async Task RunFullSystemAsync(string dbPath, string? apiKey, string? gamePath, string? steamPath)
    {
        // Initialize components
        var skillLibrary = new SkillLibrary(dbPath);
        await skillLibrary.CreateSeedSkillsAsync();
        
        // var notificationManager = new NotificationManager();
        var darkSouls = new DarkSoulsIntegration(gamePath);
        
        // Ensure Steam is running before attempting to attach/launch DSR
        EnsureSteamRunning(steamPath);
        
        Console.WriteLine("Searching for Dark Souls Remastered...");
        
        // Try to attach to running instance or launch (use default path if none provided)
        bool gameRunning = darkSouls.AttachToGame();
        if (!gameRunning)
        {
            Console.WriteLine("Launching Dark Souls Remastered...");
            gameRunning = darkSouls.LaunchGame();
        }
        
        if (!gameRunning)
        {
            Console.WriteLine("ERROR: Could not find or launch Dark Souls Remastered.");
            Console.WriteLine("Please start the game manually or provide path with --game argument.");
            return;
        }
        
        var windowHandle = darkSouls.GetGameWindowHandle();
        if (windowHandle == IntPtr.Zero)
        {
            Console.WriteLine("ERROR: Could not find game window.");
            return;
        }
        
        Console.WriteLine("Game window found!");
        Console.WriteLine();
        
        // Initialize perception (CV models optional)
        var perception = new PerceptionPipeline(windowHandle);
        var contextAnalyzer = new ContextAnalyzer();
        var executor = new ActionExecutor();
        
        // Initialize curriculum if API key available
        CurriculumAgent? curriculum = null;
        if (!string.IsNullOrEmpty(apiKey))
        {
            curriculum = new CurriculumAgent(apiKey);
            curriculum.GoalCreated += (s, e) => 
                Console.WriteLine($"[Curriculum] New goal: {e.Goal.Description}");
            curriculum.GoalCompleted += (s, e) => 
                Console.WriteLine($"[Curriculum] Completed: {e.Goal.Description}");
            
            var goals = await curriculum.GenerateInitialGoalsAsync("Dark Souls Remastered - Starting at Firelink Shrine");
            Console.WriteLine($"Generated {goals.Count} initial goals");
        }
        
        Console.WriteLine("Starting main loop... (Press Ctrl+C to stop)");
        Console.WriteLine();
        
        // Main loop
        var cancellationToken = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cancellationToken.Cancel();
        };
        
        int frameCount = 0;
        var lastSecond = DateTime.UtcNow;
        
        try
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    // Capture perception
                    var state = perception.CaptureQuick(); // Use quick capture for now (no CV)
                    
                    // If capture failed (e.g., window not ready), skip this frame
                    if (state.Width <= 0 || state.Height <= 0 || state.ScreenData == null || state.ScreenData.Length == 0)
                    {
                        await Task.Delay(16);
                        continue;
                    }
                    
                    // Analyze context
                    var context = contextAnalyzer.AnalyzeContext(state);
                    
                    // Simple skill selection (TODO: integrate local LLM)
                    var topSkills = await skillLibrary.GetTopSkillsAsync(1);
                    
                    if (topSkills.Any() && topSkills[0].Execute != null)
                    {
                        // Execute skill
                        var actions = topSkills[0].Execute!(state);
                        var result = await executor.ExecuteAsync(actions, state);
                        
                        // Record result
                        skillLibrary.RecordUsage(topSkills[0].Id, result.Success, result.ExecutionTimeMs);
                        
                        if (frameCount % 300 == 0) // Log every 5 seconds
                        {
                            Console.WriteLine($"[Loop] Executed: {topSkills[0].Name}, Success: {result.Success}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Loop] Error: {ex.Message}");
                }
                
                // Frame timing
                frameCount++;
                if ((DateTime.UtcNow - lastSecond).TotalSeconds >= 1.0)
                {
                    lastSecond = DateTime.UtcNow;
                    frameCount = 0;
                }
                
                // Target ~60 FPS
                await Task.Delay(16);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }
        finally
        {
            perception.Dispose();
            curriculum?.Dispose();
        }
        
        Console.WriteLine("Memux stopped.");
    }

    private static void EnsureSteamRunning(string? steamPath)
    {
        try
        {
            const string steamProcessName = "Steam";
            // If Steam process exists, nothing to do
            var processes = System.Diagnostics.Process.GetProcessesByName(steamProcessName);
            if (processes.Length > 0)
            {
                Console.WriteLine("Steam is already running.");
                return;
            }
            
            // Determine path
            steamPath ??= @"C:\\Program Files (x86)\\Steam\\Steam.exe";
            if (!File.Exists(steamPath))
            {
                Console.WriteLine($"Warning: Steam executable not found at {steamPath}. Proceeding without ensuring Steam.");
                return;
            }
            
            Console.WriteLine("Starting Steam...");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = steamPath,
                WorkingDirectory = Path.GetDirectoryName(steamPath),
                UseShellExecute = true
            };
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("Warning: Failed to start Steam.");
                return;
            }
            
            // Wait a few seconds for Steam to initialize
            for (int i = 0; i < 50; i++)
            {
                if (process.HasExited)
                {
                    break;
                }
                var any = System.Diagnostics.Process.GetProcessesByName(steamProcessName).Length > 0;
                if (any)
                {
                    Console.WriteLine("Steam started.");
                    return;
                }
                Thread.Sleep(200);
            }
            Console.WriteLine("Warning: Steam did not become ready in time; continuing.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: EnsureSteamRunning error: {ex.Message}");
        }
    }
}
