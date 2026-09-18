using System.Text.Json;
using Frog.Server.Logging;
using Frog.Server.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Observability;

/// <summary>
/// Emits a structured <c>ops_metrics</c> snapshot on an interval and on stop.
/// Optional JSON file: set <c>FROG_OPS_METRICS_PATH</c>.
/// Interval seconds: <c>FROG_OPS_METRICS_INTERVAL_SECONDS</c> (default 15, min 5).
/// </summary>
public sealed class OpsMetricsSnapshotHostedService(
    ServerOpsMetrics metrics,
    ConnectionManager connectionManager,
    ILogger<OpsMetricsSnapshotHostedService> logger) : BackgroundService
{
    public const string PathEnvironmentVariable = "FROG_OPS_METRICS_PATH";
    public const string IntervalEnvironmentVariable = "FROG_OPS_METRICS_INTERVAL_SECONDS";

    private readonly ServerOpsMetrics _metrics = metrics;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly ILogger<OpsMetricsSnapshotHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = ReadInterval();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                Emit("periodic");
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            Emit("shutdown");
        }
    }

    private void Emit(string trigger)
    {
        var snapshot = _metrics.Snapshot(_connectionManager.GetActiveSessions().Count);
        ServerNetworkLogs.OpsMetricsSnapshot(
            _logger,
            trigger,
            snapshot.ConnectionsAccepted,
            snapshot.ConnectionsRejected,
            snapshot.RateLimitHits,
            snapshot.RateLimitHitsLogin,
            snapshot.RateLimitHitsReconnect,
            snapshot.RateLimitHitsChat,
            snapshot.RateLimitHitsMovement,
            snapshot.PostgresErrors,
            snapshot.ActiveSessions);

        var path = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(snapshot, ServerOpsMetrics.JsonOptions);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write ops metrics file {Path}", path);
        }
    }

    private static TimeSpan ReadInterval()
    {
        var raw = Environment.GetEnvironmentVariable(IntervalEnvironmentVariable);
        if (int.TryParse(raw, out var seconds) && seconds >= 5)
        {
            return TimeSpan.FromSeconds(Math.Min(seconds, 300));
        }

        return TimeSpan.FromSeconds(15);
    }
}
