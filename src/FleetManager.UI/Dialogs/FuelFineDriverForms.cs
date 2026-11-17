using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

public sealed class FuelEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly IReadOnlyList<Vehicle> _vehicles;
    private readonly UiComboBox _vehicle;
    private readonly UiDateField _date;
    private readonly UiTextField _liters;
    private readonly UiTextField _price;
    private readonly Label _total;
    private readonly UiTextField _odometer;
    private readonly UiComboBox _driver;

    public FuelEditForm(AppServices app, IReadOnlyList<Vehicle> vehicles, IReadOnlyList<Driver> drivers)
        : base("Новая заправка", "Зафиксируйте объём, цену и показание одометра", 620, 520)
    {
        _app = app;
        _vehicles = vehicles;
        SetSaveText("Добавить заправку");

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles, nameof(Vehicle.DisplayName), nameof(Vehicle.Id));

        _date = FieldLayout.Date(false, DateTime.Today);
        _liters = FieldLayout.Text("Объём, литров");
        _price = FieldLayout.Text("Цена за литр, ₽");
        _total = FieldLayout.Value("0,00 ₽");
        _odometer = FieldLayout.Text("Показание одометра");
        _driver = new UiComboBox();
        FieldLayout.FillCombo(_driver, drivers.Select(d => new { d.Id, Title = d.FullName }), "Title", "Id");
        _driver.Inner.SelectedIndex = -1;

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА", _date), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ОБЪЁМ, Л", _liters), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ЦЕНА ЗА ЛИТР, ₽", _price), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ИТОГО", _total), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("ОДОМЕТР, КМ", _odometer), 1, 2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ВОДИТЕЛЬ (НЕОБЯЗАТЕЛЬНО)", _driver), 0, 3, 2);

        SetContent(table);

        _vehicle.SelectionChanged += (_, _) => FillOdometer();
        _liters.Input.TextChanged += (_, _) => Recalculate();
        _price.Input.TextChanged += (_, _) => Recalculate();
        Shown += (_, _) => FillOdometer();
    }

    private void FillOdometer()
    {
        if (_vehicle.Inner.SelectedValue is not int id) return;
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == id);
        if (vehicle is not null) _odometer.Text = vehicle.CurrentMileage.ToString();
    }

    private void Recalculate()
    {
        var liters = FieldLayout.ParseDecimal(_liters.Text);
        var price = FieldLayout.ParseDecimal(_price.Text);
        _total.Text = liters is > 0 && price is > 0
            ? $"{liters.Value * price.Value:N2} ₽"
            : "0,00 ₽";
        _total.ForeColor = ThemeManager.Current.Accent;
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId)
            throw new ValidationException("Выберите транспортное средство.");
        var liters = FieldLayout.ParseDecimal(_liters.Text);
        var price = FieldLayout.ParseDecimal(_price.Text);
        var odometer = FieldLayout.ParseInt(_odometer.Text);
        if (liters is null or <= 0) throw new ValidationException("Укажите объём топлива больше нуля.");
        if (price is null or < 0) throw new ValidationException("Укажите корректную цену за литр.");
        if (odometer is null or < 0) throw new ValidationException("Укажите корректный одометр.");

        var record = new FuelRecord
        {
            VehicleId = vehicleId,
            Date = _date.Inner.Value.Date,
            Liters = liters.Value,
            CostPerLiter = price.Value,
            Odometer = odometer.Value,
            DriverId = _driver.Inner.SelectedValue is int driverId ? driverId : null
        };

        await _app.Fuel.AddAsync(record);
        return true;
    }
}

