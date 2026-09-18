using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;

namespace Frog.Server.Services;

public sealed class SessionCleanupService(
    ConnectionManager connectionManager,
    SessionTeardown sessionTeardown,
    IOptions<SessionOptions> options,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly SessionTeardown _sessionTeardown = sessionTeardown;
    private readonly SessionOptions _options = options.Value;
    private readonly ILogger<SessionCleanupService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();

        var cleanupInterval = TimeSpan.FromSeconds(_options.CleanupIntervalSeconds);
        var idleTimeout = TimeSpan.FromSeconds(_options.IdleTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, stoppingToken);
                var now = DateTime.UtcNow;
                var expired = 0;
                foreach (var session in _connectionManager.GetActiveSessions())
                {
                    if (now - session.LastActivityUtc <= idleTimeout)
                    {
                        continue;
                    }

                    await _sessionTeardown
                        .TearDownAsync(session.Id, SessionTeardownOptions.IdleExpire, stoppingToken)
                        .ConfigureAwait(false);
                    expired++;
                }

                if (expired > 0)
                {
                    _logger.LogInformation("{Count} session(s) inactives expirees.", expired);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
