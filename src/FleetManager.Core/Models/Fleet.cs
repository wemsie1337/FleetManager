namespace FleetManager.Core.Models;

public sealed class Vehicle
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public int Year { get; set; }
    public int CurrentMileage { get; set; }
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int FuelTypeId { get; set; }
    public string FuelName { get; set; } = string.Empty;
    public decimal? PlannedConsumption { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? Notes { get; set; }

    public string DisplayName => $"{BrandName} {Model} ({PlateNumber})";

    public VehicleState State => (VehicleState)StatusId;
}

public sealed class VehicleStatus
{
    public int Id { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#64748B";
}

public sealed class VehicleBrand
{
    public int Id { get; set; }
    public string BrandName { get; set; } = string.Empty;
}

public sealed class FuelType
{
    public int Id { get; set; }
    public string FuelName { get; set; } = string.Empty;
    public decimal PricePerLiter { get; set; }
}
