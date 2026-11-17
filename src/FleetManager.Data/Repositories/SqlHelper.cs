namespace FleetManager.Data.Repositories;

public static class SqlHelper
{
    /// <summary>Оборачивает поисковую строку в LIKE-шаблон. Пустая строка превращается в NULL.</summary>
    public static string? Like(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";

    public static object? DbValue(object? value) => value ?? DBNull.Value;
}
