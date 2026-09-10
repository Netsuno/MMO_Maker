using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Services;

/// <summary>
/// Charge le monde publié (cartes + spawns monstres) au démarrage production PostgreSQL.
/// Échoue si aucune carte publiée / spawn config manquante.
/// </summary>
public sealed class PublishedWorldBootstrapHostedService(
    IPublishedWorldCatalog world,
    PublishedWorldMapBlobStore blobStore,
    MapService mapService,
    CombatGameplayService combat,
    IOptions<Phase7ContentOptions> contentOptions,
    IOptions<WorldMapOptions> worldMapOptions,
    ILogger<PublishedWorldBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var content = contentOptions.Value;
        if (!content.RequirePublishedWorld)
        {
            return;
        }

        var maps = await world.ListPublishedMapsAsync(cancellationToken).ConfigureAwait(false);
        if (maps.Count == 0)
        {
            throw new InvalidOperationException(
                "PostgreSQL production requires at least one published map. "
                + "Publish a Phase 6 map and configure world_spawn_settings before starting the server.");
        }

        blobStore.ReplaceAll(maps);
        mapService.LoadPublishedWorld(maps, world);

        var spawn = await world.GetSpawnConfigAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Published world loaded maps={Count} startRuntime={Start} respawnRuntime={Respawn}",
            maps.Count,
            spawn.StartRuntimeMapId,
            spawn.RespawnRuntimeMapId);

        var monsters = await world.ListMonsterSpawnsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var m in monsters)
        {
            var (px, py) = WorldMetrics.TileCenterToPixels(m.TileX, m.TileY);
            var spawned = await combat.SpawnMonsterAsync(m.RuntimeMapId, m.NpcId, px, py, cancellationToken)
                .ConfigureAwait(false);
            if (spawned is null)
            {
                throw new InvalidOperationException(
                    $"Failed to spawn published monster {m.NpcId} on map runtime {m.RuntimeMapId}.");
            }
        }

        logger.LogInformation("Published monster spawns loaded count={Count}", monsters.Count);
        _ = worldMapOptions;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
