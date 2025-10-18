using System.Drawing;
using System.Windows.Forms;
using Memux.Core.Models;
using System.Threading.Tasks;
using Memux.Core;
using System.Security.Principal;

namespace Memux.UI;

public class PerceptionViewerForm : Form
{
    private readonly PictureBox _depthPicture;
    private readonly PictureBox _objectsPicture;
    private readonly PictureBox _ocrPicture;
    private readonly Label _depthFpsLabel;
    private readonly Label _objectsFpsLabel;
    private readonly Label _ocrFpsLabel;
    private readonly Label _goalLabel;
    
    // FPS tracking
    private readonly System.Diagnostics.Stopwatch _depthFpsStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly System.Diagnostics.Stopwatch _objectsFpsStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly System.Diagnostics.Stopwatch _ocrFpsStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private long _depthLastTicks;
    private long _objectsLastTicks;
    private long _ocrLastTicks;
    private readonly ProgressBar _goalProgress;
    private readonly ListBox _subGoalsList;
    private readonly Label _nextSkillLabel;
    private readonly TreeView _subskillsTree;
    private readonly Label _depthModelLbl;
    private readonly Label _objectsModelLbl;
    private readonly Label _ocrModelLbl;
    private readonly ListBox _ocrTextList;
    private readonly ListView _programsList;
    private readonly Button _refreshProgramsBtn;
    private readonly Button _launchProgramBtn;
    
    // Depth model download controls
    private readonly Button _downloadDepthBtn;
    private readonly Button _cancelDepthBtn;
    private readonly ProgressBar _depthDownloadPb;
    private readonly Label _depthDownloadLbl;
    private readonly PictureBox _depthStatusIcon;
    
    // Object model download controls
    private readonly Button _downloadObjectBtn;
    private readonly Button _cancelObjectBtn;
    private readonly ProgressBar _objectDownloadPb;
    private readonly Label _objectDownloadLbl;
    private readonly PictureBox _objectStatusIcon;
    
    private ModelManager? _modelManager;

    public PerceptionViewerForm(ModelManager? modelManager = null)
    {
        Text = "Memux Perception Viewer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.White;
        Size = new Size(1280, 800);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        TopMost = true; // keep viewer always on top

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        Controls.Add(root);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
        };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        root.Controls.Add(left, 0, 0);

        _depthPicture = CreatePictureBox();
        _objectsPicture = CreatePictureBox();
        _ocrPicture = CreatePictureBox();
        
        _depthFpsLabel = new Label { Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.Lime };
        _objectsFpsLabel = new Label { Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.Lime };
        _ocrFpsLabel = new Label { Dock = DockStyle.Top, AutoSize = true, ForeColor = Color.Lime };

        left.Controls.Add(WrapWithLabeledPanelAndFps("Depth Map", _depthPicture, _depthFpsLabel), 0, 0);
        left.Controls.Add(WrapWithLabeledPanelAndFps("Objects", _objectsPicture, _objectsFpsLabel), 0, 1);

        var ocrSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        ocrSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        ocrSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        ocrSplit.Controls.Add(_ocrPicture, 0, 0);
        _ocrTextList = new ListBox { Dock = DockStyle.Fill };
        ocrSplit.Controls.Add(_ocrTextList, 1, 0);
        left.Controls.Add(WrapWithLabeledPanelAndFps("OCR", ocrSplit, _ocrFpsLabel), 0, 2);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.Controls.Add(right, 1, 0);

        var goalsGroup = new GroupBox
        {
            Text = "Goals",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
        };
        right.Controls.Add(goalsGroup, 0, 0);

