using Frog.Application.Gameplay;
using Frog.Server.Network;
using Frog.Server.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Gameplay;

/// <summary>
/// Balaye les piles expirées (TTL 3 min) et repousse le snapshot aux observateurs.
/// Le despawn visible suit le balayage (15 s), pas une horloge par objet.
/// </summary>
public sealed class GroundLootExpiryHostedService(
    IGroundItemRepository groundItems,
    ConnectionManager connections,
    GroundItemObserverNotifier notifier,
    ILogger<GroundLootExpiryHostedService> logger) : BackgroundService
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);

    private readonly IGroundItemRepository _groundItems = groundItems;
    private readonly ConnectionManager _connections = connections;
    private readonly GroundItemObserverNotifier _notifier = notifier;
    private readonly ILogger<GroundLootExpiryHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
                var maps = _connections.GetActiveSessions().Select(session => session.CurrentMapId).Distinct().ToArray();
                foreach (var mapId in maps)
                {
                    var removed = await _groundItems.PurgeExpiredAsync(mapId, stoppingToken).ConfigureAwait(false);
                    if (removed > 0)
                    {
                        await _notifier.BroadcastAsync(mapId, stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ground loot expiry sweep failed.");
            }
        }
    }
}
