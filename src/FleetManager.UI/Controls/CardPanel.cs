using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Панель-карточка со скруглением, фоном и мягкой тенью.</summary>
public class CardPanel : Panel
{
    private int _radius = 16;
    private int _shadowDepth = 7;

    public int Radius
    {
        get => _radius;
        set { _radius = value; Invalidate(); }
    }

    public int ShadowDepth
    {
        get => _shadowDepth;
        set { _shadowDepth = Math.Max(0, value); Invalidate(); }
    }

    public bool ShowShadow { get; set; } = true;

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Padding = new Padding(18);
        Margin = new Padding(0);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Прозрачный фон: рисуем родительский фон, чтобы не было артефактов
        if (Parent is not null)
        {
            using var brush = new SolidBrush(Parent.BackColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        var margin = ShowShadow ? Math.Max(4, ShadowDepth) : 0;
        var card = new Rectangle(margin / 2, margin / 2,
            Width - margin, Height - margin);

        if (card.Width <= 0 || card.Height <= 0) return;

        if (ShowShadow)
            Drawing.DrawSoftShadow(g, new Rectangle(margin / 2, margin / 2 + 1, card.Width, card.Height),
                Radius, ShadowDepth, t.Shadow);

        using var fill = new SolidBrush(t.Surface);
        Drawing.FillRounded(g, card, Radius, fill);
        using var border = new Pen(t.Border);
        Drawing.DrawRounded(g, card, Radius, border);

        base.OnPaint(e);
    }
}
