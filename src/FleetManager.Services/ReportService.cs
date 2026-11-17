using System.Drawing;
using FleetManager.Core.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Color = System.Drawing.Color;

namespace FleetManager.Services;

/// <summary>Экспорт табличных отчётов в Excel (EPPlus) и PDF (QuestPDF).</summary>
public sealed class ReportService
{
    private readonly AppSettings _settings;

    static ReportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportService(AppSettings settings) => _settings = settings;

    public string SuggestPath(string fileName, string extension)
    {
        var folder = string.IsNullOrWhiteSpace(_settings.ExportFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _settings.ExportFolder;

        if (!Directory.Exists(folder)) folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var safeName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(folder, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}{extension}");
    }

    public void ExportToExcel(string path, string title, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows, string sheetName = "Отчёт")
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Отчёт" : sheetName[..Math.Min(28, sheetName.Length)]);
        var columnCount = Math.Max(1, headers.Count);

        ws.Cells[1, 1, 1, columnCount].Merge = true;
        ws.Cells[1, 1].Value = title;
        ws.Cells[1, 1].Style.Font.Size = 16;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Color.SetColor(Color.White);
        ws.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 58, 138));
        ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Cells[1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        ws.Row(1).Height = 32;

        ws.Cells[2, 1].Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
        ws.Cells[2, 1].Style.Font.Size = 9;
        ws.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(100, 116, 139));

        for (var i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cells[4, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 58, 138));
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(30, 58, 138));
        }
        ws.Row(4).Height = 24;

        var rowIndex = 5;
        var stripe = false;
        foreach (var row in rows)
        {
            for (var col = 0; col < headers.Count; col++)
            {
                var cell = ws.Cells[rowIndex, col + 1];
                var value = col < row.Count ? row[col] : null;
                cell.Value = value;
                if (value is DateTime dt)
                    cell.Style.Numberformat.Format = "dd.MM.yyyy HH:mm";
                else if (value is decimal or double)
                    cell.Style.Numberformat.Format = "#,##0.00";
                else if (value is int or long)
                    cell.Style.Numberformat.Format = "#,##0";

                if (stripe)
                {
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(241, 245, 249));
                }
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
                cell.Style.Border.Bottom.Color.SetColor(Color.FromArgb(203, 213, 225));
            }
            stripe = !stripe;
            rowIndex++;
        }

        if (headers.Count > 0)
            ws.Cells[4, 1, Math.Max(4, rowIndex - 1), headers.Count].AutoFilter = true;

        ws.View.FreezePanes(5, 1);
        if (ws.Dimension is not null)
            ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 42);
        ws.PrinterSettings.Orientation = eOrientation.Landscape;
        ws.PrinterSettings.FitToPage = true;
        ws.PrinterSettings.FitToWidth = 1;
        ws.PrinterSettings.FitToHeight = 0;

        package.SaveAs(new FileInfo(path));
    }

    public void ExportToPdf(string path, string title, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows, string subtitle = "")
    {
        var data = rows.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontSize(16).SemiBold().FontColor("#1E3A8A");
                    column.Item().Text(string.IsNullOrWhiteSpace(subtitle)
                            ? $"{_settings.CompanyName} · сформировано {DateTime.Now:dd.MM.yyyy HH:mm}"
                            : subtitle)
                        .FontSize(8).FontColor("#64748B");
                    column.Item().PaddingTop(6).LineHorizontal(1).LineColor("#DBEAFE");
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell()
                                .Background("#1E3A8A")
                                .Padding(4)
                                .Text(h)
                                .FontColor("#FFFFFF")
                                .SemiBold();
                        }
                    });

                    var stripe = false;
                    foreach (var row in data)
                    {
                        for (var col = 0; col < headers.Count; col++)
                        {
                            var value = col < row.Count ? row[col] : null;
                            table.Cell()
                                .Background(stripe ? "#F1F5F9" : "#FFFFFF")
                                .BorderBottom(0.5f)
                                .BorderColor("#CBD5E1")
                                .Padding(3)
                                .Text(FormatValue(value));
                        }
                        stripe = !stripe;
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{_settings.CompanyName} · {title}")
                        .FontSize(7).FontColor("#94A3B8");
                    row.ConstantItem(100).AlignRight().Text(text =>
                    {
                        text.Span("Стр. ").FontSize(7).FontColor("#94A3B8");
                        text.CurrentPageNumber().FontSize(7).FontColor("#94A3B8");
                        text.Span(" из ").FontSize(7).FontColor("#94A3B8");
                        text.TotalPages().FontSize(7).FontColor("#94A3B8");
                    });
                });
            });
        }).GeneratePdf(path);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("dd.MM.yyyy HH:mm"),
        decimal d => d.ToString("N2"),
        double d => d.ToString("N2"),
        bool b => b ? "Да" : "Нет",
        _ => value.ToString() ?? string.Empty
    };
}
