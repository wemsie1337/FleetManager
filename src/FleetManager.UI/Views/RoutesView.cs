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

/// <summary>Маршруты: планирование, назначение водителя и ТС, смена статуса.</summary>
public sealed class RoutesView : BaseView
{
    public override string PageTitle => App.Session.IsDriver ? "Мои маршруты" : "Маршруты";

    public override string PageSubtitle => App.Session.IsDriver
        ? "Назначенные рейсы и их состояние"
        : "Планирование рейсов, закрепление водителей и транспорта";

    private readonly UiTextField _search;
    private readonly UiComboBox _statusFilter;
    private readonly UiComboBox _actionCombo;
    private readonly DataGridView _grid;
    private readonly GridPaginator<Route> _paginator;
    private readonly GradientButton _addButton;
    private readonly GradientButton _editButton;
    private readonly GradientButton _deleteButton;
    private readonly GradientButton _applyButton;
    private readonly Label _summary;

    private IReadOnlyList<Route> _items = Array.Empty<Route>();
    private IReadOnlyList<Driver> _drivers = Array.Empty<Driver>();
    private IReadOnlyList<Vehicle> _vehicles = Array.Empty<Vehicle>();
    private bool _lookupsLoaded;

    public RoutesView(AppServices app) : base(app)
    {
        var canManage = app.Session.CanManageFleet;

        _search = CreateSearchBox("Поиск по водителю, адресу, номеру...", 250);
        EnableLiveSearch(_search);

        _statusFilter = new UiComboBox { Width = 155, Height = 40 };
        _statusFilter.Inner.Items.AddRange(new object[]
        {
            "Все статусы", "Запланирован", "В работе", "Завершён", "Отменён"
        });
        _statusFilter.Inner.SelectedIndex = 0;
        _statusFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _summary = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 13, 0, 0),
            Tag = "muted"
        };

        _actionCombo = new UiComboBox { Width = 190, Height = 40 };
        _actionCombo.Inner.Items.AddRange(new object[]
        {
            "Изменить статус…", "В работу", "Завершить", "Отменить"
        });
        _actionCombo.Inner.SelectedIndex = 0;

        _applyButton = new GradientButton
        {
            Text = "Применить",
            IconGlyph = "\uE73E",
            Kind = ButtonKind.Ghost,
            Width = 118,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _applyButton.Click += async (_, _) => await ApplyStatusAsync();

        _grid = CreateGrid();
        AddColumn(_grid, "Начало", nameof(Route.StartDate), 105);
        AddColumn(_grid, "Водитель", nameof(Route.DriverName), 185);
        AddColumn(_grid, "Автомобиль", nameof(Route.VehicleName), 145);
        AddColumn(_grid, "Госномер", nameof(Route.PlateNumber), 95);
        AddColumn(_grid, "Откуда", nameof(Route.StartPoint), 110);
        AddColumn(_grid, "Куда", nameof(Route.EndPoint), 195);
        AddColumn(_grid, "План, км", nameof(Route.PlannedMileage), 80,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Статус", nameof(Route.StatusName), 120);
        AddColumn(_grid, "Завершён", nameof(Route.EndDate), 100, fill: true);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.Columns[6].DefaultCellStyle.Format = "N0";
        _grid.Columns[8].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && canManage) EditSelected();
        };
        _grid.SelectionChanged += (_, _) => UpdateActions();

        _paginator = new GridPaginator<Route>(_grid);

        _addButton = new GradientButton
        {
            Text = "Новый маршрут",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 170,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        _editButton = new GradientButton
        {
            Text = "Изменить",
            Kind = ButtonKind.Ghost,
            Width = 105,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _editButton.Click += (_, _) => EditSelected();

        _deleteButton = new GradientButton
        {
            Text = "Удалить",
            Kind = ButtonKind.Danger,
            Width = 92,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();

        var export = new GradientButton
        {
            Text = "Excel",
            IconGlyph = "\uE9F9",
            Kind = ButtonKind.Ghost,
            Width = 92,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        export.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _statusFilter, _summary);
        var flow = (FlowLayoutPanel)toolbar.Controls[0];
        if (canManage)
        {
            _actionCombo.Margin = new Padding(0, 0, 10, 0);
            flow.Controls.Add(_actionCombo);
            _applyButton.Margin = new Padding(0, 0, 10, 0);
            flow.Controls.Add(_applyButton);
        }
        refresh.Margin = new Padding(0);
        flow.Controls.Add(refresh);

        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);
        card.Controls.Add(_grid);
        card.Controls.Add(_paginator.Bar);
        card.Controls.Add(toolbar);

        if (canManage) BuildLayout(card, _addButton, _editButton, _deleteButton, export);
        else BuildLayout(card, export);

        UpdateActions();
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != nameof(Route.StatusName)) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not Route route) return;

        e.CellStyle.ForeColor = Ui.RouteStatusColor(route.Status);
        e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_lookupsLoaded)
        {
            var driversTask = App.Drivers.GetAllAsync();
            var vehiclesTask = App.Vehicles.GetAllAsync();
            await Task.WhenAll(driversTask, vehiclesTask);
            _drivers = await driversTask;
            _vehicles = await vehiclesTask;
            _lookupsLoaded = true;
        }

        _items = await App.Routes.SearchAsync(_search.Text);
        if (token.IsCancellationRequested) return;

        var filtered = FilterItems();
        _paginator.SetData(filtered);

        var active = _items.Count(r => r.Status is (int)RouteState.Planned or (int)RouteState.InProgress);
        var done = _items.Count(r => r.Status == (int)RouteState.Completed);
        _summary.Text = $"Активных {active}  ·  завершено {done}  ·  всего {_items.Count}";
        _summary.ForeColor = active > 0 ? ThemeManager.Current.PrimaryLight : ThemeManager.Current.TextMuted;

        UpdateActions();
    }

    private IEnumerable<Route> FilterItems()
    {
        IEnumerable<Route> query = _items;
        if (_statusFilter.Inner.SelectedIndex > 0)
        {
            var status = _statusFilter.Inner.SelectedIndex;
            query = query.Where(r => r.Status == status);
        }

        return query;
    }

    private void UpdateActions()
    {
        if (!App.Session.CanManageFleet) return;
        var route = _paginator.Current;
        var hasSelection = route is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _applyButton.Enabled = hasSelection;
        _actionCombo.Enabled = hasSelection;

        if (route is null) _actionCombo.Inner.SelectedIndex = 0;
    }

    private async Task AddAsync()
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new RouteEditForm(App, null, _drivers, _vehicles);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Маршрут создан", "Водитель и транспортное средство назначены.");
            await ReloadAsync();
        }
    }

    private async void EditSelected()
    {
        if (_paginator.Current is not { } route) return;
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new RouteEditForm(App, route, _drivers, _vehicles);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Маршрут обновлён", $"{route.StartPoint} → {route.EndPoint}");
            await ReloadAsync();
        }
    }

    private async Task ApplyStatusAsync()
    {
        if (_paginator.Current is not { } route) return;
        if (_actionCombo.Inner.SelectedIndex <= 0)
        {
            Ui.ShowInfo(FindForm(), "Выберите новое состояние маршрута в списке действий.");
            return;
        }

        var state = _actionCombo.Inner.SelectedIndex switch
        {
            1 => RouteState.InProgress,
            2 => RouteState.Completed,
            3 => RouteState.Cancelled,
            _ => route.Status switch
            {
                (int)RouteState.Planned => RouteState.InProgress,
                (int)RouteState.InProgress => RouteState.Completed,
                _ => RouteState.Completed
            }
        };

        if (route.Status == (int)state)
        {
            Ui.ShowInfo(FindForm(), "Маршрут уже находится в выбранном состоянии.");
            return;
        }

        try
        {
            await App.Routes.UpdateStatusAsync(route, state);
            Notify(ToastKind.Success, "Статус изменён", $"Маршрут: {state.ToRussian()}");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_paginator.Current is not { } route) return;
        if (!Ui.Confirm(FindForm(), "Удалить маршрут?",
                $"Маршрут {route.StartPoint} → {route.EndPoint} ({route.DriverName}) будет удалён."))
            return;

        try
        {
            await App.Routes.DeleteAsync(route);
            Notify(ToastKind.Success, "Маршрут удалён", $"{route.StartPoint} → {route.EndPoint}");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "Начало", "Водитель", "Автомобиль", "Госномер", "Откуда", "Куда", "План, км", "Статус" };
        var rows = _items.Select(r => (IReadOnlyList<object?>)new object?[]
        {
            r.StartDate, r.DriverName, r.VehicleName, r.PlateNumber, r.StartPoint, r.EndPoint,
            r.PlannedMileage, r.StatusName
        }).ToList();

        ExportExcel("Маршруты автопарка", "Маршруты", headers, rows);
    }
}

