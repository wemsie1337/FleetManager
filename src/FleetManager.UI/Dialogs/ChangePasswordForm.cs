using System.Drawing;
using System.Windows.Forms;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;

namespace FleetManager.UI.Dialogs;

/// <summary>Смена собственного пароля текущим пользователем.</summary>
public sealed class ChangePasswordForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly UiTextField _current;
    private readonly UiTextField _password;
    private readonly UiTextField _confirm;

    public ChangePasswordForm(AppServices app)
        : base("Смена пароля", $"Пользователь: {app.Session.FullName}", 540, 490, "Изменить пароль")
    {
        _app = app;

        _current = FieldLayout.Text("Текущий пароль");
        _current.UsePassword = true;
        _password = FieldLayout.Text("Новый пароль, минимум 8 символов");
        _password.UsePassword = true;
        _confirm = FieldLayout.Text("Повторите новый пароль");
        _confirm.UsePassword = true;

        var table = FieldLayout.Table(1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТЕКУЩИЙ ПАРОЛЬ", _current), 0, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("НОВЫЙ ПАРОЛЬ", _password), 0, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ПОДТВЕРЖДЕНИЕ", _confirm), 0, 2);
        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_app.Session.UserId is not { } userId)
            throw new ValidationException("Сессия не найдена. Войдите повторно.");

        await _app.Auth.ChangePasswordAsync(userId, _current.Text, _password.Text, _confirm.Text);
        return true;
    }
}
