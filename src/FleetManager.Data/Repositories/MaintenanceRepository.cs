using System.Data;
using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class MaintenanceRepository
{
    private readonly DatabaseContext _db;
    public MaintenanceRepository(DatabaseContext db) => _db = db;

    // ---------- Виды ТО ----------
    public async Task<IReadOnlyList<MaintenanceType>> GetTypesAsync(CancellationToken ct = default) =>
        await _db.QueryAsync<MaintenanceType>(@"
SELECT Id, TypeName, IntervalKm, Description FROM dbo.MaintenanceTypes ORDER BY TypeName", ct: ct);

    public async Task<int> SaveTypeAsync(MaintenanceType type, CancellationToken ct = default)
    {
        if (type.Id == 0)
        {
            return await _db.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.MaintenanceTypes (TypeName, IntervalKm, Description)
OUTPUT INSERTED.Id VALUES (@TypeName, @IntervalKm, @Description);", type, ct: ct);
        }

        await _db.ExecuteAsync(@"
UPDATE dbo.MaintenanceTypes SET TypeName = @TypeName, IntervalKm = @IntervalKm, Description = @Description
WHERE Id = @Id;", type, ct: ct);
        return type.Id;
    }

    // ---------- Планы ТО по ТС ----------
    public async Task<IReadOnlyList<VehicleMaintenancePlan>> GetPlansAsync(int? vehicleId, CancellationToken ct = default) =>
        await _db.QueryAsync<VehicleMaintenancePlan>(@"
SELECT p.Id, p.VehicleId, v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       p.MaintenanceTypeId, mt.TypeName, p.IntervalKm, p.LastServiceMileage, p.LastServiceDate, p.IsActive
FROM dbo.VehicleMaintenancePlans p
JOIN dbo.Vehicles v          ON v.Id = p.VehicleId
JOIN dbo.VehicleBrands b     ON b.Id = v.BrandId
JOIN dbo.MaintenanceTypes mt ON mt.Id = p.MaintenanceTypeId
WHERE (@VehicleId IS NULL OR p.VehicleId = @VehicleId)
ORDER BY v.PlateNumber, mt.TypeName;", new { VehicleId = vehicleId }, ct: ct);

    public Task<int> SavePlanAsync(VehicleMaintenancePlan plan, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
MERGE dbo.VehicleMaintenancePlans AS t
USING (SELECT @VehicleId AS VehicleId, @MaintenanceTypeId AS MaintenanceTypeId) AS s
   ON t.VehicleId = s.VehicleId AND t.MaintenanceTypeId = s.MaintenanceTypeId
WHEN MATCHED THEN
    UPDATE SET IntervalKm = @IntervalKm, LastServiceMileage = @LastServiceMileage,
               LastServiceDate = @LastServiceDate, IsActive = @IsActive
WHEN NOT MATCHED THEN
    INSERT (VehicleId, MaintenanceTypeId, IntervalKm, LastServiceMileage, LastServiceDate, IsActive)
    VALUES (@VehicleId, @MaintenanceTypeId, @IntervalKm, @LastServiceMileage, @LastServiceDate, @IsActive);", plan, ct: ct);

    public Task<int> DeletePlanAsync(int id, CancellationToken ct = default) =>
        _db.ExecuteAsync("DELETE FROM dbo.VehicleMaintenancePlans WHERE Id = @Id", new { Id = id }, ct: ct);

    // ---------- Журнал ТО ----------
    public async Task<IReadOnlyList<MaintenanceRecord>> GetRecordsAsync(int? vehicleId, CancellationToken ct = default) =>
        await _db.QueryAsync<MaintenanceRecord>(@"
SELECT mr.Id, mr.VehicleId, v.PlateNumber, CONCAT(b.BrandName, ' ', v.Model) AS VehicleName,
       mr.MaintenanceTypeId, mt.TypeName, mr.[Date], mr.MileageAtService, mr.Cost,
       mr.Performer, mr.Notes, mr.AttachmentPath, mr.CreatedAt
FROM dbo.MaintenanceRecords mr
JOIN dbo.Vehicles v          ON v.Id = mr.VehicleId
JOIN dbo.VehicleBrands b     ON b.Id = v.BrandId
JOIN dbo.MaintenanceTypes mt ON mt.Id = mr.MaintenanceTypeId
WHERE (@VehicleId IS NULL OR mr.VehicleId = @VehicleId)
ORDER BY mr.[Date] DESC, mr.Id DESC;", new { VehicleId = vehicleId }, ct: ct);

    public async Task<int> CreateRecordAsync(MaintenanceRecord record, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.MaintenanceRecords
    (VehicleId, MaintenanceTypeId, [Date], MileageAtService, Cost, Performer, Notes,
     AttachmentPath, CreatedByUserId)
VALUES
    (@VehicleId, @MaintenanceTypeId, @Date, @MileageAtService, @Cost, @Performer, @Notes,
     @AttachmentPath, @CreatedByUserId);
-- SCOPE_IDENTITY безопасен при включённых триггерах (OUTPUT ... без INTO запрещён).
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await _db.ExecuteScalarAsync<int>(sql, new
        {
            record.VehicleId,
            record.MaintenanceTypeId,
            record.Date,
            record.MileageAtService,
            record.Cost,
            record.Performer,
            record.Notes,
            record.AttachmentPath,
            record.CreatedByUserId
        }, ct: ct);
    }

    // ---------- Уведомления / сроки ----------
    public async Task<IReadOnlyList<MaintenanceDue>> GetDueAsync(int upcomingKm, int overdueKm, CancellationToken ct = default) =>
        await _db.QueryAsync<MaintenanceDue>("dbo.sp_GetMaintenanceDue",
            new { UpcomingKm = upcomingKm, OverdueKm = overdueKm },
            CommandType.StoredProcedure, ct);

    public Task RefreshAlertsAsync(int upcomingKm, int overdueKm, CancellationToken ct = default) =>
        _db.ExecuteAsync("dbo.sp_RefreshMaintenanceAlerts",
            new { UpcomingKm = upcomingKm, OverdueKm = overdueKm },
            CommandType.StoredProcedure, ct);

    public async Task<IReadOnlyList<MaintenanceAlert>> GetAlertsAsync(bool includeAcknowledged, CancellationToken ct = default) =>
        await _db.QueryAsync<MaintenanceAlert>(@"
SELECT a.Id, a.VehicleId, v.PlateNumber, a.MaintenanceTypeId, mt.TypeName,
       a.DueMileage, a.IsAcknowledged, a.CreatedAt, a.AcknowledgedAt, a.Severity
FROM dbo.MaintenanceAlerts a
JOIN dbo.Vehicles v          ON v.Id = a.VehicleId
JOIN dbo.MaintenanceTypes mt ON mt.Id = a.MaintenanceTypeId
WHERE (@IncludeAcknowledged = 1 OR a.IsAcknowledged = 0)
ORDER BY CASE a.Severity WHEN N'Overdue' THEN 1 WHEN N'Due' THEN 2 ELSE 3 END, a.CreatedAt DESC;",
            new { IncludeAcknowledged = includeAcknowledged }, ct: ct);

    public Task<int> AcknowledgeAsync(int alertId, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.MaintenanceAlerts
SET IsAcknowledged = 1, AcknowledgedAt = SYSUTCDATETIME()
WHERE Id = @Id AND IsAcknowledged = 0;", new { Id = alertId }, ct: ct);

    public Task<int> AcknowledgeAllAsync(CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
UPDATE dbo.MaintenanceAlerts
SET IsAcknowledged = 1, AcknowledgedAt = SYSUTCDATETIME()
WHERE IsAcknowledged = 0;", ct: ct);
}
