using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class RouteService
{
    private readonly RouteRepository _routes;
    private readonly AuditService _audit;
    private readonly SessionContext _session;

    public RouteService(RouteRepository routes, AuditService audit, SessionContext session)
    {
        _routes = routes;
        _audit = audit;
        _session = session;
    }

    public Task<IReadOnlyList<Route>> SearchAsync(string? search)
    {
        // Водитель видит только собственные маршруты
        var driverId = _session.IsDriver ? _session.DriverId : null;
        if (_session.IsDriver && driverId is null) return Task.FromResult<IReadOnlyList<Route>>(Array.Empty<Route>());
        return _routes.SearchAsync(driverId, search);
    }

    public async Task<int> SaveAsync(Route route)
    {
        EnsureCanManage();
        Validate(route);

        if (route.Id == 0)
        {
            var id = await _routes.CreateAsync(route);
            await _audit.LogAsync("Create", "Routes", id.ToString(), null,
                $"{route.StartPoint} → {route.EndPoint}, водитель {route.DriverId}, ТС {route.VehicleId}");
            return id;
        }

        await _routes.UpdateAsync(route);
        await _audit.LogAsync("Update", "Routes", route.Id.ToString(), null,
            $"{route.StartPoint} → {route.EndPoint}, статус {route.Status}");
        return route.Id;
    }

    public async Task UpdateStatusAsync(Route route, RouteState state)
    {
        EnsureCanManage();
        DateTime? endDate = state is RouteState.Completed or RouteState.Cancelled
            ? (route.EndDate ?? DateTime.Now)
            : null;
        await _routes.UpdateStatusAsync(route.Id, (int)state, endDate);
        await _audit.LogAsync("UpdateStatus", "Routes", route.Id.ToString(), route.StatusName, state.ToRussian());
    }

    public async Task DeleteAsync(Route route)
    {
        EnsureCanManage();
        await _routes.DeleteAsync(route.Id);
        await _audit.LogAsync("Delete", "Routes", route.Id.ToString(), $"{route.StartPoint} → {route.EndPoint}", null);
    }

    private void EnsureCanManage()
    {
        if (!_session.CanManageFleet)
            throw new ValidationException("Недостаточно прав для изменения маршрутов.");
    }

    private static void Validate(Route route)
    {
        if (route.DriverId <= 0) throw new ValidationException("Выберите водителя.");
        if (route.VehicleId <= 0) throw new ValidationException("Выберите транспортное средство.");
        if (string.IsNullOrWhiteSpace(route.StartPoint)) throw new ValidationException("Укажите пункт отправления.");
        if (string.IsNullOrWhiteSpace(route.EndPoint)) throw new ValidationException("Укажите пункт назначения.");
        if (route.PlannedMileage <= 0) throw new ValidationException("Плановый пробег должен быть больше нуля.");
        if (route.StartDate == default) throw new ValidationException("Укажите дату начала.");
        if (route.EndDate < route.StartDate) throw new ValidationException("Дата окончания не может быть раньше даты начала.");
        if (route.Status is < 1 or > 4) throw new ValidationException("Некорректный статус маршрута.");

        route.StartPoint = route.StartPoint.Trim();
        route.EndPoint = route.EndPoint.Trim();
        route.Notes = string.IsNullOrWhiteSpace(route.Notes) ? null : route.Notes.Trim();
    }
}
