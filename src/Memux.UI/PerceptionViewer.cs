using System.Drawing;
using System.Windows.Forms;
using Memux.Core.Models;

namespace Memux.UI;

public static class PerceptionViewer
{
    private static Thread? _uiThread;
    private static PerceptionViewerForm? _form;
    private static readonly AutoResetEvent _ready = new(false);
    public static event EventHandler? Closed;

    public static void Show()
    {
        if (_uiThread != null && _uiThread.IsAlive) return;

        _uiThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _form = new PerceptionViewerForm();
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
        baseBmp?.Dispose();

        try
        {
            form.BeginInvoke(new Action(() =>
            {
                if (form.IsDisposed) return;
                form.UpdatePerception(state, depthBmp, objectsBmp, ocrBmp);
                form.UpdateOcrText(state.OcrResults);
                form.UpdateGoalsAndPlan(goal, nextSkillName, subskillTree);
            }));
        }
        catch
        {
            depthBmp?.Dispose();
            objectsBmp?.Dispose();
            ocrBmp?.Dispose();
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
}


