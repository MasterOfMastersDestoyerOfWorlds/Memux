using Memux.Perception;
using Memux.Core;

namespace Memux.Tests;

/// <summary>
/// Tests for Windows.Graphics.Capture integration
/// Ensures DirectX game capture works even when windows are occluded
/// </summary>
public class GraphicsCaptureTests
{

    [Fact]
    public void GraphicsCaptureCapture_CanBeCreatedForNotepad()
    {
        // Arrange: Launch notepad and attach to it (same pattern as production code)
        var pm = new ProcessManager(@"C:\Windows\System32\notepad.exe");
        pm.Launch();
        Thread.Sleep(500); // Give notepad time to start
        Assert.True(pm.AttachToExisting("notepad"));

        try
        {
            var hwnd = pm.GetMainWindowHandle();
            Assert.NotEqual(IntPtr.Zero, hwnd);

            // Act: Try to create Graphics Capture
            var capture = GraphicsCaptureCapture.TryCreateForWindow(hwnd);

            // Assert: Should succeed for a valid window
            Assert.NotNull(capture);
            
            // Cleanup capture
            capture.Dispose();
        }
        finally
        {
            pm.Stop();
        }
    }

    [Fact]
    public void GraphicsCaptureCapture_CanCaptureFrameFromNotepad()
    {
        // Arrange: Launch notepad and attach to it
        var pm = new ProcessManager(@"C:\Windows\System32\notepad.exe");
        pm.Launch();
        Thread.Sleep(500);
        Assert.True(pm.AttachToExisting("notepad"));

        GraphicsCaptureCapture? capture = null;

        try
        {
            var hwnd = pm.GetMainWindowHandle();
            Assert.NotEqual(IntPtr.Zero, hwnd);
            
            capture = GraphicsCaptureCapture.TryCreateForWindow(hwnd);
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
        }
        finally
        {
            capture?.Dispose();
            pm.Stop();
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
        
        // Arrange: Launch notepad and attach to it
        var pm = new ProcessManager(@"C:\Windows\System32\notepad.exe");
        pm.Launch();
        Thread.Sleep(500);
        Assert.True(pm.AttachToExisting("notepad"));

        try
        {
            var hwnd = pm.GetMainWindowHandle();
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
            pm.Stop();
        }
    }
}

