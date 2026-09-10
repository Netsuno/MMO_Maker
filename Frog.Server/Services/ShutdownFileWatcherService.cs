using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Services;

/// <summary>
/// P7-G6: optional cross-platform graceful-shutdown trigger.
///
/// When the <see cref="ShutdownFileEnvironmentVariable"/> environment variable names a path,
/// this service polls for that file's existence and, as soon as it appears, requests an orderly
/// stop via <see cref="IHostApplicationLifetime.StopApplication"/> — the exact same shutdown
/// path <c>Host.CreateDefaultBuilder</c>'s <c>ConsoleLifetime</c> uses when it receives
/// SIGTERM (Linux/macOS) or Ctrl+C (Windows). This gives process supervisors and integration
/// tests that cannot reliably deliver a signal to this process (notably on Windows) a
/// deterministic way to request a graceful stop instead of killing the process.
///
/// When the variable is unset, this service is a no-op.
/// </summary>
public sealed class ShutdownFileWatcherService(
    IHostApplicationLifetime lifetime,
    ILogger<ShutdownFileWatcherService> logger) : BackgroundService
{
    public const string ShutdownFileEnvironmentVariable = "FROG_SHUTDOWN_FILE";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<ShutdownFileWatcherService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = Environment.GetEnvironmentVariable(ShutdownFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation(
                        "Shutdown sentinel {Path} detected; requesting graceful application stop.",
                        path);
                    _lifetime.StopApplication();
                    return;
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is already stopping through another path (e.g. SIGTERM); nothing left to signal.
        }
    }
}
