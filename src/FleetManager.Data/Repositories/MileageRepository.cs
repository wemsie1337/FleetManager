using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class MileageRepository
{
    private readonly DatabaseContext _db;
    public MileageRepository(DatabaseContext db) => _db = db;

    private const string SelectSql = @"
SELECT m.Id, m.VehicleId, v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       m.DriverId, ISNULL(du.FullName, N'—') AS DriverName,
       m.OdometerStart, m.OdometerEnd, m.TripMileage, m.[Date], m.Note,
       m.CreatedByUserId, ISNULL(cu.FullName, N'Система') AS CreatedByName, m.CreatedAt
FROM dbo.MileageLogs m
JOIN dbo.Vehicles v      ON v.Id = m.VehicleId
JOIN dbo.VehicleBrands b ON b.Id = v.BrandId
JOIN dbo.Drivers d       ON d.Id = m.DriverId
LEFT JOIN dbo.Users du   ON du.Id = d.UserId
LEFT JOIN dbo.Users cu   ON cu.Id = m.CreatedByUserId";

    public async Task<IReadOnlyList<MileageLog>> SearchAsync(
        int? vehicleId, int? driverId, DateTime? from, DateTime? to, string? search,
        CancellationToken ct = default)
    {
        var sql = SelectSql + @"
WHERE (@VehicleId IS NULL OR m.VehicleId = @VehicleId)
  AND (@DriverId IS NULL OR m.DriverId = @DriverId)
  AND (@From IS NULL OR m.[Date] >= @From)
  AND (@To IS NULL OR m.[Date] <= @To)
  AND (@Search IS NULL OR v.PlateNumber LIKE @Search OR du.FullName LIKE @Search OR m.Note LIKE @Search)
ORDER BY m.[Date] DESC, m.Id DESC;";
        return await _db.QueryAsync<MileageLog>(sql, new
        {
            VehicleId = vehicleId,
            DriverId = driverId,
            From = from,
            To = to,
            Search = SqlHelper.Like(search)
        }, ct: ct);
    }

    public Task<int> GetCurrentOdometerAsync(int vehicleId, CancellationToken ct = default) =>
        _db.ExecuteScalarAsync<int>(@"
SELECT ISNULL(MAX(x.Mileage), 0)
FROM (SELECT CurrentMileage AS Mileage FROM dbo.Vehicles WHERE Id = @VehicleId
      UNION ALL
      SELECT MAX(OdometerEnd) FROM dbo.MileageLogs WHERE VehicleId = @VehicleId) x;",
            new { VehicleId = vehicleId }, ct: ct);

    public async Task<int> CreateAsync(MileageLog log, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.MileageLogs (VehicleId, DriverId, OdometerStart, OdometerEnd, [Date], Note, CreatedByUserId)
VALUES (@VehicleId, @DriverId, @OdometerStart, @OdometerEnd, @Date, @Note, @CreatedByUserId);
-- Триггер на MileageLogs обновляет пробег ТС, поэтому OUTPUT без INTO использовать нельзя.
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await _db.ExecuteScalarAsync<int>(sql, log, ct: ct);
    }

    /// <summary>Сводка пробега по дням для конкретного ТС.</summary>
    public async Task<IReadOnlyList<MileageLog>> GetVehicleMonthAsync(int vehicleId, int year, int month, CancellationToken ct = default) =>
        await _db.QueryAsync<MileageLog>(@"
SELECT m.Id, m.VehicleId, m.DriverId, m.OdometerStart, m.OdometerEnd, m.TripMileage,
       m.[Date], m.Note, m.CreatedAt
FROM dbo.MileageLogs m
WHERE m.VehicleId = @VehicleId AND YEAR(m.[Date]) = @Year AND MONTH(m.[Date]) = @Month
ORDER BY m.[Date];", new { VehicleId = vehicleId, Year = year, Month = month }, ct: ct);
}
