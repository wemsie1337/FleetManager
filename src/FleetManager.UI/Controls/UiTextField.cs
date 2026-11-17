using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Текстовое поле в скруглённой «оболочке» с иконкой и placeholder.</summary>
public class UiTextField : Panel
{
    private readonly TextBox _textBox;
    private bool _focused;

    public TextBox Input => _textBox;

    public string IconGlyph { get; set; } = string.Empty;

    public string Placeholder
    {
        get => _textBox.PlaceholderText;
        set => _textBox.PlaceholderText = value;
    }

    public bool UsePassword
    {
        get => _textBox.UseSystemPasswordChar;
        set => _textBox.UseSystemPasswordChar = value;
    }

    public bool ReadOnly
    {
        get => _textBox.ReadOnly;
        set => _textBox.ReadOnly = value;
    }

    public bool Multiline
    {
        get => _textBox.Multiline;
        set
        {
            _textBox.Multiline = value;
            _textBox.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
            if (value)
            {
                Height = 90;
                GridStyler.ApplyScrollbar(_textBox);
            }
            LayoutTextBox();
            Invalidate();
        }
    }

    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value;
    }

    public UiTextField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f),
            BackColor = ThemeManager.Current.SurfaceAlt,
            ForeColor = ThemeManager.Current.Text
        };
        _textBox.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        _textBox.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        _textBox.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        Controls.Add(_textBox);

        Padding = new Padding(0);
        Height = 44;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutTextBox();
    }

    private void LayoutTextBox()
    {
        if (_textBox is null) return;
        var left = string.IsNullOrEmpty(IconGlyph) ? 14 : 42;
        if (_textBox.Multiline)
        {
            _textBox.Location = new Point(14, 10);
            _textBox.Width = Math.Max(10, Width - 28);
            _textBox.Height = Math.Max(20, Height - 20);
            return;
        }

        _textBox.Location = new Point(left, (Height - _textBox.Height) / 2);
        _textBox.Width = Math.Max(10, Width - left - 14);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using var fill = new SolidBrush(t.SurfaceAlt);
        Drawing.FillRounded(g, rect, 10, fill);
        using var border = new Pen(_focused ? t.PrimaryLight : t.Border, _focused ? 1.6f : 1f);
        Drawing.DrawRounded(g, rect, 10, border);

        if (!string.IsNullOrEmpty(IconGlyph))
        {
            TextRenderer.DrawText(g, IconGlyph, new Font("Segoe MDL2 Assets", 11f),
                new Rectangle(14, 0, 22, Height), _focused ? t.PrimaryLight : t.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }

        base.OnPaint(e);
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_textBox is not null)
        {
            _textBox.BackColor = ThemeManager.Current.SurfaceAlt;
            _textBox.ForeColor = ThemeManager.Current.Text;
        }
    }
}
