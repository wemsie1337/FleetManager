using System.Drawing;
using System.Windows.Forms;

namespace FleetManager.UI.Theme;

/// <summary>Глобальный менеджер темы + рекурсивное применение к стандартным контролам.</summary>
public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FleetManager", "theme.txt");

    public static ThemePalette Current { get; private set; } = ThemePalette.Light;
    public static bool IsDark => Current.IsDark;

    public static event EventHandler? ThemeChanged;

    public static void LoadPreference()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = File.ReadAllText(SettingsPath).Trim() == "dark" ? ThemePalette.Dark : ThemePalette.Light;
        }
        catch
        {
            Current = ThemePalette.Light;
        }
    }

    public static void Toggle() => SetDark(!IsDark);

    public static void SetDark(bool dark)
    {
        Current = dark ? ThemePalette.Dark : ThemePalette.Light;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, dark ? "dark" : "light");
        }
        catch
        {
            // сохранение настройки не критично
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
        foreach (Form form in Application.OpenForms)
            ApplyTree(form);
    }

    /// <summary>Рекурсивно применяет цвета к стандартным WinForms-контролам.</summary>
    public static void ApplyTree(Control root)
    {
        ApplyControl(root);
        foreach (Control child in root.Controls)
            ApplyTree(child);
    }

    private static void ApplyControl(Control control)
    {
        var t = Current;

        switch (control)
        {
            case Form form:
                form.BackColor = t.Background;
                form.ForeColor = t.Text;
                break;
            case UserControl uc:
                if (uc.Tag as string != "keep-bg") uc.BackColor = t.Background;
                uc.ForeColor = t.Text;
                break;
            case DataGridView grid:
                Helpers.GridStyler.Style(grid);
                break;
            case LinkLabel link:
                link.LinkColor = t.PrimaryLight;
                link.ActiveLinkColor = t.Accent;
                link.VisitedLinkColor = t.PrimaryLight;
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = (label.Tag as string) switch
                {
                    "muted" => t.TextMuted,
                    "accent" => t.Accent,
                    "danger" => t.Danger,
                    "success" => t.Success,
                    "header" => t.Text,
                    _ => t.Text
                };
                break;
            case TextBox textBox:
                textBox.BackColor = t.SurfaceAlt;
                textBox.ForeColor = t.Text;
                textBox.BorderStyle = BorderStyle.None;
                break;
            case ComboBox combo:
                combo.BackColor = t.SurfaceAlt;
                combo.ForeColor = t.Text;
                combo.FlatStyle = FlatStyle.Flat;
                break;
            case DateTimePicker picker:
                picker.BackColor = t.SurfaceAlt;
                picker.ForeColor = t.Text;
                picker.CalendarMonthBackground = t.Surface;
                picker.CalendarForeColor = t.Text;
                picker.CalendarTitleBackColor = t.Primary;
                picker.CalendarTitleForeColor = Color.White;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = t.SurfaceAlt;
                numeric.ForeColor = t.Text;
                numeric.BorderStyle = BorderStyle.None;
                break;
            case CheckBox check:
                check.BackColor = Color.Transparent;
                check.ForeColor = t.Text;
                check.FlatStyle = FlatStyle.Flat;
                break;
            case RadioButton radio:
                radio.BackColor = Color.Transparent;
                radio.ForeColor = t.Text;
                break;
            case TableLayoutPanel table:
                if (table.Tag as string == "transparent") table.BackColor = Color.Transparent;
                break;
            case FlowLayoutPanel flow:
                if (flow.Tag as string == "transparent") flow.BackColor = Color.Transparent;
                break;
            case Panel panel:
                panel.BackColor = (panel.Tag as string) switch
                {
                    "surface" => t.Surface,
                    "alt" => t.SurfaceAlt,
                    "bg" => t.Background,
                    "transparent" => Color.Transparent,
                    "border" => t.Border,
                    _ => panel.BackColor
                };
                panel.ForeColor = t.Text;
                break;
            case GroupBox group:
                group.BackColor = Color.Transparent;
                group.ForeColor = t.TextMuted;
                break;
        }
    }
}
