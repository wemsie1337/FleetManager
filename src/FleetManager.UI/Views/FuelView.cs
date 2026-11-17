using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Dialogs;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Views;

/// <summary>Топливо: заправки, расходы по месяцам и сравнение плана с фактом.</summary>
public sealed class FuelView : BaseView
{
    public override string PageTitle => "Топливо";
    public override string PageSubtitle => "Заправки, стоимость и фактический расход по автомобилям";

    private readonly UiTextField _search;
    private readonly UiComboBox _vehicleFilter;
    private readonly DataGridView _grid;
    private readonly DataGridView _consumptionGrid;
    private readonly GridPaginator<FuelRecord> _paginator;
    private readonly SimpleChart _fuelChart = new()
    {
        Dock = DockStyle.Fill,
        Kind = ChartKind.Column,
        ValueFormat = "N0"
    };
    private readonly GradientButton _addButton;
    private readonly GradientButton _deleteButton;
    private readonly Label _summary;

    private IReadOnlyList<FuelRecord> _items = Array.Empty<FuelRecord>();
    private IReadOnlyList<FuelConsumptionInfo> _consumption = Array.Empty<FuelConsumptionInfo>();
    private IReadOnlyList<Vehicle> _vehicles = Array.Empty<Vehicle>();
    private IReadOnlyList<Driver> _drivers = Array.Empty<Driver>();
    private bool _lookupsLoaded;

    public FuelView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по номеру, автомобилю, водителю...", 260);
        EnableLiveSearch(_search);

