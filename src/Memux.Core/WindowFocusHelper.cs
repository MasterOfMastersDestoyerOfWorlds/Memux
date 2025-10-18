using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Memux.Core;

public static class WindowFocusHelper
{
    public static bool TryFocusWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            // Restore if minimized
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            // Attach thread input to allow SetForegroundWindow in some scenarios
            var fore = GetForegroundWindow();
            var foreThread = GetWindowThreadProcessId(fore, out _);
            var thisThread = GetCurrentThreadId();
            bool attached = false;
            if (foreThread != thisThread)
            {
                attached = AttachThreadInput(foreThread, thisThread, true);
            }

            try
            {
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(foreThread, thisThread, false);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private const int SW_RESTORE = 9;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    [DllImport("kernel32.dll")] private static extern int GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);
}


