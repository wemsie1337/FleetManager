using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class DriverRepository
{
    private readonly DatabaseContext _db;
    public DriverRepository(DatabaseContext db) => _db = db;

    private const string SelectSql = @"
SELECT d.Id, d.UserId, ISNULL(u.FullName, N'Без учётной записи') AS FullName,
       d.LicenseNumber, d.Category, d.HireDate, d.Phone, d.IsActive, u.Login
FROM dbo.Drivers d
LEFT JOIN dbo.Users u ON u.Id = d.UserId";

    public async Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<Driver>(SelectSql + " ORDER BY FullName", ct: ct);

    public Task<Driver?> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<Driver>(SelectSql + " WHERE d.UserId = @UserId", new { UserId = userId }, ct: ct);

    public Task<Driver?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<Driver>(SelectSql + " WHERE d.Id = @Id", new { Id = id }, ct: ct);

    /// <summary>Пользователи с ролью «Водитель», ещё не привязанные к карточке водителя.</summary>
    public async Task<IReadOnlyList<User>> GetAvailableUsersAsync(int? currentDriverId, CancellationToken ct = default) =>
        await _db.QueryAsync<User>(@"
SELECT u.Id, u.Login, u.FullName, u.RoleId, r.RoleName, u.IsActive, u.CreatedAt, u.LastLoginAt,
       d.Id AS DriverId
FROM dbo.Users u
JOIN dbo.Roles r ON r.Id = u.RoleId
LEFT JOIN dbo.Drivers d ON d.UserId = u.Id
WHERE u.RoleId = 3
  AND (d.Id IS NULL OR d.Id = @CurrentDriverId)
ORDER BY u.FullName", new { CurrentDriverId = currentDriverId }, ct: ct);

    public async Task<int> CreateAsync(Driver driver, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Drivers (UserId, LicenseNumber, Category, HireDate, Phone, IsActive)
OUTPUT INSERTED.Id
VALUES (@UserId, @LicenseNumber, @Category, @HireDate, @Phone, @IsActive);";
        return await _db.ExecuteScalarAsync<int>(sql, driver, ct: ct);
    }

    public Task<int> UpdateAsync(Driver driver, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Drivers
SET UserId = @UserId, LicenseNumber = @LicenseNumber, Category = @Category,
    HireDate = @HireDate, Phone = @Phone, IsActive = @IsActive
WHERE Id = @Id;", driver, ct: ct);
}
