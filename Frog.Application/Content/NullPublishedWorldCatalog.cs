using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Catalogue monde vide (in-memory / playtest sans PG).</summary>
public sealed class NullPublishedWorldCatalog : IPublishedWorldCatalog
{
    public static readonly NullPublishedWorldCatalog Instance = new();

    private NullPublishedWorldCatalog()
    {
    }

    public Task<IReadOnlyList<PublishedMapRuntimeEntry>> ListPublishedMapsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PublishedMapRuntimeEntry>>(Array.Empty<PublishedMapRuntimeEntry>());

    public Task<PublishedMapRuntimeEntry?> LoadPublishedMapAsync(
        Guid mapId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PublishedMapRuntimeEntry?>(null);

    public Task<PublishedWorldSpawnConfig> GetSpawnConfigAsync(CancellationToken cancellationToken = default)
        => Task.FromException<PublishedWorldSpawnConfig>(
            new InvalidOperationException("No published world catalog (in-memory/null)."));

    public Task<IReadOnlyList<PublishedMonsterSpawnEntry>> ListMonsterSpawnsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PublishedMonsterSpawnEntry>>(Array.Empty<PublishedMonsterSpawnEntry>());

    public bool TryGetRuntimeMapId(Guid mapId, out int runtimeMapId)
    {
        runtimeMapId = 0;
        return false;
    }

    public bool TryGetMapGuid(int runtimeMapId, out Guid mapId)
    {
        mapId = Guid.Empty;
        return false;
    }
}
