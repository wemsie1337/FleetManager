namespace FleetManager.Core.Models;

public sealed class Route
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public int PlannedMileage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }

    public string StatusName => ((RouteState)Status).ToRussian();
}

public sealed class MileageLog
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int OdometerStart { get; set; }
    public int OdometerEnd { get; set; }
    public int TripMileage { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class FuelRecord
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Liters { get; set; }
    public decimal CostPerLiter { get; set; }
    public decimal TotalCost { get; set; }
    public int Odometer { get; set; }
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
}

public sealed class Fine
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string IncidentType { get; set; } = "Fine";

    public string IncidentTypeName => IncidentType == "Accident" ? "ДТП" : "Штраф";
    public string PaidText => IsPaid ? "Оплачен" : "Не оплачен";
}

public sealed class FuelConsumptionInfo
{
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public int TotalMileage { get; set; }
    public decimal TotalLiters { get; set; }
    public decimal TotalCost { get; set; }
    public decimal? FactConsumption { get; set; }
    public decimal? PlanConsumption { get; set; }

    public decimal? DeviationPercent =>
        FactConsumption is null || PlanConsumption is null || PlanConsumption == 0
            ? null
            : Math.Round((FactConsumption.Value - PlanConsumption.Value) / PlanConsumption.Value * 100m, 1);
}
