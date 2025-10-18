using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Memux.Core;

/// <summary>
/// Manages launching and monitoring target applications (e.g., Dark Souls Remastered)
/// </summary>
public class ProcessManager
{
    private Process? _targetProcess;
    private readonly string _executablePath;
    private readonly string? _workingDirectory;
    
    public ProcessManager(string executablePath, string? workingDirectory = null)
    {
        _executablePath = executablePath;
        _workingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath);
    }
    
    /// <summary>
    /// Launch the target application
    /// </summary>
    public bool Launch(string? arguments = null, bool runAsAdmin = false)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments!;
            }
            if (runAsAdmin)
            {
                startInfo.Verb = "runas";
            }
            
            _targetProcess = Process.Start(startInfo);
            
            if (_targetProcess == null)
            {
                return false;
            }
            
            // Wait for the process to initialize
            Thread.Sleep(2000);
            
            return !_targetProcess.HasExited;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to launch process: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Find an already running instance of the target application
    /// </summary>
    public bool AttachToExisting(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                _targetProcess = processes[0];
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to attach to process: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Get the main window handle of the target process
    /// </summary>
    public IntPtr GetMainWindowHandle()
    {
        if (_targetProcess == null || _targetProcess.HasExited)
        {
            return IntPtr.Zero;
        }
        
        // Wait for window to be created
        for (int i = 0; i < 50; i++)
        {
            _targetProcess.Refresh();
            if (_targetProcess.MainWindowHandle != IntPtr.Zero)
            {
                return _targetProcess.MainWindowHandle;
            }
            Thread.Sleep(100);
        }
        
        return _targetProcess.MainWindowHandle;
    }
    
    /// <summary>
    /// Check if the target process is still running
    /// </summary>
    public bool IsRunning()
    {
        return _targetProcess != null && !_targetProcess.HasExited;
    }
    
    /// <summary>
    /// Bring the target window to the foreground
    /// </summary>
    public void BringToForeground()
    {
        if (_targetProcess == null || _targetProcess.HasExited)
        {
            return;
        }
        
        var handle = GetMainWindowHandle();
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }
    
    /// <summary>
    /// Gracefully stop the target process
    /// </summary>
    public void Stop()
    {
        if (_targetProcess != null && !_targetProcess.HasExited)
        {
            try
            {
                _targetProcess.CloseMainWindow();
                if (!_targetProcess.WaitForExit(5000))
                {
                    _targetProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping process: {ex.Message}");
            }
        }
    }
    
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

