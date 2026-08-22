using Frog.Application.Maps;
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
        if (request.LegacyId < 0)
        {
            return new SaveMapResult.ValidationFailed("LegacyId doit être >= 0.");
        }

        if (!request.Map.Validate(out var error))
        {
            return new SaveMapResult.ValidationFailed(error ?? "Carte invalide.");
        }

        var now = _clock.GetUtcNow();
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await _db.Maps
            .Include(m => m.Cells)
            .Include(m => m.Warps)
            .Include(m => m.NpcSpawns)
            .SingleOrDefaultAsync(m => m.LegacyId == request.LegacyId, cancellationToken)
            .ConfigureAwait(false);

        long newRevision;
        if (existing is null)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveMapResult.Conflict(0);
            }

            var entity = MapPersistenceMapper.ToEntity(request, now);
            _db.Maps.Add(entity);
            newRevision = entity.Revision;
        }
        else
        {
            if (existing.Revision != request.ExpectedRevision)
            {
                return new SaveMapResult.Conflict(existing.Revision);
            }

            existing.Status = request.Status;
            existing.Revision++;
            MapPersistenceMapper.ReplaceChildren(existing, request.Map, now);
            newRevision = existing.Revision;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (TestBeforeCommitAsync is not null)
        {
            await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SaveMapResult.Success(newRevision);
    }

    public async Task<StoredMap?> LoadByLegacyIdAsync(int legacyId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Maps
            .AsNoTracking()
            .Include(m => m.Cells)
            .Include(m => m.Warps)
            .SingleOrDefaultAsync(m => m.LegacyId == legacyId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        return new StoredMap
        {
            LegacyId = entity.LegacyId,
            Map = MapPersistenceMapper.ToDomain(entity),
            Revision = entity.Revision,
            Status = entity.Status,
        };
    }
}
