using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresItemRepository : IItemRepository, IPublishedItemCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresItemRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveItemResult> SaveAsync(
        SaveItemRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveItemResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Definition.Validate(out var error))
            {
                return new SaveItemResult.ValidationFailed(error ?? "Objet invalide.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveItemResult.ValidationFailed(
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

    private async Task<SaveItemResult> SaveCoreAsync(
        FrogDbContext db,
        SaveItemRequest request,
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

            if (request.ItemId is not Guid itemId || itemId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveItemResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.Items.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.Items
                    .Where(i => i.Id == itemId && i.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(i => i.Revision, request.ExpectedRevision + 1)
                            .SetProperty(i => i.Name, request.Definition.Name.Trim())
                            .SetProperty(i => i.Kind, request.Definition.Kind)
                            .SetProperty(
                                i => i.IconLogicalPath,
                                request.Definition.IconLogicalPath.Trim().Replace('\\', '/'))
                            .SetProperty(i => i.MaxStack, request.Definition.MaxStack)
                            .SetProperty(i => i.BuyPrice, request.Definition.BuyPrice)
                            .SetProperty(i => i.SellPrice, request.Definition.SellPrice)
                            .SetProperty(
                                i => i.Description,
                                NormalizeDescription(request.Definition.Description))
                            .SetProperty(i => i.Status, ContentPublishStatus.Draft)
                            .SetProperty(i => i.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveItemResult.Conflict(
                        await ReadRevisionAsync(db, itemId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = itemId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.Items.AsNoTracking()
                        .FirstAsync(i => i.Id == itemId, cancellationToken)
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
            return new SaveItemResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveItemResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        ItemEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ItemPublishedSnapshots.Add(new ItemPublishedSnapshotEntity
        {
            Id = snapshotId,
            ItemId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Kind = entity.Kind,
            IconLogicalPath = entity.IconLogicalPath,
            MaxStack = entity.MaxStack,
            BuyPrice = entity.BuyPrice,
            SellPrice = entity.SellPrice,
            Description = entity.Description,
        });
        db.ItemPublicationHistory.Add(new ItemPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ItemId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.Items
            .Where(i => i.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Status, ContentPublishStatus.Published)
                    .SetProperty(i => i.PublishedRevision, entity.Revision)
                    .SetProperty(i => i.PublishedSnapshotId, snapshotId)
                    .SetProperty(i => i.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredItem?> LoadByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredItem?>(async (db, ct) =>
        {
            var entity = await db.Items.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == itemId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredItem?> LoadPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredItem?>(async (db, ct) =>
        {
            var tip = await db.Items.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == itemId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.ItemPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredItem
            {
                ItemId = snapshot.ItemId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ItemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ItemCatalogEntry>>(async (db, ct) =>
        {
            var query = db.Items.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(i =>
                    EF.Functions.ILike(i.Name, $"%{value}%")
                    || EF.Functions.ILike(i.IconLogicalPath, $"%{value}%")
                    || (i.Description != null && EF.Functions.ILike(i.Description, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(i => i.Status == status);
            }

            return await query
                .OrderBy(i => i.Name)
                .Select(i => new ItemCatalogEntry
                {
                    ItemId = i.Id,
                    Name = i.Name,
                    Kind = i.Kind,
                    IconLogicalPath = i.IconLogicalPath,
                    MaxStack = i.MaxStack,
                    BuyPrice = i.BuyPrice,
                    SellPrice = i.SellPrice,
                    Revision = i.Revision,
                    Status = i.Status,
                    PublishedRevision = i.PublishedRevision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteItemResult> DeleteAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteItemResult>(async (db, ct) =>
        {
            if (!await db.Items.AsNoTracking().AnyAsync(i => i.Id == itemId, ct)
                    .ConfigureAwait(false))
            {
                return new DeleteItemResult.NotFound();
            }

            var itemReference = PostgresShopRepository.SerializeItemReference(itemId);
            var referenced = await db.Shops.AsNoTracking()
                    .AnyAsync(
                        shop => EF.Functions.JsonContains(shop.ListingsJson, itemReference),
                        ct)
                    .ConfigureAwait(false)
                || await db.ShopPublishedSnapshots.AsNoTracking()
                    .AnyAsync(
                        snapshot => EF.Functions.JsonContains(snapshot.ListingsJson, itemReference),
                        ct)
                    .ConfigureAwait(false);
            if (referenced)
            {
                return new DeleteItemResult.Referenced(
                    "L’objet est référencé par un brouillon ou un snapshot publié de boutique.");
            }

            var resourceReferenced = await db.Resources.AsNoTracking()
                    .AnyAsync(
                        resource =>
                            resource.YieldItemId == itemId || resource.ToolItemId == itemId,
                        ct)
                    .ConfigureAwait(false)
                || await db.ResourcePublishedSnapshots.AsNoTracking()
                    .AnyAsync(
                        snapshot =>
                            snapshot.YieldItemId == itemId || snapshot.ToolItemId == itemId,
                        ct)
                    .ConfigureAwait(false);
            if (resourceReferenced)
            {
                return new DeleteItemResult.Referenced(
                    "L’objet est référencé par un brouillon ou un snapshot publié de ressource.");
            }

            await using var transaction = await db.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);
            try
            {
                await db.ItemPublicationHistory.Where(h => h.ItemId == itemId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.ItemPublishedSnapshots.Where(s => s.ItemId == itemId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.Items.Where(i => i.Id == itemId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteItemResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteItemResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ItemDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ItemDefinition>>(async (db, ct) =>
        {
            var tips = await db.Items.AsNoTracking()
                .Where(i => i.PublishedSnapshotId != null)
                .Select(i => i.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<ItemDefinition>();
            }

            var snapshots = await db.ItemPublishedSnapshots.AsNoTracking()
                .Where(s => tips.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(FromSnapshot).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    async Task<ItemDefinition?> IPublishedItemCatalog.LoadPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var stored = await LoadPublishedByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
        return stored?.Definition;
    }

    private static async Task<long> ReadRevisionAsync(
        FrogDbContext db,
        Guid id,
        CancellationToken cancellationToken)
    {
        var revision = await db.Items.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (long?)i.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ItemEntity ToEntity(
        ItemDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Kind = definition.Kind,
        IconLogicalPath = definition.IconLogicalPath.Trim().Replace('\\', '/'),
        MaxStack = definition.MaxStack,
        BuyPrice = definition.BuyPrice,
        SellPrice = definition.SellPrice,
        Description = NormalizeDescription(definition.Description),
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredItem ToStored(ItemEntity entity) => new()
    {
        ItemId = entity.Id,
        Definition = new ItemDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Kind = entity.Kind,
            IconLogicalPath = entity.IconLogicalPath,
            MaxStack = entity.MaxStack,
            BuyPrice = entity.BuyPrice,
            SellPrice = entity.SellPrice,
            Description = entity.Description,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ItemDefinition FromSnapshot(ItemPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.ItemId,
        Name = snapshot.Name,
        Kind = snapshot.Kind,
        IconLogicalPath = snapshot.IconLogicalPath,
        MaxStack = snapshot.MaxStack,
        BuyPrice = snapshot.BuyPrice,
        SellPrice = snapshot.SellPrice,
        Description = snapshot.Description,
    };

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance objet.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
