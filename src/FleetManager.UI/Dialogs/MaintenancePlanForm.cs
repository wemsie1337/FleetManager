using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

public sealed class MaintenancePlanForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly IReadOnlyList<Vehicle> _vehicles;
    private readonly IReadOnlyList<MaintenanceType> _types;
    private readonly UiComboBox _vehicle;
    private readonly DataGridView _grid;
    private readonly UiComboBox _type;
    private readonly UiTextField _interval;
    private readonly UiTextField _lastMileage;
    private readonly UiDateField _lastDate;
    private IReadOnlyList<VehicleMaintenancePlan> _plans = Array.Empty<VehicleMaintenancePlan>();

    public MaintenancePlanForm(AppServices app, IReadOnlyList<Vehicle> vehicles,
        IReadOnlyList<MaintenanceType> types, int? preselectedVehicleId = null)
        : base("Планы технического обслуживания", "Индивидуальные интервалы для каждого автомобиля", 920, 740, "Готово")
    {
        _app = app;
        _vehicles = vehicles;
        _types = types;
        CancelButton.Visible = false;

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles, nameof(Vehicle.DisplayName), nameof(Vehicle.Id), preselectedVehicleId);

        _grid = new DataGridView { Dock = DockStyle.Fill };
        GridStyler.Style(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Вид ТО", DataPropertyName = nameof(VehicleMaintenancePlan.TypeName), Width = 190
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Интервал, км", DataPropertyName = nameof(VehicleMaintenancePlan.IntervalKm), Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Последнее ТО, км", DataPropertyName = nameof(VehicleMaintenancePlan.LastServiceMileage), Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Дата последнего ТО", DataPropertyName = nameof(VehicleMaintenancePlan.LastServiceDate), Width = 145,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Следующее ТО, км", DataPropertyName = nameof(VehicleMaintenancePlan.NextDueMileage), Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Активен", DataPropertyName = nameof(VehicleMaintenancePlan.IsActive),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 90
        });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 5 && e.Value is bool active)
            {
                e.Value = active ? "Да" : "Нет";
                e.FormattingApplied = true;
            }
        };

        var gridHolder = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Current.SurfaceAlt,
            Padding = new Padding(1),
            Margin = new Padding(0, 0, 0, 8)
        };
        gridHolder.Controls.Add(_grid);

        _type = new UiComboBox();
        FieldLayout.FillCombo(_type, types, nameof(MaintenanceType.TypeName), nameof(MaintenanceType.Id));
        _interval = FieldLayout.Text("Например, 10000");
        _lastMileage = FieldLayout.Text("Пробег последнего ТО");
        _lastDate = FieldLayout.Date(true, DateTime.Today);

        var editor = FieldLayout.Table(4);
        FieldLayout.AddRow(editor, 76);
        FieldLayout.Add(editor, FieldLayout.Field("ВИД ТО", _type), 0, 0);
        FieldLayout.Add(editor, FieldLayout.Field("ИНТЕРВАЛ, КМ", _interval), 1, 0);
        FieldLayout.Add(editor, FieldLayout.Field("ПРОБЕГ ПОСЛЕДНЕГО ТО", _lastMileage), 2, 0);
        FieldLayout.Add(editor, FieldLayout.Field("ДАТА ПОСЛЕДНЕГО ТО", _lastDate), 3, 0);

        var addButton = new GradientButton { Text = "Сохранить план", IconGlyph = "\uE74E", Width = 170, Height = 40, Margin = new Padding(0, 0, 8, 0) };
        var newButton = new GradientButton { Text = "Новый план", Kind = ButtonKind.Ghost, Width = 140, Height = 40, Margin = new Padding(0, 0, 8, 0) };
        var deleteButton = new GradientButton { Text = "Удалить", Kind = ButtonKind.Danger, Width = 120, Height = 40, Margin = new Padding(0) };
        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        buttonFlow.Controls.Add(addButton);
        buttonFlow.Controls.Add(newButton);
        buttonFlow.Controls.Add(deleteButton);

        var editorPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Tag = "transparent" };
        editorPanel.Controls.Add(editor);
        editorPanel.Controls.Add(buttonFlow);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.Controls.Add(FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 0, 0);
        layout.Controls.Add(gridHolder, 0, 1);
        layout.Controls.Add(editorPanel, 0, 2);
        SetContent(layout);

        _vehicle.SelectionChanged += async (_, _) => await ReloadPlansAsync();
        _grid.SelectionChanged += (_, _) => FillFromSelection();
        _type.SelectionChanged += (_, _) =>
        {
            if (_type.Inner.SelectedItem is MaintenanceType t && string.IsNullOrWhiteSpace(_interval.Text))
                _interval.Text = t.IntervalKm.ToString();
        };
        addButton.Click += async (_, _) => await SavePlanAsync();
        newButton.Click += (_, _) => ClearEditor();
        deleteButton.Click += async (_, _) => await DeletePlanAsync();
        Shown += async (_, _) => await ReloadPlansAsync();
    }

    private async Task ReloadPlansAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId) return;
        try
        {
            _plans = await _app.Maintenance.GetPlansAsync(vehicleId);
            _grid.DataSource = null;
            _grid.DataSource = _plans.ToList();
            _grid.ClearSelection();
            ClearEditor();
        }
        catch (Exception ex)
        {
            Ui.ShowError(this, ex);
        }
    }

    private void FillFromSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is not VehicleMaintenancePlan plan) return;
        _type.Inner.SelectedValue = plan.MaintenanceTypeId;
        _interval.Text = plan.IntervalKm.ToString();
        _lastMileage.Text = plan.LastServiceMileage.ToString();
        _lastDate.Inner.Value = plan.LastServiceDate ?? DateTime.Today;
        _lastDate.Inner.Checked = plan.LastServiceDate.HasValue;
    }

    private void ClearEditor()
    {
        _grid.ClearSelection();
        if (_types.Count > 0) _type.Inner.SelectedIndex = 0;
        _interval.Text = _types.FirstOrDefault()?.IntervalKm.ToString() ?? string.Empty;
        var vehicle = _vehicles.FirstOrDefault(v => _vehicle.Inner.SelectedValue is int id && v.Id == id);
        _lastMileage.Text = vehicle?.CurrentMileage.ToString() ?? "0";
        _lastDate.Inner.Value = DateTime.Today;
        _lastDate.Inner.Checked = false;
        SetSubtitle("Выберите план в таблице или создайте новый");
    }

    private async Task SavePlanAsync()
    {
        try
        {
            if (_vehicle.Inner.SelectedValue is not int vehicleId)
                throw new ValidationException("Выберите транспортное средство.");
            if (_type.Inner.SelectedValue is not int typeId)
                throw new ValidationException("Выберите вид ТО.");
            var interval = FieldLayout.ParseInt(_interval.Text);
            var mileage = FieldLayout.ParseInt(_lastMileage.Text);
            if (interval is null or <= 0) throw new ValidationException("Укажите интервал ТО.");
            if (mileage is null or < 0) throw new ValidationException("Укажите пробег последнего ТО.");

            var plan = new VehicleMaintenancePlan
            {
                Id = _grid.CurrentRow?.DataBoundItem is VehicleMaintenancePlan current ? current.Id : 0,
                VehicleId = vehicleId,
                MaintenanceTypeId = typeId,
                IntervalKm = interval.Value,
                LastServiceMileage = mileage.Value,
                LastServiceDate = _lastDate.Inner.Checked ? _lastDate.Inner.Value.Date : null,
                IsActive = true
            };

            await _app.Maintenance.SavePlanAsync(plan);
            await ReloadPlansAsync();
            SetSubtitle("План сохранён, уведомления пересчитаны");
        }
        catch (Exception ex)
        {
            Ui.ShowError(this, ex);
        }
    }

    private async Task DeletePlanAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not VehicleMaintenancePlan plan)
        {
            Ui.ShowInfo(this, "Выберите план ТО для удаления.");
            return;
        }

        if (!Ui.Confirm(this, "Удалить план ТО?", $"План «{plan.TypeName}» для {plan.PlateNumber} будет удалён."))
            return;

        try
        {
            await _app.Maintenance.DeletePlanAsync(plan);
            await ReloadPlansAsync();
            SetSubtitle("План удалён");
        }
        catch (Exception ex)
        {
            Ui.ShowError(this, ex);
        }
    }
}
