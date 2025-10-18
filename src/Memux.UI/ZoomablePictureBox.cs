using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Memux.UI;

/// <summary>
/// PictureBox with zoom and pan capabilities
/// - Scroll wheel to zoom
/// - Click and drag to pan
/// - Reset button overlay to restore defaults
/// </summary>
public class ZoomablePictureBox : PictureBox
{
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 10.0f;
    private const float ZoomFactor = 1.1f;

    private float _zoomLevel = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private Point _lastMousePos;
    private bool _isDragging;
    private Rectangle _resetButtonBounds;
    private bool _isFirstImage = true;

    public float ZoomLevel => _zoomLevel;

    public ZoomablePictureBox()
    {
        SizeMode = PictureBoxSizeMode.Normal;
        DoubleBuffered = true;
        
        MouseWheel += OnMouseWheelScroll;
        MouseDown += OnMouseDownHandler;
        MouseMove += OnMouseMoveHandler;
        MouseUp += OnMouseUpHandler;
        Resize += OnResize;
    }

    private void OnResize(object? sender, EventArgs e)
    {
        // Recalculate fit-to-view on resize
        if (Image != null && _isFirstImage)
        {
            CalculateFitToView();
            Invalidate();
        }
    }

    private void CalculateFitToView()
    {
        if (Image == null || Width == 0 || Height == 0) return;

        // Calculate zoom to fit entire image in control
        float scaleX = (float)Width / Image.Width;
        float scaleY = (float)Height / Image.Height;
        _zoomLevel = Math.Min(scaleX, scaleY);
        _panOffset = PointF.Empty;
    }

    private void OnMouseWheelScroll(object? sender, MouseEventArgs e)
    {
        if (Image == null) return;

        _isFirstImage = false; // User interaction, no longer "first image"
        
        float oldZoom = _zoomLevel;
        
        if (e.Delta > 0)
        {
            _zoomLevel = Math.Min(_zoomLevel * ZoomFactor, MaxZoom);
        }
        else
        {
            _zoomLevel = Math.Max(_zoomLevel / ZoomFactor, MinZoom);
        }

        // Adjust pan to zoom towards mouse position
        if (oldZoom != _zoomLevel)
        {
            float zoomChange = _zoomLevel / oldZoom;
            _panOffset.X = e.X - (e.X - _panOffset.X) * zoomChange;
            _panOffset.Y = e.Y - (e.Y - _panOffset.Y) * zoomChange;
        }

        Invalidate();
    }

    private void OnMouseDownHandler(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Check if clicking on reset button
            if (_resetButtonBounds.Contains(e.Location))
            {
                ResetView();
                return;
            }

            _isFirstImage = false; // User interaction
            
            // Start panning
            _isDragging = true;
            _lastMousePos = e.Location;
            Cursor = Cursors.Hand;
        }
    }

    private void OnMouseMoveHandler(object? sender, MouseEventArgs e)
    {
        // Update cursor when hovering over reset button
        if (_resetButtonBounds.Contains(e.Location))
        {
            Cursor = Cursors.Hand;
        }
        else if (!_isDragging)
        {
            Cursor = Cursors.Default;
        }

        if (_isDragging && Image != null)
        {
            _panOffset.X += e.X - _lastMousePos.X;
            _panOffset.Y += e.Y - _lastMousePos.Y;
            _lastMousePos = e.Location;
            Invalidate();
        }
    }

    private void OnMouseUpHandler(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            Cursor = Cursors.Default;
        }
    }

    private void ResetView()
    {
        _isFirstImage = true;
        CalculateFitToView();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        if (Image == null)
        {
            DrawResetButton(e.Graphics);
            return;
        }

        // Check if this is a new image and calculate fit-to-view
        if (_isFirstImage)
        {
            CalculateFitToView();
        }

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

        // Save original state
        var originalTransform = e.Graphics.Transform;

        try
        {
            // Apply pan
            e.Graphics.TranslateTransform(_panOffset.X, _panOffset.Y);
            
            // Apply zoom
            e.Graphics.ScaleTransform(_zoomLevel, _zoomLevel);

            // Calculate position to center the image
            float x = (Width / _zoomLevel - Image.Width) / 2;
            float y = (Height / _zoomLevel - Image.Height) / 2;
            
            // Draw the image
            e.Graphics.DrawImage(Image, x, y, Image.Width, Image.Height);
        }
        finally
        {
            // Restore original transform for UI elements
            e.Graphics.Transform = originalTransform;
        }

        // Draw reset button (without transform)
        DrawResetButton(e.Graphics);
    }

    private void DrawResetButton(Graphics g)
    {
        // Position in top-right corner
        int size = 32;
        int margin = 8;
        _resetButtonBounds = new Rectangle(Width - size - margin, margin, size, size);

        // Semi-transparent background
        using (var brush = new SolidBrush(Color.FromArgb(180, 60, 60, 60)))
        {
            g.FillEllipse(brush, _resetButtonBounds);
        }

        // Border
        using (var pen = new Pen(Color.FromArgb(200, 200, 200, 200), 2))
        {
            g.DrawEllipse(pen, _resetButtonBounds);
        }

        // Draw refresh icon (circular arrow)
        using (var pen = new Pen(Color.White, 2))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.ArrowAnchor;
            
            int centerX = _resetButtonBounds.X + _resetButtonBounds.Width / 2;
            int centerY = _resetButtonBounds.Y + _resetButtonBounds.Height / 2;
            int radius = 10;
            
            // Draw arc (270 degrees starting from top)
            g.DrawArc(pen, centerX - radius, centerY - radius, radius * 2, radius * 2, -90, 270);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MouseWheel -= OnMouseWheelScroll;
            MouseDown -= OnMouseDownHandler;
            MouseMove -= OnMouseMoveHandler;
            MouseUp -= OnMouseUpHandler;
            Resize -= OnResize;
        }
        base.Dispose(disposing);
    }
}

