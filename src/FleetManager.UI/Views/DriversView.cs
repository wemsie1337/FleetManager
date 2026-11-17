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

/// <summary>Водители: карточки сотрудников, права и привязка к учётным записям.</summary>
public sealed class DriversView : BaseView
{
    public override string PageTitle => "Водители";
    public override string PageSubtitle => "Сотрудники, водительские удостоверения и статус";

    private readonly UiTextField _search;
    private readonly UiComboBox _statusFilter;
    private readonly DataGridView _grid;
    private readonly GridPaginator<Driver> _paginator;
    private readonly GradientButton _addButton;
    private readonly GradientButton _editButton;

    private IReadOnlyList<Driver> _items = Array.Empty<Driver>();

    public DriversView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по ФИО, правам, телефону...", 280);
        EnableLiveSearch(_search);

        _statusFilter = new UiComboBox { Width = 170, Height = 40 };
        _statusFilter.Inner.Items.AddRange(new object[] { "Все водители", "Работает", "Не работает" });
        _statusFilter.Inner.SelectedIndex = 0;
        _statusFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _grid = CreateGrid();
        AddColumn(_grid, "ФИО", nameof(Driver.FullName), 220);
        AddColumn(_grid, "Удостоверение", nameof(Driver.LicenseNumber), 150);
        AddColumn(_grid, "Категория", nameof(Driver.Category), 90,
            alignment: DataGridViewContentAlignment.MiddleCenter);
        AddColumn(_grid, "Телефон", nameof(Driver.Phone), 145);
        AddColumn(_grid, "Дата приёма", nameof(Driver.HireDate), 110);
        AddColumn(_grid, "Работает", nameof(Driver.IsActive), 95,
            alignment: DataGridViewContentAlignment.MiddleCenter);
        AddColumn(_grid, "Логин", nameof(Driver.Login), 150, fill: true);
        _grid.Columns[4].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.SelectionChanged += (_, _) => UpdateActions();

        _paginator = new GridPaginator<Driver>(_grid);

        _addButton = new GradientButton
        {
            Text = "Новый водитель",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 180,
            Height = 40
        };
        _addButton.Click += async (_, _) => await AddAsync();

        _editButton = new GradientButton
        {
            Text = "Изменить",
            Kind = ButtonKind.Ghost,
            Width = 110,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _editButton.Click += (_, _) => EditSelected();

        var export = new GradientButton
        {
            Text = "Excel", IconGlyph = "\uE9F9", Kind = ButtonKind.Ghost, Width = 92, Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        export.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _statusFilter, _editButton, refresh);

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
        if (_grid.Columns[e.ColumnIndex].Name == nameof(Driver.IsActive) && e.Value is bool active)
        {
            e.Value = active ? "Да" : "Нет";
            e.CellStyle.ForeColor = active ? ThemeManager.Current.Success : ThemeManager.Current.TextMuted;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            e.FormattingApplied = true;
        }
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        _items = await App.Drivers.GetAllAsync();
        if (token.IsCancellationRequested) return;

        var search = _search.Text.Trim();
        IEnumerable<Driver> query = _items;
        if (search.Length > 0)
            query = query.Where(d =>
                d.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.LicenseNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.Phone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (d.Login?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));

        if (_statusFilter.Inner.SelectedIndex == 1) query = query.Where(d => d.IsActive);
        else if (_statusFilter.Inner.SelectedIndex == 2) query = query.Where(d => !d.IsActive);

        _paginator.SetData(query.ToList());
        UpdateActions();
    }

    private void UpdateActions() => _editButton.Enabled = _paginator.Current is not null;

    private async Task AddAsync()
    {
        try
        {
            var users = await App.Drivers.GetAvailableUsersAsync(null);
            if (users.Count == 0)
            {
                Ui.ShowInfo(FindForm(),
                    "Нет свободных учётных записей с ролью «Водитель».\n" +
                    "Сначала создайте пользователя в разделе «Пользователи».");
                return;
            }

            using var dialog = new DriverEditForm(App, null, users);
            if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
            {
                Notify(ToastKind.Success, "Водитель добавлен", "Карточка сотрудника создана.");
                await ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private async void EditSelected()
    {
        if (_paginator.Current is not { } driver) return;
        try
        {
            var users = await App.Drivers.GetAvailableUsersAsync(driver.Id);
            if (driver.UserId is not null && users.All(u => u.Id != driver.UserId))
            {
                var current = (await App.Users.GetAllAsync()).FirstOrDefault(u => u.Id == driver.UserId);
                if (current is not null) users = users.Append(current).ToList();
            }

            using var dialog = new DriverEditForm(App, driver, users);
            if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
            {
                Notify(ToastKind.Success, "Данные обновлены", driver.FullName);
                await ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "ФИО", "Удостоверение", "Категория", "Телефон", "Дата приёма", "Работает", "Логин" };
        var rows = _items.Select(d => (IReadOnlyList<object?>)new object?[]
        {
            d.FullName, d.LicenseNumber, d.Category, d.Phone, d.HireDate, d.IsActive ? "Да" : "Нет", d.Login
        }).ToList();

        ExportExcel("Водители автопарка", "Водители", headers, rows);
    }
}

