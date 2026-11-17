using Dapper;
using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class UserRepository
{
    private readonly DatabaseContext _db;
    public UserRepository(DatabaseContext db) => _db = db;

    private const string SelectSql = @"
SELECT u.Id, u.Login, u.PasswordHash, u.Salt, u.FullName, u.RoleId, r.RoleName,
       u.IsActive, u.CreatedAt, u.LastLoginAt, d.Id AS DriverId
FROM dbo.Users u
JOIN dbo.Roles r ON r.Id = u.RoleId
LEFT JOIN dbo.Drivers d ON d.UserId = u.Id";

    public Task<User?> GetByLoginAsync(string login, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<User>(SelectSql + " WHERE u.Login = @Login", new { Login = login }, ct: ct);

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<User>(SelectSql + " WHERE u.Id = @Id", new { Id = id }, ct: ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<User>(SelectSql + " ORDER BY u.FullName", ct: ct);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<Role>("SELECT Id, RoleName, Description FROM dbo.Roles ORDER BY Id", ct: ct);

    public async Task<int> CreateAsync(User user, string passwordHash, string salt, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Users (Login, PasswordHash, Salt, FullName, RoleId, IsActive)
OUTPUT INSERTED.Id
VALUES (@Login, @PasswordHash, @Salt, @FullName, @RoleId, @IsActive);";
        return await _db.ExecuteScalarAsync<int>(sql, new
        {
            user.Login,
            PasswordHash = passwordHash,
            Salt = salt,
            user.FullName,
            user.RoleId,
            user.IsActive
        }, ct: ct);
    }

    public Task<int> UpdateAsync(User user, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Users
SET Login = @Login, FullName = @FullName, RoleId = @RoleId, IsActive = @IsActive
WHERE Id = @Id;", user, ct: ct);

    public Task<int> UpdatePasswordAsync(int userId, string passwordHash, string salt, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Users SET PasswordHash = @PasswordHash, Salt = @Salt WHERE Id = @Id;",
            new { Id = userId, PasswordHash = passwordHash, Salt = salt }, ct: ct);

    public Task<int> SetActiveAsync(int userId, bool isActive, CancellationToken ct = default) =>
        _db.ExecuteAsync("UPDATE dbo.Users SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = userId, IsActive = isActive }, ct: ct);

    public Task<int> TouchLoginAsync(int userId, CancellationToken ct = default) =>
        _db.ExecuteAsync("UPDATE dbo.Users SET LastLoginAt = SYSUTCDATETIME() WHERE Id = @Id",
            new { Id = userId }, ct: ct);

    public Task<int> CountActiveAdminsAsync(int excludeUserId, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(@"
SELECT COUNT(*) FROM dbo.Users
WHERE RoleId = 1 AND IsActive = 1 AND Id <> @Id", new { Id = excludeUserId }, ct: ct);
}
