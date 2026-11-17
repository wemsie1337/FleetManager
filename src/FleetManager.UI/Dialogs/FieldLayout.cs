using System.Globalization;
using System.Windows.Forms;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

internal static class FieldLayout
{
    public static TableLayoutPanel Table(int columns = 2)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            BackColor = System.Drawing.Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        for (var i = 0; i < columns; i++)
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        // Строка-распорка: без неё TableLayoutPanel растягивает последнюю строку с полями
        // на всё свободное место (например, поля пароля становились высотой 300 px).
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        return table;
    }

    public static void AddRow(TableLayoutPanel table, int height)
    {
        // Новая строка вставляется перед распоркой, которая всегда остаётся последней.
        var spacer = Math.Max(0, table.RowStyles.Count - 1);
        table.RowStyles.Insert(spacer, new RowStyle(SizeType.Absolute, height));
        table.RowCount = table.RowStyles.Count;
    }

    public static void Add(TableLayoutPanel table, Control control, int column, int row, int columnSpan = 1)
    {
        control.Margin = new Padding(0, 0, columnSpan == 1 ? 12 : 0, 6);
        table.Controls.Add(control, column, row);
        if (columnSpan > 1) table.SetColumnSpan(control, columnSpan);
    }

    public static Panel Field(string caption, Control input)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.Transparent,
            Tag = "transparent",
            Margin = new Padding(0, 0, 12, 6)
        };
        var label = new Label
        {
            Text = caption,
            Dock = DockStyle.Top,
            Height = 18,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = System.Drawing.Color.Transparent,
            Tag = "muted"
        };
        input.Dock = DockStyle.Fill;
        panel.Controls.Add(input);
        panel.Controls.Add(label);
        return panel;
    }

    public static UiTextField Text(string placeholder = "", bool multiline = false)
    {
        var field = new UiTextField { Placeholder = placeholder };
        if (multiline) field.Multiline = true;
        return field;
    }

    public static UiDateField Date(bool nullable = false, DateTime? value = null)
    {
        var field = new UiDateField(nullable);
        if (value.HasValue)
        {
            field.Inner.Value = value.Value;
            field.Inner.Checked = true;
        }
        return field;
    }

    public static Label Value(string text, Font? font = null, System.Drawing.Color? color = null) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        Font = font ?? new Font("Segoe UI", 12f, FontStyle.Bold),
        ForeColor = color ?? ThemeManager.Current.Accent,
        BackColor = System.Drawing.Color.Transparent,
        Padding = new Padding(4, 0, 0, 0)
    };

    public static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace(" ", string.Empty);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static void FillCombo<T>(UiComboBox combo, IEnumerable<T> items, string displayMember, string valueMember, object? selected = null)
    {
        var list = items.ToList();
        combo.Inner.DataSource = list;
        combo.Inner.DisplayMember = displayMember;
        combo.Inner.ValueMember = valueMember;
        if (list.Count == 0) return;

        if (selected is not null)
        {
            try { combo.Inner.SelectedValue = selected; }
            catch { /* значение отсутствует в списке */ }
        }

        try
        {
            if (combo.Inner.SelectedIndex < 0) combo.Inner.SelectedIndex = 0;
        }
        catch
        {
            // DataSource ещё не применён — выбор будет установлен при открытии формы.
        }
    }
}
