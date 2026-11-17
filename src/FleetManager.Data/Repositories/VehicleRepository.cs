using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class VehicleRepository
{
    private readonly DatabaseContext _db;
    public VehicleRepository(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<Vehicle>> SearchAsync(string? search, int? statusId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT * FROM dbo.vw_Vehicles
WHERE (@Search IS NULL OR PlateNumber LIKE @Search OR BrandName LIKE @Search OR Model LIKE @Search OR Vin LIKE @Search)
  AND (@StatusId IS NULL OR StatusId = @StatusId)
ORDER BY PlateNumber;";
        return await _db.QueryAsync<Vehicle>(sql, new
        {
            Search = SqlHelper.Like(search),
            StatusId = statusId
        }, ct: ct);
    }

    public Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.QueryFirstOrDefaultAsync<Vehicle>(
            "SELECT * FROM dbo.vw_Vehicles WHERE Id = @Id", new { Id = id }, ct: ct);

    public Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken ct = default) =>
        SearchAsync(null, null, ct);

    public async Task<int> CreateAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.Vehicles
    (BrandId, Model, PlateNumber, Vin, Year, CurrentMileage, StatusId, FuelTypeId,
     PlannedConsumption, PurchaseDate, Notes)
OUTPUT INSERTED.Id
VALUES
    (@BrandId, @Model, @PlateNumber, @Vin, @Year, @CurrentMileage, @StatusId, @FuelTypeId,
     @PlannedConsumption, @PurchaseDate, @Notes);";
        return await _db.ExecuteScalarAsync<int>(sql, vehicle, ct: ct);
    }

    public Task<int> UpdateAsync(Vehicle vehicle, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Vehicles
SET BrandId = @BrandId, Model = @Model, PlateNumber = @PlateNumber, Vin = @Vin,
    Year = @Year, CurrentMileage = @CurrentMileage, StatusId = @StatusId,
    FuelTypeId = @FuelTypeId, PlannedConsumption = @PlannedConsumption,
    PurchaseDate = @PurchaseDate, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id;", vehicle, ct: ct);

    public Task<int> UpdateStatusAsync(int vehicleId, int statusId, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.Vehicles SET StatusId = @StatusId, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id;",
            new { Id = vehicleId, StatusId = statusId }, ct: ct);

    public Task<int> DeleteAsync(int id, CancellationToken ct = default) =>
        _db.ExecuteAsync("DELETE FROM dbo.Vehicles WHERE Id = @Id", new { Id = id }, ct: ct);
}
