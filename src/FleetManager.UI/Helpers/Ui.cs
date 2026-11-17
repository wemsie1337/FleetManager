using System.Drawing;
using System.Windows.Forms;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Helpers;

public enum ToastKind
{
    Success,
    Error,
    Warning,
    Info
}

public interface IToastHost
{
    void ShowToast(ToastKind kind, string title, string message);
}

public static class Ui
{
    public static class Fonts
    {
        public static readonly Font Regular = new("Segoe UI", 9.5f);
        public static readonly Font Small = new("Segoe UI", 8.5f);
        public static readonly Font Medium = new("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Bold = new("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Title = new("Segoe UI", 20f, FontStyle.Bold);
        public static readonly Font Subtitle = new("Segoe UI", 9.5f);
        public static readonly Font Kpi = new("Segoe UI", 21f, FontStyle.Bold);
        public static readonly Font Icon = new("Segoe MDL2 Assets", 12f);
        public static readonly Font IconLarge = new("Segoe MDL2 Assets", 18f);
        public static readonly Font Glyph = new("Segoe MDL2 Assets", 15f);
    }

    public static string Money(decimal value) => $"{value:N0} ₽";
    public static string Money2(decimal value) => $"{value:N2} ₽";
    public static string Km(int value) => $"{value:N0} км";

    public static void ShowError(IWin32Window? owner, Exception ex) =>
        Dialogs.MessageDialog.Show(owner, "Ошибка", Services.Infrastructure.ErrorTranslator.ToFriendlyMessage(ex), ToastKind.Error);

    public static void ShowError(IWin32Window? owner, string message) =>
        Dialogs.MessageDialog.Show(owner, "Ошибка", message, ToastKind.Error);

    public static void ShowInfo(IWin32Window? owner, string message) =>
        Dialogs.MessageDialog.Show(owner, "Информация", message, ToastKind.Info);

    public static bool Confirm(IWin32Window? owner, string title, string message) =>
        Dialogs.MessageDialog.Confirm(owner, title, message);

    public static void Notify(Control source, ToastKind kind, string title, string message)
    {
        if (source.FindForm() is IToastHost host)
            host.ShowToast(kind, title, message);
    }

    public static Color SeverityColor(Core.Models.MaintenanceSeverity severity) => severity switch
    {
        Core.Models.MaintenanceSeverity.Overdue => ThemeManager.Current.Danger,
        Core.Models.MaintenanceSeverity.Due => ThemeManager.Current.Accent,
        Core.Models.MaintenanceSeverity.Upcoming => ThemeManager.Current.Warning,
        _ => ThemeManager.Current.Success
    };

    public static Color RouteStatusColor(int status) => status switch
    {
        1 => ThemeManager.Current.Info,
        2 => ThemeManager.Current.Warning,
        3 => ThemeManager.Current.Success,
        4 => ThemeManager.Current.TextMuted,
        _ => ThemeManager.Current.TextMuted
    };

    public static void EnableDoubleBuffer(Control control)
    {
        var property = typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        property?.SetValue(control, true);
    }
}
