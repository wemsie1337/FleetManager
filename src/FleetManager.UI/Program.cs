using System.Globalization;
using System.Text.Json;
using FleetManager.Core.Configuration;
using FleetManager.Services;
using FleetManager.Services.Infrastructure;
using FleetManager.UI.Forms;
using FleetManager.UI.Theme;

namespace FleetManager.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        Application.ThreadException += (_, e) => HandleFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);

        ThemeManager.LoadPreference();

        var settings = LoadSettings();
        using var services = AppServices.Create(settings);

        while (true)
        {
            using (var login = new LoginForm(services))
            {
                if (login.ShowDialog() != DialogResult.OK) break;
            }

            var main = new MainForm(services);
            Application.Run(main);
            var logout = main.LogoutRequested;
            main.Dispose();

            if (!logout) break;
        }
    }

    /// <summary>
    /// Читает appsettings.json и переменные окружения с префиксом FLEET_
    /// (например, FLEET_ConnectionString переопределяет строку подключения).
    /// </summary>
    private static AppSettings LoadSettings()
    {
        var settings = new AppSettings();

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) settings = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Повреждённый файл настроек не должен мешать запуску — работаем со значениями по умолчанию.
            Log(ex);
        }

        ApplyEnvironmentOverrides(settings);
        return settings;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Переопределение настроек переменными окружения FLEET_* (удобно для развёртывания).</summary>
    private static void ApplyEnvironmentOverrides(AppSettings settings)
    {
        static string? Read(string name) => Environment.GetEnvironmentVariable("FLEET_" + name);

        var connection = Read("CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(connection)) settings.ConnectionString = connection;

        if (int.TryParse(Read("MONITORINTERVALMINUTES"), out var interval) && interval > 0)
            settings.MonitorIntervalMinutes = interval;

        if (int.TryParse(Read("UPCOMINGTHRESHOLDKM"), out var upcoming) && upcoming >= 0)
            settings.UpcomingThresholdKm = upcoming;

        if (int.TryParse(Read("OVERDUETHRESHOLDKM"), out var overdue) && overdue >= 0)
            settings.OverdueThresholdKm = overdue;

        if (bool.TryParse(Read("ENABLESOUND"), out var sound))
            settings.EnableSound = sound;

        var company = Read("COMPANYNAME");
        if (!string.IsNullOrWhiteSpace(company)) settings.CompanyName = company;

        var exportFolder = Read("EXPORTFOLDER");
        if (!string.IsNullOrWhiteSpace(exportFolder)) settings.ExportFolder = exportFolder;

        var title = Read("APPTITLE");
        if (!string.IsNullOrWhiteSpace(title)) settings.AppTitle = title;
    }

    private static void HandleFatal(Exception exception)
    {
        Log(exception);
        MessageBox.Show(
            "Произошла непредвиденная ошибка.\n\n" + ErrorTranslator.ToFriendlyMessage(exception) +
            "\n\nПодробности сохранены в журнале %AppData%\\FleetManager\\logs.",
            "Автопарк — ошибка",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void Log(Exception? exception)
    {
        if (exception is null) return;
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FleetManager", "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"app-{DateTime.Today:yyyyMMdd}.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception.GetType().Name}: {exception.Message}{Environment.NewLine}" +
                $"{exception.StackTrace}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}");
        }
        catch
        {
            // ведение журнала не должно приводить к падению приложения
        }
    }
}
