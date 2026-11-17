using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FleetManager.UI.Controls;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Forms;

/// <summary>Базовое модальное окно с кастомным заголовком, скруглёнными углами и футером.</summary>
public abstract class BaseDialog : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    protected readonly Panel HeaderPanel;
    protected readonly Panel ContentPanel;
    protected readonly Panel FooterPanel;
    protected readonly Label TitleLabel;
    protected readonly Label SubtitleLabel;
    protected readonly GradientButton SaveButton;
    protected new readonly GradientButton CancelButton;
    private readonly RoundIconButton _closeButton;
    private bool _busy;

    protected BaseDialog(string title, string subtitle = "", int width = 600, int height = 620,
        string saveText = "Сохранить", string cancelText = "Отмена")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = ThemeManager.Current.Surface;
        ForeColor = ThemeManager.Current.Text;
        ClientSize = new Size(width, height);
        MinimumSize = new Size(420, 260);
        Font = Ui.Fonts.Regular;

        HeaderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = ThemeManager.Current.Surface,
            Tag = "surface"
        };
        HeaderPanel.MouseDown += Header_MouseDown;
        HeaderPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawLine(pen, 0, HeaderPanel.Height - 1, HeaderPanel.Width, HeaderPanel.Height - 1);
        };

        TitleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(26, 18),
            Size = new Size(width - 100, 28)
        };
        SubtitleLabel = new Label
        {
            Text = subtitle,
            Font = Ui.Fonts.Small,
            ForeColor = ThemeManager.Current.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(27, 47),
            Size = new Size(width - 100, 20),
            Tag = "muted"
        };

        _closeButton = new RoundIconButton
        {
            Glyph = "\uE711",
            Size = new Size(34, 34),
            Location = new Point(width - 48, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _closeButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        HeaderPanel.Controls.Add(TitleLabel);
        HeaderPanel.Controls.Add(SubtitleLabel);
        HeaderPanel.Controls.Add(_closeButton);

        FooterPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 78,
            BackColor = ThemeManager.Current.Surface,
            Tag = "surface"
        };
        FooterPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(ThemeManager.Current.Border);
            e.Graphics.DrawLine(pen, 0, 0, FooterPanel.Width, 0);
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 390,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Tag = "transparent",
            Padding = new Padding(0, 18, 24, 0)
        };

        SaveButton = new GradientButton
        {
            Text = saveText,
            Kind = ButtonKind.Primary,
            Width = 160,
            Height = 42,
            Margin = new Padding(10, 0, 0, 0)
        };
        SaveButton.Click += async (_, _) => await SaveAsync();

        CancelButton = new GradientButton
        {
            Text = cancelText,
            Kind = ButtonKind.Ghost,
            Width = 130,
            Height = 42,
            Margin = new Padding(0)
        };
        CancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        buttons.Controls.Add(SaveButton);
        buttons.Controls.Add(CancelButton);
        FooterPanel.Controls.Add(buttons);

        ContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Current.Surface,
            Padding = new Padding(26, 20, 26, 12),
            Tag = "surface"
        };

        Controls.Add(ContentPanel);
        Controls.Add(FooterPanel);
        Controls.Add(HeaderPanel);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };

        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    protected void SetContent(Control content)
    {
        content.Dock = DockStyle.Fill;
        ContentPanel.Controls.Add(content);
    }

    protected void SetSubtitle(string text) => SubtitleLabel.Text = text;

    protected void SetSaveText(string text)
    {
        SaveButton.Text = text;
        // Кнопка растягивается под текст ("Сохранить изменения" не должна обрезаться).
        var width = TextRenderer.MeasureText(text, SaveButton.Font).Width + 44;
        SaveButton.Width = Math.Max(160, width);
    }

    protected void SetSaveKind(ButtonKind kind) => SaveButton.Kind = kind;

    protected void HideSaveButton() => SaveButton.Visible = false;

    /// <summary>Возвращает true, если окно можно закрывать с результатом OK.</summary>
    protected virtual Task<bool> OnSaveAsync() => Task.FromResult(true);

    private async Task SaveAsync()
    {
        if (_busy) return;
        _busy = true;
        var oldText = SaveButton.Text;
        SaveButton.Enabled = false;
        SaveButton.Text = "Сохранение...";
        try
        {
            if (await OnSaveAsync())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            Ui.ShowError(this, ex);
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                SaveButton.Enabled = true;
                SaveButton.Text = oldText;
            }
        }
    }

    private void Header_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, IntPtr.Zero);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        BackColor = ThemeManager.Current.Surface;
        HeaderPanel.BackColor = ThemeManager.Current.Surface;
        FooterPanel.BackColor = ThemeManager.Current.Surface;
        ContentPanel.BackColor = ThemeManager.Current.Surface;
        TitleLabel.ForeColor = ThemeManager.Current.Text;
        SubtitleLabel.ForeColor = ThemeManager.Current.TextMuted;
        ThemeManager.ApplyTree(this);
        Invalidate(true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Drawing.RoundRect(new Rectangle(0, 0, Width, Height), 18);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(ThemeManager.Current.Border, 1.2f);
        Drawing.DrawRounded(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 18, pen);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
        base.Dispose(disposing);
    }
}
