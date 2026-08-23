using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresResourceRepository :
    IResourceRepository,
    IPublishedResourceCatalog,
    IResourceItemReferenceCatalog
{
    private readonly FrogDbContext _db;
    private readonly IPublishedItemCatalog _items;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresResourceRepository(
        FrogDbContext db,
        IPublishedItemCatalog? items = null,
        TimeProvider? clock = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _items = items ?? new PostgresItemRepository(db);
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveResourceResult> SaveAsync(
        SaveResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return new SaveResourceResult.ValidationFailed(error ?? "Ressource invalide.");
        }

        if (await _items.LoadPublishedByIdAsync(request.Definition.YieldItemId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceResult.ValidationFailed(
                $"L’objet produit {request.Definition.YieldItemId:N} doit être publié.");
        }

        if (request.Definition.ToolItemId is Guid toolItemId
            && await _items.LoadPublishedByIdAsync(toolItemId, cancellationToken)
                .ConfigureAwait(false) is null)
        {
            return new SaveResourceResult.ValidationFailed(
                $"L’outil {toolItemId:N} doit exister dans le catalogue publié.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveResourceResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
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

    private async Task<SaveResourceResult> SaveCoreAsync(
        SaveResourceRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.ResourceId is not Guid resourceId || resourceId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveResourceResult.Conflict(0);
                }

                savedId = request.Definition.Id == Guid.Empty
                    ? Guid.NewGuid()
                    : request.Definition.Id;
                var entity = ToEntity(request.Definition, savedId, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                _db.Resources.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await _db.Resources
                    .Where(resource =>
                        resource.Id == resourceId
                        && resource.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                resource => resource.Revision,
                                request.ExpectedRevision + 1)
                            .SetProperty(
                                resource => resource.Name,
                                request.Definition.Name.Trim())
                            .SetProperty(
                                resource => resource.Description,
                                NormalizeDescription(request.Definition.Description))
                            .SetProperty(
                                resource => resource.SpriteLogicalPath,
                                NormalizePath(request.Definition.SpriteLogicalPath))
                            .SetProperty(
                                resource => resource.RespawnSeconds,
                                request.Definition.RespawnSeconds)
                            .SetProperty(
                                resource => resource.ToolItemId,
                                request.Definition.ToolItemId)
                            .SetProperty(
                                resource => resource.YieldItemId,
                                request.Definition.YieldItemId)
                            .SetProperty(
                                resource => resource.YieldQuantity,
                                request.Definition.YieldQuantity)
                            .SetProperty(
                                resource => resource.Status,
                                ContentPublishStatus.Draft)
                            .SetProperty(resource => resource.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveResourceResult.Conflict(
                        await ReadRevisionAsync(resourceId, cancellationToken).ConfigureAwait(false));
                }

                savedId = resourceId;
                newRevision = request.ExpectedRevision + 1;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await _db.Resources.AsNoTracking()
                        .FirstAsync(resource => resource.Id == resourceId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(entity, now, cancellationToken)
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
            _db.ChangeTracker.Clear();
            return new SaveResourceResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            return new SaveResourceResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        ResourceEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        _db.ResourcePublishedSnapshots.Add(new ResourcePublishedSnapshotEntity
        {
            Id = snapshotId,
            ResourceId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Description = entity.Description,
            SpriteLogicalPath = entity.SpriteLogicalPath,
            RespawnSeconds = entity.RespawnSeconds,
            ToolItemId = entity.ToolItemId,
            YieldItemId = entity.YieldItemId,
            YieldQuantity = entity.YieldQuantity,
        });
        _db.ResourcePublicationHistory.Add(new ResourcePublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ResourceId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await _db.Resources
            .Where(resource => resource.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(resource => resource.Status, ContentPublishStatus.Published)
                    .SetProperty(resource => resource.PublishedRevision, entity.Revision)
                    .SetProperty(resource => resource.PublishedSnapshotId, snapshotId)
                    .SetProperty(resource => resource.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredResource?> LoadByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Resources.AsNoTracking()
            .FirstOrDefaultAsync(resource => resource.Id == resourceId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    }

    public async Task<StoredResource?> LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var snapshotId = await _db.Resources.AsNoTracking()
            .Where(resource => resource.Id == resourceId)
            .Select(resource => resource.PublishedSnapshotId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (snapshotId is not Guid id)
        {
            return null;
        }

        var snapshot = await _db.ResourcePublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(resource => resource.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return snapshot is null
            ? null
            : new StoredResource
            {
                ResourceId = snapshot.ResourceId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
    }

    public async Task<IReadOnlyList<ResourceCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Resources.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(resource =>
                EF.Functions.ILike(resource.Name, $"%{value}%")
                || EF.Functions.ILike(resource.SpriteLogicalPath, $"%{value}%")
                || (resource.Description != null
                    && EF.Functions.ILike(resource.Description, $"%{value}%")));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(resource => resource.Status == status);
        }

        return await query
            .OrderBy(resource => resource.Name)
            .Select(resource => new ResourceCatalogEntry
            {
                ResourceId = resource.Id,
                Name = resource.Name,
                SpriteLogicalPath = resource.SpriteLogicalPath,
                RespawnSeconds = resource.RespawnSeconds,
                Revision = resource.Revision,
                Status = resource.Status,
                PublishedRevision = resource.PublishedRevision,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeleteResourceResult> DeleteAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Resources.AsNoTracking()
                .AnyAsync(resource => resource.Id == resourceId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new DeleteResourceResult.NotFound();
        }

        if (await IsResourceSpawnReferenceAsync(resourceId, cancellationToken).ConfigureAwait(false))
        {
            return new DeleteResourceResult.Referenced(
                "La ressource est référencée par un brouillon ou un snapshot publié de spawn.");
        }

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await _db.ResourcePublicationHistory
                .Where(history => history.ResourceId == resourceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await _db.ResourcePublishedSnapshots
                .Where(snapshot => snapshot.ResourceId == resourceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await _db.Resources.Where(resource => resource.Id == resourceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new DeleteResourceResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteResourceResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    public async Task<IReadOnlyList<ResourceDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var tips = await _db.Resources.AsNoTracking()
            .Where(resource => resource.PublishedSnapshotId != null)
            .Select(resource => resource.PublishedSnapshotId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tips.Count == 0)
        {
            return Array.Empty<ResourceDefinition>();
        }

        var snapshots = await _db.ResourcePublishedSnapshots.AsNoTracking()
            .Where(snapshot => tips.Contains(snapshot.Id))
            .OrderBy(snapshot => snapshot.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    }

    async Task<ResourceDefinition?> IPublishedResourceCatalog.LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var stored = await LoadPublishedByIdAsync(resourceId, cancellationToken)
            .ConfigureAwait(false);
        return stored?.Definition;
    }

    public async Task<bool> IsItemReferencedAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
        => await _db.Resources.AsNoTracking()
                .AnyAsync(
                    resource =>
                        resource.YieldItemId == itemId || resource.ToolItemId == itemId,
                    cancellationToken)
                .ConfigureAwait(false)
            || await _db.ResourcePublishedSnapshots.AsNoTracking()
                .AnyAsync(
                    snapshot =>
                        snapshot.YieldItemId == itemId || snapshot.ToolItemId == itemId,
                    cancellationToken)
                .ConfigureAwait(false);

    private async Task<bool> IsResourceSpawnReferenceAsync(
        Guid resourceId,
        CancellationToken cancellationToken)
        => await _db.ResourceSpawns.AsNoTracking()
                .AnyAsync(spawn => spawn.ResourceId == resourceId, cancellationToken)
                .ConfigureAwait(false)
            || await _db.ResourceSpawnPublishedSnapshots.AsNoTracking()
                .AnyAsync(snapshot => snapshot.ResourceId == resourceId, cancellationToken)
                .ConfigureAwait(false);

    private async Task<long> ReadRevisionAsync(Guid id, CancellationToken cancellationToken)
    {
        var revision = await _db.Resources.AsNoTracking()
            .Where(resource => resource.Id == id)
            .Select(resource => (long?)resource.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ResourceEntity ToEntity(
        ResourceDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Description = NormalizeDescription(definition.Description),
        SpriteLogicalPath = NormalizePath(definition.SpriteLogicalPath),
        RespawnSeconds = definition.RespawnSeconds,
        ToolItemId = definition.ToolItemId,
        YieldItemId = definition.YieldItemId,
        YieldQuantity = definition.YieldQuantity,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredResource ToStored(ResourceEntity entity) => new()
    {
        ResourceId = entity.Id,
        Definition = new ResourceDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            SpriteLogicalPath = entity.SpriteLogicalPath,
            RespawnSeconds = entity.RespawnSeconds,
            ToolItemId = entity.ToolItemId,
            YieldItemId = entity.YieldItemId,
            YieldQuantity = entity.YieldQuantity,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ResourceDefinition FromSnapshot(ResourcePublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.ResourceId,
        Name = snapshot.Name,
        Description = snapshot.Description,
        SpriteLogicalPath = snapshot.SpriteLogicalPath,
        RespawnSeconds = snapshot.RespawnSeconds,
        ToolItemId = snapshot.ToolItemId,
        YieldItemId = snapshot.YieldItemId,
        YieldQuantity = snapshot.YieldQuantity,
    };

    private static string NormalizePath(string value) => value.Trim().Replace('\\', '/');

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance ressource.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
