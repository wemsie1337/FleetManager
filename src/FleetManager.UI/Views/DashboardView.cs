using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Views;

public sealed class DashboardView : BaseView
{
    public override string PageTitle => "Дашборд";
    public override string PageSubtitle =>
        $"{DateTime.Today:dddd, d MMMM yyyy} · оперативная сводка по автопарку";

    public event EventHandler? OpenMaintenanceRequested;

    private readonly KpiCard _onLine = new() { Title = "На линии", IconGlyph = "\uE806" };
    private readonly KpiCard _inRepair = new() { Title = "В ремонте", IconGlyph = "\uE90F" };
    private readonly KpiCard _maintenance = new() { Title = "Ожидают ТО", IconGlyph = "\uE7BA" };
    private readonly KpiCard _retired = new() { Title = "Списаны", IconGlyph = "\uE74D" };
    private readonly KpiCard _mileage = new() { Title = "Пробег за месяц", IconGlyph = "\uE9D2" };

    private readonly SimpleChart _costChart = new()
    {
        Dock = DockStyle.Fill,
        Kind = ChartKind.Line,
        ValueFormat = "N0"
    };
    private readonly FlowLayoutPanel _alertsPanel;
    private readonly Label _alertsCount;
    private readonly DataGridView _recentGrid;
    private readonly GradientButton _refreshButton;

    public DashboardView(AppServices app) : base(app)
    {
        var t = ThemeManager.Current;
        _onLine.AccentColor = t.Success;
        _inRepair.AccentColor = t.Accent;
        _maintenance.AccentColor = t.Warning;
        _retired.AccentColor = t.TextMuted;
        _mileage.AccentColor = t.PrimaryLight;

        _refreshButton = new GradientButton
        {
            Text = "Обновить",
            IconGlyph = "\uE72C",
            Kind = ButtonKind.Ghost,
            Width = 130,
            Height = 40,
            Font = new Font("Segoe UI", 9f)
        };
        _refreshButton.Click += async (_, _) => await ReloadAsync();

        _alertsCount = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = t.Danger,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(26, 22)
        };

