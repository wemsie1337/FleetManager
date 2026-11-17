using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>
/// ComboBox с полностью собственной отрисовкой закрытого состояния.
/// Стандартный ComboBox в тёмной теме рисует белую кнопку-стрелку системными
/// цветами, поэтому поверх нативной отрисовки наносится своя (фон, текст, шеврон).
/// </summary>
internal sealed class FlatComboBox : ComboBox
{
    private const int WM_PAINT = 0x000F;

    public FlatComboBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WM_PAINT || !IsHandleCreated || Width <= 0 || Height <= 0) return;
        using var g = Graphics.FromHwnd(Handle);
        PaintSurface(g);
    }

    private void PaintSurface(Graphics g)
    {
        var t = ThemeManager.Current;
        using var bg = new SolidBrush(t.SurfaceAlt);
        g.FillRectangle(bg, 0, 0, Width, Height);

        var textRect = new Rectangle(2, 0, Math.Max(0, Width - 28), Height);
        TextRenderer.DrawText(g, Text, Font, textRect,
            Enabled ? t.Text : t.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using var chevron = new Font("Segoe MDL2 Assets", 8f);
        TextRenderer.DrawText(g, "\uE70D", chevron,
            new Rectangle(Width - 24, 0, 20, Height), t.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}

/// <summary>ComboBox в скруглённой оболочке.</summary>
public class UiComboBox : Panel
{
    private bool _focused;

    public ComboBox Inner { get; }

    public event EventHandler? SelectionChanged;

    public override string Text
    {
        get => Inner.Text;
        set => Inner.Text = value;
    }

    public UiComboBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        Inner = new FlatComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            BackColor = ThemeManager.Current.SurfaceAlt,
            ForeColor = ThemeManager.Current.Text,
            IntegralHeight = false,
            MaxDropDownItems = 12,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 26
        };
        Inner.DrawItem += Inner_DrawItem;
        // ComboBox без BindingContext не применяет DataSource: Items остаётся пустым,
        // пока контрол не добавлен на форму. Диалоги заполняют списки до SetContent,
        // поэтому задаём собственный контекст заранее.
        Inner.BindingContext = new BindingContext();
        Inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        Inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Inner.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Controls.Add(Inner);

        Height = 44;
    }

    private void Inner_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var t = ThemeManager.Current;
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bg = selected ? t.GridSelection : t.Surface;
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Bounds);
        // GetItemText учитывает DisplayMember — ToString() вернул бы имя типа модели.
        var text = Inner.GetItemText(Inner.Items[e.Index]);
        TextRenderer.DrawText(e.Graphics, text, Inner.Font, e.Bounds,
            selected ? t.Text : t.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Inner is null) return;
        Inner.Location = new Point(10, (Height - Inner.Height) / 2);
        Inner.Width = Math.Max(20, Width - 20);
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

        base.OnPaint(e);
    }
}

/// <summary>Флажок с собственной отрисовкой (корректно выглядит в обеих темах).</summary>
public class UiCheck : Control
{
    private bool _checked = true;
    public event EventHandler? CheckedChanged;

    public UiCheck()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Size = new Size(18, 18);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;
        var rect = new Rectangle(1, 1, Width - 3, Height - 3);

