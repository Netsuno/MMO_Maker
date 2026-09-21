using Frog.Application.Prefabs;
using Frog.Core.Protocol;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

/// <summary>Agrège les paquets prefab des snapshots cartes publiés.</summary>
public sealed class PostgresPublishedPrefabCatalog : IPublishedPrefabCatalog
{
    private readonly FrogDbContextGate _gate;

    public PostgresPublishedPrefabCatalog(FrogDbContextGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public Task<PublishedPrefabCatalogBundle> LoadPublishedAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var rows = await (
                    from m in db.Maps.AsNoTracking()
                    where m.PublishedSnapshotId != null
                    join s in db.MapPublishedSnapshots.AsNoTracking()
                        on m.PublishedSnapshotId equals s.Id
                    select new { m.Id, s.Name, s.PrefabsJson })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var bindings = await db.RuntimeMapBindings.AsNoTracking()
                .ToDictionaryAsync(b => b.MapId, b => b.RuntimeMapId, ct)
                .ConfigureAwait(false);

            var prefabs = new Dictionary<string, PublishedPrefabWireEntry>(StringComparer.Ordinal);
            var maps = new List<PublishedPrefabMapWireEntry>();
            foreach (var row in rows)
            {
                var document = MapPersistenceMapper.DeserializePrefabs(row.PrefabsJson);
                if (document is null)
                {
                    continue;
                }

                var sprites = document.ToSpriteFiles();
                foreach (var entry in PublishedPrefabClientCoverage.ToWirePrefabs(document.Catalog, sprites))
                {
                    prefabs[entry.Id] = entry;
                }

                int? runtime = bindings.TryGetValue(row.Id, out var runtimeId) && runtimeId > 0
                    ? runtimeId
                    : null;
                maps.Add(PublishedPrefabClientCoverage.ToWireMap(
                    row.Id,
                    row.Name,
                    document.Placements,
                    runtime));
            }

            return new PublishedPrefabCatalogBundle
            {
                Prefabs = prefabs.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(),
                PrefabMaps = maps,
            };
        }, cancellationToken);
}
