using System.Drawing;
using System.Windows.Forms;
using Memux.Core.Models;
using Memux.Core;

namespace Memux.UI;

public static class PerceptionViewer
{
    private static Thread? _uiThread;
    private static PerceptionViewerForm? _form;
    private static readonly AutoResetEvent _ready = new(false);
    public static event EventHandler? Closed;
    public static event Action<IntPtr, string>? FocusedProgramChanged;
    private static readonly HashSet<int> _spawnedPids = new();
    public static IReadOnlyCollection<int> SpawnedPids => _spawnedPids;
    private static ModelManager? _modelManager;

    public static void Show(ModelManager? modelManager = null)
    {
        if (_uiThread != null && _uiThread.IsAlive) return;

        _modelManager = modelManager;
        _uiThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _form = new PerceptionViewerForm(_modelManager);
            _form.Shown += (_, __) => _form.GetType().GetMethod("RefreshPrograms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(_form, null);
            _form.Load += (_, __) => _ready.Set();
            _form.FormClosed += (_, __) =>
            {
                try { Closed?.Invoke(null, EventArgs.Empty); } catch { }
            };
            Application.Run(_form);
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();

        _ready.WaitOne(TimeSpan.FromSeconds(5));
    }

    public static void Close()
    {
        var form = _form;
        if (form == null) return;
        try
        {
            form.Invoke(new Action(() => form.Close()));
        }
        catch { }
    }

    public static void Update(PerceptionState state, Goal? goal, string? nextSkillName, IEnumerable<string> subskillTree)
    {
        var form = _form;
        if (form == null) return;

        // Create bitmaps (depth and overlays) before switching to UI thread
        Bitmap? depthBmp = PerceptionRenderer.CreateDepthBitmap(state.DepthMap, state.Width, state.Height);
        Bitmap? baseBmp = PerceptionRenderer.CreateScreenshotBitmap(state.ScreenData, state.Width, state.Height);
        Bitmap? objectsBmp = state.ObjectSegmentation != null
            ? PerceptionRenderer.CreateObjectsSegmentation(baseBmp, state.ObjectSegmentation, state.DetectedObjects, state.Width, state.Height)
            : PerceptionRenderer.CreateObjectsOverlay(baseBmp, state.DetectedObjects);
        Bitmap? ocrBmp = PerceptionRenderer.CreateOcrOverlay(baseBmp, state.OcrResults);
        Bitmap? ocrProcessedBmp = PerceptionRenderer.CreateOcrProcessedImage(state.OcrProcessedImage, state.OcrProcessedImageWidth, state.OcrProcessedImageHeight);
        baseBmp?.Dispose();

        try
        {
            form.BeginInvoke(new Action(() =>
            {
                if (form.IsDisposed) return;
                form.UpdatePerception(state, depthBmp, objectsBmp, ocrBmp, ocrProcessedBmp);
                form.UpdateGoalsAndPlan(goal, nextSkillName, subskillTree);
            }));
        }
        catch
        {
            depthBmp?.Dispose();
            objectsBmp?.Dispose();
            ocrBmp?.Dispose();
            ocrProcessedBmp?.Dispose();
        }
    }

    public static void UpdateModelProgress(ModelProgress progress)
    {
        var form = _form;
        if (form == null) return;
        try
        {
            form.BeginInvoke(new Action(() =>
            {
                if (form.IsDisposed) return;
                form.UpdateModelProgress(progress);
            }));
        }
        catch { }
    }

    internal static void RaiseFocusedProgram(IntPtr hWnd, string name)
    {
        try { FocusedProgramChanged?.Invoke(hWnd, name); } catch { }
    }

    public static void RegisterSpawnedProcess(System.Diagnostics.Process proc)
    {
        try { _spawnedPids.Add(proc.Id); } catch { }
    }

    public static IntPtr GetDesktopHandle()
    {
        return GetDesktopWindow();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}