        if (_checked)
        {
            using var fill = new SolidBrush(t.Primary);
            Drawing.FillRounded(g, rect, 5, fill);
            using var font = new Font("Segoe MDL2 Assets", 7.5f);
            TextRenderer.DrawText(g, "\uE73E", font, new Rectangle(0, 0, Width, Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
        else
        {
            using var pen = new Pen(t.Border, 1.4f);
            Drawing.DrawRounded(g, rect, 5, pen);
        }
    }
}

/// <summary>
/// Поле даты в едином стиле с остальными полями: текст + необязательный флажок
/// и всплывающий календарь. Заменяет DateTimePicker, который в тёмной теме
/// рисуется системными (белыми) цветами.
/// </summary>
public class UiDateField : Panel
{
    private const string DateFormat = "dd.MM.yyyy";
    private static readonly string[] AcceptedFormats =
        { "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };

    private readonly TextBox _text;
    private readonly UiCheck? _check;
    private readonly Label _icon;
    private DateTime _value = DateTime.Today;
    private bool _focused;

    /// <summary>Совместимость с прежним API (Inner.Value / Inner.Checked / Inner.ValueChanged).</summary>
    public UiDateField Inner => this;

    public event EventHandler? ValueChanged;

    public UiDateField(bool nullable = false)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        if (nullable)
        {
            _check = new UiCheck { Checked = false };
            _check.CheckedChanged += (_, _) =>
            {
                UpdateEnabledState();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            Controls.Add(_check);
        }

        _text = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f),
            BackColor = ThemeManager.Current.SurfaceAlt,
            ForeColor = ThemeManager.Current.Text,
            Text = _value.ToString(DateFormat)
        };
        _text.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        _text.LostFocus += (_, _) => { _focused = false; CommitText(); Invalidate(); };
        _text.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                CommitText();
                ShowCalendar();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        Controls.Add(_text);

        _icon = new Label
        {
            Text = "\uE787",
            Font = new Font("Segoe MDL2 Assets", 10f),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Size = new Size(22, 22)
        };
        _icon.Click += (_, _) => ShowCalendar();
        Controls.Add(_icon);

        ThemeManager.ThemeChanged += OnThemeChanged;
        Height = 44;
        UpdateEnabledState();
    }

    public DateTime Value
    {
        get => _value;
        set
        {
            var normalized = value.Date;
            var changed = normalized != _value;
            _value = normalized;
            _text.Text = _value.ToString(DateFormat);
            if (_check is { Checked: false }) _check.Checked = true;
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Checked
    {
        get => _check?.Checked ?? true;
        set
        {
            if (_check is null) return;
            _check.Checked = value;
        }
    }

    private void CommitText()
    {
        if (!Checked) return;
        var raw = _text.Text.Trim();
        if (DateTime.TryParseExact(raw, AcceptedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            if (parsed.Year < 1900 || parsed.Year > 2200)
            {
                _text.Text = _value.ToString(DateFormat);
                return;
            }

            Value = parsed;
            return;
        }

        // Не удалось разобрать — возвращаем последнее корректное значение.
        _text.Text = _value.ToString(DateFormat);
    }

    private void ShowCalendar()
    {
        if (!Enabled) return;
        if (!Checked) Checked = true; // клик по календарю включает необязательное поле
        var host = FindForm();
        if (host is null) return;

        var popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            BackColor = ThemeManager.Current.Surface,
            Padding = new Padding(1)
        };

        var calendar = new MonthCalendar
        {
            MaxSelectionCount = 1,
            ShowTodayCircle = true,
            TitleBackColor = ThemeManager.Current.Primary,
            TitleForeColor = Color.White,
            BackColor = ThemeManager.Current.Surface,
            ForeColor = ThemeManager.Current.Text
        };
        calendar.DateSelected += (_, e) =>
        {
            Value = e.Start.Date;
            popup.Close();
        };
        calendar.SetDate(Checked ? _value : DateTime.Today);

        popup.Controls.Add(calendar);
        var preferred = calendar.GetPreferredSize(Size.Empty);
        popup.ClientSize = new Size(preferred.Width + 2, preferred.Height + 2);

        var location = PointToScreen(new Point(0, Height + 4));
        var work = Screen.FromControl(this).WorkingArea;
        if (location.Y + popup.Height > work.Bottom)
            location = PointToScreen(new Point(0, -popup.Height - 4));
        if (location.X + popup.Width > work.Right)
            location.X = work.Right - popup.Width;
        popup.Location = location;

        popup.Deactivate += (_, _) => popup.Close();
        popup.Show(host);
    }

    private void UpdateEnabledState()
    {
        var enabled = Checked && Enabled;
        var t = ThemeManager.Current;
        _text.Enabled = enabled;
        _text.ForeColor = enabled ? t.Text : t.TextMuted;
        _icon.ForeColor = enabled ? t.TextMuted : t.Border;
        _icon.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        UpdateEnabledState();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (_text is null) return;

        var left = 12;
        if (_check is not null)
        {
            _check.Location = new Point(12, (Height - _check.Height) / 2);
            left = 38;
        }

        if (_icon is not null) _icon.Location = new Point(Width - 34, (Height - _icon.Height) / 2);
        _text.SetBounds(left, (Height - _text.Height) / 2, Math.Max(30, Width - left - 44), _text.Height);
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

        base.OnPaint(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        _text.BackColor = ThemeManager.Current.SurfaceAlt;
        UpdateEnabledState();
        Invalidate(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
        base.Dispose(disposing);
    }
}
