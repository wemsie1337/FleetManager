using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Круглая icon-кнопка с бейджем уведомлений.</summary>
public class RoundIconButton : Control
{
    private bool _hover;

    public string Glyph { get; set; } = "\uE7F4";
    public int BadgeCount { get; set; }
    public Color BadgeColor { get; set; } = ThemePalette.Light.Danger;
    public Color GlyphColor { get; set; } = Color.Empty;

    public RoundIconButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(40, 40);
        Cursor = Cursors.Hand;
        Font = Ui.Fonts.Glyph;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        if (_hover)
        {
            using var bg = new SolidBrush(t.SurfaceHover);
            g.FillEllipse(bg, 0, 0, Width - 1, Height - 1);
        }

        var color = GlyphColor.IsEmpty ? t.TextMuted : GlyphColor;
        if (_hover) color = t.Text;
        TextRenderer.DrawText(g, Glyph, Ui.Fonts.Glyph, ClientRectangle, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (BadgeCount > 0)
        {
            var text = BadgeCount > 9 ? "9+" : BadgeCount.ToString();
            var badgeSize = text == "9+" ? 18 : 17;
            var badgeRect = new Rectangle(Width - badgeSize - 2, 2, badgeSize, badgeSize);
            using var brush = new SolidBrush(BadgeColor);
            g.FillEllipse(brush, badgeRect);
            using var font = new Font("Segoe UI", 7f, FontStyle.Bold);
            TextRenderer.DrawText(g, text, font, badgeRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
