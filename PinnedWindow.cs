using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class PinnedWindow : Form
{
    private const int BorderThickness = 1;
    private const int HistoryLimit = 20;

    private Bitmap? _bitmap;
    private readonly Panel _viewport;
    private readonly PictureBox _picture;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly ToolStripMenuItem _undoItem;
    private readonly ToolStripMenuItem _redoItem;
    private readonly ToolStripMenuItem _cropItem;
    private readonly ToolStripMenuItem _removeHorizontalItem;
    private readonly ToolStripMenuItem _removeVerticalItem;
    private readonly ToolStripMenuItem _zoomMenu;
    private readonly ToolStripMenuItem _opacityMenu;
    private readonly ToolStripMenuItem _colorMenu;
    private readonly ToolStripMenuItem _widthMenu;
    private readonly System.Windows.Forms.Timer _revealTimer;
    private readonly List<Bitmap> _undoHistory = [];
    private readonly List<Bitmap> _redoHistory = [];

    private Point _dragCursorOffset;
    private Point _strokeStart;
    private Point _lastStrokePoint;
    private Point _selectionStart;
    private Point _menuImagePoint;
    private bool _moving;
    private bool _drawing;
    private bool _straightStroke;
    private bool _selecting;
    private Rectangle? _selection;
    private float _zoom = 1f;
    private int _opacityPercent;
    private Color _penColor;
    private int _penWidth;

    public PinnedWindow(Bitmap bitmap, Point screenLocation, AppSettings settings, string? title = null)
    {
        _bitmap = bitmap;
        _penColor = settings.PenColor;
        _penWidth = settings.PenWidth;
        _opacityPercent = settings.DefaultOpacityPercent;

        Text = string.IsNullOrWhiteSpace(title) ? "sukusyo" : title;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = settings.AlwaysOnTop;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        Padding = new Padding(BorderThickness);
        BackColor = Color.FromArgb(0, 174, 255);
        Location = new Point(screenLocation.X - BorderThickness, screenLocation.Y - BorderThickness);
        Opacity = _opacityPercent / 100d;

        _viewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(36, 36, 40),
        };
        _picture = new PictureBox
        {
            Image = bitmap,
            Location = Point.Empty,
            SizeMode = PictureBoxSizeMode.StretchImage,
            TabStop = false,
        };
        _picture.MouseDown += OnPictureMouseDown;
        _picture.MouseMove += OnPictureMouseMove;
        _picture.MouseUp += OnPictureMouseUp;
        _picture.MouseDoubleClick += OnPictureDoubleClick;
        _picture.Paint += OnPicturePaint;
        _viewport.Controls.Add(_picture);
        Controls.Add(_viewport);

        _alwaysOnTopItem = new ToolStripMenuItem("常に最前面", null, OnToggleAlwaysOnTop)
        {
            CheckOnClick = true,
            Checked = settings.AlwaysOnTop,
        };
        _undoItem = new ToolStripMenuItem("元に戻す", null, (_, _) => Undo()) { ShortcutKeys = Keys.Control | Keys.Z };
        _redoItem = new ToolStripMenuItem("やり直し", null, (_, _) => Redo()) { ShortcutKeys = Keys.Control | Keys.Y };
        _cropItem = new ToolStripMenuItem("トリミング", null, (_, _) => CropSelection()) { ShortcutKeys = Keys.Control | Keys.T };
        _removeHorizontalItem = new ToolStripMenuItem("横ぶっこ抜き", null, (_, _) => RemoveHorizontal()) { ShortcutKeys = Keys.Control | Keys.R };
        _removeVerticalItem = new ToolStripMenuItem("縦ぶっこ抜き", null, (_, _) => RemoveVertical()) { ShortcutKeys = Keys.Control | Keys.E };
        _zoomMenu = new ToolStripMenuItem("倍率");
        _opacityMenu = new ToolStripMenuItem("透明度");
        _colorMenu = new ToolStripMenuItem("蛍光ペンの色");
        _widthMenu = new ToolStripMenuItem("蛍光ペンの太さ");
        _menu = BuildMenu();
        _menu.Opening += OnMenuOpening;
        _picture.ContextMenuStrip = _menu;

        _revealTimer = new System.Windows.Forms.Timer
        {
            Interval = settings.HideDurationMilliseconds,
        };
        _revealTimer.Tick += (_, _) => RevealAfterTemporaryHide();

        KeyDown += OnKeyDown;
        FormClosed += OnFormClosed;
        UpdateCanvasSize(resizeWindow: true);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var fileMenu = new ToolStripMenuItem("ファイル");
        var saveItem = new ToolStripMenuItem("名前を付けて保存...", null, (_, _) => SaveAs()) { ShortcutKeys = Keys.Control | Keys.S };
        var copyItem = new ToolStripMenuItem("クリップボードにコピー", null, (_, _) => CopyToClipboard()) { ShortcutKeys = Keys.Control | Keys.C };
        fileMenu.DropDownItems.Add(saveItem);
        fileMenu.DropDownItems.Add(copyItem);

        var editMenu = new ToolStripMenuItem("編集");
        editMenu.DropDownItems.Add(_undoItem);
        editMenu.DropDownItems.Add(_redoItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add("テキストを挿入...", null, (_, _) => InsertText());
        editMenu.DropDownItems.Add(_cropItem);
        editMenu.DropDownItems.Add(_removeHorizontalItem);
        editMenu.DropDownItems.Add(_removeVerticalItem);

        var rotateMenu = new ToolStripMenuItem("回転・反転");
        rotateMenu.DropDownItems.Add("左へ90°回転", null, (_, _) => ApplyOperation(image => ImageOperations.RotateFlip(image, RotateFlipType.Rotate270FlipNone)));
        rotateMenu.DropDownItems.Add("右へ90°回転", null, (_, _) => ApplyOperation(image => ImageOperations.RotateFlip(image, RotateFlipType.Rotate90FlipNone)));
        rotateMenu.DropDownItems.Add("左右反転", null, (_, _) => ApplyOperation(image => ImageOperations.RotateFlip(image, RotateFlipType.RotateNoneFlipX)));
        rotateMenu.DropDownItems.Add("上下反転", null, (_, _) => ApplyOperation(image => ImageOperations.RotateFlip(image, RotateFlipType.RotateNoneFlipY)));

        var joinMenu = new ToolStripMenuItem("クリップボード画像を連結");
        joinMenu.DropDownItems.Add("上に連結", null, (_, _) => JoinClipboard(JoinDirection.Above));
        joinMenu.DropDownItems.Add("下に連結", null, (_, _) => JoinClipboard(JoinDirection.Below));
        joinMenu.DropDownItems.Add("左に連結", null, (_, _) => JoinClipboard(JoinDirection.Left));
        joinMenu.DropDownItems.Add("右に連結", null, (_, _) => JoinClipboard(JoinDirection.Right));

        AddZoomItem("50%", 0.5f);
        AddZoomItem("75%", 0.75f);
        AddZoomItem("100%", 1f);
        AddZoomItem("125%", 1.25f);
        AddZoomItem("150%", 1.5f);
        AddZoomItem("200%", 2f);
        AddZoomItem("300%", 3f);
        AddZoomItem("500%", 5f);

        AddOpacityItem("25%", 25);
        AddOpacityItem("50%", 50);
        AddOpacityItem("75%", 75);
        AddOpacityItem("100%", 100);

        AddColorItem("黄", Color.Yellow);
        AddColorItem("赤", Color.Red);
        AddColorItem("青", Color.DeepSkyBlue);
        AddColorItem("緑", Color.LimeGreen);
        AddColorItem("白", Color.White);
        AddColorItem("黒", Color.Black);
        _colorMenu.DropDownItems.Add(new ToolStripSeparator());
        _colorMenu.DropDownItems.Add("その他...", null, (_, _) => ChoosePenColor());

        foreach (var width in new[] { 4, 8, 12, 20 })
        {
            var item = new ToolStripMenuItem($"{width}px", null, (_, _) => SetPenWidth(width)) { Tag = width };
            _widthMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(rotateMenu);
        menu.Items.Add(joinMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_colorMenu);
        menu.Items.Add(_widthMenu);
        menu.Items.Add(_zoomMenu);
        menu.Items.Add(_opacityMenu);
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("一時的に隠す", null, (_, _) => HideTemporarily());
        menu.Items.Add(new ToolStripMenuItem("閉じる (Esc)", null, (_, _) => Close()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("操作: Ctrl+ドラッグ=蛍光ペン / Shift+ドラッグ=範囲選択 / ダブルクリック=一時退避")
        {
            Enabled = false,
        });
        return menu;
    }

    private void OnToggleAlwaysOnTop(object? sender, EventArgs e)
    {
        TopMost = _alwaysOnTopItem.Checked;
    }

    private void AddZoomItem(string label, float zoom)
    {
        var item = new ToolStripMenuItem(label, null, (_, _) => SetZoom(zoom)) { Tag = zoom };
        _zoomMenu.DropDownItems.Add(item);
    }

    private void AddOpacityItem(string label, int opacity)
    {
        var item = new ToolStripMenuItem(label, null, (_, _) => SetOpacityPercent(opacity)) { Tag = opacity };
        _opacityMenu.DropDownItems.Add(item);
    }

    private void AddColorItem(string label, Color color)
    {
        var item = new ToolStripMenuItem(label, null, (_, _) => SetPenColor(color))
        {
            Tag = color.ToArgb(),
            BackColor = color,
            ForeColor = color.GetBrightness() < 0.45f ? Color.White : Color.Black,
        };
        _colorMenu.DropDownItems.Add(item);
    }

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        _undoItem.Enabled = _undoHistory.Count > 0;
        _redoItem.Enabled = _redoHistory.Count > 0;
        var hasSelection = _selection is { Width: > 0, Height: > 0 };
        _cropItem.Enabled = hasSelection;
        _removeHorizontalItem.Enabled = hasSelection;
        _removeVerticalItem.Enabled = hasSelection;
        UpdateMenuChecks();
    }

    private void UpdateMenuChecks()
    {
        foreach (ToolStripItem item in _zoomMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && item.Tag is float value)
            {
                menuItem.Checked = Math.Abs(value - _zoom) < 0.001f;
            }
        }
        foreach (ToolStripItem item in _opacityMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && item.Tag is int value)
            {
                menuItem.Checked = value == _opacityPercent;
            }
        }
        foreach (ToolStripItem item in _colorMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && item.Tag is int argb)
            {
                menuItem.Checked = argb == _penColor.ToArgb();
            }
        }
        foreach (ToolStripItem item in _widthMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && item.Tag is int width)
            {
                menuItem.Checked = width == _penWidth;
            }
        }
    }

    private void OnPictureMouseDown(object? sender, MouseEventArgs e)
    {
        _menuImagePoint = ToImagePoint(e.Location);
        if (e.Button != MouseButtons.Left || _bitmap is null)
        {
            return;
        }

        var modifiers = Control.ModifierKeys;
        if (modifiers.HasFlag(Keys.Control))
        {
            PushUndo();
            _drawing = true;
            _straightStroke = modifiers.HasFlag(Keys.Shift);
            _strokeStart = ToImagePoint(e.Location);
            _lastStrokePoint = _strokeStart;
            _picture.Capture = true;
            return;
        }

        if (modifiers.HasFlag(Keys.Shift))
        {
            _selecting = true;
            _selectionStart = ToImagePoint(e.Location);
            _selection = new Rectangle(_selectionStart, Size.Empty);
            _picture.Capture = true;
            _picture.Invalidate();
            return;
        }

        _moving = true;
        _dragCursorOffset = Point.Subtract(Cursor.Position, new Size(Location));
        _picture.Capture = true;
    }

    private void OnPictureMouseMove(object? sender, MouseEventArgs e)
    {
        if (_bitmap is null)
        {
            return;
        }

        if (_drawing)
        {
            var current = ToImagePoint(e.Location);
            if (_straightStroke)
            {
                _lastStrokePoint = current;
                _picture.Invalidate();
            }
            else
            {
                DrawHighlight(_lastStrokePoint, current);
                _lastStrokePoint = current;
                _picture.Invalidate();
            }
            return;
        }

        if (_selecting)
        {
            _selection = NormalizeRectangle(_selectionStart, ToImagePoint(e.Location));
            _picture.Invalidate();
            return;
        }

        if (_moving)
        {
            if ((Control.MouseButtons & MouseButtons.Left) == 0)
            {
                EndPointerOperation();
                return;
            }
            Location = Point.Subtract(Cursor.Position, new Size(_dragCursorOffset));
        }
    }

    private void OnPictureMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_drawing)
        {
            var end = ToImagePoint(e.Location);
            if (_straightStroke)
            {
                DrawHighlight(_strokeStart, end);
            }
            else if (end == _strokeStart)
            {
                DrawHighlight(end, end);
            }
            _drawing = false;
            _picture.Invalidate();
        }
        else if (_selecting)
        {
            _selection = NormalizeRectangle(_selectionStart, ToImagePoint(e.Location));
            if (_selection.Value.Width < 2 || _selection.Value.Height < 2)
            {
                _selection = null;
            }
            _selecting = false;
            _picture.Invalidate();
        }

        _moving = false;
        _picture.Capture = false;
    }

    private void OnPictureDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            EndPointerOperation();
            HideTemporarily();
        }
    }

    private void EndPointerOperation()
    {
        _moving = false;
        _drawing = false;
        _selecting = false;
        _picture.Capture = false;
    }

    private void OnPicturePaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_selection is { } selection)
        {
            var scaled = ScaleRectangle(selection);
            using var shadow = new Pen(Color.Black, 3f) { DashStyle = DashStyle.Dash };
            using var border = new Pen(Color.White, 1f) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawRectangle(shadow, scaled);
            e.Graphics.DrawRectangle(border, scaled);
        }

        if (_drawing && _straightStroke)
        {
            using var preview = CreateHighlightPen(_penWidth * _zoom);
            e.Graphics.DrawLine(preview, ScalePoint(_strokeStart), ScalePoint(_lastStrokePoint));
        }
    }

    private void DrawHighlight(Point from, Point to)
    {
        if (_bitmap is null)
        {
            return;
        }

        using var graphics = Graphics.FromImage(_bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = CreateHighlightPen(_penWidth);
        if (from == to)
        {
            using var brush = new SolidBrush(pen.Color);
            var radius = _penWidth / 2f;
            graphics.FillEllipse(brush, from.X - radius, from.Y - radius, _penWidth, _penWidth);
        }
        else
        {
            graphics.DrawLine(pen, from, to);
        }
    }

    private Pen CreateHighlightPen(float width)
    {
        return new Pen(Color.FromArgb(105, _penColor), Math.Max(1f, width))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
    }

    private Point ToImagePoint(Point point)
    {
        if (_bitmap is null)
        {
            return Point.Empty;
        }
        return new Point(
            Math.Clamp((int)Math.Floor(point.X / _zoom), 0, Math.Max(0, _bitmap.Width - 1)),
            Math.Clamp((int)Math.Floor(point.Y / _zoom), 0, Math.Max(0, _bitmap.Height - 1)));
    }

    private PointF ScalePoint(Point point) => new(point.X * _zoom, point.Y * _zoom);

    private Rectangle ScaleRectangle(Rectangle rectangle) => Rectangle.FromLTRB(
        (int)Math.Round(rectangle.Left * _zoom),
        (int)Math.Round(rectangle.Top * _zoom),
        (int)Math.Round(rectangle.Right * _zoom),
        (int)Math.Round(rectangle.Bottom * _zoom));

    private static Rectangle NormalizeRectangle(Point start, Point end)
    {
        return Rectangle.FromLTRB(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            SaveAs();
        }
        else if (e.Control && e.KeyCode == Keys.C)
        {
            CopyToClipboard();
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            Redo();
        }
        else if (e.Control && e.KeyCode == Keys.T)
        {
            CropSelection();
        }
        else if (e.Control && e.KeyCode == Keys.R)
        {
            RemoveHorizontal();
        }
        else if (e.Control && e.KeyCode == Keys.E)
        {
            RemoveVertical();
        }
        else if (e.KeyCode == Keys.PageUp)
        {
            SetOpacityPercent(_opacityPercent + 5);
        }
        else if (e.KeyCode == Keys.PageDown)
        {
            SetOpacityPercent(_opacityPercent - 5);
        }
    }

    public void PinToFront()
    {
        _alwaysOnTopItem.Checked = true;
        TopMost = false;
        TopMost = true;
        BringToFront();
    }

    private void CopyToClipboard()
    {
        if (_bitmap is null)
        {
            return;
        }
        try
        {
            ScreenCapture.CopyToClipboard(_bitmap);
        }
        catch (Exception ex)
        {
            ShowError("クリップボードへコピーできませんでした", ex);
        }
    }

    private void SaveAs()
    {
        if (_bitmap is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG 画像 (*.png)|*.png|JPEG 画像 (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",
            FileName = $"sukusyo_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ScreenCapture.Save(_bitmap, dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError("画像を保存できませんでした", ex);
        }
    }

    private void InsertText()
    {
        if (_bitmap is null)
        {
            return;
        }

        using var dialog = new TextInputDialog(_penColor);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dialog.TextValue))
        {
            return;
        }

        PushUndo();
        using var graphics = Graphics.FromImage(_bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(dialog.SelectedTextColor);
        graphics.DrawString(dialog.TextValue, dialog.SelectedTextFont, brush, _menuImagePoint);
        _picture.Invalidate();
    }

    private void CropSelection()
    {
        if (_bitmap is null || _selection is not { } selection)
        {
            return;
        }
        ApplyOperation(image => ImageOperations.Crop(image, selection));
    }

    private void RemoveHorizontal()
    {
        if (_bitmap is null || _selection is not { } selection)
        {
            return;
        }
        ApplyOperation(image => ImageOperations.RemoveHorizontalStrip(image, selection));
    }

    private void RemoveVertical()
    {
        if (_bitmap is null || _selection is not { } selection)
        {
            return;
        }
        ApplyOperation(image => ImageOperations.RemoveVerticalStrip(image, selection));
    }

    private void JoinClipboard(JoinDirection direction)
    {
        if (_bitmap is null)
        {
            return;
        }

        try
        {
            if (!Clipboard.ContainsImage())
            {
                MessageBox.Show(this, "クリップボードに画像がありません。", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var clipboardImage = Clipboard.GetImage();
            if (clipboardImage is null)
            {
                return;
            }
            ApplyOperation(image => ImageOperations.Join(image, clipboardImage, direction));
        }
        catch (Exception ex)
        {
            ShowError("画像を連結できませんでした", ex);
        }
    }

    private void ApplyOperation(Func<Bitmap, Bitmap> operation)
    {
        if (_bitmap is null)
        {
            return;
        }

        try
        {
            var replacement = operation(_bitmap);
            PushUndo();
            ReplaceBitmap(replacement);
        }
        catch (Exception ex)
        {
            ShowError("画像を編集できませんでした", ex);
        }
    }

    private void PushUndo()
    {
        if (_bitmap is null)
        {
            return;
        }
        AddHistory(_undoHistory, ImageOperations.Clone(_bitmap));
        ClearHistory(_redoHistory);
    }

    private static void AddHistory(List<Bitmap> history, Bitmap bitmap)
    {
        history.Add(bitmap);
        if (history.Count > HistoryLimit)
        {
            history[0].Dispose();
            history.RemoveAt(0);
        }
    }

    private void Undo()
    {
        if (_bitmap is null || _undoHistory.Count == 0)
        {
            return;
        }

        AddHistory(_redoHistory, ImageOperations.Clone(_bitmap));
        var replacement = TakeLast(_undoHistory);
        ReplaceBitmap(replacement);
    }

    private void Redo()
    {
        if (_bitmap is null || _redoHistory.Count == 0)
        {
            return;
        }

        AddHistory(_undoHistory, ImageOperations.Clone(_bitmap));
        var replacement = TakeLast(_redoHistory);
        ReplaceBitmap(replacement);
    }

    private static Bitmap TakeLast(List<Bitmap> history)
    {
        var index = history.Count - 1;
        var bitmap = history[index];
        history.RemoveAt(index);
        return bitmap;
    }

    private void ReplaceBitmap(Bitmap replacement)
    {
        _picture.Image = null;
        _bitmap?.Dispose();
        _bitmap = replacement;
        _picture.Image = replacement;
        _selection = null;
        UpdateCanvasSize(resizeWindow: true);
        _picture.Invalidate();
    }

    private void SetZoom(float zoom)
    {
        _zoom = Math.Clamp(zoom, 0.5f, 5f);
        UpdateCanvasSize(resizeWindow: true);
        _picture.Invalidate();
    }

    private void UpdateCanvasSize(bool resizeWindow)
    {
        if (_bitmap is null)
        {
            return;
        }

        var scaledSize = new Size(
            Math.Max(1, (int)Math.Round(_bitmap.Width * _zoom)),
            Math.Max(1, (int)Math.Round(_bitmap.Height * _zoom)));
        _picture.Size = scaledSize;
        _viewport.AutoScrollMinSize = scaledSize;

        if (!resizeWindow)
        {
            return;
        }

        var workingArea = Screen.FromPoint(Location).WorkingArea;
        var maxWidth = Math.Max(120, (int)(workingArea.Width * 0.9));
        var maxHeight = Math.Max(80, (int)(workingArea.Height * 0.9));
        ClientSize = new Size(
            Math.Min(scaledSize.Width + BorderThickness * 2, maxWidth),
            Math.Min(scaledSize.Height + BorderThickness * 2, maxHeight));
    }

    private void SetOpacityPercent(int percent)
    {
        _opacityPercent = Math.Clamp(percent, 25, 100);
        Opacity = _opacityPercent / 100d;
    }

    private void SetPenColor(Color color)
    {
        _penColor = color;
        UpdateMenuChecks();
    }

    private void ChoosePenColor()
    {
        using var dialog = new ColorDialog { Color = _penColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetPenColor(dialog.Color);
        }
    }

    private void SetPenWidth(int width)
    {
        _penWidth = Math.Clamp(width, 2, 64);
        UpdateMenuChecks();
    }

    private void HideTemporarily()
    {
        _revealTimer.Stop();
        Hide();
        _revealTimer.Start();
    }

    private void RevealAfterTemporaryHide()
    {
        _revealTimer.Stop();
        Show();
        if (_alwaysOnTopItem.Checked)
        {
            TopMost = false;
            TopMost = true;
        }
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, $"{message}: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _revealTimer.Stop();
        _picture.Image = null;
        _bitmap?.Dispose();
        _bitmap = null;
        ClearHistory(_undoHistory);
        ClearHistory(_redoHistory);
    }

    private static void ClearHistory(List<Bitmap> history)
    {
        foreach (var bitmap in history)
        {
            bitmap.Dispose();
        }
        history.Clear();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            // WS_EX_TOOLWINDOW keeps the lightweight image notes out of Alt+Tab.
            createParams.ExStyle |= 0x80;
            return createParams;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // A capture is a pixel artifact. Keep it pixel-identical when moving it
        // between monitors that use different scaling factors.
    }
}
