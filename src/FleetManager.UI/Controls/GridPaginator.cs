using System.Drawing;
using System.Windows.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Controls;

/// <summary>Постраничный вывод типизированного списка в DataGridView.</summary>
public sealed class GridPaginator<T> where T : class
{
    private readonly DataGridView _grid;
    private readonly Panel _bar;
    private readonly Label _label;
    private readonly GradientButton _prev;
    private readonly GradientButton _next;
    private IReadOnlyList<T> _data = Array.Empty<T>();
    private int _page;
    private int _pageSize = 15;
    private bool _autoFit = true;
    private bool _suppressFit;

    /// <summary>Размер страницы. Ручное значение отключает автоподбор по высоте таблицы.</summary>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            _autoFit = false;
            _pageSize = Math.Max(1, value);
        }
    }

    public Control Bar => _bar;

    public GridPaginator(DataGridView grid)
    {
        _grid = grid;
        _grid.SizeChanged += (_, _) => FitPageSize();

        _bar = new Panel { Height = 46, Dock = DockStyle.Bottom, BackColor = Color.Transparent, Tag = "transparent" };

        _label = new Label
        {
            Dock = DockStyle.Left,
            Width = 260,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.Fonts.Small,
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Tag = "muted",
            Text = "Нет данных"
        };

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 210,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0)
        };

        _prev = new GradientButton
        {
            Text = "Назад",
            Kind = ButtonKind.Ghost,
            Width = 92,
            Height = 32,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 7, 8, 0),
            Enabled = false
        };
        _next = new GradientButton
        {
            Text = "Вперёд",
            Kind = ButtonKind.Ghost,
            Width = 92,
            Height = 32,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 7, 0, 0),
            Enabled = false
        };
        _prev.Click += (_, _) => ChangePage(-1);
        _next.Click += (_, _) => ChangePage(1);

        right.Controls.Add(_prev);
        right.Controls.Add(_next);
        _bar.Controls.Add(right);
        _bar.Controls.Add(_label);
        Ui.EnableDoubleBuffer(_bar);
    }

    public T? Current => _grid.CurrentRow?.DataBoundItem as T;

    public void SetData(IEnumerable<T> data)
    {
        _data = data.ToList();
        _page = 0;
        FitPageSize();
        Apply();
    }

    /// <summary>Подбирает размер страницы так, чтобы таблица не прокручивалась по вертикали.</summary>
    private void FitPageSize()
    {
        if (!_autoFit || _suppressFit) return;
        var rowHeight = _grid.RowTemplate.Height;
        if (rowHeight <= 0 || _grid.Width <= 0) return;

        var available = _grid.ClientSize.Height - _grid.ColumnHeadersHeight - 2;
        var rows = Math.Max(4, available / rowHeight);
        if (rows == _pageSize) return;

        _suppressFit = true;
        try
        {
            _pageSize = rows;
            _page = Math.Max(0, Math.Min(_page, (_data.Count + rows - 1) / rows - 1));
            Apply();
        }
        finally
        {
            _suppressFit = false;
        }
    }

    public void Clear()
    {
        _data = Array.Empty<T>();
        _page = 0;
        Apply();
    }

    private void ChangePage(int delta)
    {
        _page = Math.Clamp(_page + delta, 0, PageCount - 1);
        Apply();
    }

    private int PageCount => Math.Max(1, (int)Math.Ceiling(_data.Count / (double)PageSize));

    private void Apply()
    {
        var page = _data.Skip(_page * PageSize).Take(PageSize).ToList();
        _grid.DataSource = null;
        _grid.DataSource = page;
        _grid.ClearSelection();
        var from = _data.Count == 0 ? 0 : _page * PageSize + 1;
        var to = Math.Min(_data.Count, (_page + 1) * PageSize);
        _label.Text = _data.Count == 0
            ? "Нет данных"
            : $"Показано {from}–{to} из {_data.Count}  ·  стр. {_page + 1}/{PageCount}";

        _prev.Enabled = _page > 0;
        _next.Enabled = _page < PageCount - 1;
    }
}
