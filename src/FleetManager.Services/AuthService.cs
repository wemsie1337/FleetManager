using System.Text.RegularExpressions;
using FleetManager.Core.Models;
using FleetManager.Data.Repositories;
using FleetManager.Services.Security;

namespace FleetManager.Services;

public sealed class AuthService
{
    private static readonly Regex LoginRegex = new(@"^[a-zA-Z0-9._-]{3,32}$", RegexOptions.Compiled);

    private readonly UserRepository _users;
    private readonly SessionContext _session;
    private readonly AuditService _audit;

    public AuthService(UserRepository users, SessionContext session, AuditService audit)
    {
        _users = users;
        _session = session;
        _audit = audit;
    }

    public async Task<User> LoginAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Введите логин и пароль.");

        var user = await _users.GetByLoginAsync(login.Trim());

        if (user is null || !PasswordHasher.Verify(password, user.Salt, user.PasswordHash))
        {
            await _audit.LogForUserAsync(null, "LoginFailed", "Users", null, null,
                $"Неудачная попытка входа, логин: {login.Trim()}");
            throw new ValidationException("Неверный логин или пароль.");
        }

        if (!user.IsActive)
            throw new ValidationException("Учётная запись отключена. Обратитесь к администратору.");

        await _users.TouchLoginAsync(user.Id);
        _session.SignIn(user);
        await _audit.LogForUserAsync(user.Id, "Login", "Users", user.Id.ToString(), null, "Вход в систему");
        return user;
    }

    public async Task LogoutAsync()
    {
        if (_session.IsAuthenticated)
            await _audit.LogAsync("Logout", "Users", _session.UserId?.ToString(), null, "Выход из системы");
        _session.SignOut();
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            throw new ValidationException("Введите текущий пароль.");

        ValidateNewPassword(newPassword, confirmPassword);

        var user = await _users.GetByIdAsync(userId)
                   ?? throw new ValidationException("Пользователь не найден.");

        if (!PasswordHasher.Verify(currentPassword, user.Salt, user.PasswordHash))
            throw new ValidationException("Текущий пароль указан неверно.");

        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(newPassword, salt);
        await _users.UpdatePasswordAsync(userId, hash, salt);
        await _audit.LogAsync("ChangePassword", "Users", userId.ToString(), null, "Пароль изменён");
    }

    public static void ValidateNewPassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ValidationException("Пароль должен содержать не менее 8 символов.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Пароль должен содержать буквы и цифры.");
        if (password != confirmPassword)
            throw new ValidationException("Пароли не совпадают.");
    }

    public static bool IsValidLogin(string login) => LoginRegex.IsMatch(login.Trim());
}
