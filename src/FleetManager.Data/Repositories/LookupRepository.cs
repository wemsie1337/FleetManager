using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class LookupRepository
{
    private readonly DatabaseContext _db;
    public LookupRepository(DatabaseContext db) => _db = db;

    public async Task<IReadOnlyList<VehicleStatus>> GetVehicleStatusesAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<VehicleStatus>(
            "SELECT Id, StatusName, ColorHex FROM dbo.VehicleStatuses ORDER BY Id", ct: ct);

    public async Task<IReadOnlyList<VehicleBrand>> GetBrandsAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<VehicleBrand>(
            "SELECT Id, BrandName FROM dbo.VehicleBrands ORDER BY BrandName", ct: ct);

    public async Task<IReadOnlyList<FuelType>> GetFuelTypesAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<FuelType>(
            "SELECT Id, FuelName, PricePerLiter FROM dbo.FuelTypes ORDER BY FuelName", ct: ct);

    public async Task<int> AddBrandAsync(string name, CancellationToken ct = default) =>
        await _db.ExecuteScalarAsync<int>(@"
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleBrands WHERE BrandName = @Name)
    INSERT INTO dbo.VehicleBrands (BrandName) OUTPUT INSERTED.Id VALUES (@Name);
ELSE
    SELECT Id FROM dbo.VehicleBrands WHERE BrandName = @Name;", new { Name = name }, ct: ct);
}
