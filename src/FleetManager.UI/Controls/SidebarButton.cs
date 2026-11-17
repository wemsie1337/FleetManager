using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Пункт бокового меню с иконкой, подписью и индикатором активной страницы.</summary>
public class SidebarButton : Control
{
    private bool _active;
    private bool _hover;
    private bool _compact;
    private int _badge;

    public string Glyph { get; set; } = string.Empty;

    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public bool Compact
    {
        get => _compact;
        set { _compact = value; Invalidate(); }
    }

    public int BadgeCount
    {
        get => _badge;
        set { _badge = value; Invalidate(); }
    }

    public SidebarButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 46;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.75f);
        Margin = new Padding(0, 2, 0, 2);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_active)
        {
            using var brush = new LinearGradientBrush(rect,
                Color.FromArgb(45, 255, 255, 255), Color.FromArgb(14, 255, 255, 255), 0f);
            Drawing.FillRounded(g, rect, 11, brush);
            using var bar = new SolidBrush(ThemeManager.Current.Accent);
            g.FillRectangle(bar, 0, 10, 3, Height - 20);
        }
        else if (_hover)
        {
            using var brush = new SolidBrush(Color.FromArgb(20, 255, 255, 255));
            Drawing.FillRounded(g, rect, 11, brush);
        }

        var iconColor = _active ? Color.White : Color.FromArgb(185, 205, 235);
        var iconX = _compact ? (Width - 24) / 2 : 14;
        using var iconFont = new Font("Segoe MDL2 Assets", 13.5f);
        TextRenderer.DrawText(g, Glyph, iconFont,
            new Rectangle(iconX, 0, 24, Height), iconColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

        if (!_compact)
        {
            var textColor = _active ? Color.White : Color.FromArgb(205, 220, 242);
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(50, 0, Math.Max(10, Width - 90), Height), textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        if (_badge > 0)
        {
            var badgeText = _badge > 9 ? "9+" : _badge.ToString();
            var size = _badge > 9 ? 20 : 18;
            var badgeRect = new Rectangle(Width - size - 12, (Height - size) / 2, size, size);
            using var brush = new SolidBrush(ThemeManager.Current.Danger);
            g.FillEllipse(brush, badgeRect);
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            TextRenderer.DrawText(g, badgeText, font, badgeRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
