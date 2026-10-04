using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Gameplay;

/// <summary>
/// Fait apparaître et réapparaître les monstres des zones publiées.
/// Chaque apparition passe par <see cref="CombatGameplayService.SpawnMonsterAsync"/> :
/// le client les voit comme les autres monstres (paquet de position, kind monstre).
/// Hello reste 11. La tuile monde reste <see cref="WorldMetrics.DefaultTileSizePixels"/> ;
/// la tuile de contenu reste <see cref="TileAssetMetrics.TargetTileSizePixels"/>.
/// </summary>
public sealed class MobSpawnZoneHostedService(
    IPublishedWorldCatalog world,
    CombatGameplayService combat,
    ILogger<MobSpawnZoneHostedService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly IPublishedWorldCatalog _world = world;
    private readonly CombatGameplayService _combat = combat;
    private readonly ILogger<MobSpawnZoneHostedService> _logger = logger;
    private readonly MobSpawnZoneKeeper _keeper = new(zone => MobSpawnZoneEdit.PickTile(zone, Random.Shared));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MaintainOnceAsync(DateTime.UtcNow, stoppingToken).ConfigureAwait(false);
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Mob spawn zone tick failed.");
                try
                {
                    await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task<int> MaintainOnceAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var zones = await _world.ListMobSpawnZonesAsync(cancellationToken).ConfigureAwait(false);
        var alive = AliveIds();
        var spawned = await MobSpawnZoneMaintenance.ApplyAsync(
            _keeper,
            zones,
            alive,
            utcNow,
            SpawnAsync,
            cancellationToken).ConfigureAwait(false);
        if (spawned > 0)
        {
            _logger.LogInformation("Mob spawn zones placed count={Count}", spawned);
        }

        return spawned;
    }

    private HashSet<Guid> AliveIds()
    {
        var alive = new HashSet<Guid>();
        foreach (var mapId in _combat.ListMonsterMapIds())
        {
            foreach (var monster in _combat.ListMonstersOnMap(mapId))
            {
                alive.Add(monster.InstanceId);
            }
        }

        return alive;
    }

    private async Task<Guid?> SpawnAsync(MobSpawnOrder order, CancellationToken cancellationToken)
    {
        var (px, py) = WorldMetrics.TileCenterToPixels(order.TileX, order.TileY);
        var spawned = await _combat.SpawnMonsterAsync(
            order.RuntimeMapId,
            order.MonsterId,
            px,
            py,
            cancellationToken).ConfigureAwait(false);
        return spawned?.InstanceId;
    }
}
