using Microsoft.Data.SqlClient;

namespace FleetManager.Services.Infrastructure;

/// <summary>Преобразует технические исключения БД в понятные пользователю сообщения.</summary>
public static class ErrorTranslator
{
    public static string ToFriendlyMessage(Exception ex)
    {
        var sql = FindSqlException(ex);

        if (sql is not null)
        {
            return sql.Number switch
            {
                2601 or 2627 => "Такая запись уже существует. Проверьте уникальные поля (логин, госномер, VIN, права).",
                547 => "Операция невозможна: запись связана с другими данными. Сначала удалите или измените зависимые записи.",
                515 => "Не заполнено обязательное поле.",
                229 or 230 => "Недостаточно прав для выполнения операции в базе данных.",
                4060 or 18456 or 233 => "Не удалось подключиться к SQL Server. Проверьте строку подключения и доступность сервера.",
                208 => "В базе отсутствуют необходимые объекты. Выполните скрипт database/FleetManagerDB.sql.",
                1205 => "Обнаружена взаимоблокировка. Повторите операцию.",
                -2 => "Истекло время ожидания SQL Server. Проверьте сеть и нагрузку на сервер.",
                _ => $"Ошибка базы данных ({sql.Number}): {sql.Message}"
            };
        }

        if (ex is ValidationException validation)
            return validation.Message;

        if (ex is TimeoutException)
            return "Истекло время ожидания операции. Попробуйте ещё раз.";

        return ex.Message;
    }

    private static SqlException? FindSqlException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is SqlException sql) return sql;
            ex = ex.InnerException;
        }
        return null;
    }
}
