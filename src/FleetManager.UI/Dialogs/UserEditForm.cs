using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;

namespace FleetManager.UI.Dialogs;

public sealed class UserEditForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly User _user;
    private readonly bool _isEdit;
    private readonly UiTextField _login;
    private readonly UiTextField _fullName;
    private readonly UiComboBox _role;
    private readonly CheckBox _active;
    private readonly UiTextField _password;
    private readonly UiTextField _confirm;

    public UserEditForm(AppServices app, User? user, IReadOnlyList<Role> roles)
        : base(user is null ? "Новый пользователь" : "Редактирование пользователя",
            user is null ? "Создайте учётную запись и назначьте роль" : user.Login, 620, 500)
    {
        _app = app;
        _isEdit = user is not null;
        _user = user ?? new User { IsActive = true, RoleId = (int)UserRole.Dispatcher };
        SetSaveText(_isEdit ? "Сохранить" : "Создать пользователя");

        _login = FieldLayout.Text("Логин (латиница)");
        _login.Text = _user.Login;
        _fullName = FieldLayout.Text("Фамилия Имя Отчество");
        _fullName.Text = _user.FullName;

        _role = new UiComboBox();
        FieldLayout.FillCombo(_role,
            roles.Select(r => new { r.Id, Title = ((UserRole)r.Id).ToRussian() }),
            "Title", "Id", _user.RoleId);

        _active = new CheckBox
        {
            Text = "Учётная запись активна",
            Checked = _user.IsActive,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft
        };

        _password = FieldLayout.Text(_isEdit ? "Оставьте пустым, чтобы не менять" : "Минимум 8 символов");
        _password.UsePassword = true;
        _confirm = FieldLayout.Text("Повторите пароль");
        _confirm.UsePassword = true;

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ЛОГИН", _login), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ФИО", _fullName), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("РОЛЬ", _role), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("СТАТУС", _active), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field(_isEdit ? "НОВЫЙ ПАРОЛЬ" : "ПАРОЛЬ", _password), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("ПОДТВЕРЖДЕНИЕ ПАРОЛЯ", _confirm), 1, 2);

        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_login.Text))
            throw new ValidationException("Укажите логин.");
        if (_role.Inner.SelectedValue is not int roleId)
            throw new ValidationException("Выберите роль.");

        var password = _password.Text;
        var confirm = _confirm.Text;
        if (!_isEdit)
        {
            if (string.IsNullOrEmpty(password))
                throw new ValidationException("Задайте пароль для новой учётной записи.");
            AuthService.ValidateNewPassword(password, confirm);
        }
        else if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirm))
        {
            AuthService.ValidateNewPassword(password, confirm);
        }

        _user.Login = _login.Text.Trim();
        _user.FullName = _fullName.Text.Trim();
        _user.RoleId = roleId;
        _user.IsActive = _active.Checked;

        if (_isEdit)
        {
            await _app.Users.UpdateAsync(_user);
            if (!string.IsNullOrEmpty(password))
                await _app.Users.ResetPasswordAsync(_user.Id, password, confirm);
        }
        else
        {
            await _app.Users.CreateAsync(_user, password);
        }

        return true;
    }
}

public sealed class PasswordResetForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly User _user;
    private readonly UiTextField _password;
    private readonly UiTextField _confirm;

    public PasswordResetForm(AppServices app, User user)
        : base("Сброс пароля", $"{user.FullName} · {user.Login}", 540, 420, "Сбросить пароль")
    {
        _app = app;
        _user = user;

        _password = FieldLayout.Text("Новый пароль");
        _password.UsePassword = true;
        _confirm = FieldLayout.Text("Повторите пароль");
        _confirm.UsePassword = true;

        var table = FieldLayout.Table(1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("НОВЫЙ ПАРОЛЬ", _password), 0, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ПОДТВЕРЖДЕНИЕ", _confirm), 0, 1);
        SetContent(table);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        await _app.Users.ResetPasswordAsync(_user.Id, _password.Text, _confirm.Text);
        return true;
    }
}
