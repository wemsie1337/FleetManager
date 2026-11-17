using System.Drawing;
using System.Windows.Forms;

namespace FleetManager.UI.Forms;

/// <summary>Затемняющая подложка модального окна с плавным появлением.</summary>
internal sealed class OverlayForm : Form
{
    private readonly Timer _fadeTimer;

    public OverlayForm(Form owner)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = owner.Bounds;
        BackColor = Color.Black;
        Opacity = 0.0;
        TopMost = false;

        _fadeTimer = new Timer { Interval = 15 };
        _fadeTimer.Tick += (_, _) =>
        {
            Opacity += 0.055;
            if (Opacity >= 0.48)
            {
                Opacity = 0.48;
                _fadeTimer.Stop();
            }
        };
        Shown += (_, _) => _fadeTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fadeTimer.Dispose();
        base.Dispose(disposing);
    }
}

public static class ModalHost
{
    /// <summary>Показывает диалог по центру владельца с затемнением фона.</summary>
    public static DialogResult Show(IWin32Window? owner, BaseDialog dialog)
    {
        var ownerForm = owner as Form ?? Form.ActiveForm;
        if (ownerForm is null || !ownerForm.Visible)
            return dialog.ShowDialog();

        using var overlay = new OverlayForm(ownerForm);
        overlay.Show(ownerForm);
        try
        {
            return dialog.ShowDialog(overlay);
        }
        finally
        {
            overlay.Close();
        }
    }
}
