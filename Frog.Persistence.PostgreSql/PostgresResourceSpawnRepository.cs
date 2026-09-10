using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresResourceSpawnRepository :
    IResourceSpawnRepository,
    IPublishedResourceSpawnCatalog,
    IResourceSpawnReferenceCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly IMapRepository _maps;
    private readonly IPublishedResourceCatalog _resources;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresResourceSpawnRepository(
        FrogDbContextGate gate,
        IMapRepository? maps = null,
        IPublishedResourceCatalog? resources = null,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _maps = maps ?? new PostgresMapRepository(gate);
        _resources = resources ?? new PostgresResourceRepository(gate);
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveResourceSpawnResult> SaveAsync(
        SaveResourceSpawnRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveResourceSpawnResult>(async (db, ct) =>
        {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                error ?? "Spawn de ressource invalide.");
        }

        if (await _maps.LoadByIdAsync(request.Definition.MapId, ct)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                $"La carte {request.Definition.MapId:N} doit exister.");
        }

        if (await _resources.LoadPublishedByIdAsync(
                request.Definition.ResourceId,
                ct).ConfigureAwait(false) is null)
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                $"La ressource {request.Definition.ResourceId:N} doit être publiée.");
        }

        if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return new SaveResourceSpawnResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
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

    private async Task<SaveResourceSpawnResult> SaveCoreAsync(FrogDbContext db, SaveResourceSpawnRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.SpawnId is not Guid spawnId || spawnId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveResourceSpawnResult.Conflict(0);
                }

                savedId = request.Definition.Id == Guid.Empty
                    ? Guid.NewGuid()
                    : request.Definition.Id;
                var entity = ToEntity(request.Definition, savedId, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.ResourceSpawns.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.ResourceSpawns
                    .Where(spawn =>
                        spawn.Id == spawnId
                        && spawn.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(spawn => spawn.Revision, request.ExpectedRevision + 1)
                            .SetProperty(spawn => spawn.MapId, request.Definition.MapId)
                            .SetProperty(spawn => spawn.ResourceId, request.Definition.ResourceId)
                            .SetProperty(spawn => spawn.TileX, request.Definition.TileX)
                            .SetProperty(spawn => spawn.TileY, request.Definition.TileY)
                            .SetProperty(spawn => spawn.Status, ContentPublishStatus.Draft)
                            .SetProperty(spawn => spawn.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveResourceSpawnResult.Conflict(
                        await ReadRevisionAsync(db, spawnId, cancellationToken).ConfigureAwait(false));
                }

                savedId = spawnId;
                newRevision = request.ExpectedRevision + 1;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.ResourceSpawns.AsNoTracking()
                        .FirstAsync(spawn => spawn.Id == spawnId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, entity, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            if (TestBeforeCommitAsync is not null)
            {
                await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveResourceSpawnResult.Success(
                newRevision,
                savedId,
                publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveResourceSpawnResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(FrogDbContext db, ResourceSpawnEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ResourceSpawnPublishedSnapshots.Add(new ResourceSpawnPublishedSnapshotEntity
        {
            Id = snapshotId,
            SpawnId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            MapId = entity.MapId,
            ResourceId = entity.ResourceId,
            TileX = entity.TileX,
            TileY = entity.TileY,
        });
        db.ResourceSpawnPublicationHistory.Add(new ResourceSpawnPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            SpawnId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.ResourceSpawns
            .Where(spawn => spawn.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(spawn => spawn.Status, ContentPublishStatus.Published)
                    .SetProperty(spawn => spawn.PublishedRevision, entity.Revision)
                    .SetProperty(spawn => spawn.PublishedSnapshotId, snapshotId)
                    .SetProperty(spawn => spawn.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredResourceSpawn?> LoadByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredResourceSpawn?>(async (db, ct) =>
        {
        var entity = await db.ResourceSpawns.AsNoTracking()
            .FirstOrDefaultAsync(spawn => spawn.Id == spawnId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredResourceSpawn?> LoadPublishedByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredResourceSpawn?>(async (db, ct) =>
        {
        var snapshotId = await db.ResourceSpawns.AsNoTracking()
            .Where(spawn => spawn.Id == spawnId)
            .Select(spawn => spawn.PublishedSnapshotId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (snapshotId is not Guid id)
        {
            return null;
        }

        var snapshot = await db.ResourceSpawnPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(spawn => spawn.Id == id, ct)
            .ConfigureAwait(false);
        return snapshot is null
            ? null
            : new StoredResourceSpawn
            {
                SpawnId = snapshot.SpawnId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResourceSpawnCatalogEntry>> ListSummariesAsync(
        Guid? mapId = null,
        Guid? resourceId = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ResourceSpawnCatalogEntry>>(async (db, ct) =>
        {
        var query = db.ResourceSpawns.AsNoTracking().AsQueryable();
        if (mapId is Guid map)
        {
            query = query.Where(spawn => spawn.MapId == map);
        }

        if (resourceId is Guid resource)
        {
            query = query.Where(spawn => spawn.ResourceId == resource);
        }

        if (statusFilter is { } status)
        {
            query = query.Where(spawn => spawn.Status == status);
        }

        return await query
            .OrderBy(spawn => spawn.MapId)
            .ThenBy(spawn => spawn.TileY)
            .ThenBy(spawn => spawn.TileX)
            .Select(spawn => new ResourceSpawnCatalogEntry
            {
                SpawnId = spawn.Id,
                MapId = spawn.MapId,
                ResourceId = spawn.ResourceId,
                TileX = spawn.TileX,
                TileY = spawn.TileY,
                Revision = spawn.Revision,
                Status = spawn.Status,
                PublishedRevision = spawn.PublishedRevision,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteResourceSpawnResult> DeleteAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteResourceSpawnResult>(async (db, ct) =>
        {
        if (!await db.ResourceSpawns.AsNoTracking()
                .AnyAsync(spawn => spawn.Id == spawnId, ct)
                .ConfigureAwait(false))
        {
            return new DeleteResourceSpawnResult.NotFound();
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(ct)
            .ConfigureAwait(false);
        try
        {
            await db.ResourceSpawnPublicationHistory
                .Where(history => history.SpawnId == spawnId)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db.ResourceSpawnPublishedSnapshots
                .Where(snapshot => snapshot.SpawnId == spawnId)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await db.ResourceSpawns.Where(spawn => spawn.Id == spawnId)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new DeleteResourceSpawnResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteResourceSpawnResult.PersistenceFailed(Sanitize(ex.Message));
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResourceSpawnDefinition>> ListPublishedAsync(
        Guid? mapId = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ResourceSpawnDefinition>>(async (db, ct) =>
        {
        var tipsQuery = db.ResourceSpawns.AsNoTracking()
            .Where(spawn => spawn.PublishedSnapshotId != null);
        if (mapId is Guid map)
        {
            tipsQuery = tipsQuery.Where(spawn => spawn.MapId == map);
        }

        var tips = await tipsQuery
            .Select(spawn => spawn.PublishedSnapshotId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (tips.Count == 0)
        {
            return Array.Empty<ResourceSpawnDefinition>();
        }

        var snapshots = await db.ResourceSpawnPublishedSnapshots.AsNoTracking()
            .Where(snapshot => tips.Contains(snapshot.Id))
            .OrderBy(snapshot => snapshot.MapId)
            .ThenBy(snapshot => snapshot.TileY)
            .ThenBy(snapshot => snapshot.TileX)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsResourceReferencedAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<bool>(async (db, ct) =>
            await db.ResourceSpawns.AsNoTracking()
                .AnyAsync(spawn => spawn.ResourceId == resourceId, ct)
                .ConfigureAwait(false)
            || await db.ResourceSpawnPublishedSnapshots.AsNoTracking()
                .AnyAsync(snapshot => snapshot.ResourceId == resourceId, ct)
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.ResourceSpawns.AsNoTracking()
            .Where(spawn => spawn.Id == id)
            .Select(spawn => (long?)spawn.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ResourceSpawnEntity ToEntity(
        ResourceSpawnDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        MapId = definition.MapId,
        ResourceId = definition.ResourceId,
        TileX = definition.TileX,
        TileY = definition.TileY,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredResourceSpawn ToStored(ResourceSpawnEntity entity) => new()
    {
        SpawnId = entity.Id,
        Definition = new ResourceSpawnDefinition
        {
            Id = entity.Id,
            MapId = entity.MapId,
            ResourceId = entity.ResourceId,
            TileX = entity.TileX,
            TileY = entity.TileY,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ResourceSpawnDefinition FromSnapshot(
        ResourceSpawnPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.SpawnId,
        MapId = snapshot.MapId,
        ResourceId = snapshot.ResourceId,
        TileX = snapshot.TileX,
        TileY = snapshot.TileY,
    };

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance spawn de ressource.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
