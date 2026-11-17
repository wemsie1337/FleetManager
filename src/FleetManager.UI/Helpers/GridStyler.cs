using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Helpers;

public static class GridStyler
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowProc callback, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, System.Text.StringBuilder name, int max);

    private delegate bool EnumWindowProc(IntPtr hWnd, IntPtr param);

    private static string ClassNameOf(IntPtr hWnd)
    {
        var buffer = new System.Text.StringBuilder(128);
        GetClassNameW(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public static void Style(DataGridView grid)
    {
        var t = ThemeManager.Current;

        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = t.Border;
        grid.BackgroundColor = t.Surface;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.ScrollBars = ScrollBars.Both;
        grid.DoubleBuffered(true);

        grid.DefaultCellStyle.BackColor = t.Surface;
        grid.DefaultCellStyle.ForeColor = t.Text;
        grid.DefaultCellStyle.SelectionBackColor = t.GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = t.Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        grid.AlternatingRowsDefaultCellStyle.BackColor = t.GridAltRow;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = t.Text;

        grid.ColumnHeadersDefaultCellStyle.BackColor = t.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = t.TextMuted;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = t.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        grid.RowTemplate.Height = 40;
        grid.AutoGenerateColumns = false;
        grid.StandardTab = true;

        // Тёмные системные полосы прокрутки (Windows 11). Если тема недоступна — тихо игнорируем.
        ApplyScrollbar(grid);
        // Полосы появляются после привязки данных — окрашиваем повторно.
        grid.DataBindingComplete += (_, _) => ApplyScrollbar(grid);
    }

    /// <summary>Окрашивает системные полосы прокрутки под текущую тему (Win10/11).</summary>
    public static void ApplyScrollbar(Control control)
    {
        try
        {
            var theme = ThemeManager.IsDark ? "DarkMode_Explorer" : "Explorer";
            var apply = new Action(() =>
            {
                if (control.IsDisposed || !control.IsHandleCreated) return;
                SetWindowTheme(control.Handle, theme, null);

                // Полосы прокрутки — нативные дочерние окна с классом ScrollBar;
                // WinForms не включает их в Control.Controls, поэтому ищем через Win32.
                EnumChildWindows(control.Handle, (hWnd, _) =>
                {
                    var className = ClassNameOf(hWnd);
                    if (className.Contains("ScrollBar", StringComparison.OrdinalIgnoreCase))
                        SetWindowTheme(hWnd, theme, null);
                    return true;
                }, IntPtr.Zero);

                foreach (Control child in control.Controls)
                {
                    if (child.IsDisposed || !child.IsHandleCreated) continue;
                    SetWindowTheme(child.Handle, theme, null);
                }
            });

            apply();
            if (!control.IsHandleCreated)
                control.HandleCreated += (_, _) => apply();
            if (control.IsHandleCreated && !control.IsDisposed)
                control.BeginInvoke(apply);
        }
        catch
        {
            // uxtheme недоступен — остаются стандартные полосы прокрутки
        }
    }

    private static void DoubleBuffered(this DataGridView grid, bool enabled)
    {
        var property = typeof(DataGridView).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        property?.SetValue(grid, enabled);
    }
}
