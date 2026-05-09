using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Frog.Server.Config;
using Frog.Server.Persistence;

namespace Frog.Server.Services;

public sealed class PlayerPersistenceService(
    IOptions<PersistenceOptions> options,
    ConnectionManager connectionManager,
    IPlayerStateStore playerStateStore,
    ILogger<PlayerPersistenceService> logger) : BackgroundService
{
    private readonly PersistenceOptions _options = options.Value;
    private readonly ConnectionManager _connectionManager = connectionManager;
    private readonly IPlayerStateStore _playerStateStore = playerStateStore;
    private readonly ILogger<PlayerPersistenceService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        var interval = TimeSpan.FromSeconds(_options.SaveIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                var n = 0;
                foreach (var session in _connectionManager.GetActiveSessions())
                {
                    if (string.IsNullOrWhiteSpace(session.CharacterId))
                    {
                        continue;
                    }

                    _playerStateStore.UpsertForCharacter(
                        session.CharacterId,
                        session.CurrentMapId,
                        session.PixelX,
                        session.PixelY);
                    n++;
                }

                if (n > 0)
                {
                    _logger.LogInformation("Sauvegarde periodique: {Count} joueur(s).", n);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
