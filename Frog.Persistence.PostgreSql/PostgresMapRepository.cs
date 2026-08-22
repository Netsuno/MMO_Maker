using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresMapRepository : IMapRepository
{
    private readonly FrogDbContext _db;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresMapRepository(FrogDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public MapRepositoryCapabilities Capabilities => MapRepositoryCapabilities.PostgreSql;

    public async Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Map.Validate(out var error))
        {
            return new SaveMapResult.ValidationFailed(error ?? "Carte invalide.");
        }

        var targetMaps = await BuildTargetMapIndexAsync(request, cancellationToken).ConfigureAwait(false);
        if (!MapWarpValidator.ValidateWarpTargets(request.Map, targetMaps, out var warpError))
        {
            return new SaveMapResult.ValidationFailed(warpError ?? "Warp invalide.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveMapResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            return await SaveCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task<SaveMapResult> SaveCoreAsync(SaveMapRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

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
                _db.Maps.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = entity.Revision;
                savedId = entity.Id;

                if (request.Intent == SaveMapIntent.Publish)
                {
                    publishedRevision = await PublishSnapshotAsync(entity, request.Map, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }

                _db.ChangeTracker.Clear();
            }
            else
            {
                var updatedRows = await _db.Maps
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
                        await ReadCurrentRevisionAsync(mapId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = mapId;

                await _db.MapCells.Where(c => c.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await _db.MapWarps.Where(w => w.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await _db.MapNpcSpawns.Where(n => n.MapId == mapId).ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                var children = MapPersistenceMapper.BuildChildren(mapId, request.Map);
                foreach (var warp in children.Warps)
                {
                    warp.TargetMap = null;
                }

                _db.MapCells.AddRange(children.Cells);
                _db.MapWarps.AddRange(children.Warps);
                _db.MapNpcSpawns.AddRange(children.NpcSpawns);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _db.ChangeTracker.Clear();

                if (request.Intent == SaveMapIntent.Publish)
                {
                    var draft = await _db.Maps.SingleAsync(m => m.Id == mapId, cancellationToken).ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(draft, request.Map, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = await _db.Maps.AsNoTracking()
                        .Where(m => m.Id == mapId)
                        .Select(m => m.PublishedRevision)
                        .SingleAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                _db.ChangeTracker.Clear();
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
                    await ReadCurrentRevisionAsync(id, cancellationToken).ConfigureAwait(false));
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

    private async Task<Dictionary<Guid, (int Width, int Height)>> BuildTargetMapIndexAsync(
        SaveMapRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Maps.AsNoTracking()
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

    private async Task<long> PublishSnapshotAsync(
        MapEntity draft,
        Map map,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = MapPersistenceMapper.ToPublishedSnapshot(draft, map, nowUtc);
        _db.MapPublishedSnapshots.Add(snapshot);
        _db.MapPublicationHistory.Add(new MapPublicationHistoryEntity
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

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return draft.Revision;
    }

    private async Task<long> ReadCurrentRevisionAsync(Guid mapId, CancellationToken cancellationToken)
    {
        return await _db.Maps.AsNoTracking()
            .Where(m => m.Id == mapId)
            .Select(m => m.Revision)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        if (mapId == Guid.Empty)
        {
            return null;
        }

        var entity = await _db.Maps
            .AsNoTracking()
            .Include(m => m.Cells)
            .Include(m => m.Warps)
            .SingleOrDefaultAsync(m => m.Id == mapId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        return ToStored(entity);
    }

    public async Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        if (mapId == Guid.Empty)
        {
            return null;
        }

        var draft = await _db.Maps.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == mapId, cancellationToken)
            .ConfigureAwait(false);
        if (draft?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await _db.MapPublishedSnapshots
            .AsNoTracking()
            .Include(s => s.Cells)
            .Include(s => s.Warps)
            .SingleOrDefaultAsync(s => s.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);

        return snapshot is null ? null : MapPersistenceMapper.ToStoredFromSnapshot(snapshot, draft.PublishedRevision);
    }

    public async Task<StoredMap?> LoadPublishedByIdAndRevisionAsync(
        Guid mapId,
        long publishedRevision,
        CancellationToken cancellationToken = default)
    {
        if (mapId == Guid.Empty || publishedRevision <= 0)
        {
            return null;
        }

        var snapshot = await _db.MapPublishedSnapshots
            .AsNoTracking()
            .Include(s => s.Cells)
            .Include(s => s.Warps)
            .SingleOrDefaultAsync(s => s.MapId == mapId && s.Revision == publishedRevision, cancellationToken)
            .ConfigureAwait(false);

        return snapshot is null ? null : MapPersistenceMapper.ToStoredFromSnapshot(snapshot, publishedRevision);
    }

    public async Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Maps
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(
        Guid mapId,
        CancellationToken cancellationToken = default)
    {
        return await _db.MapPublicationHistory
            .AsNoTracking()
            .Where(h => h.MapId == mapId)
            .OrderByDescending(h => h.PublishedAtUtc)
            .Select(h => new MapPublicationRecord
            {
                MapId = h.MapId,
                Revision = h.Revision,
                PublishedAtUtc = h.PublishedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
