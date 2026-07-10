using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace Sukusyo;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int HotKeyIdClipboard = 1;
    private const int HotKeyIdPin = 2;

    private readonly NotifyIcon _tray;
    private readonly HotKeyWindow _hotKey;
    private readonly MainForm _mainForm;
    private readonly CommandRelay _commandRelay;
    private readonly AppSettings _settings;
    private readonly List<PinnedWindow> _pinnedWindows = [];
    private bool _capturing;

    public TrayApplicationContext(string[] startupArguments)
    {
        _settings = AppSettings.Load();
        var icon = BuildIcon();

        _tray = new NotifyIcon
        {
            Icon = icon,
            Text = "sukusyo — ダブルクリックでピン留めキャプチャー",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => StartPinCapture();

        _mainForm = new MainForm(
            icon,
            StartPinCapture,
            StartClipboardCapture,
            OpenImage,
            BringAllPinsToFront,
            CloseAllPins,
            ShowSettings,
            ExitApp)
        {
            WindowState = FormWindowState.Minimized,
        };
        _mainForm.Show();

        _hotKey = new HotKeyWindow();
        _hotKey.HotKeyPressed += (_, e) =>
        {
            if (e.Id == HotKeyIdClipboard)
            {
                StartClipboardCapture();
            }
            else if (e.Id == HotKeyIdPin)
            {
                StartPinCapture();
            }
        };

        RegisterHotKeyOrWarn(HotKeyIdClipboard, Keys.A, "Ctrl+Shift+A");
        RegisterHotKeyOrWarn(HotKeyIdPin, Keys.P, "Ctrl+Shift+P");

        _commandRelay = new CommandRelay();
        _commandRelay.CommandReceived += (_, command) => Dispatch(command);

        SetupJumpList();

        var initialCommand = RemoteCommand.Pin;
        if (startupArguments.Length > 0 && CommandRelay.TryParseArgument(startupArguments[0], out var parsed))
        {
            initialCommand = parsed;
        }

        // Wait until the message loop is active, then enter capture immediately.
        _mainForm.BeginInvoke(() => Dispatch(initialCommand));
    }

    private void Dispatch(RemoteCommand command)
    {
        switch (command)
        {
            case RemoteCommand.Clipboard: StartClipboardCapture(); break;
            case RemoteCommand.Pin: StartPinCapture(); break;
            case RemoteCommand.OpenImage: OpenImage(); break;
            case RemoteCommand.Settings: ShowSettings(); break;
            case RemoteCommand.BringAllToFront: BringAllPinsToFront(); break;
            case RemoteCommand.CloseAllPins: CloseAllPins(); break;
            case RemoteCommand.Exit: ExitApp(); break;
        }
    }

    private static void SetupJumpList()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        TaskbarManager.Instance.ApplicationId = "Sukusyo.App";
        var jumpList = JumpList.CreateJumpList();
        jumpList.AddUserTasks(
            MakeJumpListTask(exePath, "ピン留めキャプチャ (Ctrl+Shift+P)", "--pin"),
            MakeJumpListTask(exePath, "範囲キャプチャ (Ctrl+Shift+A)", "--clipboard"),
            MakeJumpListTask(exePath, "画像を開く", "--open"),
            MakeJumpListTask(exePath, "設定", "--settings"),
            MakeJumpListTask(exePath, "すべてのピンを最前面へ", "--bring-front"),
            MakeJumpListTask(exePath, "すべてのピンを閉じる", "--close-pins"),
            MakeJumpListTask(exePath, "終了", "--exit"));
        jumpList.Refresh();
    }

    private static JumpListLink MakeJumpListTask(string exePath, string title, string argument)
    {
        return new JumpListLink(exePath, title)
        {
            Arguments = argument,
            IconReference = new IconReference(exePath, 0),
        };
    }

    private void RegisterHotKeyOrWarn(int id, Keys key, string label)
    {
        if (_hotKey.Register(id, HotKeyWindow.Modifiers.Control | HotKeyWindow.Modifiers.Shift, key))
        {
            return;
        }

        MessageBox.Show(
            $"ホットキー {label} を登録できませんでした。ほかのアプリが使用している可能性があります。",
            "sukusyo",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("ピン留めキャプチャ (Ctrl+Shift+P)", null, (_, _) => StartPinCapture());
        menu.Items.Add("範囲キャプチャ (Ctrl+Shift+A)", null, (_, _) => StartClipboardCapture());
        menu.Items.Add("画像を開く...", null, (_, _) => OpenImage());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("すべてのピンを最前面へ", null, (_, _) => BringAllPinsToFront());
        menu.Items.Add("すべてのピンを閉じる", null, (_, _) => CloseAllPins());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定...", null, (_, _) => ShowSettings());
        menu.Items.Add("操作パネルを開く", null, (_, _) => _mainForm.ShowAndActivate());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());
        return menu;
    }

    private static Icon BuildIcon()
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var padding = size * 0.06f;
            var rectangle = new RectangleF(padding, padding, size - padding * 2, size - padding * 2);
            using var backgroundPath = RoundedRect(rectangle, size * 0.22f);
            using var gradient = new LinearGradientBrush(
                rectangle,
                Color.FromArgb(64, 132, 255),
                Color.FromArgb(130, 90, 255),
                45f);
            graphics.FillPath(gradient, backgroundPath);

            var inset = size * 0.24f;
            var arm = size * 0.16f;
            var x0 = inset;
            var y0 = inset;
            var x1 = size - inset;
            var y1 = size - inset;
            using var pen = new Pen(Color.White, size * 0.075f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            graphics.DrawLines(pen, [new PointF(x0, y0 + arm), new PointF(x0, y0), new PointF(x0 + arm, y0)]);
            graphics.DrawLines(pen, [new PointF(x1 - arm, y0), new PointF(x1, y0), new PointF(x1, y0 + arm)]);
            graphics.DrawLines(pen, [new PointF(x0, y1 - arm), new PointF(x0, y1), new PointF(x0 + arm, y1)]);
            graphics.DrawLines(pen, [new PointF(x1 - arm, y1), new PointF(x1, y1), new PointF(x1, y1 - arm)]);

            var radius = size * 0.11f;
            var center = new PointF(size * 0.76f, size * 0.76f);
            using var dotBrush = new SolidBrush(Color.FromArgb(255, 196, 61));
            using var dotPen = new Pen(Color.White, size * 0.035f);
            graphics.FillEllipse(dotBrush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            graphics.DrawEllipse(dotPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath RoundedRect(RectangleF rectangle, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void StartClipboardCapture()
    {
        RunCapture(pinResult: false);
    }

    private void StartPinCapture()
    {
        RunCapture(pinResult: true);
    }

    private void RunCapture(bool pinResult)
    {
        if (_capturing)
        {
            return;
        }

        _capturing = true;
        try
        {
            using var overlay = new OverlayForm();
            var result = overlay.ShowDialog();
            if (result != DialogResult.OK || overlay.SelectedRegion is not { } region)
            {
                return;
            }

            var bitmap = ScreenCapture.CaptureRegion(region);
            if (!pinResult)
            {
                using (bitmap)
                {
                    ScreenCapture.CopyToClipboard(bitmap);
                    TryAutoSave(bitmap, overlay.SelectedWindowTitle);
                }
                return;
            }

            try
            {
                if (_settings.AutoCopy)
                {
                    ScreenCapture.CopyToClipboard(bitmap);
                }
                TryAutoSave(bitmap, overlay.SelectedWindowTitle);
                AddPinnedWindow(new PinnedWindow(bitmap, region.Location, _settings, overlay.SelectedWindowTitle));
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"キャプチャーに失敗しました: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _capturing = false;
        }
    }

    private void TryAutoSave(Bitmap bitmap, string? title)
    {
        if (!_settings.AutoSave)
        {
            return;
        }

        try
        {
            ScreenCapture.Save(bitmap, _settings.CreateAutoSavePath(title));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"自動保存に失敗しました: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(_mainForm) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using var image = Image.FromFile(dialog.FileName);
            var bitmap = ImageOperations.Clone(image);
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            var location = new Point(
                screen.Left + Math.Max(0, (screen.Width - bitmap.Width) / 2),
                screen.Top + Math.Max(0, (screen.Height - bitmap.Height) / 2));
            AddPinnedWindow(new PinnedWindow(bitmap, location, _settings, Path.GetFileName(dialog.FileName)));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"画像を開けませんでした: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddPinnedWindow(PinnedWindow pin)
    {
        pin.FormClosed += (_, _) => _pinnedWindows.Remove(pin);
        pin.Show();
        _pinnedWindows.Add(pin);
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsDialog(_settings);
        dialog.ShowDialog(_mainForm);
    }

    private void BringAllPinsToFront()
    {
        foreach (var pin in _pinnedWindows.ToArray())
        {
            if (!pin.IsDisposed)
            {
                pin.PinToFront();
            }
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
        _commandRelay.Dispose();
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
            _commandRelay.Dispose();
        }
        base.Dispose(disposing);
    }
}
