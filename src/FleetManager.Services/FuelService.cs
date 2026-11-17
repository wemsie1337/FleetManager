using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class FuelService
{
    private readonly FuelRepository _fuel;
    private readonly AuditService _audit;
    private readonly SessionContext _session;

    public FuelService(FuelRepository fuel, AuditService audit, SessionContext session)
    {
        _fuel = fuel;
        _audit = audit;
        _session = session;
    }

    public Task<IReadOnlyList<FuelRecord>> SearchAsync(string? search, int? vehicleId) =>
        _fuel.SearchAsync(search, vehicleId);

    public Task<IReadOnlyList<FuelConsumptionInfo>> GetConsumptionAsync() => _fuel.GetConsumptionAsync();

    public async Task<int> AddAsync(FuelRecord record)
    {
        EnsureCanManage();
        if (record.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (record.Liters <= 0) throw new ValidationException("Объём топлива должен быть больше нуля.");
        if (record.CostPerLiter < 0) throw new ValidationException("Цена за литр не может быть отрицательной.");
        if (record.Odometer < 0) throw new ValidationException("Показание одометра не может быть отрицательным.");
        if (record.Date.Date > DateTime.Today)
            throw new ValidationException("Дата заправки не может быть в будущем.");

        var id = await _fuel.CreateAsync(record);
        await _audit.LogAsync("Insert", "FuelRecords", id.ToString(), null,
            $"ТС {record.VehicleId}: {record.Liters:N2} л × {record.CostPerLiter:N2} ₽, одометр {record.Odometer:N0}");
        return id;
    }

    public async Task DeleteAsync(FuelRecord record)
    {
        EnsureCanManage();
        await _fuel.DeleteAsync(record.Id);
        await _audit.LogAsync("Delete", "FuelRecords", record.Id.ToString(),
            $"{record.PlateNumber}, {record.Date:dd.MM.yyyy}, {record.Liters:N2} л", null);
    }

    private void EnsureCanManage()
    {
        if (!_session.CanManageFleet)
            throw new ValidationException("Недостаточно прав для изменения записей топлива.");
    }
}
