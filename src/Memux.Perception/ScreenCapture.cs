using System.Runtime.InteropServices;
using System.Diagnostics;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Captures game window using GDI-based methods (BitBlt, PrintWindow, CopyFromScreen)
/// Optimized for console applications and game automation
/// </summary>
public partial class ScreenCapture
{
    private IntPtr _windowHandle;
    private DesktopDuplicationCapture? _duplication;
    private GraphicsCaptureCapture? _graphicsCapture;
    // GDI-based capture optimized for console applications
    
    public ScreenCapture(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        // GDI-based capture ready
    }
    
    /// <summary>
    /// Capture the current frame from the game window
    /// Returns raw BGRA pixel data
    /// </summary>
    public (byte[] data, int width, int height) CaptureFrame()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Window handle not set");
        }

		// OBS Studio-inspired capture chain for DirectX games
		// Try multiple methods in order of reliability for hardware-accelerated content
		// Some DirectX titles render into child HWNDs (e.g., Dark Souls Remastered)
		// Prefer child capture first for those processes
		var procName = GetProcessNameForWindow(_windowHandle);
		bool preferChildCapture = IsDirectXGameNeedingChildCapture(procName);
		
        // Method 0: Windows.Graphics.Capture - captures window even when occluded (behind other windows)
        // This is the modern replacement for DLL injection-based game capture
        if (preferChildCapture)
        {
            _graphicsCapture ??= GraphicsCaptureCapture.TryCreateForWindow(_windowHandle);
            var gc = _graphicsCapture?.TryCaptureFrame();
            if (gc != null) return gc.Value;
        }
		
		if (preferChildCapture)
		{
			var childResult = TryChildWindowCapture(lenientBlackCheck: true);
			if (childResult != null) return childResult.Value;
			var altResult = TryPrintWindowCapture(alternateFlags: true, lenientBlackCheck: true);
			if (altResult != null) return altResult.Value;
		}

        // Method 1: Try PrintWindow first (most reliable for DirectX games)
		var printWindowResult = TryPrintWindowCapture();
        if (printWindowResult != null) return printWindowResult.Value;
        
        // Method 2: Try BitBlt (works for some games)
        var bitBltResult = TryBitBltCapture();
        if (bitBltResult != null) return bitBltResult.Value;
        
        // Method 3: Try CopyFromScreen with window bounds (composition output)
        var copyFromScreenResult = TryCopyFromScreenCapture();
        if (copyFromScreenResult != null) return copyFromScreenResult.Value;
        
        // Method 4: Desktop capture as final fallback
        return TryDesktopCapture();
    }
    
    /// <summary>
    /// Update the window handle (in case it changes)
    /// </summary>
    public void SetWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        // GDI capture handles window changes automatically
    }
    
    private static bool IsMostlyBlack(byte[] data)
    {
        if (data.Length < 16) return true;
        int step = Math.Max(4, data.Length / 1024);
        int nonBlack = 0;
        for (int i = 0; i < data.Length; i += step)
        {
            // BGRA bytes; consider pixel non-black if any of B,G,R exceeds threshold
            byte b = data[i];
            byte g = (i + 1) < data.Length ? data[i + 1] : (byte)0;
            byte r = (i + 2) < data.Length ? data[i + 2] : (byte)0;
            if (b > 5 || g > 5 || r > 5)
            {
                nonBlack++;
                if (nonBlack > 5) return false;
            }
        }
        return true;
    }
    
    // Windows API imports
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
    
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
    
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);
    
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
    
    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);
    
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);
    
    private const int SRCCOPY = 0x00CC0020;
    private const int BI_RGB = 0;
    private const int DIB_RGB_COLORS = 0;

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
    
    // OBS Studio-inspired capture methods for DirectX games
    private (byte[] data, int width, int height)? TryPrintWindowCapture(bool alternateFlags = false, bool lenientBlackCheck = false)
    {
        try
        {
            GetWindowRect(_windowHandle, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            
            if (width <= 0 || height <= 0) return null;
            
            using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            
            // PrintWindow with PW_RENDERFULLCONTENT by default; optionally try no flags
            bool success = alternateFlags ? PrintWindow(_windowHandle, hdc, 0) : PrintWindow(_windowHandle, hdc, PW_RENDERFULLCONTENT);
            g.ReleaseHdc(hdc);
            
            if (!success) return null;
            
            var rect2 = new System.Drawing.Rectangle(0, 0, width, height);
            var bmpData = bmp.LockBits(rect2, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] buffer = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);
            bmp.UnlockBits(bmpData);
            
            if (!lenientBlackCheck)
            {
                if (IsMostlyBlack(buffer)) return null;
            }
            else
            {
                if (IsProbablyAllBlack(buffer)) return null;
            }
            return (buffer, width, height);
        }
        catch { return null; }
    }
    
    private (byte[] data, int width, int height)? TryBitBltCapture()
    {
        try
        {
            GetClientRect(_windowHandle, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            
            if (width <= 0 || height <= 0) return null;
            
            IntPtr hdcWindow = GetDC(_windowHandle);
            IntPtr hdcMemory = CreateCompatibleDC(hdcWindow);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
            IntPtr hOld = SelectObject(hdcMemory, hBitmap);
            
            BitBlt(hdcMemory, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);
            
            BITMAPINFOHEADER bi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            };
            
            int imageSize = width * height * 4;
            byte[] data = new byte[imageSize];
            GetDIBits(hdcMemory, hBitmap, 0, (uint)height, data, ref bi, DIB_RGB_COLORS);
            
            SelectObject(hdcMemory, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hdcMemory);
            ReleaseDC(_windowHandle, hdcWindow);
            
            if (IsMostlyBlack(data)) return null;
            return (data, width, height);
        }
        catch { return null; }
    }
    
    private (byte[] data, int width, int height)? TryChildWindowCapture(bool lenientBlackCheck)
    {
        try
        {
            (byte[] data, int width, int height)? best = null;
            EnumChildWindows(_windowHandle, (child, l) =>
            {
                try
                {
                    if (!IsWindowVisible(child)) return true;
                    if (!GetClientRect(child, out RECT cr)) return true;
                    int w = cr.Right - cr.Left;
                    int h = cr.Bottom - cr.Top;
                    if (w <= 0 || h <= 0) return true;
                    var saved = _windowHandle;
                    _windowHandle = child;
                    var r1 = TryPrintWindowCapture(alternateFlags: false, lenientBlackCheck: lenientBlackCheck);
                    if (r1 == null)
                    {
                        var r2 = TryPrintWindowCapture(alternateFlags: true, lenientBlackCheck: lenientBlackCheck);
                        if (r2 != null) best = r2;
                    }
                    else
                    {
                        best = r1;
                    }
                    _windowHandle = saved;
                    return best == null;
                }
                catch { return true; }
            }, IntPtr.Zero);
            return best;
        }
        catch { return null; }
    }

    private (byte[] data, int width, int height)? TryCopyFromScreenCapture()
    {
        try
        {
            // Try DWM extended frame bounds first
            if (DwmGetWindowAttribute(_windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT wrect, Marshal.SizeOf<RECT>()) == 0)
            {
                int width = wrect.Right - wrect.Left;
                int height = wrect.Bottom - wrect.Top;
                
                if (width > 0 && height > 0)
                {
                    using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        // Ensure latest composition state
                        try { DwmFlush(); } catch { }
                        g.CopyFromScreen(wrect.Left, wrect.Top, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);
                    }
                    
                    var rect = new System.Drawing.Rectangle(0, 0, width, height);
                    var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int bytes = Math.Abs(bmpData.Stride) * height;
                    byte[] buffer = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);
                    bmp.UnlockBits(bmpData);
                    // For composition capture, do not discard dark frames
                    return (buffer, width, height);
                }
            }
            
            // Fallback to window rect
            if (GetWindowRect(_windowHandle, out RECT wrect2))
            {
                int width = wrect2.Right - wrect2.Left;
                int height = wrect2.Bottom - wrect2.Top;
                
                if (width > 0 && height > 0)
                {
                    using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        try { DwmFlush(); } catch { }
                        g.CopyFromScreen(wrect2.Left, wrect2.Top, 0, 0, new System.Drawing.Size(width, height), System.Drawing.CopyPixelOperation.SourceCopy);
                    }
                    
                    var rect = new System.Drawing.Rectangle(0, 0, width, height);
                    var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int bytes = Math.Abs(bmpData.Stride) * height;
                    byte[] buffer = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);
                    bmp.UnlockBits(bmpData);
                    // Return buffer regardless of darkness
                    return (buffer, width, height);
                }
            }
            
            return null;
        }
        catch { return null; }
    }
    
    private (byte[] data, int width, int height) TryDesktopCapture()
    {
        try
        {
            // Desktop capture as final fallback using Windows API
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            using var bmp = new System.Drawing.Bitmap(screenWidth, screenHeight);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight), System.Drawing.CopyPixelOperation.SourceCopy);
            }
            
            var rect = new System.Drawing.Rectangle(0, 0, screenWidth, screenHeight);
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int bytes = Math.Abs(bmpData.Stride) * screenHeight;
            byte[] buffer = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);
            bmp.UnlockBits(bmpData);
            
            return (buffer, screenWidth, screenHeight);
        }
        catch
        {
            // Ultimate fallback - return a small black image
            byte[] blackImage = new byte[4]; // 1x1 black pixel
            return (blackImage, 1, 1);
        }
    }

    private static bool IsProbablyAllBlack(byte[] data)
    {
        if (data == null || data.Length < 16) return true;
        int nonBlack = 0;
        int step = Math.Max(4, data.Length / 4096);
        for (int i = 0; i < data.Length; i += step)
        {
            byte b = data[i];
            byte g = (i + 1) < data.Length ? data[i + 1] : (byte)0;
            byte r = (i + 2) < data.Length ? data[i + 2] : (byte)0;
            if (b > 2 || g > 2 || r > 2)
            {
                nonBlack++;
                if (nonBlack > 32) return false;
            }
        }
        return true;
    }

    private static string? GetProcessNameForWindow(IntPtr hWnd)
    {
        try
        {
            int pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid <= 0) return null;
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch { return null; }
    }

    private static bool IsDirectXGameNeedingChildCapture(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        processName = processName.Trim();
        return processName.Equals("DarkSoulsRemastered", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("DarkSouls", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("DSRemastered", StringComparison.OrdinalIgnoreCase);
    }
}
