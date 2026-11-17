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

public sealed class MileageView : BaseView
{
    public override string PageTitle => App.Session.IsDriver ? "Мой пробег" : "Учёт пробега";
    public override string PageSubtitle => App.Session.IsDriver
        ? "Внесите показания одометра в конце смены"
        : "Ежедневные показания, история и контроль расхода";

    private readonly UiTextField _search;
    private readonly UiDateField _from;
    private readonly UiDateField _to;
    private readonly DataGridView _grid;
    private readonly GridPaginator<MileageLog> _paginator;
    private readonly GradientButton _addButton;
    private IReadOnlyList<MileageLog> _items = Array.Empty<MileageLog>();
    private IReadOnlyList<Vehicle> _vehicles = Array.Empty<Vehicle>();
    private IReadOnlyList<Driver> _drivers = Array.Empty<Driver>();
    private bool _lookupsLoaded;

    public MileageView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по номеру, водителю, заметке...", 290);
        EnableLiveSearch(_search);

        _from = new UiDateField(true) { Width = 170, Height = 40 };
        _to = new UiDateField(true) { Width = 170, Height = 40 };
        _from.Inner.ValueChanged += async (_, _) => await ReloadAsync();
        _to.Inner.ValueChanged += async (_, _) => await ReloadAsync();

        _grid = CreateGrid();
        AddColumn(_grid, "Дата", nameof(MileageLog.Date), 95);
        AddColumn(_grid, "Автомобиль", nameof(MileageLog.VehicleName), 170);
        AddColumn(_grid, "Госномер", nameof(MileageLog.PlateNumber), 105);
        AddColumn(_grid, "Водитель", nameof(MileageLog.DriverName), 170);
        AddColumn(_grid, "Начало", nameof(MileageLog.OdometerStart), 90, alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Конец", nameof(MileageLog.OdometerEnd), 90, alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Пробег", nameof(MileageLog.TripMileage), 85, alignment: DataGridViewContentAlignment.MiddleRight);
        AddColumn(_grid, "Примечание", nameof(MileageLog.Note), 160, fill: true);
        AddColumn(_grid, "Внёс", nameof(MileageLog.CreatedByName), 150);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy";
        foreach (var index in new[] { 4, 5, 6 })
        {
            _grid.Columns[index].DefaultCellStyle.Format = "N0";
            _grid.Columns[index].DefaultCellStyle.ForeColor = ThemeManager.Current.PrimaryLight;
        }

        _paginator = new GridPaginator<MileageLog>(_grid);

        _addButton = new GradientButton
        {
            Text = App.Session.IsDriver ? "Внести пробег" : "Новая запись",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 160,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        var exportExcel = new GradientButton
        {
            Text = "Excel", IconGlyph = "\uE9F9", Kind = ButtonKind.Ghost, Width = 100, Height = 40
        };
        exportExcel.Click += (_, _) => Export(true);
        var exportPdf = new GradientButton
        {
            Text = "PDF", IconGlyph = "\uE8A5", Kind = ButtonKind.Ghost, Width = 90, Height = 40
        };
        exportPdf.Click += (_, _) => Export(false);

        var fromCaption = new Label { Text = "С", Font = Ui.Fonts.Small, ForeColor = ThemeManager.Current.TextMuted, AutoSize = true, Margin = new Padding(0, 12, 4, 0), Tag = "muted" };
        var toCaption = new Label { Text = "по", Font = Ui.Fonts.Small, ForeColor = ThemeManager.Current.TextMuted, AutoSize = true, Margin = new Padding(0, 12, 4, 0), Tag = "muted" };
        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, fromCaption, _from, toCaption, _to, refresh);
        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);
        card.Controls.Add(_grid);
        card.Controls.Add(_paginator.Bar);
        card.Controls.Add(toolbar);

        BuildLayout(card, _addButton, exportPdf, exportExcel);
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_lookupsLoaded)
        {
            _vehicles = await App.Vehicles.GetAllAsync();
            _drivers = await App.Drivers.GetAllAsync();
            _lookupsLoaded = true;
        }

        var from = _from.Inner.Checked ? _from.Inner.Value.Date : (DateTime?)null;
        var to = _to.Inner.Checked ? _to.Inner.Value.Date : (DateTime?)null;
        var driverId = App.Session.IsDriver ? App.Session.DriverId : null;

        _items = await App.Mileage.SearchAsync(null, driverId, from, to, _search.Text);
        if (token.IsCancellationRequested) return;
        _paginator.SetData(_items);
    }

    private async Task AddAsync()
    {
        if (!_lookupsLoaded) await ReloadAsync();
        using var dialog = new MileageEditForm(App, _vehicles, _drivers,
            App.Session.IsDriver ? App.Session.DriverId : null);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Пробег внесён", "Показания одометра сохранены, пробег ТС обновлён.");
            await ReloadAsync();
        }
    }

    private void Export(bool excel)
    {
        var headers = new[] { "Дата", "Автомобиль", "Госномер", "Водитель", "Начало", "Конец", "Пробег", "Примечание", "Внёс" };
        var rows = _items.Select(m => (IReadOnlyList<object?>)new object?[]
        {
            m.Date, m.VehicleName, m.PlateNumber, m.DriverName, m.OdometerStart, m.OdometerEnd,
            m.TripMileage, m.Note, m.CreatedByName
        }).ToList();

        if (excel) ExportExcel("Учёт пробега", "Пробег", headers, rows);
        else ExportPdf("Учёт пробега", "Пробег", headers, rows);
    }
}