        _vehicleFilter = new UiComboBox { Width = 195, Height = 40 };
        _vehicleFilter.Inner.Items.Add("Все автомобили");
        _vehicleFilter.Inner.SelectedIndex = 0;
        _vehicleFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _summary = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Accent,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 13, 0, 0)
        };

        _grid = CreateGrid();
        AddColumn(_grid, "Дата", nameof(FuelRecord.Date), 95);
        AddColumn(_grid, "Госномер", nameof(FuelRecord.PlateNumber), 105);
        AddColumn(_grid, "Автомобиль", nameof(FuelRecord.VehicleName), 180);
        AddColumn(_grid, "Объём, л", nameof(FuelRecord.Liters), 95,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Цена, ₽", nameof(FuelRecord.CostPerLiter), 95,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Стоимость, ₽", nameof(FuelRecord.TotalCost), 115,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Одометр", nameof(FuelRecord.Odometer), 95,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Водитель", nameof(FuelRecord.DriverName), 175, fill: true);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.Columns[3].DefaultCellStyle.Format = "N2";
        _grid.Columns[4].DefaultCellStyle.Format = "N2";
        _grid.Columns[5].DefaultCellStyle.Format = "N2";
        _grid.Columns[6].DefaultCellStyle.Format = "N0";
        _grid.SelectionChanged += (_, _) => UpdateActions();

        _paginator = new GridPaginator<FuelRecord>(_grid);

        _consumptionGrid = CreateGrid();
        AddColumn(_consumptionGrid, "Госномер", nameof(FuelConsumptionInfo.PlateNumber), 88);
        AddColumn(_consumptionGrid, "Пробег", nameof(FuelConsumptionInfo.TotalMileage), 78,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_consumptionGrid, "План", nameof(FuelConsumptionInfo.PlanConsumption), 64,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_consumptionGrid, "Факт", nameof(FuelConsumptionInfo.FactConsumption), 64,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_consumptionGrid, "Δ, %", nameof(FuelConsumptionInfo.DeviationPercent), 70, fill: true,
            alignment: DataGridViewContentAlignment.MiddleRight);
        _consumptionGrid.Columns[1].DefaultCellStyle.Format = "N0";
        _consumptionGrid.Columns[2].DefaultCellStyle.Format = "0.0";
        _consumptionGrid.Columns[3].DefaultCellStyle.Format = "0.0";
        _consumptionGrid.Columns[4].DefaultCellStyle.Format = "0.0'%'";
        _consumptionGrid.CellFormatting += ConsumptionGrid_CellFormatting;

        _addButton = new GradientButton
        {
            Text = "Добавить заправку",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 190,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        _deleteButton = new GradientButton
        {
            Text = "Удалить",
            Kind = ButtonKind.Danger,
            Width = 100,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();

        var export = new GradientButton
        {
            Text = "Excel", IconGlyph = "\uE9F9", Kind = ButtonKind.Ghost, Width = 92, Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        export.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var chartCard = CreateTitledCard("Расходы на топливо по месяцам, ₽", _fuelChart);
        chartCard.Margin = new Padding(0, 0, 12, 0);

        var consumptionCard = CreateTitledCard("План / факт расхода, л на 100 км", _consumptionGrid);
        consumptionCard.Margin = new Padding(0);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Margin = new Padding(0)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.Controls.Add(chartCard, 0, 0);
        top.Controls.Add(consumptionCard, 1, 0);

        var toolbar = CreateToolbar(_search, _vehicleFilter, _summary, _deleteButton, refresh);

        var recordsCard = CreateCard();
        recordsCard.Padding = new Padding(14, 10, 14, 8);
        var recordsHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.Transparent, Tag = "transparent" };
        recordsHeader.Controls.Add(new Label
        {
            Text = "Журнал заправок",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });
        recordsCard.Controls.Add(_grid);
        recordsCard.Controls.Add(_paginator.Bar);
        recordsCard.Controls.Add(toolbar);
        recordsCard.Controls.Add(recordsHeader);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(recordsCard, 0, 1);

        BuildLayout(layout, _addButton, export);
        UpdateActions();
    }

    private CardPanel CreateTitledCard(string title, Control body)
    {
        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);

        var header = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.Transparent, Tag = "transparent" };
        header.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });

        card.Controls.Add(body);
        card.Controls.Add(header);
        return card;
    }

    private void ConsumptionGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            _consumptionGrid.Columns[e.ColumnIndex].Name != nameof(FuelConsumptionInfo.DeviationPercent))
            return;

        if (_consumptionGrid.Rows[e.RowIndex].DataBoundItem is not FuelConsumptionInfo info) return;
        if (info.DeviationPercent is not { } deviation) return;

        e.CellStyle.ForeColor = deviation > 5
            ? ThemeManager.Current.Danger
            : deviation < -5
                ? ThemeManager.Current.Success
                : ThemeManager.Current.TextMuted;
        e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_lookupsLoaded)
        {
            var vehiclesTask = App.Vehicles.GetAllAsync();
            var driversTask = App.Drivers.GetAllAsync();
            await Task.WhenAll(vehiclesTask, driversTask);
            _vehicles = await vehiclesTask;
            _drivers = await driversTask;

            _vehicleFilter.Inner.Items.Clear();
            _vehicleFilter.Inner.Items.Add("Все автомобили");
            foreach (var vehicle in _vehicles) _vehicleFilter.Inner.Items.Add(vehicle.DisplayName);
            _vehicleFilter.Inner.SelectedIndex = 0;
            _lookupsLoaded = true;
        }

        int? vehicleId = _vehicleFilter.Inner.SelectedIndex > 0
            ? _vehicles[_vehicleFilter.Inner.SelectedIndex - 1].Id
            : null;

        var recordsTask = App.Fuel.SearchAsync(_search.Text, vehicleId);
        var consumptionTask = App.Fuel.GetConsumptionAsync();
        await Task.WhenAll(recordsTask, consumptionTask);
        if (token.IsCancellationRequested) return;

        _items = await recordsTask;
        _consumption = await consumptionTask;
        _paginator.SetData(_items);
        _consumptionGrid.DataSource = _consumption.ToList();
        _consumptionGrid.ClearSelection();

        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthItems = _items.Where(r => r.Date >= monthStart).ToList();
        var monthCost = monthItems.Sum(r => r.TotalCost);
        var monthLiters = monthItems.Sum(r => r.Liters);
        _summary.Text = $"За месяц: {monthLiters:N0} л · {monthCost:N0} ₽";
        _summary.ForeColor = ThemeManager.Current.Accent;

        BuildChart();
        UpdateActions();
    }

    private void BuildChart()
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var labels = new List<string>(12);
        var values = new List<double>(12);

        for (var month = 1; month <= 12; month++)
        {
            labels.Add(new DateTime(2000, month, 1).ToString("MMM", culture));
            var cost = _items
                .Where(r => r.Date.Year == DateTime.Today.Year && r.Date.Month == month)
                .Sum(r => r.TotalCost);
            values.Add((double)cost);
        }

        _fuelChart.Labels = labels;
        _fuelChart.Series = new[]
        {
            new ChartSeries
            {
                Title = "Топливо, ₽",
                Color = Color.FromArgb(249, 115, 22),
                Values = values
            }
        };
        _fuelChart.Invalidate();
    }

    private void UpdateActions() => _deleteButton.Enabled = _paginator.Current is not null;

    private async Task AddAsync()
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new FuelEditForm(App, _vehicles, _drivers);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Заправка добавлена", "Расход и стоимость пересчитаны.");
            await ReloadAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_paginator.Current is not { } record) return;
        if (!Ui.Confirm(FindForm(), "Удалить заправку?",
                $"{record.PlateNumber}, {record.Date:dd.MM.yyyy}: {record.Liters:N2} л на {record.TotalCost:N2} ₽."))
            return;

        try
        {
            await App.Fuel.DeleteAsync(record);
            Notify(ToastKind.Success, "Запись удалена", $"{record.PlateNumber} · {record.Date:dd.MM.yyyy}");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "Дата", "Госномер", "Автомобиль", "Объём, л", "Цена, ₽", "Стоимость, ₽", "Одометр", "Водитель" };
        var rows = _items.Select(r => (IReadOnlyList<object?>)new object?[]
        {
            r.Date, r.PlateNumber, r.VehicleName, r.Liters, r.CostPerLiter, r.TotalCost, r.Odometer, r.DriverName
        }).ToList();

        ExportExcel("Журнал заправок", "Топливо", headers, rows);
    }
}

