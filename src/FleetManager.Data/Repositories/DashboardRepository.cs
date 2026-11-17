using System.Data;
using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class DashboardRepository
{
    private readonly DatabaseContext _db;
    public DashboardRepository(DatabaseContext db) => _db = db;

    public async Task<FleetKpi> GetKpiAsync(CancellationToken ct = default) =>
        await _db.QueryFirstOrDefaultAsync<FleetKpi>("dbo.sp_GetFleetKpi",
            commandType: CommandType.StoredProcedure, ct: ct) ?? new FleetKpi();

    public async Task<IReadOnlyList<MonthlyCost>> GetMonthlyCostsAsync(int year, CancellationToken ct = default) =>
        await _db.QueryAsync<MonthlyCost>("dbo.sp_GetMonthlyCosts",
            new { Year = year }, CommandType.StoredProcedure, ct);

    public async Task<IReadOnlyList<MileageLog>> GetRecentMileageAsync(int top = 8, int? driverId = null, CancellationToken ct = default) =>
        await _db.QueryAsync<MileageLog>(@"
SELECT TOP (@Top) m.Id, m.VehicleId, v.PlateNumber,
       CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       m.DriverId, ISNULL(u.FullName, N'—') AS DriverName,
       m.OdometerStart, m.OdometerEnd, m.TripMileage, m.[Date], m.Note,
       ISNULL(cu.FullName, N'Система') AS CreatedByName, m.CreatedAt
FROM dbo.MileageLogs m
JOIN dbo.Vehicles v      ON v.Id = m.VehicleId
JOIN dbo.VehicleBrands b ON b.Id = v.BrandId
JOIN dbo.Drivers d       ON d.Id = m.DriverId
LEFT JOIN dbo.Users u    ON u.Id = d.UserId
LEFT JOIN dbo.Users cu   ON cu.Id = m.CreatedByUserId
WHERE (@DriverId IS NULL OR m.DriverId = @DriverId)
ORDER BY m.[Date] DESC, m.Id DESC;", new { Top = top, DriverId = driverId }, ct: ct);
}
