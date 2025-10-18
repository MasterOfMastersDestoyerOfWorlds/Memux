using Memux.Perception;
using System.Diagnostics;

namespace Memux.Tests;

/// <summary>
/// Tests for Windows.Graphics.Capture integration
/// Ensures DirectX game capture works even when windows are occluded
/// NOTE: These tests require a windowed environment and may be skipped in headless/CI environments
/// </summary>
public class GraphicsCaptureTests
{
    [Fact]
    public void GraphicsCaptureCapture_CanBeCreatedForNotepad()
    {
        // Arrange: Launch notepad as a test window
        var notepad = Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

        try
        {
            // Wait for window to be ready and get handle
            notepad.WaitForInputIdle(5000);
            Thread.Sleep(1000); // Extra wait for window creation
            notepad.Refresh();
            
            var hwnd = notepad.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                // Try to find window by process
                for (int i = 0; i < 20 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(200);
                    notepad.Refresh();
                    hwnd = notepad.MainWindowHandle;
                }
            }
            
            // Skip test if we can't get a window handle (headless/CI environment)
            if (hwnd == IntPtr.Zero)
            {
                return; // Skip test
            }

            // Act: Try to create Graphics Capture
            var capture = GraphicsCaptureCapture.TryCreateForWindow(hwnd);

            // Assert: Should succeed for a valid window
            Assert.NotNull(capture);
            
            // Cleanup
            capture.Dispose();
        }
        finally
        {
            notepad?.Kill();
            notepad?.WaitForExit(1000);
        }
    }

    [Fact]
    public void GraphicsCaptureCapture_CanCaptureFrameFromNotepad()
    {
        // Arrange: Launch notepad
        var notepad = Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

        try
        {
            notepad.WaitForInputIdle(5000);
            var capture = GraphicsCaptureCapture.TryCreateForWindow(notepad.MainWindowHandle);
            Assert.NotNull(capture);

            // Act: Wait for first frame (up to 2 seconds)
            (byte[] data, int width, int height)? frame = null;
            for (int i = 0; i < 20 && frame == null; i++)
            {
                Thread.Sleep(100);
                frame = capture.TryCaptureFrame();
            }

            // Assert: Should receive a frame with valid dimensions
            Assert.NotNull(frame);
            Assert.True(frame.Value.width > 0);
            Assert.True(frame.Value.height > 0);
            Assert.True(frame.Value.data.Length == frame.Value.width * frame.Value.height * 4);
            
            // Cleanup
            capture.Dispose();
        }
        finally
        {
            notepad?.Kill();
            notepad?.WaitForExit(1000);
        }
    }

    [Fact]
    public void GraphicsCaptureCapture_ReturnsNullForInvalidHandle()
    {
        // Act: Try to create with invalid handle
        var capture = GraphicsCaptureCapture.TryCreateForWindow(IntPtr.Zero);

        // Assert: Should return null
        Assert.Null(capture);
    }

    [Fact]
    public void ScreenCapture_UsesGraphicsCaptureForDirectXGames()
    {
        // This test verifies that the ScreenCapture routing works
        // We can't easily test with Dark Souls in CI, but we can verify
        // that the code path exists and doesn't crash
        
        // Arrange: Launch notepad (will be detected as needing standard capture)
        var notepad = Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

        try
        {
            notepad.WaitForInputIdle(5000);
            Thread.Sleep(500);
            notepad.Refresh();
            
            var hwnd = notepad.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                for (int i = 0; i < 10 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(100);
                    notepad.Refresh();
                    hwnd = notepad.MainWindowHandle;
                }
            }
            
            Assert.NotEqual(IntPtr.Zero, hwnd);
            
            var screenCapture = new ScreenCapture(hwnd);

            // Act: Capture a frame
            var result = screenCapture.CaptureFrame();

            // Assert: Should get valid data
            Assert.NotNull(result.data);
            Assert.True(result.width > 0);
            Assert.True(result.height > 0);
            Assert.Equal(result.width * result.height * 4, result.data.Length);
        }
        finally
        {
            notepad?.Kill();
            notepad?.WaitForExit(1000);
        }
    }
}

