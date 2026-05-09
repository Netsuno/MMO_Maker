using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;
using Frog.Server.Network;
using Frog.Server.Persistence;

namespace Frog.Server.Services;

public sealed class SessionCleanupService(
    ConnectionManager connectionManager,
    ClientRegistry clientRegistry,
    PlayerLifecycleNotifier playerLifecycleNotifier,
    IPlayerStateStore playerStateStore,
    IOptions<SessionOptions> options,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly ClientRegistry _clientRegistry = clientRegistry;
    private readonly PlayerLifecycleNotifier _playerLifecycleNotifier = playerLifecycleNotifier;
    private readonly IPlayerStateStore _playerStateStore = playerStateStore;
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
                var expiredSessions = _connectionManager.RemoveExpiredSessions(idleTimeout);
                if (expiredSessions.Count == 0)
                {
                    continue;
                }

                foreach (var session in expiredSessions)
                {
                    if (!string.IsNullOrWhiteSpace(session.CharacterId))
                    {
                        _playerStateStore.UpsertForCharacter(
                            session.CharacterId,
                            session.CurrentMapId,
                            session.PixelX,
                            session.PixelY);
                    }
                    ClientSession? client = null;
                    if (_clientRegistry.TryGet(session.Id, out client))
                    {
                        _clientRegistry.Unregister(session.Id);
                    }

                    await _playerLifecycleNotifier.NotifyPlayerLeftAsync(session.Username, stoppingToken);

                    if (client is not null)
                    {
                        client.AuthenticatedSession = null;
                        client.Disconnect();
                    }
                }

                _logger.LogInformation("{Count} session(s) inactives expirees.", expiredSessions.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
