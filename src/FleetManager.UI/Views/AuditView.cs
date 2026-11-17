using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Views;

/// <summary>Журнал аудита: кто, что и когда изменил в системе.</summary>
public sealed class AuditView : BaseView
{
    public override string PageTitle => "Журнал аудита";
    public override string PageSubtitle => "История действий пользователей и изменений данных";

    private readonly UiTextField _search;
    private readonly UiComboBox _actionFilter;
    private readonly DataGridView _grid;
    private readonly GridPaginator<AuditEntry> _paginator;
    private readonly GradientButton _exportButton;
    private readonly Label _summary;

    private IReadOnlyList<AuditEntry> _items = Array.Empty<AuditEntry>();
    private bool _updatingFilter;

    public AuditView(AppServices app) : base(app)
    {
        _search = CreateSearchBox("Поиск по пользователю, таблице, значению...", 300);
        EnableLiveSearch(_search);

        _actionFilter = new UiComboBox { Width = 170, Height = 40 };
        _actionFilter.Inner.Items.Add("Все действия");
        _actionFilter.Inner.SelectedIndex = 0;
        _actionFilter.SelectionChanged += async (_, _) =>
        {
            if (!_updatingFilter) await ReloadAsync();
        };

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

        _grid = CreateGrid();
        AddColumn(_grid, "Время", nameof(AuditEntry.Timestamp), 168);
        AddColumn(_grid, "Пользователь", nameof(AuditEntry.UserName), 180);
        AddColumn(_grid, "Действие", nameof(AuditEntry.Action), 115);
        AddColumn(_grid, "Таблица", nameof(AuditEntry.TableName), 155);
        AddColumn(_grid, "Запись", nameof(AuditEntry.RecordId), 75,
            alignment: DataGridViewContentAlignment.MiddleCenter);
        AddColumn(_grid, "Было", nameof(AuditEntry.OldValue), 220);
        AddColumn(_grid, "Стало", nameof(AuditEntry.NewValue), 260, fill: true);
        _grid.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm:ss";
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowDetails(); };

        _paginator = new GridPaginator<AuditEntry>(_grid);

        _exportButton = new GradientButton
        {
            Text = "Excel", IconGlyph = "\uE9F9", Kind = ButtonKind.Ghost, Width = 92, Height = 38,
            Font = new Font("Segoe UI", 8.5f)
        };
        _exportButton.Click += (_, _) => Export();

        var refresh = new RoundIconButton { Glyph = "\uE72C", Size = new Size(36, 36) };
        refresh.Click += async (_, _) => await ReloadAsync();

        var toolbar = CreateToolbar(_search, _actionFilter, _summary, refresh);

        var card = CreateCard();
        card.Padding = new Padding(14, 10, 14, 8);
        card.Controls.Add(_grid);
        card.Controls.Add(_paginator.Bar);
        card.Controls.Add(toolbar);

        BuildLayout(card, _exportButton);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != nameof(AuditEntry.Action)) return;
        var action = e.Value?.ToString() ?? string.Empty;
        var t = ThemeManager.Current;
        e.CellStyle.ForeColor = action switch
        {
            "Delete" => t.Danger,
            "Create" or "Insert" or "Login" => t.Success,
            "Update" or "UpdateStatus" or "Upsert" => t.Warning,
            "LoginFailed" => t.Danger,
            "Logout" => t.TextMuted,
            _ => t.PrimaryLight
        };
        e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    protected override async Task LoadDataAsync(CancellationToken token)
    {
        _items = await App.Audit.GetRecentAsync(1000);
        if (token.IsCancellationRequested) return;

        var actions = _items.Select(a => a.Action).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();

        var previous = _actionFilter.Inner.SelectedItem?.ToString();
        if (_actionFilter.Inner.Items.Count == 1 ||
            _actionFilter.Inner.Items.Count - 1 != actions.Count)
        {
            _updatingFilter = true;
            try
            {
                _actionFilter.Inner.Items.Clear();
                _actionFilter.Inner.Items.Add("Все действия");
                foreach (var action in actions) _actionFilter.Inner.Items.Add(action);
                _actionFilter.Inner.SelectedItem = previous is not null && _actionFilter.Inner.Items.Contains(previous)
                    ? previous
                    : "Все действия";
            }
            finally
            {
                _updatingFilter = false;
            }
        }

        var search = _search.Text.Trim();
        IEnumerable<AuditEntry> query = _items;
        if (search.Length > 0)
            query = query.Where(a =>
                a.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Action.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.TableName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (a.RecordId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.OldValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.NewValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));

        if (_actionFilter.Inner.SelectedIndex > 0)
        {
            var action = _actionFilter.Inner.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => string.Equals(a.Action, action, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();
        _paginator.SetData(filtered);
        _summary.Text = $"Записей: {filtered.Count} из {_items.Count}";
        _summary.ForeColor = ThemeManager.Current.TextMuted;
    }

    private void ShowDetails()
    {
        if (_paginator.Current is not { } entry) return;
        var message =
            $"Пользователь: {entry.UserName}\n" +
            $"Действие: {entry.Action}\n" +
            $"Таблица: {entry.TableName}\n" +
            $"Запись: {entry.RecordId ?? "—"}\n" +
            $"Время: {entry.Timestamp:dd.MM.yyyy HH:mm:ss}\n\n" +
            $"Было:\n{entry.OldValue ?? "—"}\n\n" +
            $"Стало:\n{entry.NewValue ?? "—"}";
        Ui.ShowInfo(FindForm(), message);
    }

    private void Export()
    {
        var headers = new[] { "Время", "Пользователь", "Действие", "Таблица", "Запись", "Было", "Стало" };
        var rows = _items.Select(a => (IReadOnlyList<object?>)new object?[]
        {
            a.Timestamp, a.UserName, a.Action, a.TableName, a.RecordId, a.OldValue, a.NewValue
        }).ToList();

        ExportExcel("Журнал аудита", "Аудит", headers, rows);
    }
}

