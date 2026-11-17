using System.Data;
using FleetManager.Core.Models;

namespace FleetManager.Data.Repositories;

public sealed class AuditRepository
{
    private readonly DatabaseContext _db;
    public AuditRepository(DatabaseContext db) => _db = db;

    public Task LogAsync(int? userId, string action, string tableName, string? recordId,
        string? oldValue, string? newValue, CancellationToken ct = default) =>
        _db.ExecuteAsync(@"
INSERT INTO dbo.AuditLog (UserId, [Action], TableName, RecordId, OldValue, NewValue)
VALUES (@UserId, @Action, @TableName, @RecordId, @OldValue, @NewValue);",
            new
            {
                UserId = userId,
                Action = Cut(action, 100),
                TableName = Cut(tableName, 100),
                RecordId = Cut(recordId, 50),
                OldValue = Cut(oldValue, 4000),
                NewValue = Cut(newValue, 4000)
            }, ct: ct);

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int top = 300, CancellationToken ct = default) =>
        await _db.QueryAsync<AuditEntry>("dbo.sp_GetAuditLog", new { Top = top }, CommandType.StoredProcedure, ct);

    private static string? Cut(string? value, int length) =>
        value is null ? null : value.Length <= length ? value : value[..length];
}
