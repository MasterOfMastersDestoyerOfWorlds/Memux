using Memux.Core;
using System.Diagnostics;
using Memux.Core.Database;
using Memux.Skills;
using Memux.Actions;
using Memux.Perception;
using Memux.Selection;
using Memux.Curriculum;
using Memux.UI;
using Memux.DarkSouls;
// using Memux.CodeGen;

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
        // Ensure no stale instances are running that could lock assemblies
        KillOtherInstances();
        
        // Parse command line arguments
        string? gamePath = null;
        string? apiKey = null;
        string dbPath = "memux.db";
        // Demo flags removed
        string? depthModel = null;
        string? objectModel = null;
        string? objectClasses = null;
        string? tessData = null;
        string? llmModel = null;
        bool useGpu = true;
        string? steamPath = null;
        bool preflight = false;
        string? programKey = null; // id or name from DB programs registry
        bool noElevate = false;
        bool preflightLaunch = false;
        
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
            // Demo flags removed
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
            else if (args[i] == "--preflight")
            {
                preflight = true;
            }
            else if (args[i] == "--program" && i + 1 < args.Length)
            {
                programKey = args[i + 1];
            }
            else if (args[i] == "--no-elevate")
            {
                noElevate = true;
            }
            else if (args[i] == "--preflight-launch")
            {
                preflightLaunch = true;
            }
        }
        
        // Resolve model paths from environment variables if not provided
        depthModel ??= Environment.GetEnvironmentVariable("MEMUX_DEPTH_MODEL");
        objectModel ??= Environment.GetEnvironmentVariable("MEMUX_OBJECT_MODEL");
        objectClasses ??= Environment.GetEnvironmentVariable("MEMUX_OBJECT_CLASSES");
        tessData ??= Environment.GetEnvironmentVariable("MEMUX_TESSDATA");

        // Attempt default model discovery under ./models if still unset
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string modelsDir = Path.Combine(baseDir, "models");
            if (Directory.Exists(modelsDir))
            {
                if (string.IsNullOrEmpty(depthModel))
                {
                    string[] dm = new[] { "midas.onnx", "MiDaS.onnx", "DPT.onnx" };
                    foreach (var name in dm)
                    {
                        var p = Path.Combine(modelsDir, name);
                        if (File.Exists(p)) { depthModel = p; break; }
                    }
                }
                if (string.IsNullOrEmpty(objectModel))
                {
                    string[] ym = new[] { "yolov8.onnx", "yolov5.onnx", "yolo.onnx" };
                    foreach (var name in ym)
                    {
                        var p = Path.Combine(modelsDir, name);
                        if (File.Exists(p)) { objectModel = p; break; }
                    }
                }
                if (string.IsNullOrEmpty(objectClasses))
                {
                    string[] cn = new[] { "coco.names", "classes.txt", "labels.txt" };
                    foreach (var name in cn)
                    {
                        var p = Path.Combine(modelsDir, name);
                        if (File.Exists(p)) { objectClasses = p; break; }
                    }
                }
                if (string.IsNullOrEmpty(tessData))
                {
                    var td = Path.Combine(modelsDir, "tessdata");
                    if (Directory.Exists(td)) tessData = td;
                }
            }
        }
        catch { }

        // Check for API key in environment if not provided
        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        // Demo paths removed
        
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
        
        // Elevation check with optional auto-relaunch
        if (!IsProcessElevated() && !noElevate)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = Environment.ProcessPath ?? "dotnet",
                    Arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1).Concat(new[] { "--no-elevate" }))
                };
                Console.WriteLine("Requesting elevation (Administrator) for reliable input simulation...");
                Process.Start(psi);
                return;
            }
            catch
            {
                Console.WriteLine("WARNING: Auto-elevation failed. Continue without admin or re-run in an elevated terminal.");
            }
        }

        // Initialize system or preflight
        if (preflight)
        {
            int exit = await RunPreflightAsync(dbPath, gamePath, programKey, steamPath, preflightLaunch);
            Environment.Exit(exit);
            return;
        }
        
        await RunFullSystemAsync(dbPath, apiKey, gamePath, steamPath, depthModel, objectModel, objectClasses, tessData, useGpu, llmModel, programKey);
    }
    
    // Demo method removed
    
    static async Task<int> RunPreflightAsync(string dbPath, string? gamePath, string? programKey, string? steamPath, bool preflightLaunch)
    {
        Console.WriteLine("Running preflight checks...\n");
        int failures = 0;
        try
        {
            // Resolve via DB registry
            var db = new Memux.Core.Database.MemuxDatabase(dbPath);
            var prog = !string.IsNullOrEmpty(programKey) ? db.GetProgramByNameOrId(programKey) : db.GetDefaultProgram();
            if (prog != null) gamePath = prog.ExePath;

            if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
            {
                Console.WriteLine("[FAIL] Game executable path not found.");
                Console.WriteLine("       Provide --program <name|id> registered in DB or --game <path>.");
                failures++;
            }
            else
            {
                Console.WriteLine($"[OK] Game path: {gamePath}");
            }

            // Steam presence (optional, warn only)
            steamPath ??= @"C:\\Program Files (x86)\\Steam\\Steam.exe";
            if (!File.Exists(steamPath))
            {
                Console.WriteLine("[WARN] Steam not found at default path. Launch may still work.");
            }
            else
            {
                Console.WriteLine("[OK] Steam path found.");
            }

            // Elevation check
            bool isAdmin = IsProcessElevated();
            Console.WriteLine(isAdmin ? "[OK] Running as administrator." : "[WARN] Not running as administrator (SendInput may be unreliable)." );

            // Window checks: attach or optional launch
            IntPtr handle = IntPtr.Zero;
            string processName = prog?.ProcessName ?? "DarkSoulsRemastered";
            var existing = Process.GetProcessesByName(processName);
            if (existing.Length > 0)
            {
                handle = existing[0].MainWindowHandle;
            }
            else if (preflightLaunch && !string.IsNullOrEmpty(gamePath) && File.Exists(gamePath))
            {
                Console.WriteLine("[INFO] Launching game for window checks...");
                try
                {
                    var pm = new Memux.Core.ProcessManager(gamePath);
                    if (pm.Launch())
                    {
                        handle = pm.GetMainWindowHandle();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Launch attempt failed: {ex.Message}");
                }
            }

            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("[WARN] Could not acquire window handle (game not running?). Skipping size/focus checks.");
            }
            else
            {
                WindowFocusHelper.TryFocusWindow(handle);
                if (GetWindowRect(handle, out var rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    Console.WriteLine($"[OK] Window size: {width}x{height}");
                    if (width != 1920 || height != 1080)
                    {
                        Console.WriteLine("[WARN] Window is not 1920x1080. Attempting non-invasive resize...");
                        MoveWindow(handle, rect.Left, rect.Top, 1920, 1080, true);
                    }
                }
                else
                {
                    Console.WriteLine("[WARN] Failed to query window rect.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] Preflight exception: {ex.Message}");
            failures++;
        }
        Console.WriteLine($"\nPreflight completed with {(failures == 0 ? "no failures" : failures + " failure(s)")}.\n");
        return failures == 0 ? 0 : 1;
    }

    private static bool IsProcessElevated()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    static async Task RunFullSystemAsync(
        string dbPath,
        string? apiKey,
        string? gamePath,
        string? steamPath,
        string? depthModel,
        string? objectModel,
        string? objectClasses,
        string? tessData,
        bool useGpu,
        string? llmModel,
        string? programKey)
    {
        // Initialize components
        var skillLibrary = new SkillLibrary(dbPath);
        await skillLibrary.CreateSeedSkillsAsync();
        
        // var notificationManager = new NotificationManager();
        
        // Set defaults if not provided
        steamPath ??= @"C:\\Program Files (x86)\\Steam\\Steam.exe";
        // Resolve program via DB registry if provided / available
        var db = new Memux.Core.Database.MemuxDatabase(dbPath);
        var prog = !string.IsNullOrEmpty(programKey) ? db.GetProgramByNameOrId(programKey) : db.GetDefaultProgram();
        if (prog != null)
        {
            gamePath = prog.ExePath;
        }
        
        // Default to Steam applaunch if no path provided or registry not set
        if (string.IsNullOrEmpty(gamePath))
        {
            gamePath = @"C:\\Program Files (x86)\\Steam\\steam.exe";
            if (prog != null && string.IsNullOrEmpty(prog.LaunchArgs))
            {
                // ensure default applaunch is present when using steam.exe
                prog.LaunchArgs = "-applaunch 570940";
            }
        }
        
        // Ensure Steam is running before attempting to attach/launch DSR
        EnsureSteamRunning(steamPath);
        
        // If program registry has launch args (e.g., steam applaunch), pass through
        var darkSouls = new DarkSoulsIntegration(gamePath, prog?.LaunchArgs);
        
        Console.WriteLine("Searching for Dark Souls Remastered...");
        
        // Try to attach to running instance or launch (prefer Steam app launch)
        bool gameRunning = darkSouls.AttachToGame();
        if (!gameRunning)
        {
            Console.WriteLine("Game not found; will fallback to desktop capture.");
        }
        
        var windowHandle = darkSouls.GetGameWindowHandle();
        if (windowHandle == IntPtr.Zero)
        {
            // Fallback: capture desktop so the app can still run
            windowHandle = GetDesktopWindow();
            if (windowHandle == IntPtr.Zero)
            {
                Console.WriteLine("ERROR: Could not acquire any window handle (game or desktop).");
                return;
            }
            Console.WriteLine("Using desktop window for capture.");
        }
        
        Console.WriteLine("Game window found!");
        WindowFocusHelper.TryFocusWindow(windowHandle);
        Console.WriteLine();
        
        // Initialize perception (CV models optional)
        var cancellationToken = new CancellationTokenSource();
        PerceptionViewer.Closed += (_, __) =>
        {
            try { cancellationToken.Cancel(); } catch { }
        };
        PerceptionViewer.Show();
        var perception = new PerceptionPipeline(
            windowHandle,
            depthModel,
            objectModel,
            objectClasses,
            tessData,
            useGpu);
        var contextAnalyzer = new ContextAnalyzer();
        var executor = new ActionExecutor();
        
        // Initialize skill selector (uses LLM if model path provided, otherwise rules)
        using var selector = new SkillSelector(llmModel ?? string.Empty, skillLibrary, useCache: true);
        
        // Viewer is already shown above
        
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
        
        Console.WriteLine("Starting main loop... (will stop on process exit)");
        Console.WriteLine();
        
        // Main loop
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cancellationToken.Cancel(); };
        AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { cancellationToken.Cancel(); } catch { } };
        
        int frameCount = 0;
        var lastSecond = DateTime.UtcNow;
        
        try
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    // Capture perception (includes CV if models configured)
                    var state = perception.CaptureAndProcess();
                    
                    // If capture failed (e.g., window not ready), skip this frame
                    if (state.Width <= 0 || state.Height <= 0 || state.ScreenData == null || state.ScreenData.Length == 0)
                    {
                        // Try re-acquiring the game window handle periodically in case the HWND changed (e.g., fullscreen switch)
                        if (frameCount % 60 == 0)
                        {
                            var newHandle = darkSouls.GetGameWindowHandle();
                            if (newHandle != IntPtr.Zero)
                            {
                                perception.UpdateWindowHandle(newHandle);
                            }
                        }
                        await Task.Delay(16);
                        continue;
                    }
                    
                    // Analyze context
                    var context = contextAnalyzer.AnalyzeContext(state);

                    // Select next planned skill (uses local LLM if available, else rules)
                    var planned = await selector.SelectSkillAsync(state, curriculum?.GetCurrentGoal());
                    string? nextSkillName = planned?.Name;
                    var subskillLines = BuildDependencyTreeLines(planned, skillLibrary, 0, new HashSet<string>());

                    // Push updates to viewer (use current goal if available)
                    var currentGoal = curriculum?.GetCurrentGoal();
                    PerceptionViewer.Update(state, currentGoal, nextSkillName, subskillLines);

                    // Execute the selected skill if available
                    var chosen = planned ?? (await skillLibrary.GetTopSkillsAsync(1)).FirstOrDefault();
                    if (chosen != null && chosen.Execute != null)
                    {
                        // Inject runtime subskill invoker for call graph (if we later decide to use it)
                        chosen.InvokeSubskill = (subskillName, s) =>
                        {
                            var target = skillLibrary.GetAllSkills().FirstOrDefault(x => x.Name.Equals(subskillName, StringComparison.OrdinalIgnoreCase));
                            return target?.Execute?.Invoke(s);
                        };

                        var actions = chosen.Execute!(state);
                        var result = await executor.ExecuteAsync(actions, state);
                        skillLibrary.RecordUsage(chosen.Id, result.Success, result.ExecutionTimeMs);
                        if (frameCount % 300 == 0)
                        {
                            Console.WriteLine($"[Loop] Executed: {chosen.Name}, Success: {result.Success}");
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
                await Task.Delay(16, cancellationToken.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }
        finally
        {
            perception.Dispose();
            PerceptionViewer.Close();
            curriculum?.Dispose();
            // Kill other stale instances to avoid file locks during next build
            KillOtherInstances();
            try { cancellationToken.Dispose(); } catch { }
        }
        
        Console.WriteLine("Memux stopped.");
    }

    private static IEnumerable<string> BuildDependencyTreeLines(Skill? root, SkillLibrary skillLibrary, int depth, HashSet<string> visited)
    {
        var lines = new List<string>();
        if (root == null) return lines;
        string indent = new string(' ', depth);
        lines.Add(indent + root.Name);
        if (!visited.Add(root.Id))
        {
            lines.Add(indent + "  (cycle)");
            return lines;
        }
        foreach (var depId in root.Dependencies)
        {
            var dep = skillLibrary.GetSkillById(depId);
            if (dep != null)
            {
                lines.AddRange(BuildDependencyTreeLines(dep, skillLibrary, depth + 2, visited));
            }
        }
        return lines;
    }

    private static void KillOtherInstances()
    {
        try
        {
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("Memux.App"))
            {
                if (p.Id != currentPid && !p.HasExited)
                {
                    p.Kill(true);
                }
            }
        }
        catch { }
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

    private static bool LaunchDarkSoulsViaSteam(string? steamPath)
    {
        try
        {
            // Prefer launching through Steam so Steamworks initializes properly
            // Approach 1: steam.exe -applaunch 570940
            steamPath ??= @"C:\\Program Files (x86)\\Steam\\Steam.exe";
            if (File.Exists(steamPath))
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = steamPath,
                    Arguments = "-applaunch 570940",
                    WorkingDirectory = Path.GetDirectoryName(steamPath),
                    UseShellExecute = true
                };
                var proc = System.Diagnostics.Process.Start(startInfo);
                return proc != null;
            }
            
            // Approach 2: steam protocol (if steamPath missing)
            var protoInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "steam://rungameid/570940",
                UseShellExecute = true
            };
            var protoProc = System.Diagnostics.Process.Start(protoInfo);
            return protoProc != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to launch via Steam: {ex.Message}");
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}
