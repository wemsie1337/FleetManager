using FleetManager.Core.Models;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

public sealed class VehicleService
{
    private readonly VehicleRepository _vehicles;
    private readonly AuditService _audit;

    public VehicleService(VehicleRepository vehicles, AuditService audit)
    {
        _vehicles = vehicles;
        _audit = audit;
    }

    public Task<IReadOnlyList<Vehicle>> SearchAsync(string? search, int? statusId) =>
        _vehicles.SearchAsync(search, statusId);

    public Task<IReadOnlyList<Vehicle>> GetAllAsync() => _vehicles.GetAllAsync();

    public Task<Vehicle?> GetByIdAsync(int id) => _vehicles.GetByIdAsync(id);

    public async Task<int> SaveAsync(Vehicle vehicle)
    {
        Validate(vehicle);

        if (vehicle.Id == 0)
        {
            var id = await _vehicles.CreateAsync(vehicle);
            await _audit.LogAsync("Create", "Vehicles", id.ToString(), null,
                $"{vehicle.BrandName} {vehicle.Model}, {vehicle.PlateNumber}, пробег {vehicle.CurrentMileage}");
            return id;
        }

        var old = await _vehicles.GetByIdAsync(vehicle.Id);
        await _vehicles.UpdateAsync(vehicle);
        await _audit.LogAsync("Update", "Vehicles", vehicle.Id.ToString(),
            old is null ? null : $"{old.PlateNumber}, пробег {old.CurrentMileage}, статус {old.StatusName}",
            $"{vehicle.PlateNumber}, пробег {vehicle.CurrentMileage}, статус {vehicle.StatusId}");
        return vehicle.Id;
    }

    public async Task DeleteAsync(Vehicle vehicle)
    {
        await _vehicles.DeleteAsync(vehicle.Id);
        await _audit.LogAsync("Delete", "Vehicles", vehicle.Id.ToString(),
            $"{vehicle.BrandName} {vehicle.Model} {vehicle.PlateNumber}", null);
    }

    public async Task UpdateStatusAsync(int vehicleId, int statusId)
    {
        await _vehicles.UpdateStatusAsync(vehicleId, statusId);
        await _audit.LogAsync("UpdateStatus", "Vehicles", vehicleId.ToString(), null, $"Статус {statusId}");
    }

    private static void Validate(Vehicle vehicle)
    {
        if (vehicle.BrandId <= 0) throw new ValidationException("Выберите марку автомобиля.");
        if (string.IsNullOrWhiteSpace(vehicle.Model)) throw new ValidationException("Укажите модель автомобиля.");
        if (string.IsNullOrWhiteSpace(vehicle.PlateNumber)) throw new ValidationException("Укажите государственный номер.");
        if (vehicle.PlateNumber.Trim().Length is < 4 or > 20)
            throw new ValidationException("Госномер должен содержать от 4 до 20 символов.");
        if (vehicle.Year is < 1950 or > 2100) throw new ValidationException("Некорректный год выпуска.");
        if (vehicle.CurrentMileage < 0) throw new ValidationException("Пробег не может быть отрицательным.");
        if (vehicle.FuelTypeId <= 0) throw new ValidationException("Выберите вид топлива.");
        if (vehicle.StatusId <= 0) throw new ValidationException("Выберите статус ТС.");
        if (!string.IsNullOrWhiteSpace(vehicle.Vin) && vehicle.Vin.Trim().Length != 17)
            throw new ValidationException("VIN должен состоять из 17 символов (или оставьте поле пустым).");
        if (vehicle.PlannedConsumption is <= 0)
            throw new ValidationException("Плановый расход топлива должен быть больше нуля.");
        if (vehicle.PurchaseDate > DateTime.Today)
            throw new ValidationException("Дата покупки не может быть в будущем.");

        vehicle.Model = vehicle.Model.Trim();
        vehicle.PlateNumber = vehicle.PlateNumber.Trim().ToUpperInvariant();
        vehicle.Vin = string.IsNullOrWhiteSpace(vehicle.Vin) ? null : vehicle.Vin.Trim().ToUpperInvariant();
        vehicle.Notes = string.IsNullOrWhiteSpace(vehicle.Notes) ? null : vehicle.Notes.Trim();
    }
}