        // Create a scrollable panel for the models section
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(5)
        };
        goalsGroup.Controls.Add(scrollPanel);
        
        var goalsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            RowCount = 10,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        for (int i = 0; i < 9; i++) goalsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        goalsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scrollPanel.Controls.Add(goalsPanel);

        _goalLabel = new Label
        {
            Text = "(no goal)",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        goalsPanel.Controls.Add(_goalLabel, 0, 0);

        _goalProgress = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 20,
        };
        goalsPanel.Controls.Add(_goalProgress, 0, 1);

        var subGoalsLabel = new Label
        {
            Text = "Subgoals:",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        goalsPanel.Controls.Add(subGoalsLabel, 0, 2);

        _subGoalsList = new ListBox
        {
            Dock = DockStyle.Fill,
        };
        goalsPanel.Controls.Add(_subGoalsList, 0, 3);

        _depthModelLbl = new Label { Text = "Depth model:", Dock = DockStyle.Top, AutoSize = true };
        goalsPanel.Controls.Add(_depthModelLbl, 0, 4);
        
        // Depth model download controls
        var depthDownloadPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
        _depthStatusIcon = new PictureBox { Size = new Size(20, 20), Location = new Point(5, 5), SizeMode = PictureBoxSizeMode.StretchImage };
        _downloadDepthBtn = new Button { Text = "Download Depth Model", Size = new Size(140, 25), Location = new Point(30, 5), Enabled = false };
        _cancelDepthBtn = new Button { Text = "Cancel", Size = new Size(60, 25), Location = new Point(180, 5), Visible = false };
        _depthDownloadPb = new ProgressBar { Size = new Size(200, 20), Location = new Point(30, 35), Minimum = 0, Maximum = 100, Visible = false };
        _depthDownloadLbl = new Label { Text = "", Size = new Size(200, 15), Location = new Point(30, 35), ForeColor = Color.LightGray };
        
        _downloadDepthBtn.Click += DownloadDepthBtn_Click;
        _cancelDepthBtn.Click += CancelDepthBtn_Click;
        
        depthDownloadPanel.Controls.Add(_depthStatusIcon);
        depthDownloadPanel.Controls.Add(_downloadDepthBtn);
        depthDownloadPanel.Controls.Add(_cancelDepthBtn);
        depthDownloadPanel.Controls.Add(_depthDownloadPb);
        depthDownloadPanel.Controls.Add(_depthDownloadLbl);
        goalsPanel.Controls.Add(depthDownloadPanel, 0, 5);

        _objectsModelLbl = new Label { Text = "Objects model:", Dock = DockStyle.Top, AutoSize = true };
        goalsPanel.Controls.Add(_objectsModelLbl, 0, 6);
        
        // Object model download controls
        var objectDownloadPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
        _objectStatusIcon = new PictureBox { Size = new Size(20, 20), Location = new Point(5, 5), SizeMode = PictureBoxSizeMode.StretchImage };
        _downloadObjectBtn = new Button { Text = "Download Object Model", Size = new Size(140, 25), Location = new Point(30, 5), Enabled = false };
        _cancelObjectBtn = new Button { Text = "Cancel", Size = new Size(60, 25), Location = new Point(180, 5), Visible = false };
        _objectDownloadPb = new ProgressBar { Size = new Size(200, 20), Location = new Point(30, 35), Minimum = 0, Maximum = 100, Visible = false };
        _objectDownloadLbl = new Label { Text = "", Size = new Size(200, 15), Location = new Point(30, 35), ForeColor = Color.LightGray };
        
        _downloadObjectBtn.Click += DownloadObjectBtn_Click;
        _cancelObjectBtn.Click += CancelObjectBtn_Click;
        
        objectDownloadPanel.Controls.Add(_objectStatusIcon);
        objectDownloadPanel.Controls.Add(_downloadObjectBtn);
        objectDownloadPanel.Controls.Add(_cancelObjectBtn);
        objectDownloadPanel.Controls.Add(_objectDownloadPb);
        objectDownloadPanel.Controls.Add(_objectDownloadLbl);
        goalsPanel.Controls.Add(objectDownloadPanel, 0, 7);

        _ocrModelLbl = new Label { Text = "OCR model:", Dock = DockStyle.Top, AutoSize = true };
        goalsPanel.Controls.Add(_ocrModelLbl, 0, 8);

        var planGroup = new GroupBox
        {
            Text = "Plan",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
        };
        right.Controls.Add(planGroup, 0, 1);

        var planPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
        };
        planPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        planPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        planPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        planGroup.Controls.Add(planPanel);

        _nextSkillLabel = new Label
        {
            Text = "Next skill: (none)",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        planPanel.Controls.Add(_nextSkillLabel, 0, 0);

        _subskillsTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
        };
        planPanel.Controls.Add(_subskillsTree, 0, 1);

        // Programs panel (registry)
        var programsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        programsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        programsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        programsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var programsLabel = new Label { Text = "Programs", Dock = DockStyle.Top, AutoSize = true };
        programsPanel.Controls.Add(programsLabel, 0, 0);
        _programsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };
        _programsList.Columns.Add("Name", 120);
        _programsList.Columns.Add("Exe", 360);
        _programsList.Columns.Add("Process", 140);
        _programsList.Columns.Add("Default", 60);
        programsPanel.Controls.Add(_programsList, 0, 1);
        var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _launchProgramBtn = new Button { Text = "Launch", AutoSize = true };
        var killBtn = new Button { Text = "Kill", AutoSize = true };
        _refreshProgramsBtn = new Button { Text = "Refresh", AutoSize = true };
        buttonsPanel.Controls.Add(_launchProgramBtn);
        buttonsPanel.Controls.Add(killBtn);
        buttonsPanel.Controls.Add(_refreshProgramsBtn);
        programsPanel.Controls.Add(buttonsPanel, 0, 2);
        planPanel.Controls.Add(programsPanel, 0, 2);

        _refreshProgramsBtn.Click += (_, __) => RefreshPrograms();
        _launchProgramBtn.Click += async (_, __) => await LaunchSelectedProgramAsync();
        killBtn.Click += (_, __) => KillSelectedProgram();

        // Focus guard: periodically reassert topmost and focus
        var focusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        focusTimer.Tick += (_, __) =>
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    if (!TopMost) TopMost = true;
                    if (!ContainsFocus) { try { Activate(); } catch { } }
                    WindowFocusHelper.TryFocusWindow(this.Handle);
                }
            }
            catch { }
        };
        focusTimer.Start();
        
        // Initialize model manager for Depth/Object models only
        _modelManager = modelManager ?? new ModelManager();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
    
        try
        {
            if (!IsDisposed && IsHandleCreated)
            {
                TopMost = true;
                try { Activate(); } catch { }
                WindowFocusHelper.TryFocusWindow(this.Handle);
            }
        }
        catch { }
    }

    private static Control WrapWithLabeledPanel(string title, Control child)
    {
        var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var label = new Label { Text = title, Dock = DockStyle.Top, AutoSize = true };
        container.Controls.Add(label);
        child.Dock = DockStyle.Fill;
        container.Controls.Add(child);
        return container;
    }

    private static Control WrapWithLabeledPanelAndFps(string title, Control child, Label fpsLabel)
    {
        var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        var titleLabel = new Label { Text = title, Dock = DockStyle.Top, AutoSize = true };
        container.Controls.Add(titleLabel);
        fpsLabel.Dock = DockStyle.Top;
        container.Controls.Add(fpsLabel);
        child.Dock = DockStyle.Fill;
        container.Controls.Add(child);
        return container;
    }

    private static PictureBox CreatePictureBox()
    {
        return new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
        };
    }

    public void UpdatePerception(PerceptionState state, Bitmap? depthBitmap, Bitmap? objectsBitmap, Bitmap? ocrBitmap)
    {
        // Update header with focused program
        try
        {
            if (!string.IsNullOrEmpty(state.FocusedProgram))
            {
                if (Text != $"Memux Perception Viewer — {state.FocusedProgram} (Focused)")
                    Text = $"Memux Perception Viewer — {state.FocusedProgram} (Focused)";
            }
            else if (Text != "Memux Perception Viewer")
            {
                Text = "Memux Perception Viewer";
            }
        }
        catch { }

        // Ensure panes always show something, even if models are disabled
        if (depthBitmap == null)
        {
            depthBitmap = CreatePlaceholderBitmap(state.Width, state.Height, "Depth disabled", Color.FromArgb(10, 10, 10), Color.Gray);
        }
        if (objectsBitmap == null)
        {
            objectsBitmap = CreatePlaceholderBitmap(state.Width, state.Height, "Objects overlay", Color.Black, Color.DarkGray);
        }
        if (ocrBitmap == null)
        {
            ocrBitmap = CreatePlaceholderBitmap(state.Width, state.Height, "OCR overlay", Color.Black, Color.DimGray);
        }

        SetPicture(_depthPicture, depthBitmap);
        SetPicture(_objectsPicture, objectsBitmap);
        SetPicture(_ocrPicture, ocrBitmap);
        
        // Update FPS displays
        UpdateFpsDisplay(_depthFpsLabel, _depthFpsStopwatch, ref _depthLastTicks);
        UpdateFpsDisplay(_objectsFpsLabel, _objectsFpsStopwatch, ref _objectsLastTicks);
        UpdateFpsDisplay(_ocrFpsLabel, _ocrFpsStopwatch, ref _ocrLastTicks);
        
        // Update OCR text list
        UpdateOcrText(state.OcrResults);
    }

    private static void UpdateFpsDisplay(Label label, System.Diagnostics.Stopwatch stopwatch, ref long lastTicks)
    {
        var currentTicks = stopwatch.ElapsedTicks;
        var deltaTicks = currentTicks - lastTicks;
        lastTicks = currentTicks;
        
        if (deltaTicks > 0)
        {
            var fps = System.Diagnostics.Stopwatch.Frequency / (double)deltaTicks;
            label.Text = $"FPS: {fps:F1}";
        }
    }

    public void UpdateGoalsAndPlan(Goal? goal, string? nextSkillName, IEnumerable<string> subskillTreeLines)
    {
        _goalLabel.Text = goal?.Description ?? "(no goal)";
        _goalProgress.Value = (int)Math.Round(Math.Clamp(goal?.Progress ?? 0.0f, 0.0f, 1.0f) * 100);

        _subGoalsList.Items.Clear();
        if (goal?.SubGoals != null && goal.SubGoals.Any())
        {
            foreach (var sg in goal.SubGoals)
            {
                _subGoalsList.Items.Add(sg);
            }
        }

        _nextSkillLabel.Text = string.IsNullOrEmpty(nextSkillName)
            ? "Next skill: (none)"
            : $"Next skill: {nextSkillName}";

        _subskillsTree.BeginUpdate();
        _subskillsTree.Nodes.Clear();
        var stack = new Stack<TreeNode>();
        foreach (var line in subskillTreeLines)
        {
            int level = 0;
            while (level < line.Length && line[level] == ' ') level++;
            string text = line.TrimStart();

            var node = new TreeNode(text);
            if (level == 0)
            {
                _subskillsTree.Nodes.Add(node);
                stack.Clear();
                stack.Push(node);
                continue;
            }

            while (stack.Count > level)
            {
                stack.Pop();
            }
            if (stack.Count == 0)
            {
                _subskillsTree.Nodes.Add(node);
                stack.Push(node);
            }
            else
            {
                stack.Peek().Nodes.Add(node);
                stack.Push(node);
            }
        }
        _subskillsTree.EndUpdate();
        if (_subskillsTree.Nodes.Count > 0)
        {
            _subskillsTree.ExpandAll();
        }
    }

    public void UpdateModelProgress(ModelProgress progress)
    {
        _depthModelLbl.Text = $"Depth model: {progress.DepthStatus}";
        _objectsModelLbl.Text = $"Objects model: {progress.ObjectsStatus}";
        _ocrModelLbl.Text = $"OCR model: {progress.OcrStatus}";
    }

    private static void SetPicture(PictureBox pictureBox, Bitmap? newBitmap)
    {
        var old = pictureBox.Image as Bitmap;
        pictureBox.Image = newBitmap;
        old?.Dispose();
    }

    private static Bitmap CreatePlaceholderBitmap(int width, int height, string text, Color background, Color foreground)
    {
        int w = Math.Max(320, width > 0 ? width : 640);
        int h = Math.Max(180, height > 0 ? height : 360);
        var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(background);
            using var font = new Font("Segoe UI", Math.Max(10, Math.Min(w, h) / 20f), FontStyle.Bold);
            var size = g.MeasureString(text, font);
            var x = (w - size.Width) / 2f;
            var y = (h - size.Height) / 2f;
            using var brush = new SolidBrush(foreground);
            g.DrawString(text, font, brush, x, y);
        }
        return bmp;
    }

    public void UpdateOcrText(List<OcrResult>? results)
    {
        _ocrTextList.BeginUpdate();
        _ocrTextList.Items.Clear();
        if (results != null)
        {
            foreach (var r in results)
            {
                if (!string.IsNullOrWhiteSpace(r.Text))
                {
                    _ocrTextList.Items.Add(r.Text);
                }
            }
        }
        _ocrTextList.EndUpdate();
    }

    private void RefreshPrograms()
    {
        try
        {
            using var db = new Memux.Core.Database.MemuxDatabase("memux.db");
            var list = db.ListPrograms();
            _programsList.BeginUpdate();
            _programsList.Items.Clear();
            foreach (var p in list)
            {
                var item = new ListViewItem(new[]
                {
                    p.Name,
                    p.ExePath,
                    p.ProcessName,
                    p.IsDefault ? "Yes" : ""
                });
                item.Tag = p;
                _programsList.Items.Add(item);
            }
            _programsList.EndUpdate();
        }
        catch { }
    }

    private async Task LaunchSelectedProgramAsync()
    {
        try
        {
            if (_programsList.SelectedItems.Count == 0) return;
            var rec = _programsList.SelectedItems[0].Tag as Memux.Core.Database.ProgramRecord;
            if (rec == null) return;
            // If already running, focus and return
            var running = System.Diagnostics.Process.GetProcessesByName(rec.ProcessName);
            if (running.Length > 0)
            {
                var h0 = running[0].MainWindowHandle;
                if (h0 != IntPtr.Zero) WindowFocusHelper.TryFocusWindow(h0);
                try { Memux.UI.PerceptionViewer.RaiseFocusedProgram(h0, rec.Name); } catch { }
                return;
            }
            if (!File.Exists(rec.ExePath))
            {
                MessageBox.Show(this, "Executable not found.", "Program Launch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Snapshot processes before launch to detect the newly spawned process
            var before = System.Diagnostics.Process.GetProcesses().Select(p => p.Id).ToHashSet();

            var pm = new Memux.Core.ProcessManager(rec.ExePath);
            bool isElevated = IsProcessElevated();
            bool launchAsAdmin = rec.RunAsAdmin && isElevated;
            pm.Launch(rec.LaunchArgs, launchAsAdmin);

            // After launching via steam.exe, the target game process starts separately.
            // Poll for the desired process name and wait until it has a main window.
            IntPtr h = IntPtr.Zero;
            for (int i = 0; i < 120 && h == IntPtr.Zero; i++) // up to ~30s
            {
                await Task.Delay(250);
                // Prefer new processes since launch
                var now = System.Diagnostics.Process.GetProcesses();
                var candidates = now.Where(p => !before.Contains(p.Id)).ToList();
                var procs = candidates.Where(p => p.ProcessName.Equals(rec.ProcessName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (procs.Length == 0)
                {
                    procs = System.Diagnostics.Process.GetProcessesByName(rec.ProcessName);
                }
                if (procs.Length > 0)
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Refresh();
                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                h = p.MainWindowHandle;
                                Memux.UI.PerceptionViewer.RegisterSpawnedProcess(p);
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (h == IntPtr.Zero)
            {
                // Final attempt: give the just-launched process manager one chance (in case exePath is the game itself)
                try { h = pm.GetMainWindowHandle(); } catch { }
            }

            if (h != IntPtr.Zero)
            {
                WindowFocusHelper.TryFocusWindow(h);
                try { Memux.UI.PerceptionViewer.RaiseFocusedProgram(h, rec.Name); } catch { }
            }
        }
        catch (Exception ex)
        {
            try { MessageBox.Show(this, ex.Message, "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    private void KillSelectedProgram()
    {
        try
        {
            var rec = _programsList.SelectedItems.Count > 0 ? _programsList.SelectedItems[0].Tag as Memux.Core.Database.ProgramRecord : null;
            var procs = rec != null ? System.Diagnostics.Process.GetProcessesByName(rec.ProcessName) : Array.Empty<System.Diagnostics.Process>();
            // Also include any spawned processes we tracked
            var spawned = Memux.UI.PerceptionViewer.SpawnedPids
                .Select(pid => { try { return System.Diagnostics.Process.GetProcessById(pid); } catch { return null; } })
                .Where(p => p != null)
                .ToArray();
            if (spawned.Length > 0)
            {
                procs = procs.Concat(spawned!).DistinctBy(p => p!.Id).Select(p => p!).ToArray();
            }
            if (procs.Length == 0)
            {
                MessageBox.Show(this, "No running process found.", "Kill Program", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            foreach (var p in procs)
            {
                try
                {
                    if (!p.CloseMainWindow()) p.Kill(true);
                    else if (!p.WaitForExit(3000)) p.Kill(true);
                }
                catch { }
            }
            // After kill, revert capture to desktop
            try
            {
                var desktop = Memux.UI.PerceptionViewer.GetDesktopHandle();
                if (desktop != IntPtr.Zero)
                {
                    Memux.UI.PerceptionViewer.RaiseFocusedProgram(desktop, string.Empty);
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            try { MessageBox.Show(this, ex.Message, "Kill Error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }


    private static Bitmap CreateCheckmarkIcon()
    {
        var bmp = new Bitmap(20, 20);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // Green circle background
        using var greenBrush = new SolidBrush(Color.FromArgb(76, 175, 80));
        g.FillEllipse(greenBrush, 0, 0, 20, 20);
        
        // White checkmark
        using var whitePen = new Pen(Color.White, 2);
        g.DrawLine(whitePen, 6, 10, 9, 13);
        g.DrawLine(whitePen, 9, 13, 14, 6);
        
        return bmp;
    }

    // Depth model download handlers
    private async void DownloadDepthBtn_Click(object? sender, EventArgs e)
    {
        Console.WriteLine("[Depth Download] Starting download...");
        _downloadDepthBtn.Visible = false;
        _cancelDepthBtn.Visible = true;
        _depthDownloadPb.Visible = true;
        _depthDownloadLbl.Visible = true;
        _depthDownloadLbl.Text = "Downloading...";

        try
        {
            // TODO: Implement depth model download
            await Task.Delay(2000); // Simulate download
            _depthDownloadLbl.Text = "Download completed!";
            _depthStatusIcon.Image = CreateCheckmarkIcon();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Depth Download] Exception: {ex.Message}");
            _depthDownloadLbl.Text = $"Error: {ex.Message}";
            ShowDepthDownloadButton();
        }
    }

    private void CancelDepthBtn_Click(object? sender, EventArgs e)
    {
        Console.WriteLine("[Depth Download] User cancelled download");
        _depthDownloadLbl.Text = "Download cancelled";
        ShowDepthDownloadButton();
    }

    private void ShowDepthDownloadButton()
    {
        _downloadDepthBtn.Visible = true;
        _cancelDepthBtn.Visible = false;
        _depthDownloadPb.Visible = false;
        _depthDownloadLbl.Visible = false;
    }

    // Object model download handlers
    private async void DownloadObjectBtn_Click(object? sender, EventArgs e)
    {
        Console.WriteLine("[Object Download] Starting download...");
        _downloadObjectBtn.Visible = false;
        _cancelObjectBtn.Visible = true;
        _objectDownloadPb.Visible = true;
        _objectDownloadLbl.Visible = true;
        _objectDownloadLbl.Text = "Downloading...";

        try
        {
            // TODO: Implement object model download
            await Task.Delay(2000); // Simulate download
            _objectDownloadLbl.Text = "Download completed!";
            _objectStatusIcon.Image = CreateCheckmarkIcon();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Object Download] Exception: {ex.Message}");
            _objectDownloadLbl.Text = $"Error: {ex.Message}";
            ShowObjectDownloadButton();
        }
    }

    private void CancelObjectBtn_Click(object? sender, EventArgs e)
    {
        Console.WriteLine("[Object Download] User cancelled download");
        _objectDownloadLbl.Text = "Download cancelled";
        ShowObjectDownloadButton();
    }

    private void ShowObjectDownloadButton()
    {
        _downloadObjectBtn.Visible = true;
        _cancelObjectBtn.Visible = false;
        _objectDownloadPb.Visible = false;
        _objectDownloadLbl.Visible = false;
    }

    private static bool IsProcessElevated()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}



