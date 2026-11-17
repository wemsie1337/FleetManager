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

/// <summary>
/// Техническое обслуживание: сроки по каждому ТС, журнал выполненных работ,
/// управление индивидуальными планами и подтверждение уведомлений.
/// </summary>
public sealed class MaintenanceView : BaseView
{
    public override string PageTitle => App.Session.IsDriver ? "Уведомления ТО" : "Техническое обслуживание";

    public override string PageSubtitle => App.Session.IsDriver
        ? "Сроки планового обслуживания и история работ"
        : "Контроль регламентов, журнал работ и уведомления";

    private readonly UiTextField _search;
    private readonly UiComboBox _severityFilter;
    private readonly Label _summary;
    private readonly DataGridView _dueGrid;
    private readonly DataGridView _recordGrid;
    private readonly GridPaginator<MaintenanceDue> _duePaginator;
    private readonly GridPaginator<MaintenanceRecord> _recordPaginator;
    private readonly GradientButton _executeButton;
    private readonly GradientButton _plansButton;
    private readonly GradientButton _ackButton;
    private readonly GradientButton _exportButton;

    private IReadOnlyList<MaintenanceDue> _due = Array.Empty<MaintenanceDue>();
    private IReadOnlyList<MaintenanceRecord> _records = Array.Empty<MaintenanceRecord>();
    private IReadOnlyList<MaintenanceAlert> _alerts = Array.Empty<MaintenanceAlert>();
    private IReadOnlyList<Vehicle> _vehicles = Array.Empty<Vehicle>();
    private IReadOnlyList<MaintenanceType> _types = Array.Empty<MaintenanceType>();
    private bool _lookupsLoaded;

    public MaintenanceView(AppServices app) : base(app)
    {
        var canManage = app.Session.CanManageFleet;

        _search = CreateSearchBox("Поиск по номеру, автомобилю, виду ТО...", 240);
        EnableLiveSearch(_search);

        _severityFilter = new UiComboBox { Width = 150, Height = 40 };
        _severityFilter.Inner.Items.AddRange(new object[]
        {
            "Все сроки", "Просрочено", "Наступило", "Приближается"
        });
        _severityFilter.Inner.SelectedIndex = 0;
        _severityFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _summary = new Label
        {
            Text = "Загрузка…",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 13, 4, 0),
            Tag = "muted"
        };

