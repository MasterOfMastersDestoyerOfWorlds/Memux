using System.Drawing;
using System.Windows.Forms;

namespace Memux.UI;

/// <summary>
/// Steam-like notification system for new skills
/// Shows popup in bottom-right corner of screen
/// </summary>
public class NotificationManager
{
    private readonly List<NotificationForm> _activeNotifications = new();
    private readonly object _lock = new();
    
    public void ShowSkillNotification(string skillName, string description)
    {
        if (Application.OpenForms.Count == 0)
        {
            // Start message loop if not already running
            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ShowNotificationInternal(skillName, description);
                Application.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        else
        {
            // Use existing message loop
            var form = Application.OpenForms[0];
            form?.Invoke((Action)(() => ShowNotificationInternal(skillName, description)));
        }
    }
    
    private void ShowNotificationInternal(string skillName, string description)
    {
        var notification = new NotificationForm(skillName, description);
        
        lock (_lock)
        {
            // Position based on existing notifications
            int yOffset = _activeNotifications.Count * (notification.Height + 10);
            notification.SetPosition(yOffset);
            
            _activeNotifications.Add(notification);
            
            notification.FormClosed += (s, e) =>
            {
                lock (_lock)
                {
                    _activeNotifications.Remove(notification);
                    RepositionNotifications();
                }
            };
        }
        
        notification.Show();
    }
    
    private void RepositionNotifications()
    {
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            _activeNotifications[i].SetPosition(i * (_activeNotifications[i].Height + 10));
        }
    }
}

internal class NotificationForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private int _opacity = 255;
    
    public NotificationForm(string skillName, string description)
    {
        // Form setup
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        Size = new Size(350, 100);
        BackColor = Color.FromArgb(30, 30, 30);
        
        // Title label
        var titleLabel = new Label
        {
            Text = "New Skill Discovered!",
            ForeColor = Color.FromArgb(173, 216, 230),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = false,
            Size = new Size(330, 25),
            Location = new Point(10, 10),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(titleLabel);
        
        // Skill name label
        var nameLabel = new Label
        {
            Text = skillName,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = false,
            Size = new Size(330, 25),
            Location = new Point(10, 35),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(nameLabel);
        
        // Description label
        var descLabel = new Label
        {
            Text = description,
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Segoe UI", 8),
            AutoSize = false,
            Size = new Size(330, 25),
            Location = new Point(10, 60),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(descLabel);
        
        // Border
        Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(100, 100, 100), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
        
        // Click to dismiss
        Click += (s, e) => Close();
        foreach (Control control in Controls)
        {
            control.Click += (s, e) => Close();
        }
        
        // Auto-dismiss after 5 seconds with fade
        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (s, e) =>
        {
            _opacity -= 5;
            if (_opacity <= 0)
            {
                _timer.Stop();
                Close();
            }
            else
            {
                Opacity = _opacity / 255.0;
            }
        };
        
        // Start fade after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                Invoke(() => _timer.Start());
            }
        });
    }
    
    public void SetPosition(int yOffset)
    {
        var screen = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(
            screen.Right - Width - 20,
            screen.Bottom - Height - 20 - yOffset
        );
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