        _alertsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0)
        };
        _alertsPanel.Resize += (_, _) => ResizeAlertRows();
        GridStyler.ApplyScrollbar(_alertsPanel);

        _recentGrid = CreateGrid();
        AddColumn(_recentGrid, "Дата", nameof(MileageLog.Date), 100);
        AddColumn(_recentGrid, "Автомобиль", nameof(MileageLog.VehicleName), 190);
        AddColumn(_recentGrid, "Госномер", nameof(MileageLog.PlateNumber), 110);
        AddColumn(_recentGrid, "Водитель", nameof(MileageLog.DriverName), 180);
        AddColumn(_recentGrid, "Пробег", nameof(MileageLog.TripMileage), 90, alignment: DataGridViewContentAlignment.MiddleRight);
        _recentGrid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        _recentGrid.Columns[4].DefaultCellStyle.Format = "N0";

        var kpiTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Margin = new Padding(0)
        };
        for (var i = 0; i < 5; i++) kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        kpiTable.Controls.Add(_onLine, 0, 0);
        kpiTable.Controls.Add(_inRepair, 1, 0);
        kpiTable.Controls.Add(_maintenance, 2, 0);
        kpiTable.Controls.Add(_retired, 3, 0);
        kpiTable.Controls.Add(_mileage, 4, 0);
        foreach (Control card in kpiTable.Controls)
        {
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(2, 2, 10, 2);
        }

        var charts = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        var costCard = CreateTitledCard("Расходы по месяцам, ₽", _costChart, null);
        costCard.Margin = new Padding(2, 4, 12, 4);
        charts.Controls.Add(costCard, 0, 0);

        var alertsBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Tag = "transparent" };
        alertsBody.Controls.Add(_alertsPanel);
        var alertsCard = CreateTitledCard("Требует внимания", alertsBody, _alertsCount);
        alertsCard.Margin = new Padding(0, 4, 2, 4);
        charts.Controls.Add(alertsCard, 1, 0);

        var recentCard = CreateTitledCard("Последние поездки", _recentGrid, null);
        recentCard.Margin = new Padding(2, 4, 2, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 282));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(kpiTable, 0, 0);
        layout.Controls.Add(charts, 0, 1);
        layout.Controls.Add(recentCard, 0, 2);

        BuildLayout(layout, _refreshButton);
    }

    private CardPanel CreateTitledCard(string title, Control body, Label? badge)
    {
        var card = CreateCard();
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        var label = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            Dock = DockStyle.Left,
            Width = 320,
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(label);
        if (badge is not null)
        {
            badge.Dock = DockStyle.Right;
            badge.TextAlign = ContentAlignment.MiddleCenter;
            header.Controls.Add(badge);
            var spacer = new Panel { Dock = DockStyle.Right, Width = 8, BackColor = Color.Transparent };
            header.Controls.Add(spacer);
        }

        card.Controls.Add(body);
        card.Controls.Add(header);
        return card;
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        var data = await App.Dashboard.LoadAsync(DateTime.Today.Year,
            App.Session.IsDriver ? App.Session.DriverId : null);
        if (token.IsCancellationRequested) return;

        var kpi = data.Kpi;
        _onLine.Value = kpi.OnLine.ToString();
        _onLine.Subtitle = $"из {kpi.TotalVehicles} ТС в автопарке";
        _inRepair.Value = kpi.InRepair.ToString();
        _inRepair.Subtitle = "на ремонте и диагностике";
        _maintenance.Value = (kpi.UpcomingCount + kpi.DueCount + kpi.OverdueCount).ToString();
        _maintenance.Subtitle = $"просрочено: {kpi.OverdueCount}";
        _retired.Value = kpi.Retired.ToString();
        _retired.Subtitle = $"водителей: {kpi.ActiveDrivers}";
        _mileage.Value = $"{kpi.MonthMileage:N0}";
        _mileage.Subtitle = $"км · топливо {kpi.MonthFuelCost:N0} ₽";

        BuildCostChart(data.Monthly);
        RenderAlerts(data.Alerts);
        _recentGrid.DataSource = data.RecentMileage.ToList();
        _recentGrid.ClearSelection();
    }

    private void BuildCostChart(IReadOnlyList<MonthlyCost> monthly)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");

        _costChart.Labels = monthly
            .Select(m => new DateTime(2000, Math.Max(1, m.Month), 1).ToString("MMM", culture))
            .ToList();

        _costChart.Series = new[]
        {
            new ChartSeries
            {
                Title = "Топливо",
                Color = Color.FromArgb(249, 115, 22),
                Values = monthly.Select(m => (double)m.FuelCost).ToList()
            },
            new ChartSeries
            {
                Title = "ТО и ремонт",
                Color = Color.FromArgb(20, 184, 166),
                Values = monthly.Select(m => (double)m.MaintenanceCost).ToList()
            }
        };

        _costChart.Invalidate();
    }

    private void RenderAlerts(IReadOnlyList<MaintenanceAlert> alerts)
    {
        _alertsPanel.SuspendLayout();
        _alertsPanel.Controls.Clear();
        _alertsCount.Text = alerts.Count.ToString();
        _alertsCount.BackColor = alerts.Count > 0 ? ThemeManager.Current.Danger : ThemeManager.Current.Success;

        if (alerts.Count == 0)
        {
            var ok = new Label
            {
                Text = "✓  Все автомобили в норме.\nАктивных уведомлений нет.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ThemeManager.Current.Success,
                BackColor = Color.Transparent,
                AutoSize = false,
                Height = 60,
                Width = 280,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _alertsPanel.Controls.Add(ok);
        }
        else
        {
            foreach (var alert in alerts.Take(8))
            {
                var row = new AlertRow { Alert = alert, Width = Math.Max(180, _alertsPanel.ClientSize.Width - 36) };
                row.Click += (_, _) => OpenMaintenanceRequested?.Invoke(this, EventArgs.Empty);
                _alertsPanel.Controls.Add(row);
            }
        }

        _alertsPanel.ResumeLayout();
    }

    private void ResizeAlertRows()
    {
        foreach (Control control in _alertsPanel.Controls)
            if (control is AlertRow row) row.Width = Math.Max(180, _alertsPanel.ClientSize.Width - 36);
    }
}
