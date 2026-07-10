using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class OverlayForm : Form
{
    private readonly Bitmap _desktopSnapshot;
    private Point _startPoint;
    private Point _currentPoint;
    private bool _isDragging;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle? SelectedRegion { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedWindowTitle { get; private set; }

    public OverlayForm()
    {
        _desktopSnapshot = ScreenCapture.CaptureVirtualScreen();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;

        Bounds = SystemInformation.VirtualScreen;

        KeyDown += OnKeyDown;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        Paint += OnPaint;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelCapture();
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CancelCapture();
            return;
        }
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _isDragging = true;
        _startPoint = e.Location;
        _currentPoint = e.Location;
        Invalidate();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _currentPoint = e.Location;
        Invalidate();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CancelCapture();
            return;
        }
        if (e.Button != MouseButtons.Left || !_isDragging)
        {
            return;
        }

        _isDragging = false;
        _currentPoint = e.Location;
        var selection = GetSelectionRect();

        if (selection.Width < 3 && selection.Height < 3)
        {
            CompleteWindowCapture(e.Location);
            return;
        }

        CompleteCapture(new Rectangle(
            Bounds.Left + selection.Left,
            Bounds.Top + selection.Top,
            selection.Width,
            selection.Height));
    }

    private void CompleteWindowCapture(Point clientPoint)
    {
        var screenPoint = new Point(Bounds.Left + clientPoint.X, Bounds.Top + clientPoint.Y);

        // WindowFromPoint must see the application under this overlay.
        Hide();
        var target = WindowCapture.FindAt(
            screenPoint,
            clientAreaOnly: Control.ModifierKeys.HasFlag(Keys.Control));
        if (target is null)
        {
            CancelCapture();
            return;
        }

        SelectedWindowTitle = target.Value.Title;
        CompleteCapture(target.Value.Bounds);
    }

    private void CompleteCapture(Rectangle region)
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        region = Rectangle.Intersect(region, virtualScreen);
        if (region.Width < 1 || region.Height < 1)
        {
            CancelCapture();
            return;
        }

        SelectedRegion = region;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelCapture()
    {
        SelectedRegion = null;
        SelectedWindowTitle = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private Rectangle GetSelectionRect()
    {
        var x = Math.Min(_startPoint.X, _currentPoint.X);
        var y = Math.Min(_startPoint.Y, _currentPoint.Y);
        var width = Math.Abs(_startPoint.X - _currentPoint.X);
        var height = Math.Abs(_startPoint.Y - _currentPoint.Y);
        return new Rectangle(x, y, width, height);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(_desktopSnapshot, Point.Empty);
        graphics.CompositingMode = CompositingMode.SourceOver;

        using (var dimBrush = new SolidBrush(Color.FromArgb(105, 0, 0, 0)))
        {
            graphics.FillRectangle(dimBrush, ClientRectangle);
        }

        if (!_isDragging)
        {
            DrawInstructions(graphics);
            return;
        }

        var selection = GetSelectionRect();
        if (selection.Width < 1 || selection.Height < 1)
        {
            return;
        }

        // Paint the original pixels back into the selection, creating a true
        // undimmed cut-out instead of a translucent rectangle on an opaque form.
        graphics.DrawImage(_desktopSnapshot, selection, selection, GraphicsUnit.Pixel);
        using (var borderPen = new Pen(Color.FromArgb(0, 174, 255), 2f))
        {
            graphics.DrawRectangle(borderPen, selection);
        }

        DrawSizeLabel(graphics, selection);
    }

    private static void DrawInstructions(Graphics graphics)
    {
        const string text = "ドラッグ: 範囲  /  クリック: ウィンドウ  /  Ctrl+クリック: 内容領域  /  右クリック・Esc: キャンセル";
        using var font = new Font("Yu Gothic UI", 11f, FontStyle.Bold);
        var size = graphics.MeasureString(text, font);
        var area = new RectangleF(16, 16, size.Width + 20, size.Height + 12);
        using var background = new SolidBrush(Color.FromArgb(210, 24, 24, 28));
        using var foreground = new SolidBrush(Color.White);
        graphics.FillRectangle(background, area);
        graphics.DrawString(text, font, foreground, area.X + 10, area.Y + 6);
    }

    private void DrawSizeLabel(Graphics graphics, Rectangle selection)
    {
        var label = $"{selection.Width} × {selection.Height}";
        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        var size = graphics.MeasureString(label, font);
        var x = Math.Max(2f, selection.Right - size.Width - 7f);
        var y = selection.Bottom + 5f;
        if (y + size.Height + 6 > ClientSize.Height)
        {
            y = Math.Max(2f, selection.Top - size.Height - 9f);
        }

        using var background = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
        using var foreground = new SolidBrush(Color.White);
        graphics.FillRectangle(background, x - 4, y - 2, size.Width + 8, size.Height + 4);
        graphics.DrawString(label, font, foreground, x, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _desktopSnapshot.Dispose();
        }
        base.Dispose(disposing);
    }
}
