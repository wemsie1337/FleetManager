using FleetManager.Core.Configuration;
using FleetManager.Data;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

/// <summary>
/// Композиционный корень приложения. Создаёт подключение, репозитории и сервисы.
/// UI не работает с репозиториями напрямую.
/// </summary>
public sealed class AppServices : IDisposable
{
    public AppSettings Settings { get; }
    public DatabaseContext Database { get; }
    public SessionContext Session { get; }

    public AuditService Audit { get; }
    public AuthService Auth { get; }
    public UserService Users { get; }
    public LookupService Lookups { get; }
    public DriverService Drivers { get; }
    public VehicleService Vehicles { get; }
    public MileageService Mileage { get; }
    public MaintenanceService Maintenance { get; }
    public RouteService Routes { get; }
    public FuelService Fuel { get; }
    public FineService Fines { get; }
    public DashboardService Dashboard { get; }
    public ReportService Reports { get; }
    public MaintenanceMonitor Monitor { get; }

    private AppServices(AppSettings settings)
    {
        Settings = settings;
        Database = new DatabaseContext(settings.ConnectionString);
        Session = new SessionContext();

        var users = new UserRepository(Database);
        var lookups = new LookupRepository(Database);
        var drivers = new DriverRepository(Database);
        var vehicles = new VehicleRepository(Database);
        var mileage = new MileageRepository(Database);
        var maintenance = new MaintenanceRepository(Database);
        var routes = new RouteRepository(Database);
        var fuel = new FuelRepository(Database);
        var fines = new FineRepository(Database);
        var audit = new AuditRepository(Database);
        var dashboard = new DashboardRepository(Database);

        Audit = new AuditService(audit, Session);
        Auth = new AuthService(users, Session, Audit);
        Users = new UserService(users, Session, Audit);
        Lookups = new LookupService(lookups);
        Drivers = new DriverService(drivers, Audit);
        Vehicles = new VehicleService(vehicles, Audit);
        Mileage = new MileageService(mileage, Audit, Session);
        Maintenance = new MaintenanceService(maintenance, vehicles, Audit, Session, settings);
        Routes = new RouteService(routes, Audit, Session);
        Fuel = new FuelService(fuel, Audit, Session);
        Fines = new FineService(fines, Audit, Session);
        Dashboard = new DashboardService(dashboard, maintenance);
        Reports = new ReportService(settings);
        Monitor = new MaintenanceMonitor(Maintenance, settings.MonitorIntervalMinutes);
    }

    public static AppServices Create(AppSettings settings) => new(settings);

    public void Dispose() => Monitor.Dispose();
}
