using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Forms;

public class LoginForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
    private const int HeroWidth = 462;

    private readonly AppServices _app;
    private readonly UiTextField _login;
    private readonly UiTextField _password;
    private readonly GradientButton _loginButton;
    private readonly Label _errorLabel;
    private readonly CheckBox _showPassword;
    private readonly RoundIconButton _themeButton;
    private readonly Label _dbStatus;
    private readonly HeroPanel _hero;
    private readonly Timer _shakeTimer;
    private int _shakeStep;

    public LoginForm(AppServices app)
    {
        _app = app;
        Text = "Автопарк — вход в систему";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 620);
        MinimumSize = Size;
        MaximumSize = Size;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = ThemeManager.Current.Surface;
        Font = Ui.Fonts.Regular;
        Opacity = 0;

        _hero = new HeroPanel { Dock = DockStyle.Left, Width = HeroWidth, Tag = "keep-bg" };
        _hero.MouseDown += DragWindow;

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Current.Surface,
            Tag = "surface"
        };
        right.MouseDown += DragWindow;

        _themeButton = new RoundIconButton
        {
            Glyph = ThemeManager.IsDark ? "\uE706" : "\uE708",
            GlyphColor = ThemeManager.Current.TextMuted,
            Location = new Point(488, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _themeButton.Click += (_, _) =>
        {
            ThemeManager.Toggle();
            ApplyTheme();
        };

        var close = new RoundIconButton
        {
            Glyph = "\uE711",
            Location = new Point(438, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        close.Click += (_, _) => Close();

        var welcome = new Label
        {
            Text = "С возвращением",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            Location = new Point(62, 102),
            Size = new Size(420, 44)
        };
        var welcomeSub = new Label
        {
            Text = "Войдите, чтобы управлять автопарком",
            Font = new Font("Segoe UI", 10f),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Tag = "muted",
            Location = new Point(64, 150),
            Size = new Size(400, 24)
        };

        var loginCaption = CreateCaption("ЛОГИН", 62, 204);
        _login = new UiTextField
        {
            Placeholder = "Введите логин",
            IconGlyph = "\uE77B",
            Location = new Point(62, 228),
            Width = 408,
            Text = "admin"
        };

        var passwordCaption = CreateCaption("ПАРОЛЬ", 62, 294);
        _password = new UiTextField
        {
            Placeholder = "Введите пароль",
            IconGlyph = "\uE72E",
            UsePassword = true,
            Location = new Point(62, 318),
            Width = 408
        };
        _password.Input.KeyDown += LoginKeyDown;

        _showPassword = new CheckBox
        {
            Text = "Показать пароль",
            Font = Ui.Fonts.Small,
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(326, 370),
            Cursor = Cursors.Hand
        };
        _showPassword.CheckedChanged += (_, _) => _password.UsePassword = !_showPassword.Checked;

        _errorLabel = new Label
        {
            Text = "",
            Font = Ui.Fonts.Small,
            ForeColor = ThemeManager.Current.Danger,
            BackColor = Color.Transparent,
            Location = new Point(64, 398),
            Size = new Size(404, 34),
            Visible = false
        };

        _loginButton = new GradientButton
        {
            Text = "Войти в систему",
            IconGlyph = "\uE72B",
            Kind = ButtonKind.Accent,
            Location = new Point(62, 434),
            Size = new Size(408, 48),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
        };
        _loginButton.Click += async (_, _) => await LoginAsync();

        var demo = CreateDemoPanel(62, 500, 408);

        _dbStatus = new Label
        {
            Text = "Проверка подключения к базе данных…",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Location = new Point(64, 586),
            Size = new Size(420, 20),
            Tag = "muted"
        };

        right.Controls.AddRange(new Control[]
        {
            _themeButton, close, welcome, welcomeSub, loginCaption, _login, passwordCaption, _password,
            _showPassword, _errorLabel, _loginButton, demo, _dbStatus
        });

        Controls.Add(right);
        Controls.Add(_hero);

        _login.Input.KeyDown += LoginKeyDown;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        _hero.MouseDown += DragWindow;
        Shown += async (_, _) =>
        {
            _login.Input.Focus();
            _login.Input.SelectAll();
            await FadeInAsync();
            await CheckDatabaseAsync();
        };

        _shakeTimer = new Timer { Interval = 30 };
        _shakeTimer.Tick += (_, _) =>
        {
            var offsets = new[] { -10, 10, -8, 8, -5, 5, -2, 2, 0 };
            _shakeStep++;
            Left += offsets[Math.Min(_shakeStep, offsets.Length - 1)];
            if (_shakeStep >= offsets.Length)
            {
                _shakeTimer.Stop();
                _shakeStep = 0;
            }
        };

        ThemeManager.ApplyTree(this);
    }

    private Label CreateCaption(string text, int x, int y) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
        ForeColor = ThemeManager.Current.TextMuted,
        BackColor = Color.Transparent,
        Location = new Point(x, y),
        Size = new Size(300, 18),
        Tag = "muted"
    };

    private Panel CreateDemoPanel(int x, int y, int width)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, 72),
            BackColor = ThemeManager.Current.SurfaceAlt,
            Tag = "alt"
        };
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Drawing.RoundRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 10);
            using var fill = new SolidBrush(ThemeManager.Current.SurfaceAlt);
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawPath(pen, path);
        };

        var title = new Label
        {
            Text = "ДЕМО-ДОСТУП",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Accent,
            BackColor = Color.Transparent,
            Location = new Point(14, 8),
            Size = new Size(200, 16)
        };
        var text = new Label
        {
            Text = "admin / Admin@123   ·   dispatcher / Dispatch@123   ·   driver1 / Driver@123",
            Font = new Font("Segoe UI", 7.8f),
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            Tag = "muted",
            Location = new Point(14, 26),
            Size = new Size(width - 28, 38)
        };
        panel.Controls.Add(title);
        panel.Controls.Add(text);
        return panel;
    }

    private void LoginKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            _ = LoginAsync();
        }
    }

    private async Task LoginAsync()
    {
        if (!_loginButton.Enabled) return;
        _errorLabel.Visible = false;
        _loginButton.Enabled = false;
        _loginButton.Text = "Проверка...";
        try
        {
            await _app.Auth.LoginAsync(_login.Text, _password.Text);
            Opacity = 0;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = "⚠  " + Services.Infrastructure.ErrorTranslator.ToFriendlyMessage(ex);
            _errorLabel.Visible = true;
            _password.Text = string.Empty;
            _password.Input.Focus();
            _shakeStep = 0;
            _shakeTimer.Start();
        }
        finally
        {
            _loginButton.Enabled = true;
            _loginButton.Text = "Войти в систему";
        }
    }

    private async Task CheckDatabaseAsync()
    {
        var (ok, message) = await _app.Database.TestConnectionAsync();
        _dbStatus.Text = ok
            ? "● База данных подключена"
            : "● Нет подключения к SQL Server. Проверьте appsettings.json";
        _dbStatus.ForeColor = ok ? ThemeManager.Current.Success : ThemeManager.Current.Danger;
        if (!ok)
        {
            _errorLabel.Text = "Не удалось подключиться к базе: " + message;
            _errorLabel.Visible = true;
        }
    }

    private async Task FadeInAsync()
    {
        for (var i = 0; i <= 10; i++)
        {
            Opacity = i / 10.0;
            await Task.Delay(18);
        }
        Opacity = 1;
    }

    private void ApplyTheme()
    {
        _themeButton.Glyph = ThemeManager.IsDark ? "\uE706" : "\uE708";
        BackColor = ThemeManager.Current.Surface;
        ThemeManager.ApplyTree(this);
        _hero.Invalidate();
        Invalidate(true);
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, IntPtr.Zero);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Drawing.RoundRect(new Rectangle(0, 0, Width, Height), 22);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(ThemeManager.Current.Border, 1.2f);
        Drawing.DrawRounded(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 22, pen);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shakeTimer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Левая градиентная панель логина с декоративной графикой.</summary>
