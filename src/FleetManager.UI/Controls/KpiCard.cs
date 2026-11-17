using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>KPI-карточка с hover-поднятием и мягкой тенью.</summary>
public class KpiCard : Control
{
    private readonly Timer _hoverTimer;
    private float _hover;
    private bool _hovering;

    public string Title { get; set; } = "Показатель";
    public string Value { get; set; } = "0";
    public string Subtitle { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE7F4";
    public Color AccentColor { get; set; } = ThemePalette.Light.PrimaryLight;

    public KpiCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 124;
        Margin = new Padding(0);

        _hoverTimer = new Timer { Interval = 16 };
        _hoverTimer.Tick += (_, _) =>
        {
            var target = _hovering ? 1f : 0f;
            _hover += (target - _hover) * 0.22f;
            if (Math.Abs(target - _hover) < 0.02f)
            {
                _hover = target;
                _hoverTimer.Stop();
            }
            Invalidate();
        };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovering = true;
        _hoverTimer.Start();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovering = false;
        _hoverTimer.Start();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        var lift = (int)Math.Round(_hover * 5);
        var margin = 8;
        var card = new Rectangle(margin, margin - lift, Width - margin * 2, Height - margin * 2);

        using var shadow = new SolidBrush(Color.FromArgb((int)(t.Shadow.A * (0.55 + _hover * 0.45)), t.Shadow));
        for (var i = 0; i < 6; i++)
        {
            var alpha = (int)((t.Shadow.A * (0.9 + _hover * 0.7)) * (1.0 - i / 7.0));
            using var pen = new Pen(Color.FromArgb(alpha, t.Shadow), 1.4f);
            Drawing.DrawRounded(g, new Rectangle(card.X - i / 2, card.Y + i / 2, card.Width + i, card.Height + i),
                16 + i / 2, pen);
        }

        using var fill = new SolidBrush(t.Surface);
        Drawing.FillRounded(g, card, 16, fill);
        using var border = new Pen(_hover > 0.3 ? Drawing.WithAlpha(AccentColor, 90) : t.Border);
        Drawing.DrawRounded(g, card, 16, border);

        var iconRect = new Rectangle(card.X + 18, card.Y + 18, 42, 42);
        using var iconBg = new LinearGradientBrush(iconRect,
            Drawing.WithAlpha(AccentColor, 42), Drawing.WithAlpha(AccentColor, 16), 45f);
        Drawing.FillRounded(g, iconRect, 12, iconBg);
        TextRenderer.DrawText(g, IconGlyph, Ui.Fonts.IconLarge, iconRect, AccentColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, Title, new Font("Segoe UI", 9f), 
            new Rectangle(card.X + 72, card.Y + 16, card.Width - 86, 22), t.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(g, Value, Ui.Fonts.Kpi,
            new Rectangle(card.X + 72, card.Y + 36, card.Width - 86, 36), AccentColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            TextRenderer.DrawText(g, Subtitle, Ui.Fonts.Small,
                new Rectangle(card.X + 18, card.Bottom - 30, card.Width - 32, 20), t.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        using var accent = new SolidBrush(Drawing.WithAlpha(AccentColor, _hover > 0.4 ? 230 : 150));
        Drawing.FillRounded(g, new Rectangle(card.X + 18, card.Bottom - 7, 36 + (int)(_hover * 18), 3), 2, accent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hoverTimer.Dispose();
        base.Dispose(disposing);
    }
}
