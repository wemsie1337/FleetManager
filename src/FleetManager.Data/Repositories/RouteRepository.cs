using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class RouteRepository
{
    private readonly DatabaseContext _db;
    public RouteRepository(DatabaseContext db) => _db = db;

    private const string SelectSql = @"
SELECT r.Id, r.DriverId, ISNULL(u.FullName, N'—') AS DriverName, r.VehicleId,
       v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       r.StartPoint, r.EndPoint, r.PlannedMileage, r.StartDate, r.EndDate, r.Status, r.Notes
FROM dbo.Routes r
JOIN dbo.Drivers d      ON d.Id = r.DriverId
LEFT JOIN dbo.Users u   ON u.Id = d.UserId
JOIN dbo.Vehicles v     ON v.Id = r.VehicleId
JOIN dbo.VehicleBrands b ON b.Id = v.BrandId";

    public async Task<IReadOnlyList<Route>> SearchAsync(int? driverId, string? search, CancellationToken ct = default)
    {
        var sql = SelectSql + @"
WHERE (@DriverId IS NULL OR r.DriverId = @DriverId)
  AND (@Search IS NULL OR r.StartPoint LIKE @Search OR r.EndPoint LIKE @Search
       OR v.PlateNumber LIKE @Search OR u.FullName LIKE @Search)
ORDER BY r.StartDate DESC;";
        return await _db.QueryAsync<Route>(sql, new
        {
            DriverId = driverId,
            Search = SqlHelper.Like(search)
        }, ct: ct);
    }

    public Task<Route?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<Route>(SelectSql + " WHERE r.Id = @Id", new { Id = id }, ct: ct);

    public async Task<int> CreateAsync(Route route, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Routes (DriverId, VehicleId, StartPoint, EndPoint, PlannedMileage, StartDate, EndDate, Status, Notes)
OUTPUT INSERTED.Id
VALUES (@DriverId, @VehicleId, @StartPoint, @EndPoint, @PlannedMileage, @StartDate, @EndDate, @Status, @Notes);";
        return await _db.ExecuteScalarAsync<int>(sql, route, ct: ct);
    }

    public Task<int> UpdateAsync(Route route, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Routes
SET DriverId = @DriverId, VehicleId = @VehicleId, StartPoint = @StartPoint, EndPoint = @EndPoint,
    PlannedMileage = @PlannedMileage, StartDate = @StartDate, EndDate = @EndDate,
    Status = @Status, Notes = @Notes
WHERE Id = @Id;", route, ct: ct);

    public Task<int> UpdateStatusAsync(int routeId, int status, DateTime? endDate, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Routes SET Status = @Status, EndDate = @EndDate WHERE Id = @Id;",
            new { Id = routeId, Status = status, EndDate = endDate }, ct: ct);

    public Task<int> DeleteAsync(int id, CancellationToken ct = default) =>
        _db.ExecuteAsync("DELETE FROM dbo.Routes WHERE Id = @Id", new { Id = id }, ct: ct);
}
