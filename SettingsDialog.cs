using System.Drawing;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class SettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly CheckBox _autoCopy;
    private readonly CheckBox _autoSave;
    private readonly TextBox _autoSaveDirectory;
    private readonly CheckBox _alwaysOnTop;
    private readonly NumericUpDown _opacity;
    private readonly NumericUpDown _hideDuration;
    private readonly NumericUpDown _penWidth;
    private readonly Button _penColor;
    private Color _selectedPenColor;

    public SettingsDialog(AppSettings settings)
    {
        _settings = settings;
        _selectedPenColor = settings.PenColor;

        Text = "sukusyo の設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 355);
        Padding = new Padding(16);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (var index = 0; index < table.RowCount; index++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }

        _autoCopy = new CheckBox { Text = "キャプチャー後に自動コピー", Checked = settings.AutoCopy, AutoSize = true };
        _autoSave = new CheckBox { Text = "キャプチャー後に自動保存", Checked = settings.AutoSave, AutoSize = true };
        _autoSaveDirectory = new TextBox { Text = settings.AutoSaveDirectory, Dock = DockStyle.Fill };
        var browse = new Button { Text = "参照...", Dock = DockStyle.Fill };
        browse.Click += (_, _) => BrowseDirectory();
        _alwaysOnTop = new CheckBox { Text = "付箋を常に最前面へ表示", Checked = settings.AlwaysOnTop, AutoSize = true };
        _opacity = CreateNumber(settings.DefaultOpacityPercent, 25, 100, 5, "%");
        _hideDuration = CreateNumber(settings.HideDurationMilliseconds, 250, 10000, 250, "ms");
        _penWidth = CreateNumber(settings.PenWidth, 2, 64, 2, "px");
        _penColor = new Button { Text = "色を選択...", AutoSize = true, Anchor = AnchorStyles.Left };
        _penColor.Click += (_, _) => ChoosePenColor();
        UpdatePenColorButton();

        table.Controls.Add(Span(_autoCopy), 0, 0);
        table.SetColumnSpan(table.GetControlFromPosition(0, 0)!, 3);
        table.Controls.Add(Span(_autoSave), 0, 1);
        table.SetColumnSpan(table.GetControlFromPosition(0, 1)!, 3);
        table.Controls.Add(CreateLabel("自動保存フォルダー"), 0, 2);
        table.Controls.Add(_autoSaveDirectory, 1, 2);
        table.Controls.Add(browse, 2, 2);
        table.Controls.Add(Span(_alwaysOnTop), 0, 3);
        table.SetColumnSpan(table.GetControlFromPosition(0, 3)!, 3);
        table.Controls.Add(CreateLabel("既定の透明度"), 0, 4);
        table.Controls.Add(_opacity, 1, 4);
        table.Controls.Add(CreateLabel("一時的に隠す時間"), 0, 5);
        table.Controls.Add(_hideDuration, 1, 5);
        table.Controls.Add(CreateLabel("蛍光ペンの太さ"), 0, 6);
        table.Controls.Add(_penWidth, 1, 6);
        table.Controls.Add(CreateLabel("蛍光ペンの色"), 0, 7);
        table.Controls.Add(_penColor, 1, 7);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.None, Width = 88 };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Width = 88 };
        ok.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(table);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Control Span(Control control)
    {
        control.Margin = new Padding(3, 6, 3, 6);
        return control;
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 7, 3, 3),
    };

    private static NumericUpDown CreateNumber(int value, int minimum, int maximum, int increment, string suffix)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            Increment = increment,
            ThousandsSeparator = true,
            Width = 130,
            TextAlign = HorizontalAlignment.Right,
            Tag = suffix,
        };
    }

    private void BrowseDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "キャプチャー画像を自動保存するフォルダーを選択してください。",
            SelectedPath = _autoSaveDirectory.Text,
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _autoSaveDirectory.Text = dialog.SelectedPath;
        }
    }

    private void ChoosePenColor()
    {
        using var dialog = new ColorDialog
        {
            Color = _selectedPenColor,
            FullOpen = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _selectedPenColor = dialog.Color;
            UpdatePenColorButton();
        }
    }

    private void UpdatePenColorButton()
    {
        _penColor.BackColor = _selectedPenColor;
        _penColor.ForeColor = _selectedPenColor.GetBrightness() < 0.45f ? Color.White : Color.Black;
    }

    private void SaveAndClose()
    {
        if (_autoSave.Checked && string.IsNullOrWhiteSpace(_autoSaveDirectory.Text))
        {
            MessageBox.Show(this, "自動保存フォルダーを指定してください。", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _autoSaveDirectory.Focus();
            return;
        }

        _settings.AutoCopy = _autoCopy.Checked;
        _settings.AutoSave = _autoSave.Checked;
        _settings.AutoSaveDirectory = _autoSaveDirectory.Text.Trim();
        _settings.AlwaysOnTop = _alwaysOnTop.Checked;
        _settings.DefaultOpacityPercent = (int)_opacity.Value;
        _settings.HideDurationMilliseconds = (int)_hideDuration.Value;
        _settings.PenWidth = (int)_penWidth.Value;
        _settings.PenColorArgb = _selectedPenColor.ToArgb();

        try
        {
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"設定を保存できませんでした: {ex.Message}", "sukusyo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
