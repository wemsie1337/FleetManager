using FleetManager.Core.Models;

namespace FleetManager.Services;

/// <summary>Текущая сессия: авторизованный пользователь и связанный водитель.</summary>
public sealed class SessionContext
{
    public User? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;
    public int? UserId => CurrentUser?.Id;
    public int? DriverId => CurrentUser?.DriverId;
    public string FullName => CurrentUser?.FullName ?? string.Empty;
    public UserRole? Role => CurrentUser is null ? null : (UserRole)CurrentUser.RoleId;

    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsDispatcher => Role == UserRole.Dispatcher;
    public bool IsDriver => Role == UserRole.Driver;
    public bool CanManageFleet => IsAdmin || IsDispatcher;

    public void SignIn(User user) => CurrentUser = user;
    public void SignOut() => CurrentUser = null;
}
