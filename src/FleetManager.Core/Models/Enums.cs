namespace FleetManager.Core.Models;

/// <summary>Роли пользователей. Значения совпадают с таблицей Roles.</summary>
public enum UserRole
{
    Admin = 1,
    Dispatcher = 2,
    Driver = 3
}

/// <summary>Статусы транспортного средства (таблица VehicleStatuses).</summary>
public enum VehicleState
{
    OnLine = 1,
    InRepair = 2,
    InMaintenance = 3,
    Retired = 4
}

/// <summary>Статусы маршрута.</summary>
public enum RouteState
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>Уровень срочности ТО.</summary>
public enum MaintenanceSeverity
{
    Ok = 0,
    Upcoming = 1,
    Due = 2,
    Overdue = 3
}

public static class EnumText
{
    public static string ToRussian(this RouteState state) => state switch
    {
        RouteState.Planned => "Запланирован",
        RouteState.InProgress => "В работе",
        RouteState.Completed => "Завершён",
        RouteState.Cancelled => "Отменён",
        _ => "Неизвестно"
    };

    public static string ToRussian(this VehicleState state) => state switch
    {
        VehicleState.OnLine => "На линии",
        VehicleState.InRepair => "В ремонте",
        VehicleState.InMaintenance => "На ТО",
        VehicleState.Retired => "Списан",
        _ => "Неизвестно"
    };

    public static string ToRussian(this UserRole role) => role switch
    {
        UserRole.Admin => "Администратор",
        UserRole.Dispatcher => "Диспетчер",
        UserRole.Driver => "Водитель",
        _ => "Неизвестно"
    };

    public static string ToRussian(this MaintenanceSeverity severity) => severity switch
    {
        MaintenanceSeverity.Ok => "В норме",
        MaintenanceSeverity.Upcoming => "Приближается",
        MaintenanceSeverity.Due => "Наступило",
        MaintenanceSeverity.Overdue => "Просрочено",
        _ => "Неизвестно"
    };
}
