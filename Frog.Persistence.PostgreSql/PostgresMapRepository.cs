using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresMapRepository : IMapRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresMapRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public MapRepositoryCapabilities Capabilities => MapRepositoryCapabilities.PostgreSql;

    public async Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveMapResult>(async (db, ct) =>
        {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Map.Validate(out var error))
        {
            return new SaveMapResult.ValidationFailed(error ?? "Carte invalide.");
        }

        var targetMaps = await BuildTargetMapIndexAsync(db, request, ct).ConfigureAwait(false);
        if (!MapWarpValidator.ValidateWarpTargets(request.Map, targetMaps, out var warpError))
        {
            return new SaveMapResult.ValidationFailed(warpError ?? "Warp invalide.");
        }

        if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return new SaveMapResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            return await SaveCoreAsync(db, request, ct).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveMapResult> SaveCoreAsync(FrogDbContext db, SaveMapRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.MapId is not Guid mapId || mapId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveMapResult.Conflict(0);
                }

                var entity = MapPersistenceMapper.ToEntity(request.Map, now);
                entity.Status = MapPublishStatus.Draft;
                db.Maps.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = entity.Revision;
                savedId = entity.Id;

                if (request.Intent == SaveMapIntent.Publish)
                {
                    publishedRevision = await PublishSnapshotAsync(db, entity, request.Map, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }

                db.ChangeTracker.Clear();
            }
            else
            {
                var updatedRows = await db.Maps
                    .Where(m => m.Id == mapId && m.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(m => m.Revision, request.ExpectedRevision + 1)
                            .SetProperty(m => m.Name, request.Map.Name)
                            .SetProperty(m => m.Width, request.Map.Width)
                            .SetProperty(m => m.Height, request.Map.Height)
                            .SetProperty(m => m.AllowPlayerOverlap, request.Map.AllowPlayerOverlap)
                            .SetProperty(m => m.Status, MapPublishStatus.Draft)
                            .SetProperty(m => m.UpdatedAtUtc, now)
                            .SetProperty(m => m.LayersCatalogJson, MapPersistenceMapper.SerializeLayersCatalog(request.Map)),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveMapResult.Conflict(
                        await ReadCurrentRevisionAsync(db, mapId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = mapId;

                var children = MapPersistenceMapper.BuildChildren(mapId, request.Map);
                IReadOnlyList<MapNpcSpawnEntity> npcSpawnsToWrite = children.NpcSpawns;
                if (children.NpcSpawns.Count == 0)
                {
                    npcSpawnsToWrite = await db.MapNpcSpawns.AsNoTracking()
                        .Where(n => n.MapId == mapId)
                        .Select(n => new MapNpcSpawnEntity
                        {
                            Id = n.Id,
                            MapId = n.MapId,
                            NpcDefinitionId = n.NpcDefinitionId,
                            NpcId = n.NpcId,
                            X = n.X,
                            Y = n.Y,
                            Direction = n.Direction,
                        })
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                await db.MapCells.Where(c => c.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await db.MapWarps.Where(w => w.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await db.MapNpcSpawns.Where(n => n.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var entry in db.ChangeTracker.Entries<MapNpcSpawnEntity>()
                             .Where(e => e.Entity.MapId == mapId)
                             .ToList())
                {
                    entry.State = EntityState.Detached;
                }

                foreach (var warp in children.Warps)
                {
                    warp.TargetMap = null;
                }

                db.MapCells.AddRange(children.Cells);
                db.MapWarps.AddRange(children.Warps);
                db.MapNpcSpawns.AddRange(npcSpawnsToWrite);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                db.ChangeTracker.Clear();

                if (request.Intent == SaveMapIntent.Publish)
                {
                    var draft = await db.Maps.SingleAsync(m => m.Id == mapId, cancellationToken).ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, draft, request.Map, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = await db.Maps.AsNoTracking()
                        .Where(m => m.Id == mapId)
                        .Select(m => m.PublishedRevision)
                        .SingleAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                db.ChangeTracker.Clear();
            }

            if (TestBeforeCommitAsync is not null)
            {
                try
                {
                    await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return new SaveMapResult.PersistenceFailed(ex.Message);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SaveMapResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (request.MapId is Guid id && id != Guid.Empty)
            {
                return new SaveMapResult.Conflict(
                    await ReadCurrentRevisionAsync(db, id, cancellationToken).ConfigureAwait(false));
            }

            return new SaveMapResult.Conflict(0);
        }
        catch (DbUpdateException ex)
        {
            return new SaveMapResult.PersistenceFailed(ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SaveMapResult.PersistenceFailed(ex.Message);
        }
    }

    private async Task<Dictionary<Guid, (int Width, int Height)>> BuildTargetMapIndexAsync(FrogDbContext db, SaveMapRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await db.Maps.AsNoTracking()
            .Select(m => new { m.Id, m.Width, m.Height })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dict = rows.ToDictionary(r => r.Id, r => (r.Width, r.Height));
        if (request.MapId is Guid mapId && mapId != Guid.Empty)
        {
            dict[mapId] = (request.Map.Width, request.Map.Height);
        }

        return dict;
    }

    private async Task<long> PublishSnapshotAsync(FrogDbContext db, MapEntity draft,
        Map map,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = MapPersistenceMapper.ToPublishedSnapshot(draft, map, nowUtc);

        // Copy draft NPC spawns into the immutable snapshot, resolving editor aliases → Guid.
        var draftSpawns = await db.MapNpcSpawns.AsNoTracking()
            .Where(s => s.MapId == draft.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (draftSpawns.Count > 0)
        {
            var aliasIds = draftSpawns.Select(s => s.NpcDefinitionId).Distinct().ToArray();
            var npcsByAlias = await db.Npcs.AsNoTracking()
                .Where(n => n.EditorAliasId != null && aliasIds.Contains(n.EditorAliasId.Value))
                .ToDictionaryAsync(n => n.EditorAliasId!.Value, cancellationToken)
                .ConfigureAwait(false);

            foreach (var spawn in draftSpawns)
            {
                Guid npcId;
                if (spawn.NpcId != Guid.Empty)
                {
                    npcId = spawn.NpcId;
                }
                else if (npcsByAlias.TryGetValue(spawn.NpcDefinitionId, out var fromAlias))
                {
                    npcId = fromAlias.Id;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot publish map '{draft.Name}': NPC spawn at ({spawn.X},{spawn.Y}) has no published NPC.");
                }

                var isPublished = await db.Npcs.AsNoTracking()
                    .AnyAsync(n => n.Id == npcId && n.PublishedSnapshotId != null, cancellationToken)
                    .ConfigureAwait(false);
                if (!isPublished)
                {
                    throw new InvalidOperationException(
                        $"Cannot publish map '{draft.Name}': NPC {npcId} is not published.");
                }

                snapshot.NpcSpawns.Add(new MapPublishedNpcSpawnEntity
                {
                    Id = Guid.NewGuid(),
                    SnapshotId = snapshot.Id,
                    NpcId = npcId,
                    X = spawn.X,
                    Y = spawn.Y,
                    Direction = spawn.Direction,
                });
            }
        }

        db.MapPublishedSnapshots.Add(snapshot);
        db.MapPublicationHistory.Add(new MapPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            MapId = draft.Id,
            SnapshotId = snapshot.Id,
            Revision = draft.Revision,
            PublishedAtUtc = nowUtc,
        });

        draft.PublishedRevision = draft.Revision;
        draft.PublishedSnapshotId = snapshot.Id;
        draft.Status = MapPublishStatus.Published;

        // Ensure durable runtime binding exists for this map Guid.
        if (!await db.RuntimeMapBindings.AnyAsync(b => b.MapId == draft.Id, cancellationToken).ConfigureAwait(false))
        {
            var nextId = await db.RuntimeMapBindings.MaxAsync(b => (int?)b.RuntimeMapId, cancellationToken)
                .ConfigureAwait(false) ?? 0;
            db.RuntimeMapBindings.Add(new RuntimeMapBindingEntity
            {
                MapId = draft.Id,
                RuntimeMapId = nextId + 1,
                CreatedAtUtc = nowUtc,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return draft.Revision;
    }

    private async Task<long> ReadCurrentRevisionAsync(FrogDbContext db, Guid mapId, CancellationToken cancellationToken)
    {
        return await db.Maps.AsNoTracking()
            .Where(m => m.Id == mapId)
            .Select(m => m.Revision)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredMap?>(async (db, ct) =>
        {
        if (mapId == Guid.Empty)
        {
            return null;
        }

        var entity = await db.Maps
            .AsNoTracking()
            .Include(m => m.Cells)
            .Include(m => m.Warps)
            .SingleOrDefaultAsync(m => m.Id == mapId, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        return ToStored(entity);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredMap?>(async (db, ct) =>
        {
        if (mapId == Guid.Empty)
        {
            return null;
        }

        var draft = await db.Maps.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == mapId, ct)
            .ConfigureAwait(false);
        if (draft?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await db.MapPublishedSnapshots
            .AsNoTracking()
            .Include(s => s.Cells)
            .Include(s => s.Warps)
            .SingleOrDefaultAsync(s => s.Id == snapshotId, ct)
            .ConfigureAwait(false);

        return snapshot is null ? null : MapPersistenceMapper.ToStoredFromSnapshot(snapshot, draft.PublishedRevision);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredMap?> LoadPublishedByIdAndRevisionAsync(
        Guid mapId,
        long publishedRevision,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredMap?>(async (db, ct) =>
        {
        if (mapId == Guid.Empty || publishedRevision <= 0)
        {
            return null;
        }

        var snapshot = await db.MapPublishedSnapshots
            .AsNoTracking()
            .Include(s => s.Cells)
            .Include(s => s.Warps)
            .SingleOrDefaultAsync(s => s.MapId == mapId && s.Revision == publishedRevision, ct)
            .ConfigureAwait(false);

        return snapshot is null ? null : MapPersistenceMapper.ToStoredFromSnapshot(snapshot, publishedRevision);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<MapCatalogEntry>>(async (db, ct) =>
        {
        return await db.Maps
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ThenBy(m => m.Id)
            .Select(m => new MapCatalogEntry
            {
                MapId = m.Id,
                Name = m.Name,
                Width = m.Width,
                Height = m.Height,
                Revision = m.Revision,
                Status = m.PublishedRevision != null ? MapPublishStatus.Published : MapPublishStatus.Draft,
                PublishedRevision = m.PublishedRevision,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(
        Guid mapId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<MapPublicationRecord>>(async (db, ct) =>
        {
        return await db.MapPublicationHistory
            .AsNoTracking()
            .Where(h => h.MapId == mapId)
            .OrderByDescending(h => h.PublishedAtUtc)
            .Select(h => new MapPublicationRecord
            {
                MapId = h.MapId,
                Revision = h.Revision,
                PublishedAtUtc = h.PublishedAtUtc,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private static StoredMap ToStored(MapEntity entity)
        => new()
        {
            MapId = entity.Id,
            Map = MapPersistenceMapper.ToDomain(entity),
            Revision = entity.Revision,
            Status = entity.PublishedRevision is not null ? MapPublishStatus.Published : MapPublishStatus.Draft,
            PublishedRevision = entity.PublishedRevision,
        };
}
