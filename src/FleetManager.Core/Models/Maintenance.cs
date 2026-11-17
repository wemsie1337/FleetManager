namespace FleetManager.Core.Models;

public sealed class MaintenanceType
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int IntervalKm { get; set; }
    public string? Description { get; set; }
}

public sealed class VehicleMaintenancePlan
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int MaintenanceTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int IntervalKm { get; set; }
    public int LastServiceMileage { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public bool IsActive { get; set; } = true;

    public int NextDueMileage => LastServiceMileage + IntervalKm;
}

public sealed class MaintenanceRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int MaintenanceTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int MileageAtService { get; set; }
    public decimal Cost { get; set; }
    public string Performer { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MaintenanceDue
{
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int MaintenanceTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int IntervalKm { get; set; }
    public int LastServiceMileage { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public int NextDueMileage { get; set; }
    public int CurrentMileage { get; set; }
    public int RemainingKm { get; set; }
    public string Severity { get; set; } = "OK";

    public MaintenanceSeverity SeverityValue => Severity switch
    {
        "Overdue" => MaintenanceSeverity.Overdue,
        "Due" => MaintenanceSeverity.Due,
        "Upcoming" => MaintenanceSeverity.Upcoming,
        _ => MaintenanceSeverity.Ok
    };

    public string SeverityText => SeverityValue.ToRussian();
    public string RemainingText => RemainingKm < 0 ? $"просрочено на {-RemainingKm:N0} км" : $"осталось {RemainingKm:N0} км";
}

public sealed class MaintenanceAlert
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public int MaintenanceTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int DueMileage { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string Severity { get; set; } = "Due";
}
