using Memux.Core;
using System.Diagnostics;
using Memux.Core.Database;
using Memux.Skills;
using Memux.Actions;
using Memux.Perception;
using Memux.Selection;
using Memux.Curriculum;
using Memux.UI;

namespace Memux;





class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Memux: General Skill Acquisition System ===");
        Console.WriteLine();
        KillOtherInstances();

        string? gamePath = null;
        string? apiKey = null;
        string dbPath = "memux.db";
        string? depthModel = null;
        string? objectModel = null;
        string? objectClasses = null;
        string? tessData = null;
        string? llmModel = null;
        bool useGpu = true;
        string? steamPath = null;
        bool preflight = false;
        string? programKey = null;
        bool noElevate = true;
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

        depthModel ??= Environment.GetEnvironmentVariable("MEMUX_DEPTH_MODEL");
        objectModel ??= Environment.GetEnvironmentVariable("MEMUX_OBJECT_MODEL");
        objectClasses ??= Environment.GetEnvironmentVariable("MEMUX_OBJECT_CLASSES");
        tessData ??= Environment.GetEnvironmentVariable("MEMUX_TESSDATA");

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

        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");


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
        await RunFullSystemAsync(dbPath, apiKey, depthModel, objectModel, objectClasses, tessData, useGpu, llmModel, programKey);
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
        string? depthModel,
        string? objectModel,
        string? objectClasses,
        string? tessData,
        bool useGpu,
        string? llmModel,
        string? programKey)
    {
        var skillLibrary = new SkillLibrary(dbPath);
        await skillLibrary.CreateSeedSkillsAsync();


        var db = new Memux.Core.Database.MemuxDatabase(dbPath);
        var prog = !string.IsNullOrEmpty(programKey) ? db.GetProgramByNameOrId(programKey) : db.GetDefaultProgram();


        var cancellationToken = new CancellationTokenSource();
        PerceptionViewer.Closed += (_, __) =>
        {
            try { cancellationToken.Cancel(); } catch { }

            try
            {
                foreach (var pid in Memux.UI.PerceptionViewer.SpawnedPids)
                {
                    try
                    {
                        var p = System.Diagnostics.Process.GetProcessById(pid);
                        if (!p.HasExited)
                        {
                            if (!p.CloseMainWindow()) p.Kill(true);
                            else if (!p.WaitForExit(3000)) p.Kill(true);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        };


        var modelManager = new ModelManager();
        PerceptionViewer.Show(modelManager);

        var desktopHandle = GetDesktopWindow();
        if (desktopHandle == IntPtr.Zero)
        {
            Console.WriteLine("ERROR: Could not acquire desktop window handle.");
            return;
        }

        var perception = new PerceptionPipeline(
            desktopHandle,
            depthModel,
            objectModel,
            objectClasses,
            tessData,
            useGpu);
        string? focusedProgramName = null;
        PerceptionViewer.FocusedProgramChanged += (h, name) =>
        {
            try
            {
                if (h != IntPtr.Zero)
                {
                    perception.UpdateWindowHandle(h);
                }
                else
                {
                    var desktop = GetDesktopWindow();
                    if (desktop != IntPtr.Zero) perception.UpdateWindowHandle(desktop);
                }
                focusedProgramName = string.IsNullOrEmpty(name) ? null : name;
            }
            catch { }
        };

        Console.WriteLine("Viewer started. Launch a program from the Programs panel to focus capture.");
        var contextAnalyzer = new ContextAnalyzer();
        var executor = new ActionExecutor();


        using var selector = new SkillSelector(llmModel ?? string.Empty, skillLibrary, useCache: true);




        CurriculumAgent? curriculum = null;
        if (!string.IsNullOrEmpty(apiKey))
        {
            curriculum = new CurriculumAgent(apiKey);
            curriculum.GoalCreated += (s, e) =>
                Console.WriteLine($"[Curriculum] New goal: {e.Goal.Description}");
            curriculum.GoalCompleted += (s, e) =>
                Console.WriteLine($"[Curriculum] Completed: {e.Goal.Description}");

            var goals = await curriculum.GenerateInitialGoalsAsync($"Starting with {prog?.Name ?? "target program"}");
            Console.WriteLine($"Generated {goals.Count} initial goals");
        }

        Console.WriteLine("Starting main loop... (will stop on process exit)");
        Console.WriteLine();


        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cancellationToken.Cancel(); };
        AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { cancellationToken.Cancel(); } catch { } };

        int frameCount = 0;
        var lastSecond = DateTime.UtcNow;
        int blankFrames = 0;

        try
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {

                    var state = perception.CaptureAndProcess();
                    state.FocusedProgram = focusedProgramName ?? prog?.Name;


                    if (state.Width <= 0 || state.Height <= 0 || state.ScreenData == null || state.ScreenData.Length == 0)
                    {
                        blankFrames++;
                        if (blankFrames >= 10)
                        {
                            TryReacquireWindowHandle(perception, prog?.ProcessName);
                            blankFrames = 0;
                        }
                        await Task.Delay(16);
                        continue;
                    }
                    else
                    {
                        blankFrames = 0;
                    }

                    var context = contextAnalyzer.AnalyzeContext(state);


                    var planned = await selector.SelectSkillAsync(state, curriculum?.GetCurrentGoal());
                    string? nextSkillName = planned?.Name;
                    var subskillLines = BuildDependencyTreeLines(planned, skillLibrary, 0, new HashSet<string>());


                    var currentGoal = curriculum?.GetCurrentGoal();
                    PerceptionViewer.Update(state, currentGoal, nextSkillName, subskillLines);


                    var chosen = planned ?? (await skillLibrary.GetTopSkillsAsync(1)).FirstOrDefault();
                    if (chosen != null && chosen.Execute != null)
                    {

                        chosen.InvokeSubskill = (subskillName, s) =>
                        {
                            var target = skillLibrary.GetAllSkills().FirstOrDefault(x => x.Name.Equals(subskillName, StringComparison.OrdinalIgnoreCase));
                            return target?.Execute?.Invoke(s);
                        };

                        var actions = chosen.Execute!(state);
                        var result = await executor.ExecuteAsync(actions, state);
                        skillLibrary.RecordUsage(chosen.Id, result.Success, result.ExecutionTimeMs);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Loop] Error: {ex.Message}");
                }


                frameCount++;
                if ((DateTime.UtcNow - lastSecond).TotalSeconds >= 1.0)
                {
                    lastSecond = DateTime.UtcNow;
                    frameCount = 0;
                }


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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    private static void TryReacquireWindowHandle(Memux.Perception.PerceptionPipeline perception, string? processName)
    {
        try
        {
            if (string.IsNullOrEmpty(processName)) return;
            var procs = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var p in procs)
            {
                try
                {
                    p.Refresh();
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        perception.UpdateWindowHandle(p.MainWindowHandle);
                        WindowFocusHelper.TryFocusWindow(p.MainWindowHandle);
                        return;
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
