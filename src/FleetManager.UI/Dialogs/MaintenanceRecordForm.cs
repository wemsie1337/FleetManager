using System.Drawing;
using System.Windows.Forms;
using FleetManager.Core.Models;
using FleetManager.Services;
using FleetManager.UI.Controls;
using FleetManager.UI.Forms;
using FleetManager.UI.Helpers;
using FleetManager.UI.Theme;

namespace FleetManager.UI.Dialogs;

public sealed class MaintenanceRecordForm : BaseDialog
{
    private readonly AppServices _app;
    private readonly UiComboBox _vehicle;
    private readonly UiComboBox _type;
    private readonly UiDateField _date;
    private readonly UiTextField _mileage;
    private readonly UiTextField _cost;
    private readonly UiTextField _performer;
    private readonly UiTextField _attachment;
    private readonly UiTextField _notes;
    private string? _attachmentPath;

    public MaintenanceRecordForm(AppServices app, IReadOnlyList<Vehicle> vehicles,
        IReadOnlyList<MaintenanceType> types, int? preselectedVehicleId = null)
        : base("Выполнение ТО", "Зафиксируйте работы, стоимость и исполнителя", 680, 720)
    {
        _app = app;
        SetSaveText("Зафиксировать ТО");

        _vehicle = new UiComboBox();
        FieldLayout.FillCombo(_vehicle, vehicles, nameof(Vehicle.DisplayName), nameof(Vehicle.Id), preselectedVehicleId);

        _type = new UiComboBox();
        FieldLayout.FillCombo(_type, types, nameof(MaintenanceType.TypeName), nameof(MaintenanceType.Id));

        _date = FieldLayout.Date(false, DateTime.Today);
        _mileage = FieldLayout.Text("Пробег на момент ТО");
        _cost = FieldLayout.Text("Стоимость работ и запчастей, ₽");
        _performer = FieldLayout.Text("Сервис / механик");
        _performer.Text = _app.Session.FullName;

        _attachment = new UiTextField { ReadOnly = true, Placeholder = "Файл не выбран", Height = 42 };
        var browse = new GradientButton
        {
            Text = "Обзор...",
            Kind = ButtonKind.Ghost,
            Width = 118,
            Height = 42,
            Font = new Font("Segoe UI", 8.5f)
        };
        browse.Click += (_, _) => BrowseAttachment();

        var attachmentPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Tag = "transparent"
        };
        attachmentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        attachmentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        _attachment.Dock = DockStyle.Fill;
        _attachment.Margin = new Padding(0, 0, 8, 0);
        browse.Dock = DockStyle.Top;
        browse.Margin = new Padding(0);
        attachmentPanel.Controls.Add(_attachment, 0, 0);
        attachmentPanel.Controls.Add(browse, 1, 0);

        _notes = FieldLayout.Text("Описание выполненных работ", multiline: true);

        var table = FieldLayout.Table(2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ТРАНСПОРТНОЕ СРЕДСТВО", _vehicle), 0, 0);
        FieldLayout.Add(table, FieldLayout.Field("ВИД ТО", _type), 1, 0);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ДАТА", _date), 0, 1);
        FieldLayout.Add(table, FieldLayout.Field("ПРОБЕГ ПРИ ТО, КМ", _mileage), 1, 1);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("СТОИМОСТЬ, ₽", _cost), 0, 2);
        FieldLayout.Add(table, FieldLayout.Field("ИСПОЛНИТЕЛЬ", _performer), 1, 2);
        FieldLayout.AddRow(table, 76);
        FieldLayout.Add(table, FieldLayout.Field("ЧЕК / ФОТО", attachmentPanel), 0, 3, 2);
        FieldLayout.AddRow(table, 118);
        FieldLayout.Add(table, FieldLayout.Field("ОПИСАНИЕ РАБОТ", _notes), 0, 4, 2);

        SetContent(table);

        _vehicle.SelectionChanged += async (_, _) => await FillMileageAsync();
        Shown += async (_, _) => await FillMileageAsync();
    }

    private async Task FillMileageAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId) return;
        try
        {
            var current = await _app.Mileage.GetCurrentOdometerAsync(vehicleId);
            if (!IsDisposed) _mileage.Text = current.ToString();
        }
        catch
        {
            // не критично
        }
    }

    private void BrowseAttachment()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Фото или чек выполненного ТО",
            Filter = "Изображения и PDF|*.jpg;*.jpeg;*.png;*.bmp;*.pdf|Все файлы|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _attachmentPath = dialog.FileName;
        _attachment.Text = Path.GetFileName(dialog.FileName);
    }

    protected override async Task<bool> OnSaveAsync()
    {
        if (_vehicle.Inner.SelectedValue is not int vehicleId)
            throw new ValidationException("Выберите транспортное средство.");
        if (_type.Inner.SelectedValue is not int typeId)
            throw new ValidationException("Выберите вид ТО.");
        var mileage = FieldLayout.ParseInt(_mileage.Text);
        if (mileage is null) throw new ValidationException("Укажите пробег при ТО.");
        var cost = FieldLayout.ParseDecimal(_cost.Text) ?? 0;
        if (cost < 0) throw new ValidationException("Стоимость не может быть отрицательной.");
        if (string.IsNullOrWhiteSpace(_performer.Text))
            throw new ValidationException("Укажите исполнителя работ.");

        var storedPath = CopyAttachment(_attachmentPath);

        var record = new MaintenanceRecord
        {
            VehicleId = vehicleId,
            MaintenanceTypeId = typeId,
            Date = _date.Inner.Value.Date,
            MileageAtService = mileage.Value,
            Cost = cost,
            Performer = _performer.Text,
            Notes = _notes.Text,
            AttachmentPath = storedPath
        };

        await _app.Maintenance.SaveRecordAsync(record);
        return true;
    }

    private static string? CopyAttachment(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return null;
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FleetManager", "attachments");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, $"{Guid.NewGuid():N}{Path.GetExtension(source)}");
            File.Copy(source, target, overwrite: false);
            return target;
        }
        catch
        {
            return source;
        }
    }
}
