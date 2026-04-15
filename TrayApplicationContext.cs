using System.Drawing;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly HotKeyWindow _hotKey;
    private bool _capturing;

    public TrayApplicationContext()
    {
        _tray = new NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "sukusyo — Ctrl+Shift+A で範囲キャプチャ",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => StartCapture();

        _hotKey = new HotKeyWindow();
        _hotKey.HotKeyPressed += (_, _) => StartCapture();

        if (!_hotKey.Register(HotKeyWindow.Modifiers.Control | HotKeyWindow.Modifiers.Shift, Keys.A))
        {
            MessageBox.Show(
                "ホットキー Ctrl+Shift+A を登録できませんでした。他のアプリが使用している可能性があります。",
                "sukusyo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("範囲キャプチャ (Ctrl+Shift+A)", null, (_, _) => StartCapture());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());
        return menu;
    }

    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(255, 30, 144, 255));
            g.FillRectangle(bg, 4, 6, 24, 20);
            using var pen = new Pen(Color.White, 2);
            g.DrawRectangle(pen, 8, 11, 16, 12);
            using var dot = new SolidBrush(Color.White);
            g.FillEllipse(dot, 14, 15, 4, 4);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void StartCapture()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            // small delay so the hotkey's modifier keys are released before overlay appears
            using var overlay = new OverlayForm();
            var result = overlay.ShowDialog();
            if (result != DialogResult.OK || overlay.SelectedRegion is not { } region) return;

            using var bmp = ScreenCapture.CaptureRegion(region);
            ScreenCapture.CopyToClipboard(bmp);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"キャプチャに失敗しました: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _capturing = false;
        }
    }

    private void ExitApp()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _hotKey.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _hotKey.Dispose();
        }
        base.Dispose(disposing);
    }
}
