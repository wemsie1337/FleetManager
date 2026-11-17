using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Строка уведомления о ТО для дашборда.</summary>
public class AlertRow : Control
{
    public MaintenanceAlert? Alert { get; set; }

    public AlertRow()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 58;
        Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 0, 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Alert is null)
        {
            base.OnPaint(e);
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;
        var color = Ui.SeverityColor(Alert.Severity switch
        {
            "Overdue" => MaintenanceSeverity.Overdue,
            "Due" => MaintenanceSeverity.Due,
            _ => MaintenanceSeverity.Upcoming
        });

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var bg = new SolidBrush(t.SurfaceAlt);
        Drawing.FillRounded(g, rect, 10, bg);

        using var dot = new SolidBrush(color);
        g.FillEllipse(dot, 14, Height / 2 - 5, 10, 10);

        TextRenderer.DrawText(g, Alert.PlateNumber, Ui.Fonts.Bold,
            new Rectangle(34, 8, Width - 120, 20), t.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, Alert.TypeName, Ui.Fonts.Small,
            new Rectangle(34, 28, Width - 120, 20), t.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var badgeText = Alert.Severity switch
        {
            "Overdue" => "Просрочено",
            "Due" => "Пора",
            _ => "Скоро"
        };
        var badgeRect = new Rectangle(Width - 100, Height / 2 - 12, 88, 24);
        using var badgeBrush = new SolidBrush(Drawing.WithAlpha(color, 28));
        Drawing.FillRounded(g, badgeRect, 8, badgeBrush);
        TextRenderer.DrawText(g, badgeText, new Font("Segoe UI", 7.5f, FontStyle.Bold), badgeRect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Кнопка профиля с аватаром-инициалами.</summary>
public class ProfileButton : Control
{
    private bool _hover;

    public string FullName { get; set; } = string.Empty;
    public string RoleText { get; set; } = string.Empty;
    public bool OnDarkBackground { get; set; }
    public bool ShowChevron { get; set; } = true;

    public ProfileButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(210, 44);
        Cursor = Cursors.Hand;
    }

    private string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
        }
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
            using var hover = new SolidBrush(OnDarkBackground
                ? Color.FromArgb(28, 255, 255, 255)
                : t.SurfaceHover);
            Drawing.FillRounded(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, hover);
        }

        var avatar = new Rectangle(4, (Height - 34) / 2, 34, 34);
        using var avatarBrush = new LinearGradientBrush(avatar, t.Accent, t.AccentDark, 45f);
        g.FillEllipse(avatarBrush, avatar);
        using var avatarFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        TextRenderer.DrawText(g, Initials, avatarFont, avatar, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        var textColor = OnDarkBackground ? Color.White : t.Text;
        var mutedColor = OnDarkBackground ? Color.FromArgb(180, 200, 232) : t.TextMuted;
        TextRenderer.DrawText(g, FullName, Ui.Fonts.Bold,
            new Rectangle(46, 5, Width - 46 - (ShowChevron ? 26 : 4), 20), textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, RoleText, Ui.Fonts.Small,
            new Rectangle(46, 23, Width - 46 - (ShowChevron ? 26 : 4), 18), mutedColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (ShowChevron)
            TextRenderer.DrawText(g, "\uE70D", new Font("Segoe MDL2 Assets", 7f),
                new Rectangle(Width - 22, 0, 18, Height), mutedColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Логотип в сайдбаре: градиентная иконка + название.</summary>
public class AppLogo : Control
{
    private bool _compact;

    public bool Compact
    {
        get => _compact;
        set { _compact = value; Invalidate(); }
    }

    public AppLogo()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 84;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        var box = new Rectangle(_compact ? (Width - 44) / 2 : 16, 16, 44, 44);
        using var brush = new LinearGradientBrush(box, t.Accent, t.AccentDark, 45f);
        Drawing.FillRounded(g, box, 12, brush);

        using var iconFont = new Font("Segoe MDL2 Assets", 19f);
        TextRenderer.DrawText(g, "\uE804", iconFont, box, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (!_compact)
        {
            TextRenderer.DrawText(g, "АВТОПАРК", new Font("Segoe UI", 13.5f, FontStyle.Bold),
                new Rectangle(70, 17, Width - 80, 26), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "FLEET MANAGEMENT", new Font("Segoe UI", 7f, FontStyle.Bold),
                new Rectangle(71, 41, Width - 80, 18), Color.FromArgb(150, 180, 220),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
