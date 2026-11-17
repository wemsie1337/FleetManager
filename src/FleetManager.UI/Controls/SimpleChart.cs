using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

public enum ChartKind
{
    Line,
    Column
}

public sealed class ChartSeries
{
    public string Title { get; init; } = string.Empty;
    public Color Color { get; init; } = Color.FromArgb(249, 115, 22);
    public IReadOnlyList<double> Values { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Лёгкий график на GDI+ (линии или столбцы) без внешних библиотек.
/// Используется для расходов на топливо и динамики затрат.
/// </summary>
public sealed class SimpleChart : Control
{
    public ChartKind Kind { get; set; } = ChartKind.Line;
    public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ChartSeries> Series { get; set; } = Array.Empty<ChartSeries>();
    public string ValueFormat { get; set; } = "N0";

    public SimpleChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        if (Width < 120 || Height < 90) return;

        var plot = new Rectangle(58, 26, Width - 72, Height - 54);
        if (plot.Width <= 20 || plot.Height <= 20) return;

        var count = Labels.Count;
        if (count == 0)
        {
            TextRenderer.DrawText(g, "Нет данных за выбранный период",
                new Font("Segoe UI", 9f), ClientRectangle, t.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        if (max <= 0) max = 1;
        max = NiceCeiling(max);

        using var gridPen = new Pen(Drawing.WithAlpha(t.Border, 170), 1f) { DashStyle = DashStyle.Dot };
        using var axisFont = new Font("Segoe UI", 7.5f);
        const int steps = 4;
        for (var i = 0; i <= steps; i++)
        {
            var y = plot.Bottom - (int)Math.Round(plot.Height * i / (double)steps);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

            var value = max * i / steps;
            TextRenderer.DrawText(g, value.ToString(ValueFormat),
                axisFont, new Rectangle(0, y - 9, plot.Left - 8, 18), t.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        var slot = plot.Width / (double)count;
        var labelEvery = count > 9 ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            if (i % labelEvery != 0 && i != count - 1) continue;
            var cx = plot.Left + (int)Math.Round(slot * (i + 0.5));
            TextRenderer.DrawText(g, Labels[i], axisFont,
                new Rectangle(cx - (int)(slot / 2), plot.Bottom + 5, (int)Math.Max(30, slot), 18),
                t.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        if (Kind == ChartKind.Column)
            DrawColumns(g, plot, slot, max);
        else
            DrawLines(g, plot, slot, max);

        DrawLegend(g, t);
    }

    private void DrawLines(Graphics g, Rectangle plot, double slot, double max)
    {
        foreach (var series in Series)
        {
            if (series.Values.Count == 0) continue;

            var points = new List<PointF>();
            for (var i = 0; i < series.Values.Count && i < Labels.Count; i++)
            {
                var x = (float)(plot.Left + slot * (i + 0.5));
                var y = (float)(plot.Bottom - Math.Clamp(series.Values[i] / max, 0, 1) * plot.Height);
                points.Add(new PointF(x, y));
            }
            if (points.Count == 0) continue;

            // Мягкая заливка под линией
            if (points.Count > 1)
            {
                using var path = new GraphicsPath();
                path.AddLines(points.ToArray());
                path.AddLine(points[^1].X, plot.Bottom, points[0].X, plot.Bottom);
                path.CloseFigure();
                using var fill = new LinearGradientBrush(
                    new Rectangle(plot.Left, plot.Top, Math.Max(1, plot.Width), Math.Max(1, plot.Height)),
                    Drawing.WithAlpha(series.Color, 42), Drawing.WithAlpha(series.Color, 4), 90f);
                g.FillPath(fill, path);
            }

            using var pen = new Pen(series.Color, 2.6f)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            if (points.Count > 1) g.DrawLines(pen, points.ToArray());

            using var marker = new SolidBrush(series.Color);
            foreach (var point in points)
                g.FillEllipse(marker, point.X - 3.2f, point.Y - 3.2f, 6.4f, 6.4f);
        }
    }

    private void DrawColumns(Graphics g, Rectangle plot, double slot, double max)
    {
        var seriesCount = Math.Max(1, Series.Count);
        var groupWidth = slot * 0.58;
        var barWidth = Math.Max(3f, (float)(groupWidth / seriesCount) - 2f);

        for (var s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            using var brush = new LinearGradientBrush(
                new Rectangle(plot.Left, plot.Top, Math.Max(1, plot.Width), Math.Max(1, plot.Height)),
                Drawing.Blend(series.Color, Color.White, 0.12), series.Color, 90f);

            for (var i = 0; i < series.Values.Count && i < Labels.Count; i++)
            {
                var value = series.Values[i];
                if (value <= 0) continue;

                var height = (int)Math.Round(Math.Clamp(value / max, 0, 1) * plot.Height);
                var groupLeft = plot.Left + slot * (i + 0.5) - groupWidth / 2;
                var x = (float)(groupLeft + s * (groupWidth / seriesCount));
                var rect = new RectangleF(x, plot.Bottom - height, barWidth, height);

                using var path = Drawing.RoundRect(
                    Rectangle.Round(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height + 4)), 4);
                g.FillPath(brush, path);
            }
        }
    }

    private void DrawLegend(Graphics g, ThemePalette t)
    {
        if (Series.Count <= 1) return;

        var x = Width - 12;
        using var font = new Font("Segoe UI", 7.5f);
        for (var i = Series.Count - 1; i >= 0; i--)
        {
            var series = Series[i];
            var textSize = TextRenderer.MeasureText(series.Title, font);
            x -= textSize.Width;
            TextRenderer.DrawText(g, series.Title, font,
                new Rectangle(x, 3, textSize.Width + 2, 16), t.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            x -= 14;
            using var dot = new SolidBrush(series.Color);
            g.FillEllipse(dot, x, 7, 7, 7);
            x -= 14;
        }
    }

    private static double NiceCeiling(double value)
    {
        if (value <= 0) return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return Math.Max(1, nice * magnitude);
    }
}
