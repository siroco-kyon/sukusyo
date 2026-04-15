using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class OverlayForm : Form
{
    private Point _startPoint;
    private Point _currentPoint;
    private bool _isDragging;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle? SelectedRegion { get; private set; }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        BackColor = Color.Black;
        Opacity = 0.35;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;

        var vs = SystemInformation.VirtualScreen;
        Bounds = vs;

        KeyDown += OnKey;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        Paint += OnPaint;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            SelectedRegion = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _isDragging = true;
        _startPoint = e.Location;
        _currentPoint = e.Location;
        Invalidate();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        _currentPoint = e.Location;
        Invalidate();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_isDragging) return;
        _isDragging = false;
        _currentPoint = e.Location;

        var rectClient = GetSelectionRect();
        if (rectClient.Width < 3 || rectClient.Height < 3)
        {
            SelectedRegion = null;
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        // convert client coords to screen coords (virtual screen pixels)
        var topLeft = PointToScreen(rectClient.Location);
        SelectedRegion = new Rectangle(topLeft, rectClient.Size);
        DialogResult = DialogResult.OK;
        Close();
    }

    private Rectangle GetSelectionRect()
    {
        int x = Math.Min(_startPoint.X, _currentPoint.X);
        int y = Math.Min(_startPoint.Y, _currentPoint.Y);
        int w = Math.Abs(_startPoint.X - _currentPoint.X);
        int h = Math.Abs(_startPoint.Y - _currentPoint.Y);
        return new Rectangle(x, y, w, h);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if (!_isDragging) return;

        var rect = GetSelectionRect();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // clear the selected area to appear "cut out" from the dim overlay
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;

        // fill selection area with a fully-transparent hole effect:
        // since Opacity is set on the form, we draw a lighter fill + border
        using (var clearBrush = new SolidBrush(Color.FromArgb(1, 0, 0, 0)))
        {
            g.FillRectangle(clearBrush, rect);
        }
        using (var pen = new Pen(Color.FromArgb(255, 0, 170, 255), 2))
        {
            g.DrawRectangle(pen, rect);
        }

        // size label
        string label = $"{rect.Width} × {rect.Height}";
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        var textSize = g.MeasureString(label, font);
        float tx = rect.Right - textSize.Width - 4;
        float ty = rect.Bottom + 4;
        if (ty + textSize.Height > ClientSize.Height) ty = rect.Top - textSize.Height - 4;
        if (tx < 0) tx = rect.Left + 4;

        using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
        {
            g.FillRectangle(bg, tx - 3, ty - 2, textSize.Width + 6, textSize.Height + 4);
        }
        using (var fg = new SolidBrush(Color.White))
        {
            g.DrawString(label, font, fg, tx, ty);
        }
    }
}
