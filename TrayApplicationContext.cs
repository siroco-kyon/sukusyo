using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int HotKeyIdClipboard = 1;
    private const int HotKeyIdPin = 2;

    private readonly NotifyIcon _tray;
    private readonly HotKeyWindow _hotKey;
    private readonly MainForm _mainForm;
    private readonly List<PinnedWindow> _pinnedWindows = [];
    private bool _capturing;

    public TrayApplicationContext()
    {
        var icon = BuildIcon();

        _tray = new NotifyIcon
        {
            Icon = icon,
            Text = "sukusyo — Ctrl+Shift+A クリップボード / Ctrl+Shift+P ピン留め",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => StartClipboardCapture();

        _mainForm = new MainForm(icon, StartClipboardCapture, StartPinCapture, BringAllPinsToFront, CloseAllPins, ExitApp);
        _mainForm.WindowState = FormWindowState.Minimized;
        _mainForm.Show();

        _hotKey = new HotKeyWindow();
        _hotKey.HotKeyPressed += (_, e) =>
        {
            if (e.Id == HotKeyIdClipboard) StartClipboardCapture();
            else if (e.Id == HotKeyIdPin) StartPinCapture();
        };

        RegisterHotKeyOrWarn(HotKeyIdClipboard, Keys.A, "Ctrl+Shift+A");
        RegisterHotKeyOrWarn(HotKeyIdPin, Keys.P, "Ctrl+Shift+P");
    }

    private void RegisterHotKeyOrWarn(int id, Keys key, string label)
    {
        if (!_hotKey.Register(id, HotKeyWindow.Modifiers.Control | HotKeyWindow.Modifiers.Shift, key))
        {
            MessageBox.Show(
                $"ホットキー {label} を登録できませんでした。他のアプリが使用している可能性があります。",
                "sukusyo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("ウィンドウを開く", null, (_, _) => _mainForm.ShowAndActivate());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("範囲キャプチャ (Ctrl+Shift+A)", null, (_, _) => StartClipboardCapture());
        menu.Items.Add("ピン留めキャプチャ (Ctrl+Shift+P)", null, (_, _) => StartPinCapture());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("すべてのピンを最前面へ", null, (_, _) => BringAllPinsToFront());
        menu.Items.Add("すべてのピンを閉じる", null, (_, _) => CloseAllPins());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());
        return menu;
    }

    private static Icon BuildIcon()
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // rounded-square gradient background
            float pad = size * 0.06f;
            var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
            using var bgPath = RoundedRect(rect, size * 0.22f);
            using var gradient = new LinearGradientBrush(
                rect,
                Color.FromArgb(255, 64, 132, 255),
                Color.FromArgb(255, 130, 90, 255),
                45f);
            g.FillPath(gradient, bgPath);

            // viewfinder corner brackets, suggesting a region capture
            float inset = size * 0.24f;
            float arm = size * 0.16f;
            float x0 = inset, y0 = inset, x1 = size - inset, y1 = size - inset;
            using var pen = new Pen(Color.White, size * 0.075f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, [new PointF(x0, y0 + arm), new PointF(x0, y0), new PointF(x0 + arm, y0)]);
            g.DrawLines(pen, [new PointF(x1 - arm, y0), new PointF(x1, y0), new PointF(x1, y0 + arm)]);
            g.DrawLines(pen, [new PointF(x0, y1 - arm), new PointF(x0, y1), new PointF(x0 + arm, y1)]);
            g.DrawLines(pen, [new PointF(x1 - arm, y1), new PointF(x1, y1), new PointF(x1, y1 - arm)]);

            // small pin accent, representing the pin-to-screen feature
            float dotR = size * 0.11f;
            var dotCenter = new PointF(size * 0.76f, size * 0.76f);
            using var dotBrush = new SolidBrush(Color.FromArgb(255, 255, 196, 61));
            g.FillEllipse(dotBrush, dotCenter.X - dotR, dotCenter.Y - dotR, dotR * 2, dotR * 2);
            using var dotPen = new Pen(Color.White, size * 0.035f);
            g.DrawEllipse(dotPen, dotCenter.X - dotR, dotCenter.Y - dotR, dotR * 2, dotR * 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void StartClipboardCapture()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
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

    private void StartPinCapture()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            using var overlay = new OverlayForm();
            var result = overlay.ShowDialog();
            if (result != DialogResult.OK || overlay.SelectedRegion is not { } region) return;

            var bmp = ScreenCapture.CaptureRegion(region);
            PinnedWindow pin;
            try
            {
                pin = new PinnedWindow(bmp, region.Location);
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
            pin.FormClosed += (_, _) => _pinnedWindows.Remove(pin);
            pin.Show();
            _pinnedWindows.Add(pin);
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

    private void BringAllPinsToFront()
    {
        foreach (var pin in _pinnedWindows)
        {
            pin.PinToFront();
        }
    }

    private void CloseAllPins()
    {
        foreach (var pin in _pinnedWindows.ToArray())
        {
            pin.Close();
        }
    }

    private void ExitApp()
    {
        CloseAllPins();
        _mainForm.ForceClose();
        _tray.Visible = false;
        _tray.Dispose();
        _hotKey.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseAllPins();
            _mainForm.Dispose();
            _tray.Dispose();
            _hotKey.Dispose();
        }
        base.Dispose(disposing);
    }
}
