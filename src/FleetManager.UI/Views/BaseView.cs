using System.Drawing;
using System.Windows.Forms;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Views;

public interface IGlobalSearchView
{
    void ApplyGlobalSearch(string text);
}

/// <summary>Базовая страница: заголовок, панель действий, загрузка данных и экспорт.</summary>
public abstract class BaseView : UserControl, IGlobalSearchView
{
    protected AppServices App { get; }

    private Label? _titleLabel;
    private Label? _subtitleLabel;
    private FlowLayoutPanel? _actionsPanel;
    private UiTextField? _primarySearch;
    private Timer? _searchTimer;
    private CancellationTokenSource? _cts;
    private bool _loaded;
    private bool _disposed;

    public abstract string PageTitle { get; }
    public abstract string PageSubtitle { get; }

    protected BaseView(AppServices app)
    {
        App = app;
        Dock = DockStyle.Fill;
        BackColor = ThemeManager.Current.Background;
        Padding = new Padding(26, 14, 26, 20);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Ui.Fonts.Regular;
    }

    protected void BuildLayout(Control content, params Control[] headerActions)
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };

        _titleLabel = new Label
        {
            Text = PageTitle,
            Font = Ui.Fonts.Title,
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(2, 2),
            Size = new Size(520, 36),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };
        _subtitleLabel = new Label
        {
            Text = PageSubtitle,
            Font = Ui.Fonts.Subtitle,
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(4, 38),
            Size = new Size(520, 22),
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Tag = "muted"
        };

        _actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0, 12, 0, 0)
        };
        foreach (var action in headerActions)
        {
            action.Margin = new Padding(8, 0, 0, 0);
            _actionsPanel.Controls.Add(action);
        }

        // ВАЖНО: порядок добавления задаёт z-порядок. Панель действий добавляется первой,
        // чтобы оказаться на переднем плане и не затираться прозрачными подписями заголовка.
        header.Controls.Add(_actionsPanel);
        header.Controls.Add(_subtitleLabel);
        header.Controls.Add(_titleLabel);

        void FitHeaderText()
        {
            var actionsWidth = _actionsPanel.PreferredSize.Width;
            var available = Math.Max(180, header.ClientSize.Width - actionsWidth - 16);
            _titleLabel.Width = available;
            _subtitleLabel.Width = available;
        }

        header.SizeChanged += (_, _) => FitHeaderText();
        header.HandleCreated += (_, _) => FitHeaderText();
        FitHeaderText();

        Controls.Add(content);
        content.Dock = DockStyle.Fill;
        content.Margin = new Padding(0);
        Controls.Add(header);

        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    protected DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Font = Ui.Fonts.Regular,
            AllowUserToOrderColumns = false,
            AllowUserToResizeColumns = true
        };
        GridStyler.Style(grid);
        return grid;
    }

    protected static DataGridViewTextBoxColumn AddColumn(DataGridView grid, string header, string property,
        int width, bool fill = false, DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Name = property,
            Width = width,
            FillWeight = Math.Max(40, width),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            MinimumWidth = 24,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment },
            Tag = width
        };
        grid.Columns.Add(column);
        EnsureColumnFit(grid);
        return column;
    }

    /// <summary>
    /// Пропорционально распределяет ширину грида между колонками по их «дизайнерской» ширине.
    /// Собственный расчёт надёжнее DataGridView Fill (он сглаживает веса и обрезает текст).
    /// </summary>
    private static void EnsureColumnFit(DataGridView grid)
    {
        if (grid.Tag is "columns-auto-fit") return;
        grid.Tag = "columns-auto-fit";
        var fitting = false;
        void Fit()
        {
            if (fitting) return;
            fitting = true;
            try { FitColumns(grid); }
            finally { fitting = false; }
        }
        grid.ColumnAdded += (_, _) => Fit();
        grid.Layout += (_, _) => Fit();
        grid.SizeChanged += (_, _) => Fit();
        grid.DataBindingComplete += (_, _) => Fit();
        grid.VisibleChanged += (_, _) => Fit();
        Fit();
    }

    private static void FitColumns(DataGridView grid)
    {
        if (grid.Columns.Count == 0) return;
        var available = grid.DisplayRectangle.Width;
        if (available <= 0) available = grid.ClientSize.Width;
        if (available <= 0) return;

        var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
        if (columns.Count == 0) return;

        int Weight(DataGridViewColumn c) => c.Tag is int w && w > 0 ? w : (int)c.FillWeight;
        var totalWeight = columns.Sum(Weight);
        if (totalWeight <= 0) return;

        var remaining = available;
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var width = i == columns.Count - 1
                ? remaining
                : Math.Max(28, (int)Math.Round((double)available * Weight(column) / totalWeight));
            column.Width = Math.Max(5, width);
            remaining -= column.Width;
        }
    }

    protected UiTextField CreateSearchBox(string placeholder = "Поиск...", int width = 280)
    {
        var box = new UiTextField
        {
            Placeholder = placeholder,
            IconGlyph = "\uE721",
            Width = width,
            Height = 40,
            Margin = new Padding(0, 0, 10, 0)
        };
        _primarySearch = box;
        return box;
    }

    /// <summary>Живой поиск с задержкой 350 мс.</summary>
    protected void EnableLiveSearch(UiTextField box)
    {
        _searchTimer = new Timer { Interval = 350 };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await ReloadAsync();
        };
        box.Input.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
    }

    public void ApplyGlobalSearch(string text)
    {
        if (_primarySearch is null) return;
        _primarySearch.Text = text;
        _primarySearch.Input.Focus();
    }

    protected CardPanel CreateCard()
    {
        return new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Tag = "keep-bg"
        };
    }

    protected Panel CreateToolbar(params Control[] controls)
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0, 5, 0, 0)
        };
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 0, 10, 0);
            flow.Controls.Add(control);
        }
        toolbar.Controls.Add(flow);
        return toolbar;
    }

    protected Label CreateCaption(string text, int width = 0) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
        ForeColor = ThemeManager.Current.TextMuted,
        BackColor = Color.Transparent,
        AutoSize = width <= 0,
        Height = 18,
        Width = width,
        Tag = "muted",
        Margin = new Padding(0, 0, 0, 4)
    };

    protected void Notify(ToastKind kind, string title, string message) =>
        Ui.Notify(this, kind, title, message);

    protected void ExportExcel(string title, string baseName, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Export(title, baseName, ".xlsx", "Excel|*.xlsx", path =>
            App.Reports.ExportToExcel(path, title, headers, rows, baseName));
    }

    protected void ExportPdf(string title, string baseName, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows, string subtitle = "")
    {
        Export(title, baseName, ".pdf", "PDF|*.pdf", path =>
            App.Reports.ExportToPdf(path, title, headers, rows, subtitle));
    }

    private void Export(string title, string baseName, string extension, string filter, Action<string> exportAction)
    {
        var suggested = App.Reports.SuggestPath(baseName, extension);
        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт отчёта",
            Filter = filter,
            FileName = Path.GetFileName(suggested),
            InitialDirectory = Path.GetDirectoryName(suggested),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            exportAction(dialog.FileName);
            Notify(ToastKind.Success, "Отчёт сохранён", dialog.FileName);
        }
        catch (Exception ex)
        {
            Ui.ShowError(FindForm(), ex);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_loaded) return;
        _loaded = true;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            Cursor = Cursors.WaitCursor;
            await LoadDataAsync(token);
        }
        catch (OperationCanceledException)
        {
            // новая загрузка отменила предыдущую
        }
        catch (Exception ex)
        {
            if (!_disposed) Ui.ShowError(FindForm(), ex);
        }
        finally
        {
            if (!_disposed) Cursor = Cursors.Default;
        }
    }

    protected abstract Task LoadDataAsync(CancellationToken token);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_titleLabel is not null) _titleLabel.ForeColor = ThemeManager.Current.Text;
        if (_subtitleLabel is not null) _subtitleLabel.ForeColor = ThemeManager.Current.TextMuted;
        BackColor = ThemeManager.Current.Background;
        ThemeManager.ApplyTree(this);
        ApplyScrollbars(this);
        Invalidate(true);
    }

    /// <summary>Перекрашивает системные полосы прокрутки после смены темы.</summary>
    private static void ApplyScrollbars(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is DataGridView || (child is ScrollableControl { AutoScroll: true }))
                GridStyler.ApplyScrollbar(child);
            if (child.HasChildren) ApplyScrollbars(child);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _cts?.Cancel();
            _cts?.Dispose();
            _searchTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
