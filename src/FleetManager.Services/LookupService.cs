using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class LookupService
{
    private readonly LookupRepository _lookups;
    public LookupService(LookupRepository lookups) => _lookups = lookups;

    public Task<IReadOnlyList<VehicleStatus>> GetVehicleStatusesAsync() => _lookups.GetVehicleStatusesAsync();
    public Task<IReadOnlyList<VehicleBrand>> GetBrandsAsync() => _lookups.GetBrandsAsync();
    public Task<IReadOnlyList<FuelType>> GetFuelTypesAsync() => _lookups.GetFuelTypesAsync();

    public Task<int> AddBrandAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Введите название марки.");
        return _lookups.AddBrandAsync(name.Trim());
    }
}

public sealed class DriverService
{
    private readonly DriverRepository _drivers;
    private readonly AuditService _audit;

    public DriverService(DriverRepository drivers, AuditService audit)
    {
        _drivers = drivers;
        _audit = audit;
    }

    public Task<IReadOnlyList<Driver>> GetAllAsync() => _drivers.GetAllAsync();
    public Task<IReadOnlyList<User>> GetAvailableUsersAsync(int? currentDriverId) =>
        _drivers.GetAvailableUsersAsync(currentDriverId);

    public async Task<int> SaveAsync(Driver driver)
    {
        if (driver.UserId is null or <= 0)
            throw new ValidationException("Выберите учётную запись пользователя с ролью «Водитель».");
        if (string.IsNullOrWhiteSpace(driver.LicenseNumber))
            throw new ValidationException("Укажите номер водительского удостоверения.");
        if (string.IsNullOrWhiteSpace(driver.Category))
            throw new ValidationException("Укажите категорию прав.");
        if (string.IsNullOrWhiteSpace(driver.Phone))
            throw new ValidationException("Укажите телефон водителя.");
        if (driver.HireDate == default)
            throw new ValidationException("Укажите дату приёма на работу.");
        if (driver.HireDate > DateTime.Today)
            throw new ValidationException("Дата приёма не может быть в будущем.");

        driver.LicenseNumber = driver.LicenseNumber.Trim().ToUpperInvariant();
        driver.Category = driver.Category.Trim().ToUpperInvariant();
        driver.Phone = driver.Phone.Trim();

        if (driver.Id == 0)
        {
            var id = await _drivers.CreateAsync(driver);
            await _audit.LogAsync("Create", "Drivers", id.ToString(), null, driver.LicenseNumber);
            return id;
        }

        await _drivers.UpdateAsync(driver);
        await _audit.LogAsync("Update", "Drivers", driver.Id.ToString(), null, driver.LicenseNumber);
        return driver.Id;
    }
}

public sealed class DashboardData
{
    public FleetKpi Kpi { get; init; } = new();
    public IReadOnlyList<MonthlyCost> Monthly { get; init; } = Array.Empty<MonthlyCost>();
    public IReadOnlyList<MaintenanceAlert> Alerts { get; init; } = Array.Empty<MaintenanceAlert>();
    public IReadOnlyList<MileageLog> RecentMileage { get; init; } = Array.Empty<MileageLog>();
    public int Year { get; init; }
}

public sealed class DashboardService
{
    private readonly DashboardRepository _dashboard;
    private readonly MaintenanceRepository _maintenance;

    public DashboardService(DashboardRepository dashboard, MaintenanceRepository maintenance)
    {
        _dashboard = dashboard;
        _maintenance = maintenance;
    }

    public async Task<DashboardData> LoadAsync(int year, int? driverId = null)
    {
        var kpiTask = _dashboard.GetKpiAsync();
        var monthlyTask = _dashboard.GetMonthlyCostsAsync(year);
        var alertsTask = _maintenance.GetAlertsAsync(false);
        var mileageTask = _dashboard.GetRecentMileageAsync(8, driverId);
        await Task.WhenAll(kpiTask, monthlyTask, alertsTask, mileageTask);

        return new DashboardData
        {
            Kpi = kpiTask.Result,
            Monthly = monthlyTask.Result,
            Alerts = alertsTask.Result,
            RecentMileage = mileageTask.Result,
            Year = year
        };
    }
}
