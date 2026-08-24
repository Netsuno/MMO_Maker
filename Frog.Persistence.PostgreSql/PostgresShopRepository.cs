using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresShopRepository :
    IShopRepository,
    IPublishedShopCatalog,
    IShopItemReferenceCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate;
    private readonly IPublishedItemCatalog _items;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresShopRepository(
        FrogDbContextGate gate,
        IPublishedItemCatalog? items = null,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _items = items ?? new PostgresItemRepository(gate);
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveShopResult> SaveAsync(
        SaveShopRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveShopResult>(async (db, ct) =>
        {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return new SaveShopResult.ValidationFailed(error ?? "Boutique invalide.");
        }

        foreach (var listing in request.Definition.Listings)
        {
            if (await _items.LoadPublishedByIdAsync(listing.ItemId, ct)
                    .ConfigureAwait(false) is null)
            {
                return new SaveShopResult.ValidationFailed(
                    $"L’objet {listing.ItemId:N} doit exister dans le catalogue publié.");
            }
        }

        if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return new SaveShopResult.ValidationFailed(
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

    private async Task<SaveShopResult> SaveCoreAsync(FrogDbContext db, SaveShopRequest request,
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
            var listingsJson = SerializeListings(request.Definition.Listings);

            if (request.ShopId is not Guid shopId || shopId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveShopResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, listingsJson, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.Shops.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.Shops
                    .Where(shop => shop.Id == shopId && shop.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(shop => shop.Revision, request.ExpectedRevision + 1)
                            .SetProperty(shop => shop.Name, request.Definition.Name.Trim())
                            .SetProperty(
                                shop => shop.Description,
                                NormalizeDescription(request.Definition.Description))
                            .SetProperty(shop => shop.ListingsJson, listingsJson)
                            .SetProperty(shop => shop.Status, ContentPublishStatus.Draft)
                            .SetProperty(shop => shop.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveShopResult.Conflict(
                        await ReadRevisionAsync(db, shopId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = shopId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.Shops.AsNoTracking()
                        .FirstAsync(shop => shop.Id == shopId, cancellationToken)
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
            return new SaveShopResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveShopResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(FrogDbContext db, ShopEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ShopPublishedSnapshots.Add(new ShopPublishedSnapshotEntity
        {
            Id = snapshotId,
            ShopId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Description = entity.Description,
            ListingsJson = entity.ListingsJson,
        });
        db.ShopPublicationHistory.Add(new ShopPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ShopId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.Shops
            .Where(shop => shop.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(shop => shop.Status, ContentPublishStatus.Published)
                    .SetProperty(shop => shop.PublishedRevision, entity.Revision)
                    .SetProperty(shop => shop.PublishedSnapshotId, snapshotId)
                    .SetProperty(shop => shop.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredShop?> LoadByIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredShop?>(async (db, ct) =>
        {
        var entity = await db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(shop => shop.Id == shopId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredShop?> LoadPublishedByIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredShop?>(async (db, ct) =>
        {
        var tip = await db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(shop => shop.Id == shopId, ct)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await db.ShopPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(shop => shop.Id == snapshotId, ct)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        return new StoredShop
        {
            ShopId = snapshot.ShopId,
            Definition = FromSnapshot(snapshot),
            Revision = snapshot.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = snapshot.Revision,
        };
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ShopCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ShopCatalogEntry>>(async (db, ct) =>
        {
        var query = db.Shops.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(shop =>
                EF.Functions.ILike(shop.Name, $"%{value}%")
                || (shop.Description != null
                    && EF.Functions.ILike(shop.Description, $"%{value}%")));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(shop => shop.Status == status);
        }

        var rows = await query
            .OrderBy(shop => shop.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(shop => new ShopCatalogEntry
        {
            ShopId = shop.Id,
            Name = shop.Name,
            ListingCount = DeserializeListings(shop.ListingsJson).Count,
            Revision = shop.Revision,
            Status = shop.Status,
            PublishedRevision = shop.PublishedRevision,
        }).ToList();
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteShopResult> DeleteAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteShopResult>(async (db, ct) =>
        {
        if (!await db.Shops.AsNoTracking().AnyAsync(shop => shop.Id == shopId, ct)
                .ConfigureAwait(false))
        {
            return new DeleteShopResult.NotFound();
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(ct)
            .ConfigureAwait(false);
        try
        {
            await db.ShopPublicationHistory.Where(history => history.ShopId == shopId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.ShopPublishedSnapshots.Where(snapshot => snapshot.ShopId == shopId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.Shops.Where(shop => shop.Id == shopId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new DeleteShopResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteShopResult.PersistenceFailed(Sanitize(ex.Message));
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ShopDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ShopDefinition>>(async (db, ct) =>
        {
        var tips = await db.Shops.AsNoTracking()
            .Where(shop => shop.PublishedSnapshotId != null)
            .Select(shop => shop.PublishedSnapshotId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (tips.Count == 0)
        {
            return Array.Empty<ShopDefinition>();
        }

        var snapshots = await db.ShopPublishedSnapshots.AsNoTracking()
            .Where(snapshot => tips.Contains(snapshot.Id))
            .OrderBy(snapshot => snapshot.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsItemReferencedAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<bool>(async (db, ct) =>
        {
        var itemReference = SerializeItemReference(itemId);
        return await db.Shops.AsNoTracking()
                .AnyAsync(
                    shop => EF.Functions.JsonContains(shop.ListingsJson, itemReference),
                    ct)
                .ConfigureAwait(false)
            || await db.ShopPublishedSnapshots.AsNoTracking()
                .AnyAsync(
                    snapshot => EF.Functions.JsonContains(snapshot.ListingsJson, itemReference),
                    ct)
                .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.Shops.AsNoTracking()
            .Where(shop => shop.Id == id)
            .Select(shop => (long?)shop.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ShopEntity ToEntity(
        ShopDefinition definition,
        string listingsJson,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Description = NormalizeDescription(definition.Description),
        ListingsJson = listingsJson,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredShop ToStored(ShopEntity entity) => new()
    {
        ShopId = entity.Id,
        Definition = new ShopDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Listings = DeserializeListings(entity.ListingsJson),
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ShopDefinition FromSnapshot(ShopPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.ShopId,
        Name = snapshot.Name,
        Description = snapshot.Description,
        Listings = DeserializeListings(snapshot.ListingsJson),
    };

    private static string SerializeListings(IReadOnlyList<ShopListing> listings)
        => JsonSerializer.Serialize(listings, JsonOptions);

    private static List<ShopListing> DeserializeListings(string json)
        => JsonSerializer.Deserialize<List<ShopListing>>(json, JsonOptions) ?? new List<ShopListing>();

    internal static string SerializeItemReference(Guid itemId)
        => JsonSerializer.Serialize(new[] { new { itemId } }, JsonOptions);

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance boutique.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