        _dueGrid = CreateGrid();
        AddColumn(_dueGrid, "Госномер", nameof(MaintenanceDue.PlateNumber), 100);
        AddColumn(_dueGrid, "Вид ТО", nameof(MaintenanceDue.TypeName), 135);
        AddColumn(_dueGrid, "Пробег", nameof(MaintenanceDue.CurrentMileage), 85,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_dueGrid, "След. ТО", nameof(MaintenanceDue.NextDueMileage), 100,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_dueGrid, "Остаток", nameof(MaintenanceDue.RemainingText), 160, fill: true);
        AddColumn(_dueGrid, "Статус", nameof(MaintenanceDue.SeverityText), 100);
        _dueGrid.Columns[2].DefaultCellStyle.Format = "N0";
        _dueGrid.Columns[3].DefaultCellStyle.Format = "N0";
        _dueGrid.CellFormatting += DueGrid_CellFormatting;
        _dueGrid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && canManage) await ExecuteAsync(GetSelectedVehicleId());
        };
        _dueGrid.SelectionChanged += (_, _) => UpdateActions();

        _duePaginator = new GridPaginator<MaintenanceDue>(_dueGrid);

        _recordGrid = CreateGrid();
        AddColumn(_recordGrid, "Дата", nameof(MaintenanceRecord.Date), 85);
        AddColumn(_recordGrid, "Госномер", nameof(MaintenanceRecord.PlateNumber), 92);
        AddColumn(_recordGrid, "Вид ТО", nameof(MaintenanceRecord.TypeName), 120);
        AddColumn(_recordGrid, "Пробег", nameof(MaintenanceRecord.MileageAtService), 80,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_recordGrid, "Стоимость", nameof(MaintenanceRecord.Cost), 95,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_recordGrid, "Работы", nameof(MaintenanceRecord.Notes), 190, fill: true);
        _recordGrid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        _recordGrid.Columns[3].DefaultCellStyle.Format = "N0";
        _recordGrid.Columns[4].DefaultCellStyle.Format = "N2";

        _recordPaginator = new GridPaginator<MaintenanceRecord>(_recordGrid);

        _executeButton = new GradientButton
        {
            Text = "Выполнить ТО",
            IconGlyph = "\uE90F",
            Kind = ButtonKind.Accent,
            Width = 165,
            Height = 40
        };
        _executeButton.Click += async (_, _) => await ExecuteAsync(GetSelectedVehicleId());

        _plansButton = new GradientButton
        {
            Text = "Планы ТО",
            IconGlyph = "\uE8EF",
            Kind = ButtonKind.Ghost,
            Width = 125,
            Height = 40
        };
        _plansButton.Click += async (_, _) => await OpenPlansAsync();

        _ackButton = new GradientButton
        {
            Text = "Скрыть уведомления",
            IconGlyph = "\uE7BA",
            Kind = ButtonKind.Ghost,
            Width = 175,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        _ackButton.Click += async (_, _) => await AcknowledgeAllAsync();

        _exportButton = new GradientButton
        {
            Text = "Excel",
            IconGlyph = "\uE9F9",
            Kind = ButtonKind.Ghost,
            Width = 88,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        _exportButton.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _severityFilter, _summary, refresh, _ackButton, _exportButton);

        var dueCard = CreateGridCard("Сроки ТО и регламентные работы", _dueGrid, _duePaginator.Bar);
        dueCard.Margin = new Padding(0, 0, 0, 12);
        var recordCard = CreateGridCard("Журнал выполненных работ", _recordGrid, _recordPaginator.Bar);
        recordCard.Margin = new Padding(0);

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Margin = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        split.Controls.Add(dueCard, 0, 0);
        split.Controls.Add(recordCard, 0, 1);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Tag = "transparent" };
        body.Controls.Add(split);
        body.Controls.Add(toolbar);

        if (canManage) BuildLayout(body, _executeButton, _plansButton);
        else
        {
            _ackButton.Visible = false;
            BuildLayout(body);
        }

        UpdateActions();
    }

    private CardPanel CreateGridCard(string title, DataGridView grid, Control footer)
    {
        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);

        var header = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.Transparent, Tag = "transparent" };
        header.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        });

        card.Controls.Add(grid);
        card.Controls.Add(footer);
        card.Controls.Add(header);
        return card;
    }

    private int? GetSelectedVehicleId() =>
        _duePaginator.Current?.VehicleId;

    private void DueGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_dueGrid.Rows[e.RowIndex].DataBoundItem is not MaintenanceDue due) return;

        var t = ThemeManager.Current;
        var color = Ui.SeverityColor(due.SeverityValue);
        if (_dueGrid.Columns[e.ColumnIndex].Name is nameof(MaintenanceDue.SeverityText)
            or nameof(MaintenanceDue.RemainingText))
        {
            e.CellStyle.ForeColor = color;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        else if (due.SeverityValue == MaintenanceSeverity.Overdue)
        {
            e.CellStyle.ForeColor = t.Danger;
        }
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_lookupsLoaded)
        {
            var vehiclesTask = App.Vehicles.GetAllAsync();
            var typesTask = App.Maintenance.GetTypesAsync();
            await Task.WhenAll(vehiclesTask, typesTask);
            _vehicles = await vehiclesTask;
            _types = await typesTask;
            _lookupsLoaded = true;
        }

        var dueTask = App.Maintenance.GetDueAsync();
        var recordsTask = App.Maintenance.GetRecordsAsync(null);
        var alertsTask = App.Maintenance.GetActiveAlertsAsync();
        await Task.WhenAll(dueTask, recordsTask, alertsTask);
        if (token.IsCancellationRequested) return;

        _due = await dueTask;
        _records = await recordsTask;
        _alerts = await alertsTask;

        var overdue = _due.Count(d => d.SeverityValue == MaintenanceSeverity.Overdue);
        var due = _due.Count(d => d.SeverityValue == MaintenanceSeverity.Due);
        var upcoming = _due.Count(d => d.SeverityValue == MaintenanceSeverity.Upcoming);

        _summary.Text = $"Просрочено {overdue}  ·  наступило {due}  ·  скоро {upcoming}";
        _summary.ForeColor = overdue > 0
            ? ThemeManager.Current.Danger
            : due > 0
                ? ThemeManager.Current.Accent
                : upcoming > 0
                    ? ThemeManager.Current.Warning
                    : ThemeManager.Current.Success;

        var filtered = FilterDue();
        _duePaginator.SetData(filtered);
        _recordPaginator.SetData(_records);
        UpdateActions();
    }

    private IEnumerable<MaintenanceDue> FilterDue()
    {
        IEnumerable<MaintenanceDue> query = _due;

        var severity = _severityFilter.Inner.SelectedIndex switch
        {
            1 => "Overdue",
            2 => "Due",
            3 => "Upcoming",
            _ => null
        };
        if (severity is not null)
            query = query.Where(d => string.Equals(d.Severity, severity, StringComparison.OrdinalIgnoreCase));

        var search = _search.Text.Trim();
        if (search.Length > 0)
            query = query.Where(d =>
                d.PlateNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.VehicleName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.TypeName.Contains(search, StringComparison.OrdinalIgnoreCase));

        return query;
    }

    private void UpdateActions()
    {
        var hasSelection = _duePaginator.Current is not null;
        _executeButton.Enabled = hasSelection;
        _plansButton.Enabled = App.Session.CanManageFleet;
        _ackButton.Enabled = _alerts.Count > 0;
        _ackButton.Text = _alerts.Count > 0 ? $"Скрыть уведомления ({_alerts.Count})" : "Уведомлений нет";
        _ackButton.Width = _alerts.Count > 0 ? 205 : 175;
    }

    private async Task ExecuteAsync(int? vehicleId)
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new MaintenanceRecordForm(App, _vehicles, _types, vehicleId);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "ТО зафиксировано", "План обновлён, уведомления пересчитаны.");
            await ReloadAsync();
        }
    }

    private async Task OpenPlansAsync()
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new MaintenancePlanForm(App, _vehicles, _types, GetSelectedVehicleId());
        ModalHost.Show(FindForm(), dialog);
        await ReloadAsync();
    }

    private async Task AcknowledgeAllAsync()
    {
        if (_alerts.Count == 0) return;
        if (!Ui.Confirm(FindForm(), "Скрыть уведомления?",
                "Все активные уведомления будут отмечены как просмотренные. Сроки ТО при этом не изменятся."))
            return;

        try
        {
            await App.Maintenance.AcknowledgeAllAsync();
            Notify(ToastKind.Success, "Уведомления скрыты", "Активные напоминания отмечены как просмотренные.");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "Госномер", "Автомобиль", "Вид ТО", "Пробег", "Следующее ТО", "Остаток", "Статус" };
        var rows = _due.Select(d => (IReadOnlyList<object?>)new object?[]
        {
            d.PlateNumber, d.VehicleName, d.TypeName, d.CurrentMileage, d.NextDueMileage,
            d.RemainingText, d.SeverityText
        }).ToList();

        ExportExcel("Сроки технического обслуживания", "Плановое ТО", headers, rows);
    }
}

