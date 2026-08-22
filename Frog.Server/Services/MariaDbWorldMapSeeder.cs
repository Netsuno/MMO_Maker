using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Server.Config;
using Frog.Server.Database;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Services;

/// <summary>
/// Si MariaDB est activé et <see cref="WorldMapOptions.DatabaseFallbackMapId"/> &gt; 0, insère la carte « Starter Meadow »
/// dans <c>frog_map</c> lorsque la ligne est absente (premier déploiement).
/// </summary>
public sealed class MariaDbWorldMapSeeder(
    IOptions<MariaDbOptions> mariaOptions,
    IOptions<WorldMapOptions> mapOptions,
    IMapBlobStore mapBlobStore,
    ILogger<MariaDbWorldMapSeeder> logger) : IHostedService
{
    private readonly MariaDbOptions _maria = mariaOptions.Value;
    private readonly WorldMapOptions _maps = mapOptions.Value;
    private readonly IMapBlobStore _mapBlobStore = mapBlobStore;
    private readonly ILogger<MariaDbWorldMapSeeder> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_maria.Enabled || string.IsNullOrWhiteSpace(_maria.ConnectionString))
        {
            return Task.CompletedTask;
        }

        var mapId = _maps.DatabaseFallbackMapId;
        if (mapId <= 0)
        {
            return Task.CompletedTask;
        }

        if (_mapBlobStore.TryGetHead(mapId, out _, out _))
        {
            return Task.CompletedTask;
        }

        var serializer = new MapSerializer();
        var bytes = serializer.Serialize(MapSamples.StarterMeadow(MapSamples.RuntimeMapIdToGuid(mapId)));
        MariaDbMapBlobStore.UpsertMap(_maria.ConnectionString, mapId, "world", "Starter Meadow", bytes);
        _logger.LogInformation("Carte monde id={MapId} inseree dans frog_map (seed automatique).", mapId);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
