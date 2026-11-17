using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace FleetManager.Data;

/// <summary>
/// Тонкая обёртка над ADO.NET + Dapper. Создаёт новое соединение на операцию
/// (пулинг соединений SqlClient делает это дешёвым) и гарантированно закрывает его.
/// </summary>
public sealed class DatabaseContext
{
    private readonly string _connectionString;

    public DatabaseContext(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Строка подключения не задана.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var command = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: ct);
        var rows = await connection.QueryAsync<T>(command);
        return rows.AsList();
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var command = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: ct);
        return await connection.QueryFirstOrDefaultAsync<T>(command);
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var command = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: ct);
        return await connection.ExecuteAsync(command);
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        CommandType commandType = CommandType.Text,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var command = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: ct);
        return await connection.ExecuteScalarAsync<T>(command);
    }

    /// <summary>Выполняет действие в транзакции. Используется там, где важна атомарность.</summary>
    public async Task<T> InTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> action,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            var result = await action(connection, transaction);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Проверка доступности БД при старте приложения.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(ct);
            await using var command = new SqlCommand("SELECT DB_NAME(), @@VERSION", connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var dbName = reader.GetString(0);
                var version = reader.GetString(1).Split('\n')[0].Trim();
                return (true, $"База «{dbName}» доступна. {version}");
            }
            return (false, "Не удалось получить сведения о базе данных.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
