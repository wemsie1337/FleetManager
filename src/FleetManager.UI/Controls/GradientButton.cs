using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

public enum ButtonKind
{
    Primary,
    Accent,
    Ghost,
    Danger,
    Success
}

/// <summary>Кнопка с градиентом, hover-состоянием и ripple-эффектом.</summary>
public class GradientButton : Control
{
    private readonly Timer _animationTimer;
    private bool _hover;
    private bool _pressed;
    private float _ripple = -1f;
    private Point _rippleCenter;
    private int _radius = 10;

    public ButtonKind Kind { get; set; } = ButtonKind.Primary;
    public string IconGlyph { get; set; } = string.Empty;
    public int IconSize { get; set; } = 11;

    public int Radius
    {
        get => _radius;
        set { _radius = value; Invalidate(); }
    }

    public GradientButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Font = Ui.Fonts.Medium;
        Size = new Size(148, 42);
        Cursor = Cursors.Hand;
        TabStop = true;

        _animationTimer = new Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) =>
        {
            _ripple += 0.07f;
            if (_ripple >= 1.2f)
            {
                _ripple = -1f;
                _animationTimer.Stop();
            }
            Invalidate();
        };
    }

    public void PerformClick()
    {
        if (Enabled) OnClick(EventArgs.Empty);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            _rippleCenter = e.Location;
            _ripple = 0f;
            _animationTimer.Start();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            PerformClick();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color from, to, fore, borderColor;
        switch (Kind)
        {
            case ButtonKind.Accent:
                from = t.Accent; to = t.AccentDark; fore = Color.White; borderColor = t.Accent;
                break;
            case ButtonKind.Ghost:
                from = _hover ? t.SurfaceHover : t.SurfaceAlt;
                to = from;
                fore = t.Text;
                borderColor = t.Border;
                break;
            case ButtonKind.Danger:
                from = t.Danger; to = Drawing.Darken(t.Danger, 0.2); fore = Color.White; borderColor = t.Danger;
                break;
            case ButtonKind.Success:
                from = t.Success; to = Drawing.Darken(t.Success, 0.2); fore = Color.White; borderColor = t.Success;
                break;
            default:
                from = _hover ? t.PrimaryLight : t.Primary;
                to = t.PrimaryDark;
                fore = Color.White;
                borderColor = t.Primary;
                break;
        }

        if (!Enabled)
        {
            from = Drawing.Blend(from, t.SurfaceAlt, 0.65);
            to = Drawing.Blend(to, t.SurfaceAlt, 0.65);
            fore = t.TextMuted;
            borderColor = t.Border;
        }

        var drawRect = _pressed ? new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height) : rect;
        if (Kind != ButtonKind.Ghost || _hover)
            Drawing.FillGradient(g, drawRect, from, to, 35f, _radius);
        else
            Drawing.FillRounded(g, drawRect, _radius, new SolidBrush(t.SurfaceAlt));

        using (var pen = new Pen(borderColor))
            Drawing.DrawRounded(g, drawRect, _radius, pen);

        // Ripple
        if (_ripple is >= 0f and < 1.2f)
        {
            using var clip = Drawing.RoundRect(drawRect, _radius);
            var oldClip = g.Clip;
            g.SetClip(clip);
            var maxDist = Math.Max(Width, Height) * 1.4f;
            var radius = maxDist * _ripple;
            // Альфа должна оставаться в диапазоне 0..255 (иначе ArgumentException при отрисовке).
            var alpha = Math.Clamp((int)(70 * (1 - _ripple)), 0, 255);
            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.FillEllipse(brush, _rippleCenter.X - radius, _rippleCenter.Y - radius, radius * 2, radius * 2);
            g.Clip = oldClip;
        }

        // Icon + text
        var iconWidth = string.IsNullOrEmpty(IconGlyph) ? 0 : IconSize + 22;
        var textSize = TextRenderer.MeasureText(Text, Font);
        var totalWidth = textSize.Width + iconWidth;
        var startX = drawRect.X + (drawRect.Width - totalWidth) / 2;
        var centerY = drawRect.Y + drawRect.Height / 2;

        if (!string.IsNullOrEmpty(IconGlyph))
        {
            using var iconFont = new Font("Segoe MDL2 Assets", IconSize, FontStyle.Regular);
            TextRenderer.DrawText(g, IconGlyph, iconFont,
                new Rectangle(startX, centerY - 12, IconSize + 12, 24), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        var textRect = new Rectangle(startX + iconWidth, centerY - 12,
            textSize.Width + 4, 24);
        TextRenderer.DrawText(g, Text, Font, textRect, fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animationTimer.Dispose();
        base.Dispose(disposing);
    }
}
