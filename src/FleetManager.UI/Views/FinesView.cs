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

/// <summary>Штрафы и ДТП: журнал происшествий, оплата и аналитика задолженности.</summary>
public sealed class FinesView : BaseView
{
    public override string PageTitle => "Штрафы и ДТП";
    public override string PageSubtitle => "Происшествия, задолженность и отметки об оплате";

    private readonly UiTextField _search;
    private readonly UiComboBox _statusFilter;
    private readonly DataGridView _grid;
    private readonly GridPaginator<Fine> _paginator;
    private readonly GradientButton _addButton;
    private readonly GradientButton _payButton;
    private readonly GradientButton _deleteButton;
    private readonly Label _summary;

    private IReadOnlyList<Fine> _items = Array.Empty<Fine>();
    private IReadOnlyList<Vehicle> _vehicles = Array.Empty<Vehicle>();
    private IReadOnlyList<Driver> _drivers = Array.Empty<Driver>();
    private bool _lookupsLoaded;

    public FinesView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по номеру, водителю, описанию...", 260);
        EnableLiveSearch(_search);

        _statusFilter = new UiComboBox { Width = 160, Height = 40 };
        _statusFilter.Inner.Items.AddRange(new object[] { "Все записи", "Не оплачены", "Оплачены" });
        _statusFilter.Inner.SelectedIndex = 0;
        _statusFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _summary = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Danger,
            BackColor = Color.Transparent,
            Margin = new Padding(4, 13, 0, 0)
        };

        _grid = CreateGrid();
        AddColumn(_grid, "Дата", nameof(Fine.Date), 92);
        AddColumn(_grid, "Госномер", nameof(Fine.PlateNumber), 100);
        AddColumn(_grid, "Автомобиль", nameof(Fine.VehicleName), 165);
        AddColumn(_grid, "Водитель", nameof(Fine.DriverName), 155);
        AddColumn(_grid, "Тип", nameof(Fine.IncidentTypeName), 85);
        AddColumn(_grid, "Сумма, ₽", nameof(Fine.Amount), 105,
            alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Описание", nameof(Fine.Description), 220, fill: true);
        AddColumn(_grid, "Оплата", nameof(Fine.PaidText), 120);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.Columns[5].DefaultCellStyle.Format = "N2";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.SelectionChanged += (_, _) => UpdateActions();

        _paginator = new GridPaginator<Fine>(_grid);

        _addButton = new GradientButton
        {
            Text = "Добавить запись",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 175,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        _payButton = new GradientButton
        {
            Text = "Оплатить",
            Kind = ButtonKind.Success,
            Width = 118,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _payButton.Click += async (_, _) => await TogglePaidAsync();

        _deleteButton = new GradientButton
        {
            Text = "Удалить",
            Kind = ButtonKind.Danger,
            Width = 95,
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

        var toolbar = CreateToolbar(_search, _statusFilter, _summary, _payButton, _deleteButton, refresh);

        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);
        card.Controls.Add(_grid);
        card.Controls.Add(_paginator.Bar);
        card.Controls.Add(toolbar);

        BuildLayout(card, _addButton, export);
        UpdateActions();
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name == nameof(Fine.PaidText) &&
            _grid.Rows[e.RowIndex].DataBoundItem is Fine fine)
        {
            e.CellStyle.ForeColor = fine.IsPaid ? ThemeManager.Current.Success : ThemeManager.Current.Danger;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        else if (_grid.Columns[e.ColumnIndex].Name == nameof(Fine.Amount))
        {
            e.CellStyle.ForeColor = ThemeManager.Current.Text;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
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
            _lookupsLoaded = true;
        }

        bool? isPaid = _statusFilter.Inner.SelectedIndex switch
        {
            1 => false,
            2 => true,
            _ => null
        };

        _items = await App.Fines.SearchAsync(_search.Text, isPaid);
        if (token.IsCancellationRequested) return;
        _paginator.SetData(_items);

        var unpaid = _items.Where(f => !f.IsPaid).ToList();
        _summary.Text = $"Не оплачено: {unpaid.Count} на {unpaid.Sum(f => f.Amount):N0} ₽";
        _summary.ForeColor = unpaid.Count > 0 ? ThemeManager.Current.Danger : ThemeManager.Current.Success;

        UpdateActions();
    }

    private void UpdateActions()
    {
        var fine = _paginator.Current;
        var hasSelection = fine is not null;
        _payButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        if (fine is not null)
        {
            _payButton.Text = fine.IsPaid ? "Снять оплату" : "Оплатить";
            _payButton.Kind = fine.IsPaid ? ButtonKind.Ghost : ButtonKind.Success;
        }
    }

    private async Task AddAsync()
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new FineEditForm(App, _vehicles, _drivers);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Запись добавлена", "Штраф или ДТП внесён в журнал.");
            await ReloadAsync();
        }
    }

    private async Task TogglePaidAsync()
    {
        if (_paginator.Current is not { } fine) return;
        try
        {
            await App.Fines.SetPaidAsync(fine, !fine.IsPaid);
            Notify(ToastKind.Success, fine.IsPaid ? "Оплата снята" : "Штраф оплачен",
                $"{fine.PlateNumber} · {fine.Amount:N2} ₽");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_paginator.Current is not { } fine) return;
        if (!Ui.Confirm(FindForm(), "Удалить запись?",
                $"{fine.IncidentTypeName} от {fine.Date:dd.MM.yyyy} на {fine.Amount:N2} ₽ будет удалён."))
            return;

        try
        {
            await App.Fines.DeleteAsync(fine);
            Notify(ToastKind.Success, "Запись удалена", fine.Description);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "Дата", "Госномер", "Автомобиль", "Водитель", "Тип", "Сумма, ₽", "Описание", "Оплата" };
        var rows = _items.Select(f => (IReadOnlyList<object?>)new object?[]
        {
            f.Date, f.PlateNumber, f.VehicleName, f.DriverName, f.IncidentTypeName, f.Amount, f.Description, f.PaidText
        }).ToList();

        ExportExcel("Штрафы и ДТП", "Штрафы", headers, rows);
    }
}

