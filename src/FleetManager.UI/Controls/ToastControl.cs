using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Toast-уведомление с иконкой, полосой прогресса и автозакрытием.</summary>
public class ToastControl : Control
{
    private readonly Timer _lifeTimer;
    private int _totalMs;
    private int _elapsedMs;

    public ToastKind Kind { get; set; } = ToastKind.Info;
    public string TitleText { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;

    public event EventHandler? Dismissed;

    public ToastControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(360, 88);
        Cursor = Cursors.Hand;

        _lifeTimer = new Timer { Interval = 100 };
        _lifeTimer.Tick += (_, _) =>
        {
            _elapsedMs += 100;
            Invalidate();
            if (_elapsedMs >= _totalMs) Dismiss();
        };
    }

    public void StartLifetime(int milliseconds)
    {
        _totalMs = Math.Max(1500, milliseconds);
        _elapsedMs = 0;
        _lifeTimer.Start();
    }

    public void Dismiss()
    {
        if (!_lifeTimer.Enabled && _elapsedMs == 0) return;
        _lifeTimer.Stop();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        Dismiss();
        base.OnClick(e);
    }

    private (Color accent, string glyph) GetStyle() => Kind switch
    {
        ToastKind.Success => (ThemeManager.Current.Success, "\uE73E"),
        ToastKind.Error => (ThemeManager.Current.Danger, "\uEA39"),
        ToastKind.Warning => (ThemeManager.Current.Warning, "\uE7BA"),
        _ => (ThemeManager.Current.Info, "\uE946")
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;
        var (accent, glyph) = GetStyle();
        var rect = new Rectangle(1, 1, Width - 10, Height - 8);

        Drawing.DrawSoftShadow(g, rect, 14, 5, t.Shadow);
        using var fill = new SolidBrush(t.Surface);
        Drawing.FillRounded(g, rect, 14, fill);
        using var border = new Pen(Drawing.WithAlpha(accent, 110));
        Drawing.DrawRounded(g, rect, 14, border);

        // Цветная полоса слева
        using var stripePath = Drawing.RoundRect(new Rectangle(rect.X, rect.Y, 5, rect.Height), 14);
        using var stripe = new SolidBrush(accent);
        g.FillPath(stripe, stripePath);

        var iconRect = new Rectangle(rect.X + 18, rect.Y + 20, 40, 40);
        using var iconBg = new SolidBrush(Drawing.WithAlpha(accent, 30));
        g.FillEllipse(iconBg, iconRect);
        TextRenderer.DrawText(g, glyph, new Font("Segoe MDL2 Assets", 15f), iconRect, accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, TitleText, Ui.Fonts.Medium,
            new Rectangle(rect.X + 70, rect.Y + 14, rect.Width - 100, 22), t.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, MessageText, Ui.Fonts.Small,
            new Rectangle(rect.X + 70, rect.Y + 37, rect.Width - 100, 34), t.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

        // Прогресс жизни
        var progress = _totalMs <= 0 ? 1f : Math.Clamp(1f - (float)_elapsedMs / _totalMs, 0f, 1f);
        using var track = new SolidBrush(Color.FromArgb(35, accent));
        g.FillRectangle(track, rect.X + 14, rect.Bottom - 7, rect.Width - 28, 3);
        using var bar = new SolidBrush(accent);
        g.FillRectangle(bar, rect.X + 14, rect.Bottom - 7, (int)((rect.Width - 28) * progress), 3);

        TextRenderer.DrawText(g, "\uE711", new Font("Segoe MDL2 Assets", 8f),
            new Rectangle(rect.Right - 28, rect.Y + 10, 18, 18), t.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _lifeTimer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Контейнер тостов в правом верхнем углу. Показывает не более 3 одновременно.</summary>
public class ToastHost : Panel, IToastHost
{
    private readonly List<ToastControl> _toasts = new();

    public ToastHost()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        // Пустой хост не должен перекрывать контент: высота растёт только под тосты.
        Size = new Size(370, 0);
    }

    public void ShowToast(ToastKind kind, string title, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowToast(kind, title, message)));
            return;
        }

        var toast = new ToastControl
        {
            Kind = kind,
            TitleText = title,
            MessageText = message,
            Width = Width - 4
        };
        toast.Dismissed += (_, _) => RemoveToast(toast);

        _toasts.Insert(0, toast);
        Controls.Add(toast);
        toast.BringToFront();

        while (_toasts.Count > 3)
            RemoveToast(_toasts[^1], animate: false);

        LayoutToasts();
        toast.Left = Width;
        var slide = new Timer { Interval = 15 };
        slide.Tick += (_, _) =>
        {
            toast.Left -= 34;
            if (toast.Left <= 4)
            {
                toast.Left = 4;
                slide.Stop();
                slide.Dispose();
            }
        };
        slide.Start();

        toast.StartLifetime(kind is ToastKind.Error or ToastKind.Warning ? 9000 : 5500);
    }

    private void RemoveToast(ToastControl toast, bool animate = true)
    {
        if (!_toasts.Contains(toast)) return;
        _toasts.Remove(toast);
        Controls.Remove(toast);
        toast.Dispose();
        LayoutToasts();
    }

    private void LayoutToasts()
    {
        for (var i = 0; i < _toasts.Count; i++)
        {
            _toasts[i].Top = i * 98;
            if (_toasts[i].Left < 4) _toasts[i].Left = 4;
        }

        // Хост занимает только область тостов, чтобы не перекрывать контент пустотой.
        var targetHeight = _toasts.Count == 0 ? 0 : _toasts.Count * 98 - 10;
        if (Height != targetHeight) Height = targetHeight;
    }
}
