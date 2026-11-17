using FleetManager.Core.Configuration;
using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class MaintenanceService
{
    private readonly MaintenanceRepository _maintenance;
    private readonly VehicleRepository _vehicles;
    private readonly AuditService _audit;
    private readonly SessionContext _session;
    private readonly AppSettings _settings;

    public MaintenanceService(MaintenanceRepository maintenance, VehicleRepository vehicles,
        AuditService audit, SessionContext session, AppSettings settings)
    {
        _maintenance = maintenance;
        _vehicles = vehicles;
        _audit = audit;
        _session = session;
        _settings = settings;
    }

    public Task<IReadOnlyList<MaintenanceType>> GetTypesAsync() => _maintenance.GetTypesAsync();
    public Task<IReadOnlyList<VehicleMaintenancePlan>> GetPlansAsync(int? vehicleId) => _maintenance.GetPlansAsync(vehicleId);
    public Task<IReadOnlyList<MaintenanceRecord>> GetRecordsAsync(int? vehicleId) => _maintenance.GetRecordsAsync(vehicleId);

    public Task<IReadOnlyList<MaintenanceDue>> GetDueAsync() =>
        _maintenance.GetDueAsync(_settings.UpcomingThresholdKm, _settings.OverdueThresholdKm);

    public Task<IReadOnlyList<MaintenanceAlert>> GetActiveAlertsAsync() => _maintenance.GetAlertsAsync(false);

    public async Task RefreshAlertsAsync() =>
        await _maintenance.RefreshAlertsAsync(_settings.UpcomingThresholdKm, _settings.OverdueThresholdKm);

    public async Task<int> SaveRecordAsync(MaintenanceRecord record)
    {
        if (record.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (record.MaintenanceTypeId <= 0) throw new ValidationException("Выберите вид ТО.");
        if (record.MileageAtService < 0) throw new ValidationException("Пробег не может быть отрицательным.");
        if (record.Cost < 0) throw new ValidationException("Стоимость не может быть отрицательной.");
        if (string.IsNullOrWhiteSpace(record.Performer)) throw new ValidationException("Укажите исполнителя работ.");
        if (record.Date.Date > DateTime.Today.AddDays(1))
            throw new ValidationException("Дата ТО не может быть в будущем.");

        var vehicle = await _vehicles.GetByIdAsync(record.VehicleId)
                      ?? throw new ValidationException("Транспортное средство не найдено.");
        if (record.MileageAtService > vehicle.CurrentMileage + 100)
            throw new ValidationException(
                $"Пробег при ТО не может превышать текущий пробег ТС ({vehicle.CurrentMileage:N0} км).");

        record.Performer = record.Performer.Trim();
        record.Notes = string.IsNullOrWhiteSpace(record.Notes) ? null : record.Notes.Trim();
        record.CreatedByUserId = _session.UserId;

        var id = await _maintenance.CreateRecordAsync(record);
        await RefreshAlertsAsync();
        await _audit.LogAsync("Insert", "MaintenanceRecords", id.ToString(), null,
            $"ТС {vehicle.PlateNumber}: ТО №{record.MaintenanceTypeId}, {record.MileageAtService:N0} км, {record.Cost:N2} ₽");
        return id;
    }

    public async Task SavePlanAsync(VehicleMaintenancePlan plan)
    {
        if (plan.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (plan.MaintenanceTypeId <= 0) throw new ValidationException("Выберите вид ТО.");
        if (plan.IntervalKm <= 0) throw new ValidationException("Интервал ТО должен быть больше нуля.");
        if (plan.LastServiceMileage < 0) throw new ValidationException("Пробег последнего ТО не может быть отрицательным.");
        if (plan.LastServiceDate > DateTime.Today)
            throw new ValidationException("Дата последнего ТО не может быть в будущем.");

        var vehicle = await _vehicles.GetByIdAsync(plan.VehicleId)
                      ?? throw new ValidationException("Транспортное средство не найдено.");
        if (plan.LastServiceMileage > vehicle.CurrentMileage)
            throw new ValidationException(
                $"Пробег последнего ТО не может превышать текущий пробег ТС ({vehicle.CurrentMileage:N0} км).");

        await _maintenance.SavePlanAsync(plan);
        await RefreshAlertsAsync();
        await _audit.LogAsync("Upsert", "VehicleMaintenancePlans", $"{plan.VehicleId}/{plan.MaintenanceTypeId}",
            null, $"Интервал {plan.IntervalKm:N0} км, последнее ТО {plan.LastServiceMileage:N0} км");
    }

    public async Task DeletePlanAsync(VehicleMaintenancePlan plan)
    {
        await _maintenance.DeletePlanAsync(plan.Id);
        await RefreshAlertsAsync();
        await _audit.LogAsync("Delete", "VehicleMaintenancePlans", plan.Id.ToString(), plan.TypeName, null);
    }

    public async Task AcknowledgeAsync(int alertId)
    {
        await _maintenance.AcknowledgeAsync(alertId);
        await _audit.LogAsync("Acknowledge", "MaintenanceAlerts", alertId.ToString());
    }

    public async Task AcknowledgeAllAsync()
    {
        await _maintenance.AcknowledgeAllAsync();
        await _audit.LogAsync("AcknowledgeAll", "MaintenanceAlerts");
    }
}
