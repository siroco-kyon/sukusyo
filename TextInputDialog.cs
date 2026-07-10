using System.Drawing;
using System.Windows.Forms;

namespace Sukusyo;

internal sealed class TextInputDialog : Form
{
    private readonly TextBox _text;
    private readonly Button _fontButton;
    private readonly Button _colorButton;

    public Font SelectedTextFont { get; private set; }
    public Color SelectedTextColor { get; private set; }
    public string TextValue => _text.Text;

    public TextInputDialog(Color initialColor)
    {
        SelectedTextFont = new Font("Yu Gothic UI", 16f, FontStyle.Regular);
        SelectedTextColor = initialColor;

        Text = "テキスト入力";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 240);
        Padding = new Padding(12);

        _text = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
        };

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 0, 0),
        };
        _fontButton = new Button { Text = "フォント...", AutoSize = true };
        _colorButton = new Button { Text = "文字色...", AutoSize = true };
        _fontButton.Click += (_, _) => ChooseFont();
        _colorButton.Click += (_, _) => ChooseColor();
        options.Controls.Add(_fontButton);
        options.Controls.Add(_colorButton);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 6, 0, 0),
        };
        var ok = new Button { Text = "挿入", DialogResult = DialogResult.OK, Width = 88 };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Width = 88 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(_text);
        Controls.Add(options);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => _text.Focus();
        UpdateColorButton();
    }

    private void ChooseFont()
    {
        using var dialog = new FontDialog { Font = SelectedTextFont, ShowColor = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectedTextFont.Dispose();
            SelectedTextFont = (Font)dialog.Font.Clone();
            _fontButton.Text = $"{SelectedTextFont.Name} {SelectedTextFont.Size:g}pt";
        }
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = SelectedTextColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectedTextColor = dialog.Color;
            UpdateColorButton();
        }
    }

    private void UpdateColorButton()
    {
        _colorButton.BackColor = SelectedTextColor;
        _colorButton.ForeColor = SelectedTextColor.GetBrightness() < 0.45f ? Color.White : Color.Black;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SelectedTextFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
