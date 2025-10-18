using System.Drawing;
using System.Windows.Forms;
using Memux.Core.Models;
using System.Threading.Tasks;
using Memux.Core;

namespace Memux.UI;

public class PerceptionViewerForm : Form
{
    private readonly PictureBox _depthPicture;
    private readonly PictureBox _objectsPicture;
    private readonly PictureBox _ocrPicture;
    private readonly Label _goalLabel;
    private readonly ProgressBar _goalProgress;
    private readonly ListBox _subGoalsList;
    private readonly Label _nextSkillLabel;
    private readonly TreeView _subskillsTree;
    private readonly ProgressBar _depthModelPb;
    private readonly ProgressBar _objectsModelPb;
    private readonly ProgressBar _ocrModelPb;
    private readonly Label _depthModelLbl;
    private readonly Label _objectsModelLbl;
    private readonly Label _ocrModelLbl;
    private readonly ListBox _ocrTextList;
    private readonly ListView _programsList;
    private readonly Button _refreshProgramsBtn;
    private readonly Button _launchProgramBtn;

    public PerceptionViewerForm()
    {
        Text = "Memux Perception Viewer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(24, 24, 24);
        ForeColor = Color.White;
        Size = new Size(1280, 800);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        TopMost = true; // ensure visible on first show

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

        left.Controls.Add(WrapWithLabeledPanel("Depth Map", _depthPicture), 0, 0);
        left.Controls.Add(WrapWithLabeledPanel("Objects", _objectsPicture), 0, 1);

        var ocrSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        ocrSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        ocrSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        ocrSplit.Controls.Add(_ocrPicture, 0, 0);
        _ocrTextList = new ListBox { Dock = DockStyle.Fill };
        ocrSplit.Controls.Add(_ocrTextList, 1, 0);
        left.Controls.Add(WrapWithLabeledPanel("OCR", ocrSplit), 0, 2);

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

        var goalsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 10,
        };
        for (int i = 0; i < 9; i++) goalsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        goalsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        goalsGroup.Controls.Add(goalsPanel);

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
        _depthModelPb = new ProgressBar { Dock = DockStyle.Top, Height = 16, Minimum = 0, Maximum = 100 };
        goalsPanel.Controls.Add(_depthModelLbl, 0, 4);
        goalsPanel.Controls.Add(_depthModelPb, 0, 5);

        _objectsModelLbl = new Label { Text = "Objects model:", Dock = DockStyle.Top, AutoSize = true };
        _objectsModelPb = new ProgressBar { Dock = DockStyle.Top, Height = 16, Minimum = 0, Maximum = 100 };
        goalsPanel.Controls.Add(_objectsModelLbl, 0, 6);
        goalsPanel.Controls.Add(_objectsModelPb, 0, 7);

        _ocrModelLbl = new Label { Text = "OCR model:", Dock = DockStyle.Top, AutoSize = true };
        _ocrModelPb = new ProgressBar { Dock = DockStyle.Top, Height = 16, Minimum = 0, Maximum = 100 };
        goalsPanel.Controls.Add(_ocrModelLbl, 0, 8);
        goalsPanel.Controls.Add(_ocrModelPb, 0, 9);

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
        _refreshProgramsBtn = new Button { Text = "Refresh", AutoSize = true };
        buttonsPanel.Controls.Add(_launchProgramBtn);
        buttonsPanel.Controls.Add(_refreshProgramsBtn);
        programsPanel.Controls.Add(buttonsPanel, 0, 2);
        planPanel.Controls.Add(programsPanel, 0, 2);

        _refreshProgramsBtn.Click += (_, __) => RefreshPrograms();
        _launchProgramBtn.Click += async (_, __) => await LaunchSelectedProgramAsync();

        // Drop TopMost after a short delay to avoid staying above everything
        Task.Delay(800).ContinueWith(_ =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                try { Invoke(() => TopMost = false); } catch { }
            }
        });
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
        _depthModelPb.Value = Math.Clamp(progress.DepthPercent, 0, 100);
        _objectsModelPb.Value = Math.Clamp(progress.ObjectsPercent, 0, 100);
        _ocrModelPb.Value = Math.Clamp(progress.OcrPercent, 0, 100);
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
                return;
            }
            if (!File.Exists(rec.ExePath))
            {
                MessageBox.Show(this, "Executable not found.", "Program Launch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var pm = new Memux.Core.ProcessManager(rec.ExePath);
            pm.Launch(rec.LaunchArgs, rec.RunAsAdmin);
            await Task.Delay(1000);
            var h = pm.GetMainWindowHandle();
            if (h != IntPtr.Zero)
            {
                WindowFocusHelper.TryFocusWindow(h);
            }
        }
        catch (Exception ex)
        {
            try { MessageBox.Show(this, ex.Message, "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }
}



