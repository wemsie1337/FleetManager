using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;

namespace FleetManager.UI.Dialogs;

public sealed class VehicleEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly Vehicle _vehicle;
    private readonly UiComboBox _brand;
    private readonly UiTextField _model;
    private readonly UiTextField _plate;
    private readonly UiTextField _vin;
    private readonly UiTextField _year;
    private readonly UiTextField _mileage;
    private readonly UiComboBox _status;
    private readonly UiComboBox _fuel;
    private readonly UiTextField _consumption;
    private readonly UiDateField _purchaseDate;
    private readonly UiTextField _notes;

    public VehicleEditForm(AppServices app, Vehicle? vehicle,
        IReadOnlyList<VehicleBrand> brands,
        IReadOnlyList<VehicleStatus> statuses,
        IReadOnlyList<FuelType> fuels)
        : base(vehicle is null ? "Новое транспортное средство" : "Редактирование ТС",
            vehicle is null ? "Добавьте автомобиль в автопарк" : $"{vehicle.PlateNumber} · {vehicle.BrandName} {vehicle.Model}",
            700, 700)
    {
        _app = app;
        _vehicle = vehicle ?? new Vehicle { Year = DateTime.Today.Year, CurrentMileage = 0 };
        var isEdit = vehicle is not null;
        SetSaveText(isEdit ? "Сохранить изменения" : "Добавить ТС");

        _brand = new UiComboBox();
        FieldLayout.FillCombo(_brand, brands, nameof(VehicleBrand.BrandName), nameof(VehicleBrand.Id),
            isEdit ? _vehicle.BrandId : null);

        _model = FieldLayout.Text("Например, 5490");
        _model.Text = _vehicle.Model;
        _plate = FieldLayout.Text("А123ВС77");
        _plate.Text = _vehicle.PlateNumber;
        _vin = FieldLayout.Text("17 символов, необязательно");
        _vin.Text = _vehicle.Vin ?? string.Empty;

        _year = FieldLayout.Text("Год выпуска");
        _year.Text = _vehicle.Year == 0 ? DateTime.Today.Year.ToString() : _vehicle.Year.ToString();

        _mileage = FieldLayout.Text("Текущий пробег, км");
        _mileage.Text = _vehicle.CurrentMileage.ToString();

        _status = new UiComboBox();
        FieldLayout.FillCombo(_status, statuses, nameof(VehicleStatus.StatusName), nameof(VehicleStatus.Id),
            isEdit ? _vehicle.StatusId : 1);

        _fuel = new UiComboBox();
        FieldLayout.FillCombo(_fuel, fuels, nameof(FuelType.FuelName), nameof(FuelType.Id),
            isEdit ? _vehicle.FuelTypeId : null);

        _consumption = FieldLayout.Text("Плановый расход, л/100 км");
        _consumption.Text = _vehicle.PlannedConsumption?.ToString("0.0") ?? string.Empty;

        _purchaseDate = FieldLayout.Date(true, _vehicle.PurchaseDate ?? DateTime.Today);

        _notes = FieldLayout.Text("Комментарий, особенности, закрепление", multiline: true);
        _notes.Text = _vehicle.Notes ?? string.Empty;

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("МАРКА", _brand), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("МОДЕЛЬ", _model), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ГОСУДАРСТВЕННЫЙ НОМЕР", _plate), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("VIN", _vin), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ГОД ВЫПУСКА", _year), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("ТЕКУЩИЙ ПРОБЕГ, КМ", _mileage), 1, 2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("СТАТУС", _status), 0, 3);
        FieldLayout.Add(table, FieldLayout.Field("ВИД ТОПЛИВА", _fuel), 1, 3);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ПЛАНОВЫЙ РАСХОД, Л/100 КМ", _consumption), 0, 4);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА ПОКУПКИ", _purchaseDate), 1, 4);
        FieldLayout.AddRow(table, 122);
        FieldLayout.Add(table, FieldLayout.Field("ПРИМЕЧАНИЕ", _notes), 0, 5, 2);

        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (!int.TryParse(_year.Text.Trim(), out var year) || year is < 1950 or > 2100)
            throw new ValidationException("Укажите корректный год выпуска (1950–2100).");
        if (!int.TryParse(_mileage.Text.Trim(), out var mileage) || mileage < 0)
            throw new ValidationException("Укажите корректный пробег.");
        if (_brand.Inner.SelectedValue is not int brandId || brandId <= 0)
            throw new ValidationException("Выберите марку автомобиля.");
        if (_status.Inner.SelectedValue is not int statusId)
            throw new ValidationException("Выберите статус ТС.");
        if (_fuel.Inner.SelectedValue is not int fuelId)
            throw new ValidationException("Выберите вид топлива.");

        decimal? consumption = null;
        if (!string.IsNullOrWhiteSpace(_consumption.Text))
        {
            consumption = FieldLayout.ParseDecimal(_consumption.Text);
            if (consumption is null or <= 0)
                throw new ValidationException("Плановый расход должен быть положительным числом.");
        }

        _vehicle.BrandId = brandId;
        _vehicle.BrandName = (_brand.Inner.SelectedItem as VehicleBrand)?.BrandName ?? _vehicle.BrandName;
        _vehicle.Model = _model.Text;
        _vehicle.PlateNumber = _plate.Text;
        _vehicle.Vin = _vin.Text;
        _vehicle.Year = year;
        _vehicle.CurrentMileage = mileage;
        _vehicle.StatusId = statusId;
        _vehicle.FuelTypeId = fuelId;
        _vehicle.PlannedConsumption = consumption;
        _vehicle.PurchaseDate = _purchaseDate.Inner.Checked ? _purchaseDate.Inner.Value.Date : null;
        _vehicle.Notes = _notes.Text;

        await _app.Vehicles.SaveAsync(_vehicle);
        return true;
    }
}
