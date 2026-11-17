using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Dialogs;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;
using FleetManager.UI.Views;

namespace FleetManager.UI.Forms;

/// <summary>
/// Главное окно: анимированный сайдбар, верхняя панель с глобальным поиском,
/// уведомлениями и профилем, кэшированные страницы с плавным появлением.
/// </summary>
public sealed class MainForm : Form, IToastHost
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
    private const int WM_NCHITTEST = 0x84;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private const int SidebarExpandedWidth = 252;
    private const int SidebarCompactWidth = 78;

    private sealed record NavItem(string Key, string Title, string Glyph, UserRole[] Roles);

    private static readonly NavItem[] Navigation =
    {
        new("dashboard", "Дашборд", "\uE80F", new[] { UserRole.Admin, UserRole.Dispatcher, UserRole.Driver }),
        new("vehicles", "Транспорт", "\uE804", new[] { UserRole.Admin, UserRole.Dispatcher }),
        new("drivers", "Водители", "\uE77B", new[] { UserRole.Admin, UserRole.Dispatcher }),
        new("routes", "Маршруты", "\uE707", new[] { UserRole.Admin, UserRole.Dispatcher, UserRole.Driver }),
        new("mileage", "Пробег", "\uE9D2", new[] { UserRole.Admin, UserRole.Dispatcher, UserRole.Driver }),
        new("maintenance", "ТО и ремонт", "\uE90F", new[] { UserRole.Admin, UserRole.Dispatcher, UserRole.Driver }),
        new("fuel", "Топливо", "\uE945", new[] { UserRole.Admin, UserRole.Dispatcher }),
        new("fines", "Штрафы и ДТП", "\uE7BA", new[] { UserRole.Admin, UserRole.Dispatcher }),
        new("users", "Пользователи", "\uE716", new[] { UserRole.Admin }),
        new("audit", "Аудит", "\uE81C", new[] { UserRole.Admin })
    };

    private readonly AppServices _app;
    private readonly SidebarSurface _sidebar;
    private readonly AppLogo _logo;
    private readonly FlowLayoutPanel _navPanel;
    private readonly Panel _topBar;
    private readonly Panel _content;
    private Label _crumb = null!;
    private UiTextField _globalSearch = null!;
    private RoundIconButton _noticeButton = null!;
    private RoundIconButton _themeButton;
    private RoundIconButton _minButton = null!;
    private RoundIconButton _maxButton = null!;
    private RoundIconButton _closeButton = null!;
    private RoundIconButton _collapseButton;
    private ProfileButton _profile = null!;
    private readonly ToastHost _toasts;
    private readonly ToolTip _tips = new();
    private readonly Dictionary<string, SidebarButton> _navButtons = new();
    private readonly Dictionary<string, BaseView> _views = new();

    private IReadOnlyList<MaintenanceAlert> _alerts = Array.Empty<MaintenanceAlert>();
    private CardPanel? _flyout;
    private FlowLayoutPanel? _flyoutList;
    private Label? _flyoutCount;
    private BaseView? _currentView;
    private string _currentKey = string.Empty;
    private bool _expanded = true;
    private bool _animatingSidebar;

    public bool LogoutRequested { get; private set; }

    public MainForm(AppServices app)
    {
        _app = app;

        Text = $"{app.Settings.AppTitle} — {app.Settings.CompanyName}";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;
        MinimumSize = new Size(1180, 680);
        Font = Ui.Fonts.Regular;
        BackColor = ThemeManager.Current.Background;
        ForeColor = ThemeManager.Current.Text;
        Opacity = 0;

        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        ClientSize = new Size(
            Math.Max(1180, Math.Min(1440, work.Width - 60)),
            Math.Max(680, Math.Min(900, work.Height - 60)));

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Current.Background,
            Tag = "bg",
            Padding = new Padding(0)
        };

        _topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = ThemeManager.Current.Surface,
            Tag = "surface"
        };
        _topBar.Paint += (_, e) =>
        {
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
        };
        _topBar.MouseDown += DragWindow;
        _topBar.MouseDoubleClick += (_, _) => ToggleMaximize();

        _sidebar = new SidebarSurface { Dock = DockStyle.Left, Width = SidebarExpandedWidth, Tag = "keep-bg" };

        _logo = new AppLogo { Dock = DockStyle.Top, Height = 84 };
        _logo.MouseDown += DragWindow;
        _logo.MouseDoubleClick += (_, _) => ToggleMaximize();

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 82, BackColor = Color.Transparent, Tag = "transparent" };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(35, 255, 255, 255));
            e.Graphics.DrawLine(pen, 16, 0, footer.Width - 16, 0);
        };

        _themeButton = new RoundIconButton
        {
            Glyph = ThemeManager.IsDark ? "\uE706" : "\uE708",
            GlyphColor = Color.FromArgb(200, 216, 238),
            Size = new Size(36, 36),
            Location = new Point(16, 12)
        };
        _themeButton.Click += (_, _) => ToggleTheme();
        _tips.SetToolTip(_themeButton, "Светлая / тёмная тема");

        _collapseButton = new RoundIconButton
        {
            Glyph = "\uE76B",
            GlyphColor = Color.FromArgb(200, 216, 238),
            Size = new Size(36, 36),
            Location = new Point(62, 12)
        };
        _collapseButton.Click += (_, _) => ToggleSidebar();
        _tips.SetToolTip(_collapseButton, "Свернуть / развернуть меню");

        var version = new Label
        {
            Text = "v1.0 · .NET 8 · SQL Server",
            Font = new Font("Segoe UI", 7f),
            ForeColor = Color.FromArgb(130, 158, 195),
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(18, 54),
            Size = new Size(210, 18)
        };

        footer.Controls.Add(version);
        footer.Controls.Add(_collapseButton);
        footer.Controls.Add(_themeButton);

        _navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(16, 8, 12, 8)
        };
        GridStyler.ApplyScrollbar(_navPanel);
        _navPanel.Resize += (_, _) =>
        {
            foreach (var button in _navButtons.Values)
                button.Width = Math.Max(46, _navPanel.ClientSize.Width - 8);
        };

        _sidebar.Controls.Add(_navPanel);
        _sidebar.Controls.Add(footer);
        _sidebar.Controls.Add(_logo);

        BuildTopBar();

        _toasts = new ToastHost { Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _toasts.Location = new Point(ClientSize.Width - _toasts.Width - 18, _topBar.Height + 10);

        // ВАЖНО: порядок добавления = z-порядок (первый — самый верхний).
        // Тосты добавляются первыми, иначе контент перекрывает их при отрисовке.
        Controls.Add(_toasts);
        Controls.Add(_content);
        Controls.Add(_topBar);
        Controls.Add(_sidebar);

        BuildNavigation();

        ThemeManager.ThemeChanged += OnThemeChanged;
        Shown += MainForm_Shown;
        FormClosed += MainForm_FormClosed;

        var defaultKey = Navigation.FirstOrDefault(n => n.Key == "dashboard")?.Key ?? Navigation[0].Key;
        _ = NavigateAsync(defaultKey, force: true);
    }

    // ---------------- верхняя панель ----------------

    private void BuildTopBar()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(22, 0, 10, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var crumbPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Tag = "transparent" };
        crumbPanel.MouseDown += DragWindow;
        crumbPanel.MouseDoubleClick += (_, _) => ToggleMaximize();

        var overline = new Label
        {
            Text = "АВТОПАРК · РАБОЧЕЕ ПРОСТРАНСТВО",
            Font = new Font("Segoe UI", 6.8f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(0, 12),
            Size = new Size(225, 16),
            Tag = "muted"
        };
        overline.MouseDown += DragWindow;
        overline.MouseDoubleClick += (_, _) => ToggleMaximize();

        _crumb = new Label
        {
            Text = "Дашборд",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(0, 28),
            Size = new Size(225, 26)
        };
        _crumb.MouseDown += DragWindow;
        _crumb.MouseDoubleClick += (_, _) => ToggleMaximize();

        crumbPanel.Controls.Add(_crumb);
        crumbPanel.Controls.Add(overline);

        var searchHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Tag = "transparent" };
        _globalSearch = new UiTextField
        {
            Placeholder = "Поиск по разделу — нажмите Enter",
            IconGlyph = "\uE721",
            Width = 330,
            Height = 40,
            Location = new Point(8, 13),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };
        _globalSearch.Input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _currentView?.ApplyGlobalSearch(_globalSearch.Text);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _globalSearch.Text = string.Empty;
                _currentView?.ApplyGlobalSearch(string.Empty);
            }
        };
        searchHost.Controls.Add(_globalSearch);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0, 11, 0, 0)
        };

        _closeButton = new RoundIconButton
        {
            Glyph = "\uE8BB",
            Size = new Size(38, 38),
            GlyphColor = ThemeManager.Current.Danger,
            Margin = new Padding(2, 0, 0, 0)
        };
        _closeButton.Click += (_, _) => Close();
        _tips.SetToolTip(_closeButton, "Закрыть");

        _maxButton = new RoundIconButton
        {
            Glyph = "\uE922",
            Size = new Size(38, 38),
            GlyphColor = ThemeManager.Current.TextMuted
        };
        _maxButton.Click += (_, _) => ToggleMaximize();
        _tips.SetToolTip(_maxButton, "Развернуть / восстановить");

        _minButton = new RoundIconButton
        {
            Glyph = "\uE921",
            Size = new Size(38, 38),
            GlyphColor = ThemeManager.Current.TextMuted
        };
        _minButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _tips.SetToolTip(_minButton, "Свернуть");

        _profile = new ProfileButton
        {
            FullName = _app.Session.FullName,
            RoleText = _app.Session.CurrentUser?.RoleTitle ?? string.Empty,
            ShowChevron = true,
            Size = new Size(200, 44),
            Margin = new Padding(10, 0, 8, 0)
        };
        _profile.Click += (_, _) => ShowProfileMenu();

        _noticeButton = new RoundIconButton
        {
            Glyph = "\uEA8F",
            Size = new Size(38, 38),
            GlyphColor = ThemeManager.Current.TextMuted,
            Margin = new Padding(2, 2, 2, 0)
        };
        _noticeButton.Click += (_, _) => ToggleNotifications();
        _tips.SetToolTip(_noticeButton, "Уведомления о ТО");

        right.Controls.Add(_closeButton);
        right.Controls.Add(_maxButton);
        right.Controls.Add(_minButton);
        right.Controls.Add(_profile);
        right.Controls.Add(_noticeButton);

        layout.Controls.Add(crumbPanel, 0, 0);
        layout.Controls.Add(searchHost, 1, 0);
        layout.Controls.Add(right, 2, 0);
        _topBar.Controls.Add(layout);
    }

    private void BuildNavigation()
    {
        var role = _app.Session.Role ?? UserRole.Driver;
        foreach (var item in Navigation.Where(n => n.Roles.Contains(role)))
        {
            var button = new SidebarButton
            {
                Text = item.Title,
                Glyph = item.Glyph,
                Width = SidebarExpandedWidth - 30,
                Height = 46,
                Margin = new Padding(0, 3, 0, 3)
            };
            button.Click += async (_, _) => await NavigateAsync(item.Key);
            _tips.SetToolTip(button, item.Title);
            _navButtons[item.Key] = button;
            _navPanel.Controls.Add(button);
        }
    }

    // ---------------- навигация ----------------

    private async Task NavigateAsync(string key, bool force = false)
    {
        if (!_navButtons.TryGetValue(key, out var navButton)) return;
        if (!force && key == _currentKey)
        {
            if (_currentView is not null) await _currentView.ReloadAsync();
            return;
        }

        HideFlyout();
        foreach (var pair in _navButtons) pair.Value.Active = pair.Key == key;

        _currentKey = key;
        var createdNow = !_views.TryGetValue(key, out var view);
        if (view is null)
        {
            view = CreateView(key);
            _views[key] = view;
            if (view is DashboardView dashboard)
                dashboard.OpenMaintenanceRequested += (_, _) => _ = NavigateAsync("maintenance");
        }

        if (!ReferenceEquals(_currentView, view))
        {
            _content.SuspendLayout();
            if (_currentView is not null) _content.Controls.Remove(_currentView);
            view.Dock = DockStyle.Fill;
            _content.Controls.Add(view);
            _content.ResumeLayout();
            _currentView = view;
            PlayFade();
        }

        _crumb.Text = Navigation.First(n => n.Key == key).Title;
        _globalSearch.Text = string.Empty;

        if (!createdNow)
            await view.ReloadAsync();
    }

    private BaseView CreateView(string key) => key switch
    {
        "dashboard" => new DashboardView(_app),
        "vehicles" => new VehiclesView(_app),
        "drivers" => new DriversView(_app),
        "routes" => new RoutesView(_app),
        "mileage" => new MileageView(_app),
        "maintenance" => new MaintenanceView(_app),
        "fuel" => new FuelView(_app),
        "fines" => new FinesView(_app),
        "users" => new UsersView(_app),
        "audit" => new AuditView(_app),
        _ => new DashboardView(_app)
    };

    private void PlayFade()
    {
        var overlay = new FadeLayer { Dock = DockStyle.Fill };
        _content.Controls.Add(overlay);
        overlay.BringToFront();
    }

    // ---------------- уведомления ----------------

    private void ToggleNotifications()
    {
        if (_flyout is { Visible: true })
        {
            HideFlyout();
            return;
        }

        EnsureFlyout();
        RebuildFlyoutList();
        if (_flyout is null) return;
        LayoutFlyout();
        _flyout.Visible = true;
        _flyout.BringToFront();
    }

    private void EnsureFlyout()
    {
        if (_flyout is not null) return;

        _flyout = new CardPanel
        {
            Size = new Size(400, 440),
            Radius = 16,
            Padding = new Padding(14),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent, Tag = "transparent" };
        var title = new Label
        {
            Text = "Уведомления ТО",
            Dock = DockStyle.Left,
            Width = 180,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _flyoutCount = new Label
        {
            Text = "0",
            Dock = DockStyle.Left,
            Width = 40,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = ThemeManager.Current.Danger,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0)
        };
        var close = new RoundIconButton { Glyph = "\uE711", Dock = DockStyle.Right, Size = new Size(32, 32) };
        close.Click += (_, _) => HideFlyout();

        header.Controls.Add(close);
        header.Controls.Add(_flyoutCount);
        header.Controls.Add(title);

        _flyoutList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0, 6, 0, 0)
        };
        _flyoutList.Resize += (_, _) => ResizeFlyoutRows();
        GridStyler.ApplyScrollbar(_flyoutList);

        _flyout.Controls.Add(_flyoutList);
        _flyout.Controls.Add(header);
        Controls.Add(_flyout);
        _flyout.BringToFront();
    }

    private void RebuildFlyoutList()
    {
        if (_flyoutList is null) return;

        _flyoutList.SuspendLayout();
        foreach (Control control in _flyoutList.Controls) control.Dispose();
        _flyoutList.Controls.Clear();

        if (_flyoutCount is not null)
        {
            _flyoutCount.Text = _alerts.Count.ToString();
            _flyoutCount.BackColor = _alerts.Count > 0
                ? ThemeManager.Current.Danger
                : ThemeManager.Current.Success;
        }

        if (_alerts.Count == 0)
        {
            _flyoutList.Controls.Add(new Label
            {
                Text = "✓  Активных уведомлений нет.\nВсе регламенты ТО соблюдаются.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ThemeManager.Current.Success,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(330, 70),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 14, 0, 0)
            });
        }
        else
        {
            foreach (var alert in _alerts.Take(20))
            {
                var row = new AlertRow
                {
                    Alert = alert,
                    Width = Math.Max(220, _flyoutList.ClientSize.Width - 20)
                };
                row.Click += (_, _) =>
                {
                    HideFlyout();
                    _ = NavigateAsync("maintenance");
                };
                _flyoutList.Controls.Add(row);
            }
        }

        _flyoutList.ResumeLayout();
    }

    private void ResizeFlyoutRows()
    {
        if (_flyoutList is null) return;
        foreach (Control control in _flyoutList.Controls)
            if (control is AlertRow row)
                row.Width = Math.Max(220, _flyoutList.ClientSize.Width - 20);
    }

    private void HideFlyout()
    {
        if (_flyout is not null) _flyout.Visible = false;
    }

    private void LayoutFlyout()
    {
        if (_flyout is null) return;
        _flyout.Location = new Point(
            Math.Max(10, ClientSize.Width - _flyout.Width - 26),
            _topBar.Bottom + 8);
    }

    // ---------------- монитор ТО ----------------

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        _app.Monitor.AlertsUpdated += Monitor_AlertsUpdated;
        _app.Monitor.MonitorError += Monitor_MonitorError;

        ApplyAlerts(_app.Monitor.LastAlerts, Array.Empty<MaintenanceAlert>());

        if (!_app.Monitor.IsRunning) _app.Monitor.Start();

        try
        {
            await _app.Monitor.CheckNowAsync();
        }
        catch (Exception ex)
        {
            ShowToast(ToastKind.Warning, "Мониторинг ТО", "Не удалось проверить сроки ТО: " +
                Services.Infrastructure.ErrorTranslator.ToFriendlyMessage(ex));
        }

        for (var i = 0; i <= 10; i++)
        {
            if (IsDisposed) return;
            Opacity = i / 10.0;
            await Task.Delay(16);
        }
        Opacity = 1;
    }

    private void Monitor_AlertsUpdated(object? sender, MaintenanceAlertsEventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() => ApplyAlerts(e.Alerts, e.NewAlerts)));
    }

    private void Monitor_MonitorError(object? sender, string message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() =>
            ShowToast(ToastKind.Warning, "Мониторинг ТО", message)));
    }

    private void ApplyAlerts(IReadOnlyList<MaintenanceAlert> alerts,
        IReadOnlyList<MaintenanceAlert> newAlerts)
    {
        _alerts = alerts;
        _noticeButton.BadgeCount = alerts.Count;
        if (_navButtons.TryGetValue("maintenance", out var button))
            button.BadgeCount = alerts.Count;

        if (_flyout is { Visible: true }) RebuildFlyoutList();

        if (newAlerts.Count == 0 || !_app.Session.CanManageFleet) return;

        var first = newAlerts.FirstOrDefault(a => a.Severity == "Overdue") ?? newAlerts[0];
        var text = first.Severity == "Overdue"
            ? $"{first.PlateNumber}: просрочено ТО «{first.TypeName}»"
            : $"{first.PlateNumber}: приближается ТО «{first.TypeName}»";
        if (newAlerts.Count > 1) text += $" · ещё {newAlerts.Count - 1}";

        ShowToast(ToastKind.Warning, "Контроль технического обслуживания", text);
        if (_app.Settings.EnableSound)
        {
            try { SystemSounds.Exclamation.Play(); }
            catch { /* звук не критичен */ }
        }
    }

    // ---------------- профиль / выход ----------------

    private void ShowProfileMenu()
    {
        var t = ThemeManager.Current;
        var menu = new ContextMenuStrip
        {
            BackColor = t.Surface,
            ForeColor = t.Text,
            Font = Ui.Fonts.Regular,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new ToolStripProfessionalRenderer(new MenuColors())
        };

        var header = new ToolStripMenuItem($"{_app.Session.FullName} · {_app.Session.CurrentUser?.RoleTitle}")
        {
            Enabled = false,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var changePassword = new ToolStripMenuItem("Сменить пароль");
        changePassword.Click += (_, _) => OpenChangePassword();
        menu.Items.Add(changePassword);

        var theme = new ToolStripMenuItem(ThemeManager.IsDark ? "Светлая тема" : "Тёмная тема");
        theme.Click += (_, _) => ToggleTheme();
        menu.Items.Add(theme);

        menu.Items.Add(new ToolStripSeparator());
        var logout = new ToolStripMenuItem("Выйти из системы");
        logout.Click += async (_, _) => await LogoutAsync();
        menu.Items.Add(logout);

        menu.Show(_profile, new Point(_profile.Width - menu.PreferredSize.Width, _profile.Height + 4));
    }

    private void OpenChangePassword()
    {
        using var dialog = new ChangePasswordForm(_app);
        if (ModalHost.Show(this, dialog) == DialogResult.OK)
            ShowToast(ToastKind.Success, "Пароль изменён", "Используйте новый пароль при следующем входе.");
    }

    private async Task LogoutAsync()
    {
        if (!Ui.Confirm(this, "Выйти из системы?",
                $"{_app.Session.FullName}, текущий сеанс будет завершён."))
            return;

        LogoutRequested = true;
        await _app.Auth.LogoutAsync();
        Close();
    }

    // ---------------- тема / окно ----------------

    private void ToggleTheme()
    {
        ThemeManager.Toggle();
        UpdateThemeArtifacts();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        BackColor = ThemeManager.Current.Background;
        _topBar.BackColor = ThemeManager.Current.Surface;
        _content.BackColor = ThemeManager.Current.Background;
        _crumb.ForeColor = ThemeManager.Current.Text;
        _noticeButton.GlyphColor = ThemeManager.Current.TextMuted;
        _minButton.GlyphColor = ThemeManager.Current.TextMuted;
        _maxButton.GlyphColor = ThemeManager.Current.TextMuted;
        _closeButton.GlyphColor = ThemeManager.Current.Danger;
        _sidebar.Invalidate();
        _profile.Invalidate();
        if (_flyout is not null)
        {
            _flyout.Invalidate(true);
            RebuildFlyoutList();
        }
        ThemeManager.ApplyTree(this);
        GridStyler.ApplyScrollbar(_navPanel);
        if (_flyoutList is not null) GridStyler.ApplyScrollbar(_flyoutList);
        Invalidate(true);
    }

    private void UpdateThemeArtifacts()
    {
        _themeButton.Glyph = ThemeManager.IsDark ? "\uE706" : "\uE708";
        _themeButton.Invalidate();
        _profile.Invalidate();
        _sidebar.Invalidate();
    }

    private void ToggleSidebar()
    {
        if (_animatingSidebar) return;
        _animatingSidebar = true;
        _expanded = !_expanded;

        _logo.Compact = !_expanded;
        foreach (var button in _navButtons.Values) button.Compact = !_expanded;
        _themeButton.Visible = _expanded;
        _collapseButton.Location = _expanded ? new Point(62, 12) : new Point(21, 12);
        _collapseButton.Glyph = _expanded ? "\uE76B" : "\uE76C";

        var target = _expanded ? SidebarExpandedWidth : SidebarCompactWidth;
        var timer = new Timer { Interval = 12 };
        timer.Tick += (_, _) =>
        {
            if (IsDisposed)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }

            var step = _expanded ? 18 : -18;
            var next = _sidebar.Width + step;
            if ((_expanded && next >= target) || (!_expanded && next <= target))
            {
                _sidebar.Width = target;
                timer.Stop();
                timer.Dispose();
                _animatingSidebar = false;
            }
            else
            {
                _sidebar.Width = next;
            }

            foreach (var button in _navButtons.Values)
                button.Width = Math.Max(46, _sidebar.Width - 30);
        };
        timer.Start();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        _maxButton.Glyph = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
        PerformLayout();
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
    }

    public void ShowToast(ToastKind kind, string title, string message) =>
        _toasts.ShowToast(kind, title, message);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Normal)
        {
            using var path = Drawing.RoundRect(new Rectangle(0, 0, Width, Height), 18);
            var old = Region;
            Region = new Region(path);
            old?.Dispose();
        }
        else
        {
            var old = Region;
            Region = null;
            old?.Dispose();
        }

        if (_maxButton is not null)
            _maxButton.Glyph = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
        if (_toasts is not null && _topBar is not null)
            _toasts.Location = new Point(ClientSize.Width - _toasts.Width - 18, _topBar.Height + 10);
        LayoutFlyout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (WindowState == FormWindowState.Normal)
        {
            using var pen = new Pen(ThemeManager.Current.Border, 1.2f);
            Drawing.DrawRounded(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 18, pen);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if ((int)m.Result == 1) // HTCLIENT
            {
                var value = m.LParam.ToInt64();
                var screenPoint = new Point(
                    unchecked((short)(value & 0xFFFF)),
                    unchecked((short)((value >> 16) & 0xFFFF)));
                var point = PointToClient(screenPoint);
                const int grip = 7;
                var left = point.X <= grip;
                var right = point.X >= ClientSize.Width - grip;
                var top = point.Y <= grip;
                var bottom = point.Y >= ClientSize.Height - grip;

                m.Result = (IntPtr)(top && left ? HTTOPLEFT
                    : top && right ? HTTOPRIGHT
                    : bottom && left ? HTBOTTOMLEFT
                    : bottom && right ? HTBOTTOMRIGHT
                    : left ? HTLEFT
                    : right ? HTRIGHT
                    : top ? HTTOP
                    : bottom ? HTBOTTOM
                    : 1);
            }
            return;
        }

        base.WndProc(ref m);
    }

    private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _app.Monitor.AlertsUpdated -= Monitor_AlertsUpdated;
        _app.Monitor.MonitorError -= Monitor_MonitorError;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _tips.Dispose();
        _flyout?.Dispose();
    }

    // ---------------- вспомогательные элементы ----------------

    /// <summary>Тёмно-синяя панель сайдбара с градиентом и декоративными кругами.</summary>
    private sealed class SidebarSurface : Panel
    {
        public SidebarSurface()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = ThemeManager.Current.SidebarStart;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;
            using (var gradient = new LinearGradientBrush(ClientRectangle, t.SidebarStart, t.SidebarEnd, 65f))
                g.FillRectangle(gradient, ClientRectangle);

            using var circle1 = new SolidBrush(Color.FromArgb(16, 255, 255, 255));
            g.FillEllipse(circle1, -90, Height - 230, 300, 300);
            using var circle2 = new SolidBrush(Color.FromArgb(14, 249, 115, 22));
            g.FillEllipse(circle2, Width - 110, -80, 220, 220);
        }
    }

    /// <summary>Полупрозрачный слой, создающий эффект плавного появления страницы.</summary>
    private sealed class FadeLayer : Control
    {
        private readonly Timer _timer;
        private int _alpha = 215;

        public FadeLayer()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _timer = new Timer { Interval = 16 };
            _timer.Tick += (_, _) =>
            {
                _alpha -= 26;
                if (_alpha <= 0)
                {
                    _timer.Stop();
                    var parent = Parent;
                    parent?.Controls.Remove(this);
                    Dispose();
                    return;
                }
                Invalidate();
            };
            _timer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // фон рисуется в OnPaint с нужной прозрачностью
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_alpha <= 0) return;
            using var brush = new SolidBrush(Color.FromArgb(_alpha, ThemeManager.Current.Background));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ThemeManager.Current.Surface;
        public override Color ImageMarginGradientBegin => ThemeManager.Current.Surface;
        public override Color ImageMarginGradientMiddle => ThemeManager.Current.Surface;
        public override Color ImageMarginGradientEnd => ThemeManager.Current.Surface;
        public override Color MenuBorder => ThemeManager.Current.Border;
        public override Color MenuItemBorder => ThemeManager.Current.Border;
        public override Color MenuItemSelected => ThemeManager.Current.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => ThemeManager.Current.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => ThemeManager.Current.SurfaceHover;
        public override Color SeparatorDark => ThemeManager.Current.Border;
        public override Color SeparatorLight => ThemeManager.Current.Border;
    }
}
