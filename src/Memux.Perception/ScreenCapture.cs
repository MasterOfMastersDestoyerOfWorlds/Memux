using System.Runtime.InteropServices;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Captures game window using Windows Graphics Capture API
/// Falls back to BitBlt for compatibility
/// </summary>
public class ScreenCapture
{
    private IntPtr _windowHandle;
    
    public ScreenCapture(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
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
        
        // Get window dimensions
        GetClientRect(_windowHandle, out RECT rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Invalid window dimensions");
        }
        
        // Create device contexts
        IntPtr hdcWindow = GetDC(_windowHandle);
        IntPtr hdcMemory = CreateCompatibleDC(hdcWindow);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
        IntPtr hOld = SelectObject(hdcMemory, hBitmap);
        
        // BitBlt to copy window contents
        BitBlt(hdcMemory, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);
        
        // Get bitmap data
        BITMAP bitmap = new BITMAP();
        GetObject(hBitmap, Marshal.SizeOf(bitmap), ref bitmap);
        
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // Negative height for top-down bitmap
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BI_RGB
        };
        
        int imageSize = width * height * 4; // BGRA
        byte[] data = new byte[imageSize];
        
        GetDIBits(hdcMemory, hBitmap, 0, (uint)height, data, ref bi, DIB_RGB_COLORS);
        
        // Cleanup
        SelectObject(hdcMemory, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMemory);
        ReleaseDC(_windowHandle, hdcWindow);
        
        return (data, width, height);
    }
    
    /// <summary>
    /// Update the window handle (in case it changes)
    /// </summary>
    public void SetWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }
    
    // Windows API imports
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    
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
}

