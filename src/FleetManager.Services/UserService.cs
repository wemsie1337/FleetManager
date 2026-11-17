using FleetManager.Core.Models;
using FleetManager.Data.Repositories;
using FleetManager.Services.Security;

namespace FleetManager.Services;

public sealed class UserService
{
    private readonly UserRepository _users;
    private readonly SessionContext _session;
    private readonly AuditService _audit;

    public UserService(UserRepository users, SessionContext session, AuditService audit)
    {
        _users = users;
        _session = session;
        _audit = audit;
    }

    public Task<IReadOnlyList<User>> GetAllAsync() => _users.GetAllAsync();
    public Task<IReadOnlyList<Role>> GetRolesAsync() => _users.GetRolesAsync();

    public async Task<int> CreateAsync(User user, string password)
    {
        EnsureAdmin();
        ValidateUser(user);
        if (!AuthService.IsValidLogin(user.Login))
            throw new ValidationException("Логин: 3–32 символа, только латиница, цифры, точка, дефис и подчёркивание.");
        AuthService.ValidateNewPassword(password, password);

        var existing = await _users.GetByLoginAsync(user.Login.Trim());
        if (existing is not null)
            throw new ValidationException("Пользователь с таким логином уже существует.");

        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(password, salt);
        user.Login = user.Login.Trim();
        var id = await _users.CreateAsync(user, hash, salt);
        await _audit.LogAsync("Create", "Users", id.ToString(), null,
            $"Создан пользователь {user.Login}, роль {user.RoleId}");
        return id;
    }

    public async Task UpdateAsync(User user)
    {
        EnsureAdmin();
        ValidateUser(user);

        if (user.RoleId != (int)UserRole.Admin || !user.IsActive)
        {
            var remainingAdmins = await _users.CountActiveAdminsAsync(user.Id);
            if (remainingAdmins == 0)
                throw new ValidationException("Нельзя отключить или лишить прав последнего администратора.");
        }

        await _users.UpdateAsync(user);
        await _audit.LogAsync("Update", "Users", user.Id.ToString(), null,
            $"Обновлён пользователь {user.Login}, роль {user.RoleId}, активен: {user.IsActive}");
    }

    public async Task SetActiveAsync(int userId, bool isActive)
    {
        EnsureAdmin();
        if (!isActive)
        {
            var remainingAdmins = await _users.CountActiveAdminsAsync(userId);
            if (remainingAdmins == 0)
                throw new ValidationException("Нельзя отключить последнего администратора.");
        }

        await _users.SetActiveAsync(userId, isActive);
        await _audit.LogAsync("Update", "Users", userId.ToString(), null,
            isActive ? "Учётная запись включена" : "Учётная запись отключена");
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, string confirmPassword)
    {
        EnsureAdmin();
        AuthService.ValidateNewPassword(newPassword, confirmPassword);
        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(newPassword, salt);
        await _users.UpdatePasswordAsync(userId, hash, salt);
        await _audit.LogAsync("ResetPassword", "Users", userId.ToString(), null, "Пароль сброшен администратором");
    }

    private void EnsureAdmin()
    {
        if (!_session.IsAdmin)
            throw new ValidationException("Действие доступно только администратору.");
    }

    private static void ValidateUser(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Login))
            throw new ValidationException("Укажите логин.");
        if (string.IsNullOrWhiteSpace(user.FullName) || user.FullName.Trim().Length < 3)
            throw new ValidationException("Укажите ФИО пользователя (не менее 3 символов).");
        if (user.RoleId is < 1 or > 3)
            throw new ValidationException("Выберите роль пользователя.");
    }
}