internal sealed class HeroPanel : Panel
{
    public HeroPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = ThemeManager.Current;

        using var gradient = new LinearGradientBrush(ClientRectangle, t.SidebarStart, t.SidebarEnd, 35f);
        g.FillRectangle(gradient, ClientRectangle);

        using var c1 = new SolidBrush(Color.FromArgb(22, 255, 255, 255));
        g.FillEllipse(c1, -120, Height - 240, 360, 360);
        using var c2 = new SolidBrush(Color.FromArgb(18, 249, 115, 22));
        g.FillEllipse(c2, Width - 160, -130, 300, 300);
        using var c3 = new SolidBrush(Color.FromArgb(12, 255, 255, 255));
        g.FillEllipse(c3, Width - 40, Height - 160, 200, 200);

        // Логотип
        var logo = new Rectangle(54, 50, 50, 50);
        using var logoBrush = new LinearGradientBrush(logo, t.Accent, t.AccentDark, 45f);
        Drawing.FillRounded(g, logo, 14, logoBrush);
        TextRenderer.DrawText(g, "\uE804", new Font("Segoe MDL2 Assets", 22f), logo, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "АВТОПАРК", new Font("Segoe UI", 15f, FontStyle.Bold),
            new Rectangle(118, 54, 250, 26), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "FLEET MANAGEMENT SYSTEM", new Font("Segoe UI", 7.5f, FontStyle.Bold),
            new Rectangle(119, 78, 280, 18), Color.FromArgb(155, 185, 225),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, "Управляйте автопарком\nна новых скоростях", new Font("Segoe UI", 23f, FontStyle.Bold),
            new Rectangle(54, 168, 370, 82), Color.White,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, "Пробег, ТО, маршруты и расходы —\nв одном современном рабочем пространстве.",
            new Font("Segoe UI", 10f), new Rectangle(56, 258, 350, 52),
            Color.FromArgb(205, 220, 242),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix);

