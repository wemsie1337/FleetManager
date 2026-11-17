using System.Diagnostics;
using FleetManager.Data.Repositories;

namespace FleetManager.Services;

/// <summary>Единая точка аудита действий. Ошибки логирования не должны ломать бизнес-операцию.</summary>
public sealed class AuditService
{
    private readonly AuditRepository _repository;
    private readonly SessionContext _session;

    public AuditService(AuditRepository repository, SessionContext session)
    {
        _repository = repository;
        _session = session;
    }

    public Task LogAsync(string action, string tableName, string? recordId = null,
        string? oldValue = null, string? newValue = null) =>
        LogForUserAsync(_session.UserId, action, tableName, recordId, oldValue, newValue);

    public async Task LogForUserAsync(int? userId, string action, string tableName, string? recordId = null,
        string? oldValue = null, string? newValue = null)
    {
        try
        {
            await _repository.LogAsync(userId, action, tableName, recordId, oldValue, newValue);
        }
        catch (Exception ex)
        {
            // Аудит не должен препятствовать основной операции
            Debug.WriteLine($"[AUDIT ERROR] {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<Core.Models.AuditEntry>> GetRecentAsync(int top = 300) =>
        await _repository.GetRecentAsync(top);
}
