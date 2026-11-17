using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Dialogs;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Views;

public sealed class VehiclesView : BaseView
{
    public override string PageTitle => "Транспорт";
    public override string PageSubtitle => "Автопарк, статусы и технические характеристики";

    private readonly UiTextField _search;
    private readonly UiComboBox _statusFilter;
    private readonly DataGridView _grid;
    private readonly GridPaginator<Vehicle> _paginator;
    private readonly GradientButton _addButton;
    private readonly GradientButton _editButton;
    private readonly GradientButton _deleteButton;
    private IReadOnlyList<Vehicle> _items = Array.Empty<Vehicle>();
    private IReadOnlyList<VehicleStatus> _statuses = Array.Empty<VehicleStatus>();
    private IReadOnlyList<VehicleBrand> _brands = Array.Empty<VehicleBrand>();
    private IReadOnlyList<FuelType> _fuels = Array.Empty<FuelType>();
    private bool _filtersLoaded;

    public VehiclesView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по номеру, марке, VIN...", 300);
        EnableLiveSearch(_search);

        _statusFilter = new UiComboBox { Width = 180, Height = 40 };
        _statusFilter.Inner.Items.Add("Все статусы");
        _statusFilter.Inner.SelectedIndex = 0;
        _statusFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _grid = CreateGrid();
        AddColumn(_grid, "Госномер", nameof(Vehicle.PlateNumber), 105);
        AddColumn(_grid, "Марка", nameof(Vehicle.BrandName), 110);
        AddColumn(_grid, "Модель", nameof(Vehicle.Model), 120);
        AddColumn(_grid, "Год", nameof(Vehicle.Year), 60, alignment: DataGridViewContentAlignment.MiddleCenter);
        AddColumn(_grid, "Статус", nameof(Vehicle.StatusName), 110);
        AddColumn(_grid, "Пробег, км", nameof(Vehicle.CurrentMileage), 105, alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Топливо", nameof(Vehicle.FuelName), 85);
        AddColumn(_grid, "Расход", nameof(Vehicle.PlannedConsumption), 80, alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "VIN", nameof(Vehicle.Vin), 160, fill: true);
        _grid.Columns[5].DefaultCellStyle.Format = "N0";
        _grid.Columns[7].DefaultCellStyle.Format = "0.0";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.SelectionChanged += (_, _) => UpdateActionState();

        _paginator = new GridPaginator<Vehicle>(_grid);

        _addButton = new GradientButton
        {
            Text = "Добавить ТС",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 155,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        var exportExcel = new GradientButton
        {
            Text = "Excel",
            IconGlyph = "\uE9F9",
            Kind = ButtonKind.Ghost,
            Width = 100,
            Height = 40
        };
        exportExcel.Click += (_, _) => Export(true);

        var exportPdf = new GradientButton
        {
            Text = "PDF",
            IconGlyph = "\uE8A5",
            Kind = ButtonKind.Ghost,
            Width = 90,
            Height = 40
        };
        exportPdf.Click += (_, _) => Export(false);

        _editButton = new GradientButton
        {
            Text = "Изменить",
            Kind = ButtonKind.Ghost,
            Width = 120,
            Height = 38,
            Enabled = false,
            Font = new Font("Segoe UI", 8.5f)
        };
        _editButton.Click += (_, _) => EditSelected();

        _deleteButton = new GradientButton
        {
            Text = "Удалить",
            Kind = ButtonKind.Danger,
            Width = 105,
            Height = 38,
            Enabled = false,
            Font = new Font("Segoe UI", 8.5f)
        };
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _statusFilter, _editButton, _deleteButton, refresh);

        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);
        card.Controls.Add(_grid);
        card.Controls.Add(_paginator.Bar);
        card.Controls.Add(toolbar);

        BuildLayout(card, _addButton, exportPdf, exportExcel);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != nameof(Vehicle.StatusName)) return;
        var t = ThemeManager.Current;
        var value = e.Value?.ToString();
        e.CellStyle.ForeColor = value switch
        {
            "На линии" => t.Success,
            "В ремонте" => t.Accent,
            "На ТО" => t.Info,
            _ => t.TextMuted
        };
        e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_filtersLoaded)
        {
            _statuses = await App.Lookups.GetVehicleStatusesAsync();
            _brands = await App.Lookups.GetBrandsAsync();
            _fuels = await App.Lookups.GetFuelTypesAsync();
            _statusFilter.Inner.Items.Clear();
            _statusFilter.Inner.Items.Add("Все статусы");
            foreach (var status in _statuses) _statusFilter.Inner.Items.Add(status.StatusName);
            _statusFilter.Inner.SelectedIndex = 0;
            _filtersLoaded = true;
        }

        int? statusId = _statusFilter.Inner.SelectedIndex > 0
            ? _statuses[_statusFilter.Inner.SelectedIndex - 1].Id
            : null;

        _items = await App.Vehicles.SearchAsync(_search.Text, statusId);
        if (token.IsCancellationRequested) return;
        _paginator.SetData(_items);
        UpdateActionState();
    }

    private async Task AddAsync()
    {
        if (!_filtersLoaded) await ReloadAsync();
        using var dialog = new VehicleEditForm(App, null, _brands, _statuses, _fuels);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "ТС добавлено", "Автомобиль внесён в автопарк.");
            await ReloadAsync();
        }
    }

    private async void EditSelected()
    {
        if (_paginator.Current is not { } vehicle) return;
        if (!_filtersLoaded) await ReloadAsync();
        using var dialog = new VehicleEditForm(App, vehicle, _brands, _statuses, _fuels);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Изменения сохранены", $"{vehicle.PlateNumber} обновлён.");
            await ReloadAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_paginator.Current is not { } vehicle) return;
        if (!Ui.Confirm(FindForm(), "Удалить транспортное средство?",
                $"{vehicle.BrandName} {vehicle.Model} ({vehicle.PlateNumber}) будет удалено безвозвратно."))
            return;

        try
        {
            await App.Vehicles.DeleteAsync(vehicle);
            Notify(ToastKind.Success, "ТС удалено", vehicle.PlateNumber);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void UpdateActionState()
    {
        var hasSelection = _paginator.Current is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void Export(bool excel)
    {
        var headers = new[] { "Госномер", "Марка", "Модель", "Год", "Статус", "Пробег, км", "Топливо", "Расход" };
        var rows = _items.Select(v => (IReadOnlyList<object?>)new object?[]
        {
            v.PlateNumber, v.BrandName, v.Model, v.Year, v.StatusName, v.CurrentMileage, v.FuelName,
            v.PlannedConsumption
        }).ToList();

        if (excel) ExportExcel("Автопарк", "Автопарк", headers, rows);
        else ExportPdf("Автопарк", "Автопарк", headers, rows);
    }
}
