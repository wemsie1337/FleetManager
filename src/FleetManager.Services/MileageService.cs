using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class MileageService
{
    private readonly MileageRepository _mileage;
    private readonly AuditService _audit;
    private readonly SessionContext _session;

    public MileageService(MileageRepository mileage, AuditService audit, SessionContext session)
    {
        _mileage = mileage;
        _audit = audit;
        _session = session;
    }

    public Task<IReadOnlyList<MileageLog>> SearchAsync(
        int? vehicleId, int? driverId, DateTime? from, DateTime? to, string? search) =>
        _mileage.SearchAsync(vehicleId, driverId, from, to, search);

    public Task<int> GetCurrentOdometerAsync(int vehicleId) => _mileage.GetCurrentOdometerAsync(vehicleId);

    public Task<IReadOnlyList<MileageLog>> GetVehicleMonthAsync(int vehicleId, int year, int month) =>
        _mileage.GetVehicleMonthAsync(vehicleId, year, month);

    public async Task<int> AddAsync(MileageLog log)
    {
        Validate(log);

        var current = await _mileage.GetCurrentOdometerAsync(log.VehicleId);
        if (log.OdometerStart < current)
            throw new ValidationException(
                $"Начальный одометр не может быть меньше текущего пробега ТС ({current:N0} км).");

        log.CreatedByUserId = _session.UserId;
        log.Date = log.Date.Date;
        log.Note = string.IsNullOrWhiteSpace(log.Note) ? null : log.Note.Trim();

        var id = await _mileage.CreateAsync(log);
        await _audit.LogAsync("Insert", "MileageLogs", id.ToString(), null,
            $"ТС {log.VehicleId}: {log.OdometerStart:N0} → {log.OdometerEnd:N0} км, пробег {log.OdometerEnd - log.OdometerStart:N0} км, дата {log.Date:dd.MM.yyyy}");
        return id;
    }

    private static void Validate(MileageLog log)
    {
        if (log.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (log.DriverId <= 0) throw new ValidationException("Выберите водителя.");
        if (log.OdometerStart < 0 || log.OdometerEnd < 0)
            throw new ValidationException("Показания одометра не могут быть отрицательными.");
        if (log.OdometerEnd <= log.OdometerStart)
            throw new ValidationException("Конечный одометр должен быть больше начального.");
        if (log.Date.Date > DateTime.Today)
            throw new ValidationException("Дата поездки не может быть в будущем.");
        var delta = log.OdometerEnd - log.OdometerStart;
        if (delta > 3000)
            throw new ValidationException("Суточный пробег выглядит некорректно (более 3 000 км). Проверьте показания.");
    }
}
