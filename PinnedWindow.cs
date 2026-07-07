using System.Drawing;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class PinnedWindow : Form
{
    private Bitmap? _bitmap;
    private Point _dragCursorOffset;
    private bool _dragging;
    private readonly PictureBox _picture;
    private readonly ToolStripMenuItem _alwaysOnTopItem;

    private const int BorderThickness = 1;

    public PinnedWindow(Bitmap bitmap, Point screenLocation)
    {
        _bitmap = bitmap;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        Padding = new Padding(BorderThickness);
        BackColor = Color.FromArgb(255, 0, 170, 255);
        // outer size includes the border padding; inner PictureBox ends up exactly
        // bitmap-sized and offset so the image aligns with where it was captured
        ClientSize = new Size(bitmap.Width + BorderThickness * 2, bitmap.Height + BorderThickness * 2);
        Location = new Point(screenLocation.X - BorderThickness, screenLocation.Y - BorderThickness);

        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = bitmap,
            SizeMode = PictureBoxSizeMode.Normal,
        };
        _picture.MouseDown += OnMouseDown;
        _picture.MouseMove += OnMouseMove;
        _picture.MouseUp += OnMouseUp;
        Controls.Add(_picture);

        _alwaysOnTopItem = new ToolStripMenuItem("最前面に固定", null, OnToggleAlwaysOnTop)
        {
            CheckOnClick = true,
            Checked = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("クリップボードにコピー", null, (_, _) => CopyToClipboard());
        menu.Items.Add("名前を付けて保存...", null, (_, _) => SaveAs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("閉じる", null, (_, _) => Close());
        _picture.ContextMenuStrip = menu;

        KeyDown += OnKeyDown;
        FormClosed += (_, _) =>
        {
            _picture.Image = null;
            _bitmap?.Dispose();
            _bitmap = null;
        };
    }

    private void OnToggleAlwaysOnTop(object? sender, EventArgs e)
    {
        TopMost = _alwaysOnTopItem.Checked;
    }

    public void PinToFront()
    {
        _alwaysOnTopItem.Checked = true;
        TopMost = false;
        TopMost = true;
        BringToFront();
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
    }

    private void CopyToClipboard()
    {
        if (_bitmap is not null)
        {
            ScreenCapture.CopyToClipboard(_bitmap);
        }
    }

    private void SaveAs()
    {
        if (_bitmap is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG 画像 (*.png)|*.png|JPEG 画像 (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",
            FileName = $"sukusyo_{DateTime.Now:yyyyMMdd_HHmmss}.png",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                ScreenCapture.Save(_bitmap, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存に失敗しました: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragCursorOffset = Point.Subtract(Cursor.Position, new Size(Location));
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if ((Control.MouseButtons & MouseButtons.Left) == 0)
        {
            // button was released outside this control (e.g. capture lost to
            // another window) and OnMouseUp never fired to clear the flag
            _dragging = false;
            return;
        }
        Location = Point.Subtract(Cursor.Position, new Size(_dragCursorOffset));
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TOOLWINDOW: keep out of Alt+Tab since this is a floating utility window
            cp.ExStyle |= 0x80;
            return cp;
        }
    }

    // don't steal focus from whatever the user was working in when the pin first appears;
    // it can still be activated normally by clicking on it afterward
    protected override bool ShowWithoutActivation => true;

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // intentionally do not call base: the pin must stay pixel-identical to the
        // bitmap it captured, so ignore the OS's suggested rescale/reposition when
        // it's dragged across monitors with different DPI scale factors
    }
}