        DrawFeature(g, 330, "\uE7F4", "Контроль ТО и уведомления");
        DrawFeature(g, 382, "\uE9D9", "Маршруты и путевые листы");
        DrawFeature(g, 434, "\uE9D2", "Аналитика расходов и KPI");

        // Декоративная карточка
        var card = new Rectangle(54, Height - 138, Width - 108, 88);
        using var cardBrush = new SolidBrush(Color.FromArgb(22, 255, 255, 255));
        Drawing.FillRounded(g, card, 14, cardBrush);
        using var cardBorder = new Pen(Color.FromArgb(45, 255, 255, 255));
        Drawing.DrawRounded(g, card, 14, cardBorder);
        TextRenderer.DrawText(g, "10 ТС · 5 водителей · 2 филиала", new Font("Segoe UI", 10f, FontStyle.Bold),
            new Rectangle(card.X + 18, card.Y + 16, card.Width - 36, 24), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "Демонстрационные данные уже в базе", new Font("Segoe UI", 8.5f),
            new Rectangle(card.X + 18, card.Y + 42, card.Width - 36, 22), Color.FromArgb(180, 205, 235),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(g, "v1.0 · .NET 8 · Windows Forms · SQL Server", new Font("Segoe UI", 7.5f),
            new Rectangle(54, Height - 36, Width - 108, 18), Color.FromArgb(140, 170, 210),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private static void DrawFeature(Graphics g, int y, string glyph, string text)
    {
        var iconRect = new Rectangle(56, y, 32, 32);
        using var bg = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
        g.FillEllipse(bg, iconRect);
        TextRenderer.DrawText(g, glyph, new Font("Segoe MDL2 Assets", 11f), iconRect, Color.FromArgb(255, 210, 170),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, text, new Font("Segoe UI", 10f),
            new Rectangle(100, y, 320, 32), Color.FromArgb(225, 235, 248),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
