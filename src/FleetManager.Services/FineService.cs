using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class FineService
{
    private readonly FineRepository _fines;
    private readonly AuditService _audit;
    private readonly SessionContext _session;

    public FineService(FineRepository fines, AuditService audit, SessionContext session)
    {
        _fines = fines;
        _audit = audit;
        _session = session;
    }

    public Task<IReadOnlyList<Fine>> SearchAsync(string? search, bool? isPaid) => _fines.SearchAsync(search, isPaid);

    public async Task<int> AddAsync(Fine fine)
    {
        EnsureCanManage();
        if (fine.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (fine.Amount <= 0) throw new ValidationException("Сумма должна быть больше нуля.");
        if (string.IsNullOrWhiteSpace(fine.Description)) throw new ValidationException("Укажите описание нарушения.");
        if (fine.Date.Date > DateTime.Today) throw new ValidationException("Дата не может быть в будущем.");
        if (fine.IncidentType is not ("Fine" or "Accident"))
            throw new ValidationException("Выберите тип происшествия.");

        var id = await _fines.CreateAsync(fine);
        await _audit.LogAsync("Insert", "Fines", id.ToString(), null,
            $"{fine.IncidentTypeName}, ТС {fine.VehicleId}, {fine.Amount:N2} ₽");
        return id;
    }

    public async Task SetPaidAsync(Fine fine, bool isPaid)
    {
        EnsureCanManage();
        await _fines.SetPaidAsync(fine.Id, isPaid);
        await _audit.LogAsync("Update", "Fines", fine.Id.ToString(),
            fine.PaidText, isPaid ? "Оплачен" : "Не оплачен");
    }

    public async Task DeleteAsync(Fine fine)
    {
        EnsureCanManage();
        await _fines.DeleteAsync(fine.Id);
        await _audit.LogAsync("Delete", "Fines", fine.Id.ToString(), fine.Description, null);
    }

    private void EnsureCanManage()
    {
        if (!_session.CanManageFleet)
            throw new ValidationException("Недостаточно прав для изменения журнала штрафов.");
    }
}
