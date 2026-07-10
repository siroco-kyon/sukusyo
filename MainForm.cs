using System.Drawing;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class MainForm : Form
{
    private bool _allowClose;

    public MainForm(
        Icon icon,
        Action onPinCapture,
        Action onClipboardCapture,
        Action onOpenImage,
        Action onBringAllPinsToFront,
        Action onCloseAllPins,
        Action onSettings,
        Action onExit)
    {
        Text = "sukusyo";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 360);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
        };
        for (var index = 0; index < layout.RowCount; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / layout.RowCount));
        }

        layout.Controls.Add(CreateButton("ピン留めキャプチャ (Ctrl+Shift+P)", (_, _) => onPinCapture()));
        layout.Controls.Add(CreateButton("範囲キャプチャ (Ctrl+Shift+A)", (_, _) => onClipboardCapture()));
        layout.Controls.Add(CreateButton("画像を開く...", (_, _) => onOpenImage()));
        layout.Controls.Add(CreateButton("すべてのピンを最前面へ", (_, _) => onBringAllPinsToFront()));
        layout.Controls.Add(CreateButton("すべてのピンを閉じる", (_, _) => onCloseAllPins()));
        layout.Controls.Add(CreateButton("設定...", (_, _) => onSettings()));
        layout.Controls.Add(CreateButton("最小化して常駐", (_, _) => Hide()));
        layout.Controls.Add(CreateButton("終了", (_, _) => onExit()));

        Controls.Add(layout);
        FormClosing += (_, e) =>
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
        };
        button.Click += onClick;
        return button;
    }
}
