using System.Data;
using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class FuelRepository
{
    private readonly DatabaseContext _db;
    public FuelRepository(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<FuelRecord>> SearchAsync(string? search, int? vehicleId, CancellationToken ct = default) =>
        await _db.QueryAsync<FuelRecord>(@"
SELECT f.Id, f.VehicleId, v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       f.[Date], f.Liters, f.CostPerLiter, f.TotalCost, f.Odometer, f.DriverId,
       ISNULL(u.FullName, N'—') AS DriverName
FROM dbo.FuelRecords f
JOIN dbo.Vehicles v      ON v.Id = f.VehicleId
JOIN dbo.VehicleBrands b ON b.Id = v.BrandId
LEFT JOIN dbo.Drivers d  ON d.Id = f.DriverId
LEFT JOIN dbo.Users u    ON u.Id = d.UserId
WHERE (@Search IS NULL OR v.PlateNumber LIKE @Search OR b.BrandName LIKE @Search)
  AND (@VehicleId IS NULL OR f.VehicleId = @VehicleId)
ORDER BY f.[Date] DESC, f.Id DESC;", new
        {
            Search = SqlHelper.Like(search),
            VehicleId = vehicleId
        }, ct: ct);

    public async Task<int> CreateAsync(FuelRecord record, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.FuelRecords (VehicleId, [Date], Liters, CostPerLiter, Odometer, DriverId)
OUTPUT INSERTED.Id
VALUES (@VehicleId, @Date, @Liters, @CostPerLiter, @Odometer, @DriverId);";
        return await _db.ExecuteScalarAsync<int>(sql, record, ct: ct);
    }

    public Task<int> DeleteAsync(int id, CancellationToken ct = default) =>
        _db.ExecuteAsync("DELETE FROM dbo.FuelRecords WHERE Id = @Id", new { Id = id }, ct: ct);

    public async Task<IReadOnlyList<FuelConsumptionInfo>> GetConsumptionAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<FuelConsumptionInfo>("dbo.sp_GetFuelConsumption",
            commandType: CommandType.StoredProcedure, ct: ct);
}
