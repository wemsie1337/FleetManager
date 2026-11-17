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

/// <summary>Пользователи и роли. Доступно только администратору.</summary>
public sealed class UsersView : BaseView
{
    public override string PageTitle => "Пользователи";
    public override string PageSubtitle => "Учётные записи, роли и блокировка доступа";

    private readonly UiTextField _search;
    private readonly UiComboBox _roleFilter;
    private readonly DataGridView _grid;
    private readonly GridPaginator<User> _paginator;
    private readonly GradientButton _addButton;
    private readonly GradientButton _editButton;
    private readonly GradientButton _resetButton;
    private readonly GradientButton _toggleButton;

    private IReadOnlyList<User> _items = Array.Empty<User>();
    private IReadOnlyList<Role> _roles = Array.Empty<Role>();
    private bool _rolesLoaded;

    public UsersView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по логину, ФИО, роли...", 270);
        EnableLiveSearch(_search);

        _roleFilter = new UiComboBox { Width = 165, Height = 40 };
        _roleFilter.Inner.Items.Add("Все роли");
        _roleFilter.Inner.SelectedIndex = 0;
        _roleFilter.SelectionChanged += async (_, _) => await ReloadAsync();

        _grid = CreateGrid();
        AddColumn(_grid, "Логин", nameof(User.Login), 130);
        AddColumn(_grid, "ФИО", nameof(User.FullName), 230);
        AddColumn(_grid, "Роль", nameof(User.RoleTitle), 130);
        AddColumn(_grid, "Активен", nameof(User.IsActive), 90,
            alignment: DataGridViewContentAlignment.MiddleCenter);
        AddColumn(_grid, "Создан", nameof(User.CreatedAt), 130);
        AddColumn(_grid, "Последний вход", nameof(User.LastLoginAt), 150, fill: true);
        _grid.Columns[4].DefaultCellStyle.Format = "dd.MM.yyyy";
        _grid.Columns[5].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.SelectionChanged += (_, _) => UpdateActions();

        _paginator = new GridPaginator<User>(_grid);

        _addButton = new GradientButton
        {
            Text = "Новый пользователь",
            IconGlyph = "\uE710",
            Kind = ButtonKind.Accent,
            Width = 200,
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

        _resetButton = new GradientButton
        {
            Text = "Сбросить пароль",
            Kind = ButtonKind.Ghost,
            Width = 155,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _resetButton.Click += async (_, _) => await ResetPasswordAsync();

        _toggleButton = new GradientButton
        {
            Text = "Отключить",
            Kind = ButtonKind.Danger,
            Width = 125,
            Height = 38,
            Font = new Font("Segoe UI", 8.5f),
            Enabled = false
        };
        _toggleButton.Click += async (_, _) => await ToggleActiveAsync();

        var export = new GradientButton
        {
            Text = "Excel", IconGlyph = "\uE9F9", Kind = ButtonKind.Ghost, Width = 92, Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        export.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _roleFilter, _editButton, _resetButton, _toggleButton, refresh);

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
        if (_grid.Columns[e.ColumnIndex].Name == nameof(User.IsActive) && e.Value is bool active)
        {
            e.Value = active ? "Да" : "Нет";
            e.CellStyle.ForeColor = active ? ThemeManager.Current.Success : ThemeManager.Current.Danger;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            e.FormattingApplied = true;
        }
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        if (!_rolesLoaded)
        {
            _roles = await App.Users.GetRolesAsync();
            _roleFilter.Inner.Items.Clear();
            _roleFilter.Inner.Items.Add("Все роли");
            foreach (var role in _roles) _roleFilter.Inner.Items.Add(role.RoleName);
            _roleFilter.Inner.SelectedIndex = 0;
            _rolesLoaded = true;
        }

        _items = await App.Users.GetAllAsync();
        if (token.IsCancellationRequested) return;

        var search = _search.Text.Trim();
        IEnumerable<User> query = _items;
        if (search.Length > 0)
            query = query.Where(u =>
                u.Login.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.RoleTitle.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (_roleFilter.Inner.SelectedIndex > 0)
        {
            var role = _roles[_roleFilter.Inner.SelectedIndex - 1];
            query = query.Where(u => u.RoleId == role.Id);
        }

        _paginator.SetData(query.ToList());
        UpdateActions();
    }

    private void UpdateActions()
    {
        var user = _paginator.Current;
        var hasSelection = user is not null;
        _editButton.Enabled = hasSelection;
        _resetButton.Enabled = hasSelection;
        _toggleButton.Enabled = hasSelection;
        if (user is not null)
        {
            _toggleButton.Text = user.IsActive ? "Отключить" : "Включить";
            _toggleButton.Kind = user.IsActive ? ButtonKind.Danger : ButtonKind.Success;
        }
    }

    private async Task AddAsync()
    {
        if (!_rolesLoaded) await ReloadAsync();
        using var dialog = new UserEditForm(App, null, _roles);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Пользователь создан", "Учётная запись готова к работе.");
            await ReloadAsync();
        }
    }

    private async void EditSelected()
    {
        if (_paginator.Current is not { } user) return;
        if (!_rolesLoaded) await ReloadAsync();
        using var dialog = new UserEditForm(App, user, _roles);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Пользователь обновлён", user.Login);
            await ReloadAsync();
        }
    }

    private async Task ResetPasswordAsync()
    {
        if (_paginator.Current is not { } user) return;
        using var dialog = new PasswordResetForm(App, user);
        if (ModalHost.Show(FindForm(), dialog) == DialogResult.OK)
        {
            Notify(ToastKind.Success, "Пароль сброшен", $"Пользователь {user.Login} должен войти с новым паролем.");
            await ReloadAsync();
        }
    }

    private async Task ToggleActiveAsync()
    {
        if (_paginator.Current is not { } user) return;
        var activate = !user.IsActive;
        if (!Ui.Confirm(FindForm(), activate ? "Включить учётную запись?" : "Отключить учётную запись?",
                activate
                    ? $"{user.FullName} снова сможет входить в систему."
                    : $"{user.FullName} потеряет доступ к приложению. История действий сохранится."))
            return;

        try
        {
            await App.Users.SetActiveAsync(user.Id, activate);
            Notify(ToastKind.Success, activate ? "Учётная запись включена" : "Учётная запись отключена", user.Login);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
    }

    private void Export()
    {
        var headers = new[] { "Логин", "ФИО", "Роль", "Активен", "Создан", "Последний вход" };
        var rows = _items.Select(u => (IReadOnlyList<object?>)new object?[]
        {
            u.Login, u.FullName, u.RoleTitle, u.IsActive ? "Да" : "Нет", u.CreatedAt, u.LastLoginAt
        }).ToList();

        ExportExcel("Пользователи системы", "Пользователи", headers, rows);
    }
}