public sealed class FineEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly UiComboBox _vehicle;
    private readonly UiComboBox _driver;
    private readonly UiDateField _date;
    private readonly UiComboBox _type;
    private readonly UiTextField _amount;
    private readonly CheckBox _paid;
    private readonly UiTextField _description;

    public FineEditForm(AppServices app, IReadOnlyList<Vehicle> vehicles, IReadOnlyList<Driver> drivers)
        : base("Новый штраф / ДТП", "Занесите происшествие в журнал", 660, 520)
    {
        _app = app;
        SetSaveText("Добавить запись");

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles, nameof(Vehicle.DisplayName), nameof(Vehicle.Id));

        _driver = new UiComboBox();
        FieldLayout.FillCombo(_driver, drivers.Select(d => new { d.Id, Title = d.FullName }), "Title", "Id");
        _driver.Inner.SelectedIndex = -1;

        _date = FieldLayout.Date(false, DateTime.Today);

        _type = new UiComboBox();
        _type.Inner.Items.AddRange(new object[] { "Штраф ГИБДД", "ДТП" });
        _type.Inner.SelectedIndex = 0;

        _amount = FieldLayout.Text("Сумма, ₽");
        _paid = new CheckBox
        {
            Text = "Оплачено",
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f)
        };
        _description = FieldLayout.Text("Обстоятельства, статья, комментарий", multiline: true);

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ВОДИТЕЛЬ (НЕОБЯЗАТЕЛЬНО)", _driver), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА", _date), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ТИП", _type), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("СУММА, ₽", _amount), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("СТАТУС", _paid), 1, 2);
        FieldLayout.AddRow(table, 122);
        FieldLayout.Add(table, FieldLayout.Field("ОПИСАНИЕ", _description), 0, 3, 2);

        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId)
            throw new ValidationException("Выберите транспортное средство.");
        var amount = FieldLayout.ParseDecimal(_amount.Text);
        if (amount is null or <= 0) throw new ValidationException("Укажите сумму больше нуля.");

        var fine = new Fine
        {
            VehicleId = vehicleId,
            DriverId = _driver.Inner.SelectedValue is int driverId ? driverId : null,
            Date = _date.Inner.Value.Date,
            Amount = amount.Value,
            Description = _description.Text,
            IsPaid = _paid.Checked,
            PaidDate = _paid.Checked ? DateTime.Today : null,
            IncidentType = _type.Inner.SelectedIndex == 1 ? "Accident" : "Fine"
        };

        await _app.Fines.AddAsync(fine);
        return true;
    }
}

public sealed class DriverEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly Driver _driver;
    private readonly UiComboBox _user;
    private readonly UiTextField _license;
    private readonly UiComboBox _category;
    private readonly UiDateField _hireDate;
    private readonly UiTextField _phone;
    private readonly CheckBox _active;

    public DriverEditForm(AppServices app, Driver? driver, IReadOnlyList<User> availableUsers)
        : base(driver is null ? "Новый водитель" : "Редактирование водителя",
            driver is null ? "Привяжите учётную запись и удостоверение" : driver.FullName, 620, 520)
    {
        _app = app;
        _driver = driver ?? new Driver { HireDate = DateTime.Today, IsActive = true, Category = "CE" };
        SetSaveText(driver is null ? "Добавить водителя" : "Сохранить");

        _user = new UiComboBox();
        FieldLayout.FillCombo(_user,
            availableUsers.Select(u => new { u.Id, Title = $"{u.FullName} ({u.Login})" }),
            "Title", "Id", driver?.UserId);

        _license = FieldLayout.Text("Серия и номер");
        _license.Text = driver?.LicenseNumber ?? string.Empty;

        _category = new UiComboBox();
        _category.Inner.Items.AddRange(new object[] { "B", "C", "CE", "D", "DE" });
        _category.Inner.SelectedItem = driver?.Category ?? "CE";

        _hireDate = FieldLayout.Date(false, driver?.HireDate ?? DateTime.Today);
        _phone = FieldLayout.Text("+7 (___) ___-__-__");
        _phone.Text = driver?.Phone ?? string.Empty;

        _active = new CheckBox
        {
            Text = "Работает",
            Checked = driver?.IsActive ?? true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f)
        };

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("УЧЁТНАЯ ЗАПИСЬ", _user), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ВОДИТЕЛЬСКОЕ УДОСТОВЕРЕНИЕ", _license), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("КАТЕГОРИЯ", _category), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА ПРИЁМА", _hireDate), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТЕЛЕФОН", _phone), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("СТАТУС", _active), 1, 2);
        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_user.Inner.SelectedValue is not int userId)
            throw new ValidationException("Выберите учётную запись водителя.");
        if (string.IsNullOrWhiteSpace(_license.Text))
            throw new ValidationException("Укажите серию и номер водительского удостоверения.");
        if (!string.IsNullOrWhiteSpace(_phone.Text) && _phone.Text.Count(char.IsDigit) < 10)
            throw new ValidationException("Телефон должен содержать не менее 10 цифр.");

        _driver.UserId = userId;
        _driver.LicenseNumber = _license.Text;
        _driver.Category = _category.Inner.SelectedItem?.ToString() ?? "CE";
        _driver.HireDate = _hireDate.Inner.Value.Date;
        _driver.Phone = _phone.Text;
        _driver.IsActive = _active.Checked;

        await _app.Drivers.SaveAsync(_driver);
        return true;
    }
}
