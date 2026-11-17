using System.Drawing;
using System.Windows.Forms;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

/// <summary>Стилизованное информационное окно / подтверждение.</summary>
public sealed class MessageDialog : BaseDialog
{
    private MessageDialog(string title, string message, ToastKind kind, bool confirm)
        : base(title, confirm ? "Подтвердите действие" : "Уведомление", 500, confirm ? 300 : 290,
            confirm ? "Подтвердить" : "Понятно", "Отмена")
    {
        var (glyph, color) = kind switch
        {
            ToastKind.Success => ("\uE73E", ThemeManager.Current.Success),
            ToastKind.Error => ("\uEA39", ThemeManager.Current.Danger),
            ToastKind.Warning => ("\uE7BA", ThemeManager.Current.Warning),
            _ => ("\uE946", ThemeManager.Current.Info)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var icon = new Label
        {
            Text = glyph,
            Font = new Font("Segoe MDL2 Assets", 26f),
            ForeColor = color,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };

        var text = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 10f),
            ForeColor = ThemeManager.Current.Text,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };

        table.Controls.Add(icon, 0, 0);
        table.Controls.Add(text, 1, 0);
        SetContent(table);

        if (!confirm)
        {
            HideSaveButton();
            CancelButton.Text = "Понятно";
            CancelButton.Kind = ButtonKind.Primary;
            CancelButton.Width = 160;
        }
        else
        {
            SetSaveText("Подтвердить");
        }
    }

    public static void Show(IWin32Window? owner, string title, string message, ToastKind kind = ToastKind.Info)
    {
        using var dialog = new MessageDialog(title, message, kind, false);
        ModalHost.Show(owner, dialog);
    }

    public static bool Confirm(IWin32Window? owner, string title, string message)
    {
        using var dialog = new MessageDialog(title, message, ToastKind.Warning, true);
        return ModalHost.Show(owner, dialog) == DialogResult.OK;
    }
}
