using System.Collections.Concurrent;
using System.Security.Cryptography;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

/// <summary>Charge cartes/spawns publiés + liaisons runtime depuis PostgreSQL.</summary>
public sealed class PostgresPublishedWorldCatalog : IPublishedWorldCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly MapSerializer _serializer = new();
    private readonly ConcurrentDictionary<Guid, int> _guidToRuntime = new();
    private readonly ConcurrentDictionary<int, Guid> _runtimeToGuid = new();

    public PostgresPublishedWorldCatalog(FrogDbContextGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public async Task<IReadOnlyList<PublishedMapRuntimeEntry>> ListPublishedMapsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<PublishedMapRuntimeEntry>>(async (db, ct) =>
        {
            await EnsureBindingsLoadedAsync(db, ct).ConfigureAwait(false);

            var drafts = await db.Maps.AsNoTracking()
                .Where(m => m.PublishedSnapshotId != null)
                .Select(m => new { m.Id, m.PublishedSnapshotId, m.PublishedRevision })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var list = new List<PublishedMapRuntimeEntry>(drafts.Count);
            foreach (var d in drafts)
            {
                var entry = await LoadEntryCoreAsync(db, d.Id, d.PublishedSnapshotId!.Value, d.PublishedRevision, ct)
                    .ConfigureAwait(false);
                if (entry is not null)
                {
                    list.Add(entry);
                }
            }

            return list;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<PublishedMapRuntimeEntry?> LoadPublishedMapAsync(
        Guid mapId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<PublishedMapRuntimeEntry?>(async (db, ct) =>
        {
            await EnsureBindingsLoadedAsync(db, ct).ConfigureAwait(false);
            var draft = await db.Maps.AsNoTracking()
                .Where(m => m.Id == mapId && m.PublishedSnapshotId != null)
                .Select(m => new { m.Id, m.PublishedSnapshotId, m.PublishedRevision })
                .SingleOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (draft is null)
            {
                return null;
            }

            return await LoadEntryCoreAsync(db, draft.Id, draft.PublishedSnapshotId!.Value, draft.PublishedRevision, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    public Task<PublishedWorldSpawnConfig> GetSpawnConfigAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await EnsureBindingsLoadedAsync(db, ct).ConfigureAwait(false);
            var settings = await db.WorldSpawnSettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == 1, ct)
                .ConfigureAwait(false);
            if (settings is null)
            {
                throw new InvalidOperationException(
                    "Published world spawn settings missing (world_spawn_settings). "
                    + "Publish at least one map and configure start/respawn before starting the server.");
            }

            if (!_guidToRuntime.TryGetValue(settings.StartMapId, out var startRuntime)
                || !_guidToRuntime.TryGetValue(settings.RespawnMapId, out var respawnRuntime))
            {
                throw new InvalidOperationException(
                    "World spawn settings reference unpublished or unbound maps.");
            }

            return new PublishedWorldSpawnConfig(
                settings.StartMapId,
                startRuntime,
                settings.StartTileX,
                settings.StartTileY,
                settings.RespawnMapId,
                respawnRuntime,
                settings.RespawnTileX,
                settings.RespawnTileY);
        }, cancellationToken);

    public Task<IReadOnlyList<PublishedMonsterSpawnEntry>> ListMonsterSpawnsAsync(
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<PublishedMonsterSpawnEntry>>(async (db, ct) =>
        {
            await EnsureBindingsLoadedAsync(db, ct).ConfigureAwait(false);
            var rows = await (
                    from m in db.Maps.AsNoTracking()
                    where m.PublishedSnapshotId != null
                    join s in db.MapPublishedNpcSpawns.AsNoTracking()
                        on m.PublishedSnapshotId equals s.SnapshotId
                    join n in db.Npcs.AsNoTracking() on s.NpcId equals n.Id
                    where n.Kind == NpcKind.Monster && n.PublishedSnapshotId != null
                    select new { m.Id, s.NpcId, s.X, s.Y, s.Direction })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var list = new List<PublishedMonsterSpawnEntry>(rows.Count);
            foreach (var r in rows)
            {
                if (!_guidToRuntime.TryGetValue(r.Id, out var runtime))
                {
                    continue;
                }

                list.Add(new PublishedMonsterSpawnEntry(r.Id, runtime, r.NpcId, r.X, r.Y, r.Direction));
            }

            return list;
        }, cancellationToken);

    public bool TryGetRuntimeMapId(Guid mapId, out int runtimeMapId)
        => _guidToRuntime.TryGetValue(mapId, out runtimeMapId);

    public bool TryGetMapGuid(int runtimeMapId, out Guid mapId)
        => _runtimeToGuid.TryGetValue(runtimeMapId, out mapId);

    private async Task EnsureBindingsLoadedAsync(FrogDbContext db, CancellationToken ct)
    {
        var bindings = await db.RuntimeMapBindings.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        _guidToRuntime.Clear();
        _runtimeToGuid.Clear();
        foreach (var b in bindings)
        {
            _guidToRuntime[b.MapId] = b.RuntimeMapId;
            _runtimeToGuid[b.RuntimeMapId] = b.MapId;
        }
    }

    private async Task<PublishedMapRuntimeEntry?> LoadEntryCoreAsync(
        FrogDbContext db,
        Guid mapId,
        Guid snapshotId,
        long? publishedRevision,
        CancellationToken ct)
    {
        var snapshot = await db.MapPublishedSnapshots.AsNoTracking()
            .Include(s => s.Cells)
            .Include(s => s.Warps)
            .SingleOrDefaultAsync(s => s.Id == snapshotId, ct)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        if (!_guidToRuntime.TryGetValue(mapId, out var runtimeId))
        {
            throw new InvalidOperationException(
                $"Published map {mapId} has no runtime_map_bindings row. Re-publish the map.");
        }

        var stored = MapPersistenceMapper.ToStoredFromSnapshot(snapshot, publishedRevision);
        var bytes = _serializer.Serialize(stored.Map);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new PublishedMapRuntimeEntry(
            mapId,
            runtimeId,
            publishedRevision ?? snapshot.Revision,
            stored.Map,
            bytes,
            sha);
    }
}
