using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;

namespace FleetManager.UI.Dialogs;

public sealed class RouteEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly Route _route;
    private readonly UiComboBox _driver;
    private readonly UiComboBox _vehicle;
    private readonly UiTextField _startPoint;
    private readonly UiTextField _endPoint;
    private readonly UiTextField _plannedMileage;
    private readonly UiDateField _startDate;
    private readonly UiDateField _endDate;
    private readonly UiComboBox _status;
    private readonly UiTextField _notes;

    public RouteEditForm(AppServices app, Route? route, IReadOnlyList<Driver> drivers,
        IReadOnlyList<Vehicle> vehicles)
        : base(route is null ? "Новый маршрут" : "Редактирование маршрута",
            route is null ? "Назначьте водителя и транспортное средство" : $"{route.StartPoint} → {route.EndPoint}",
            680, 700)
    {
        _app = app;
        _route = route ?? new Route
        {
            StartDate = DateTime.Today.AddHours(8),
            Status = (int)RouteState.Planned
        };
        SetSaveText(route is null ? "Назначить маршрут" : "Сохранить");

        _driver = new UiComboBox();
        FieldLayout.FillCombo(_driver, drivers.Select(d => new { d.Id, Title = $"{d.FullName} · {d.Category}" }),
            "Title", "Id", route?.DriverId);

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles.Select(v => new { v.Id, Title = $"{v.PlateNumber} · {v.BrandName} {v.Model}" }),
            "Title", "Id", route?.VehicleId);

        _startPoint = FieldLayout.Text("Откуда");
        _startPoint.Text = route?.StartPoint ?? string.Empty;
        _endPoint = FieldLayout.Text("Куда");
        _endPoint.Text = route?.EndPoint ?? string.Empty;

        _plannedMileage = FieldLayout.Text("Плановый пробег, км");
        _plannedMileage.Text = route?.PlannedMileage.ToString() ?? string.Empty;

        _startDate = FieldLayout.Date(false, route?.StartDate ?? DateTime.Today.AddHours(8));
        _endDate = FieldLayout.Date(true, route?.EndDate);

        _status = new UiComboBox();
        _status.Inner.Items.AddRange(new object[] { "Запланирован", "В работе", "Завершён", "Отменён" });
        _status.Inner.SelectedIndex = route is null ? 0 : Math.Clamp(route.Status - 1, 0, 3);

        _notes = FieldLayout.Text("Комментарий к маршруту", multiline: true);
        _notes.Text = route?.Notes ?? string.Empty;

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ВОДИТЕЛЬ", _driver), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ПУНКТ ОТПРАВЛЕНИЯ", _startPoint), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ПУНКТ НАЗНАЧЕНИЯ", _endPoint), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ПЛАНОВЫЙ ПРОБЕГ, КМ", _plannedMileage), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("СТАТУС", _status), 1, 2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА НАЧАЛА", _startDate), 0, 3);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА ОКОНЧАНИЯ", _endDate), 1, 3);
        FieldLayout.AddRow(table, 116);
        FieldLayout.Add(table, FieldLayout.Field("ПРИМЕЧАНИЕ", _notes), 0, 4, 2);

        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_driver.Inner.SelectedValue is not int driverId)
            throw new ValidationException("Выберите водителя.");
        if (_vehicle.Inner.SelectedValue is not int vehicleId)
            throw new ValidationException("Выберите транспортное средство.");
        if (string.IsNullOrWhiteSpace(_startPoint.Text))
            throw new ValidationException("Укажите пункт отправления.");
        if (string.IsNullOrWhiteSpace(_endPoint.Text))
            throw new ValidationException("Укажите пункт назначения.");
        var mileage = FieldLayout.ParseInt(_plannedMileage.Text);
        if (mileage is null or <= 0)
            throw new ValidationException("Укажите плановый пробег больше нуля.");

        DateTime? end = _endDate.Inner.Checked ? _endDate.Inner.Value : null;
        if (end.HasValue && end < _startDate.Inner.Value)
            throw new ValidationException("Дата окончания не может быть раньше даты начала.");

        _route.DriverId = driverId;
        _route.VehicleId = vehicleId;
        _route.StartPoint = _startPoint.Text;
        _route.EndPoint = _endPoint.Text;
        _route.PlannedMileage = mileage.Value;
        _route.StartDate = _startDate.Inner.Value;
        _route.EndDate = end;
        _route.Status = _status.Inner.SelectedIndex + 1;
        _route.Notes = _notes.Text;

        await _app.Routes.SaveAsync(_route);
        return true;
    }
}
