using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

public sealed class MileageEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly UiComboBox _vehicle;
    private readonly UiComboBox _driver;
    private readonly UiDateField _date;
    private readonly UiTextField _start;
    private readonly UiTextField _end;
    private readonly Label _trip;
    private readonly UiTextField _note;
    private bool _loadingOdometer;

    public MileageEditForm(AppServices app, IReadOnlyList<Vehicle> vehicles, IReadOnlyList<Driver> drivers,
        int? lockedDriverId = null)
        : base("Ввод показаний одометра", "Начало и конец смены — пробег рассчитается автоматически", 620, 620)
    {
        _app = app;
        SetSaveText("Сохранить пробег");

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles, nameof(Vehicle.DisplayName), nameof(Vehicle.Id));

        _driver = new UiComboBox();
        FieldLayout.FillCombo(_driver, drivers.Select(d => new { d.Id, Title = $"{d.FullName} · {d.LicenseNumber}" }),
            "Title", "Id", lockedDriverId);
        if (lockedDriverId.HasValue)
        {
            _driver.Inner.Enabled = false;
            if (_driver.Inner.SelectedValue is not int) _driver.Inner.SelectedValue = lockedDriverId.Value;
        }

        _date = FieldLayout.Date(false, DateTime.Today);
        _start = FieldLayout.Text("Начальный одометр");
        _end = FieldLayout.Text("Конечный одометр");
        _trip = FieldLayout.Value("—", new Font("Segoe UI", 13f, FontStyle.Bold));
        _note = FieldLayout.Text("Комментарий к смене", multiline: true);

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ВОДИТЕЛЬ", _driver), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА", _date), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ОДОМЕТР НА НАЧАЛО, КМ", _start), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ОДОМЕТР НА КОНЕЦ, КМ", _end), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("ПРОБЕГ ЗА СМЕНУ", _trip), 1, 2);
        FieldLayout.AddRow(table, 112);
        FieldLayout.Add(table, FieldLayout.Field("ПРИМЕЧАНИЕ", _note), 0, 3, 2);

        SetContent(table);

        _vehicle.SelectionChanged += async (_, _) => await LoadOdometerAsync();
        _start.Input.TextChanged += (_, _) => UpdateTrip();
        _end.Input.TextChanged += (_, _) => UpdateTrip();
        Shown += async (_, _) => await LoadOdometerAsync();
    }

    private async Task LoadOdometerAsync()
    {
        if (_loadingOdometer || _vehicle.Inner.SelectedValue is not int vehicleId) return;
        _loadingOdometer = true;
        try
        {
            var current = await _app.Mileage.GetCurrentOdometerAsync(vehicleId);
            if (IsDisposed) return;
            _start.Text = current.ToString();
            _end.Text = current.ToString();
            UpdateTrip();
        }
        catch
        {
            // ошибку покажем при сохранении
        }
        finally
        {
            _loadingOdometer = false;
        }
    }

    private void UpdateTrip()
    {
        var start = FieldLayout.ParseInt(_start.Text);
        var end = FieldLayout.ParseInt(_end.Text);
        if (start is null || end is null || end <= start)
        {
            _trip.Text = end is not null && start is not null && end < start ? "меньше начала" : "—";
            _trip.ForeColor = ThemeManager.Current.TextMuted;
            return;
        }

        _trip.Text = $"{end.Value - start.Value:N0} км";
        _trip.ForeColor = ThemeManager.Current.Success;
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId)
            throw new ValidationException("Выберите транспортное средство.");
        if (_driver.Inner.SelectedValue is not int driverId)
            throw new ValidationException("Выберите водителя.");

        var start = FieldLayout.ParseInt(_start.Text);
        var end = FieldLayout.ParseInt(_end.Text);
        if (start is null || end is null)
            throw new ValidationException("Укажите показания одометра.");
        if (end <= start)
            throw new ValidationException("Конечный одометр должен быть больше начального.");

        var log = new MileageLog
        {
            VehicleId = vehicleId,
            DriverId = driverId,
            Date = _date.Inner.Value.Date,
            OdometerStart = start.Value,
            OdometerEnd = end.Value,
            Note = _note.Text
        };

        await _app.Mileage.AddAsync(log);
        return true;
    }
}
