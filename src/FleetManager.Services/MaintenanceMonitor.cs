using FleetManager.Core.Models;

namespace FleetManager.Services;

public sealed class MaintenanceAlertsEventArgs : EventArgs
{
    public IReadOnlyList<MaintenanceAlert> Alerts { get; }
    public IReadOnlyList<MaintenanceAlert> NewAlerts { get; }

    public MaintenanceAlertsEventArgs(IReadOnlyList<MaintenanceAlert> alerts, IReadOnlyList<MaintenanceAlert> newAlerts)
    {
        Alerts = alerts;
        NewAlerts = newAlerts;
    }
}

/// <summary>
/// Фоновый монитор ТО. Периодически пересчитывает сроки в БД и поднимает событие
/// для UI. Работает на фоновом потоке; UI обязан маршалить вызовы через BeginInvoke.
/// </summary>
public sealed class MaintenanceMonitor : IDisposable
{
    private readonly MaintenanceService _maintenance;
    private readonly int _intervalMinutes;
    private readonly HashSet<string> _notifiedKeys = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public event EventHandler<MaintenanceAlertsEventArgs>? AlertsUpdated;
    public event EventHandler<string>? MonitorError;

    public IReadOnlyList<MaintenanceAlert> LastAlerts { get; private set; } = Array.Empty<MaintenanceAlert>();
    public bool IsRunning => _loop is { IsCompleted: false };

    public MaintenanceMonitor(MaintenanceService maintenance, int intervalMinutes)
    {
        _maintenance = maintenance;
        _intervalMinutes = Math.Clamp(intervalMinutes, 1, 24 * 60);
    }

    public void Start()
    {
        if (_disposed || IsRunning) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    /// <summary>Ручная проверка (например, при входе или по кнопке «Обновить»).</summary>
    public Task CheckNowAsync(CancellationToken ct = default) => CheckCoreAsync(ct);

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            // Даём UI время отрисоваться и не дублируем проверку при входе
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await CheckCoreAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MonitorError?.Invoke(this, ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // штатное завершение
        }
    }

    private async Task CheckCoreAsync(CancellationToken ct)
    {
        await _maintenance.RefreshAlertsAsync();
        var alerts = await _maintenance.GetActiveAlertsAsync();
        if (ct.IsCancellationRequested) return;

        var newAlerts = new List<MaintenanceAlert>();
        foreach (var alert in alerts)
        {
            var key = $"{alert.Id}:{alert.Severity}";
            if (_notifiedKeys.Add(key)) newAlerts.Add(alert);
        }

        LastAlerts = alerts;
        AlertsUpdated?.Invoke(this, new MaintenanceAlertsEventArgs(alerts, newAlerts));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
