using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class FineRepository
{
    private readonly DatabaseContext _db;
    public FineRepository(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<Fine>> SearchAsync(string? search, bool? isPaid, CancellationToken ct = default) =>
        await _db.QueryAsync<Fine>(@"
SELECT f.Id, f.VehicleId, v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       f.DriverId, ISNULL(u.FullName, N'—') AS DriverName, f.[Date], f.Amount,
       f.Description, f.IsPaid, f.PaidDate, f.IncidentType
FROM dbo.Fines f
JOIN dbo.Vehicles v      ON v.Id = f.VehicleId
JOIN dbo.VehicleBrands b ON b.Id = v.BrandId
LEFT JOIN dbo.Drivers d  ON d.Id = f.DriverId
LEFT JOIN dbo.Users u    ON u.Id = d.UserId
WHERE (@Search IS NULL OR v.PlateNumber LIKE @Search OR f.Description LIKE @Search)
  AND (@IsPaid IS NULL OR f.IsPaid = @IsPaid)
ORDER BY f.[Date] DESC, f.Id DESC;", new
        {
            Search = SqlHelper.Like(search),
            IsPaid = isPaid
        }, ct: ct);

    public async Task<int> CreateAsync(Fine fine, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Fines (VehicleId, DriverId, [Date], Amount, Description, IsPaid, PaidDate, IncidentType)
OUTPUT INSERTED.Id
VALUES (@VehicleId, @DriverId, @Date, @Amount, @Description, @IsPaid, @PaidDate, @IncidentType);";
        return await _db.ExecuteScalarAsync<int>(sql, fine, ct: ct);
    }

    public Task<int> SetPaidAsync(int id, bool isPaid, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Fines SET IsPaid = @IsPaid, PaidDate = CASE WHEN @IsPaid = 1 THEN CAST(GETDATE() AS DATE) ELSE NULL END
WHERE Id = @Id;", new { Id = id, IsPaid = isPaid }, ct: ct);

    public Task<int> DeleteAsync(int id, CancellationToken ct = default) =>
        _db.ExecuteAsync("DELETE FROM dbo.Fines WHERE Id = @Id", new { Id = id }, ct: ct);
}
