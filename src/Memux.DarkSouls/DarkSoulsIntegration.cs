using System.Diagnostics;
using System.Runtime.InteropServices;
using Memux.Core;

namespace Memux.DarkSouls;

/// <summary>
/// Dark Souls Remastered specific integration
/// Minimal game-specific code - just window finding and input mapping
/// </summary>
public class DarkSoulsIntegration
{
    private const string PROCESS_NAME = "DarkSoulsRemastered";
    private const string WINDOW_TITLE = "DARK SOULS";
    // Default Steam launch
    private const string DEFAULT_STEAM_EXE = @"C:\\Program Files (x86)\\Steam\\steam.exe";
    private const string DEFAULT_STEAM_ARGS = "-applaunch 570940";

    private readonly ProcessManager _processManager;
    private readonly string? _launchArgs;

    public DarkSoulsIntegration(string? executablePath = null, string? launchArgs = null)
    {
        // If no explicit executable provided, default to Steam applaunch
        if (string.IsNullOrEmpty(executablePath))
        {
            executablePath = DEFAULT_STEAM_EXE;
            launchArgs ??= DEFAULT_STEAM_ARGS;
        }
        _launchArgs = launchArgs;
        _processManager = new ProcessManager(executablePath);
    }

    /// <summary>
    /// Launch Dark Souls Remastered
    /// </summary>
    public bool LaunchGame()
    {
        Console.WriteLine("Launching Dark Souls Remastered...");
        return _processManager.Launch(_launchArgs);
    }

    /// <summary>
    /// Attach to an already running instance
    /// </summary>
    public bool AttachToGame()
    {
        Console.WriteLine("Searching for Dark Souls Remastered process...");
        return _processManager.AttachToExisting(PROCESS_NAME);
    }

    /// <summary>
    /// Get the game window handle
    /// </summary>
    public IntPtr GetGameWindowHandle()
    {
        // First try the process manager
        var handle = _processManager.GetMainWindowHandle();
        if (handle != IntPtr.Zero)
        {
            WindowFocusHelper.TryFocusWindow(handle);
            return handle;
        }
        
        // Fallback: search by window title
        var h = FindWindow(null, WINDOW_TITLE);
        if (h != IntPtr.Zero)
        {
            WindowFocusHelper.TryFocusWindow(h);
        }
        return h;
    }

    /// <summary>
    /// Check if game is running
    /// </summary>
    public bool IsGameRunning()
    {
        return _processManager.IsRunning();
    }

    /// <summary>
    /// Bring game window to foreground
    /// </summary>
    public void FocusGameWindow()
    {
        _processManager.BringToForeground();
    }

    /// <summary>
    /// Get game-specific context hints based on screen analysis
    /// These are optional heuristics to help perception
    /// </summary>
    public Dictionary<string, object> GetContextHints(byte[] screenData, int width, int height)
    {
        var hints = new Dictionary<string, object>();
        
        // TODO: Add Dark Souls specific heuristics
        // For example:
        // - Health bar detection (red bar in top-left)
        // - Stamina bar detection (green bar below health)
        // - "YOU DIED" screen detection (large text in center)
        // - Bonfire menu detection
        
        return hints;
    }

    /// <summary>
    /// Map abstract action names to Dark Souls specific inputs
    /// </summary>
    public string MapAction(string abstractAction)
    {
        // Map common actions to Dark Souls controls
        return abstractAction.ToUpper() switch
        {
            "ATTACK" => "RB",           // Right bumper
            "HEAVY_ATTACK" => "RT",     // Right trigger
            "DODGE" => "B",             // B button
            "BLOCK" => "LB",            // Left bumper
            "PARRY" => "LT",            // Left trigger
            "USE_ITEM" => "X",          // X button
            "INTERACT" => "A",          // A button
            "JUMP" => "A",              // A button (when running)
            "SPRINT" => "B",            // B button (hold)
            "LOCK_ON" => "RS",          // Right stick click
            "SWITCH_TARGET_LEFT" => "LEFT",
            "SWITCH_TARGET_RIGHT" => "RIGHT",
            _ => abstractAction
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
}

