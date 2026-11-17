using System.Drawing;
using System.Drawing.Drawing2D;

namespace FleetManager.UI.Helpers;

/// <summary>Графические примитивы для кастомной отрисовки.</summary>
public static class Drawing
{
    public static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        var arc = new Rectangle(rect.Location, new Size(d, d));
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - d - 1;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - d - 1;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Rectangle rect, int radius, Brush brush)
    {
        using var path = RoundRect(rect, radius);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    public static void DrawRounded(Graphics g, Rectangle rect, int radius, Pen pen)
    {
        using var path = RoundRect(rect, radius);
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawPath(pen, path);
        g.SmoothingMode = old;
    }

    public static void FillGradient(Graphics g, Rectangle rect, Color from, Color to, float angle = 45f, int radius = 0)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var brush = new LinearGradientBrush(rect, from, to, angle);
        if (radius > 0) FillRounded(g, rect, radius, brush);
        else g.FillRectangle(brush, rect);
    }

    public static void DrawSoftShadow(Graphics g, Rectangle rect, int radius, int depth, Color shadow)
    {
        for (var i = depth; i >= 1; i--)
        {
            var alpha = (int)(shadow.A * (1.0 - (double)i / (depth + 1)) * 0.55);
            if (alpha <= 0) continue;
            var r = new Rectangle(rect.X - i / 2, rect.Y + i / 2, rect.Width + i, rect.Height + i);
            using var pen = new Pen(Color.FromArgb(alpha, shadow), 1.6f);
            DrawRounded(g, r, radius + i / 2, pen);
        }
    }

    public static Color WithAlpha(Color color, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), color);

    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static Color Darken(Color color, double amount) => Blend(color, Color.Black, amount);
    public static Color Lighten(Color color, double amount) => Blend(color, Color.White, amount);

    /// <summary>Заливка градиентом с закруглением (для аватаров/иконок).</summary>
    public static void FillCircle(Graphics g, Rectangle rect, Color from, Color to)
    {
        using var brush = new LinearGradientBrush(rect, from, to, 45f);
        g.FillEllipse(brush, rect);
    }
}
