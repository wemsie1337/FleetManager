namespace FleetManager.Core.Configuration;

/// <summary>Настройки приложения. Загружаются из appsettings.json.</summary>
public sealed class AppSettings
{
    public string ConnectionString { get; set; } =
        @"Server=localhost\SQLEXPRESS;Database=FleetManagerDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    /// <summary>Период фоновой проверки ТО в минутах.</summary>
    public int MonitorIntervalMinutes { get; set; } = 5;

    /// <summary>За сколько километров до ТО показывать жёлтое предупреждение.</summary>
    public int UpcomingThresholdKm { get; set; } = 500;

    /// <summary>На сколько километров можно просрочить ТО до критического статуса.</summary>
    public int OverdueThresholdKm { get; set; } = 1000;

    public bool EnableSound { get; set; } = true;

    public string CompanyName { get; set; } = "ООО «ТрансЛогистика»";

    /// <summary>Папка для экспорта по умолчанию. Пусто — «Мои документы».</summary>
    public string ExportFolder { get; set; } = string.Empty;

    public string AppTitle { get; set; } = "Автопарк";
}
