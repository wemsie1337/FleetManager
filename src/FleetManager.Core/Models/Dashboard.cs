namespace FleetManager.Core.Models;

public sealed class FleetKpi
{
    public int TotalVehicles { get; set; }
    public int OnLine { get; set; }
    public int InRepair { get; set; }
    public int InMaintenance { get; set; }
    public int Retired { get; set; }
    public int ActiveDrivers { get; set; }
    public int ActiveRoutes { get; set; }
    public int UpcomingCount { get; set; }
    public int DueCount { get; set; }
    public int OverdueCount { get; set; }
    public int MonthMileage { get; set; }
    public decimal MonthFuelCost { get; set; }
    public decimal MonthMaintenanceCost { get; set; }
    public int UnpaidFines { get; set; }
    public decimal UnpaidFinesAmount { get; set; }
}

public sealed class MonthlyCost
{
    public int Month { get; set; }
    public decimal FuelCost { get; set; }
    public decimal MaintenanceCost { get; set; }
    public int Mileage { get; set; }

    public string MonthName => new DateTime(2000, Month, 1).ToString("MMM");
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}
