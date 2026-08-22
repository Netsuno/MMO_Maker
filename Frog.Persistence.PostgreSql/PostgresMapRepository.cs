using Frog.Application.Maps;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresMapRepository : IMapRepository
{
    private readonly FrogDbContext _db;
    private readonly TimeProvider _clock;

    /// <summary>Hook de test uniquement : exécuté dans la transaction avant Commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresMapRepository(FrogDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Map.Validate(out var error))
        {
            return new SaveMapResult.ValidationFailed(error ?? "Carte invalide.");
        }

        var now = _clock.GetUtcNow();
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        MapEntity? existing = null;
        if (request.MapId is Guid mapId && mapId != Guid.Empty)
        {
            _db.ChangeTracker.Clear();
            existing = await _db.Maps
                .SingleOrDefaultAsync(m => m.Id == mapId, cancellationToken)
                .ConfigureAwait(false);
        }

        long newRevision;
        Guid savedId;
        if (existing is null)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveMapResult.Conflict(0);
            }

            var entity = MapPersistenceMapper.ToEntity(request, now);
            _db.Maps.Add(entity);
            newRevision = entity.Revision;
            savedId = entity.Id;
        }
        else
        {
            if (existing.Revision != request.ExpectedRevision)
            {
                return new SaveMapResult.Conflict(existing.Revision);
            }

            existing.Status = request.Status;
            existing.Revision++;
            MapPersistenceMapper.ApplyMapFields(existing, request.Map, now);
            await _db.MapCells.Where(c => c.MapId == existing.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.MapWarps.Where(w => w.MapId == existing.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.MapNpcSpawns.Where(n => n.MapId == existing.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var children = MapPersistenceMapper.BuildChildren(existing.Id, request.Map);
            foreach (var warp in children.Warps)
            {
                warp.TargetMap = null;
            }

            _db.MapCells.AddRange(children.Cells);
            _db.MapWarps.AddRange(children.Warps);
            _db.MapNpcSpawns.AddRange(children.NpcSpawns);

            newRevision = existing.Revision;
            savedId = existing.Id;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (TestBeforeCommitAsync is not null)
        {
            await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SaveMapResult.Success(newRevision, savedId);
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

        return new StoredMap
        {
            MapId = entity.Id,
            Map = MapPersistenceMapper.ToDomain(entity),
            Revision = entity.Revision,
            Status = entity.Status,
        };
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
                Status = m.Status,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
