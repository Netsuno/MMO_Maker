using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryShopRepository :
    IShopRepository,
    IPublishedShopCatalog,
    IShopItemReferenceCatalog
{
    private readonly IPublishedItemCatalog _items;
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _publishedTips = new();
    private readonly ConcurrentDictionary<(Guid ShopId, long Revision), PublishedRecord> _snapshots = new();

    public InMemoryShopRepository(
        IPublishedItemCatalog items,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
        if (items is InMemoryItemRepository itemRepository)
        {
            itemRepository.RegisterShopReferences(this);
        }
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveShopResult> SaveAsync(
        SaveShopRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveShopResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!request.Definition.Validate(out var error))
        {
            return new SaveShopResult.ValidationFailed(error!);
        }

        foreach (var listing in request.Definition.Listings)
        {
            if (await _items.LoadPublishedByIdAsync(listing.ItemId, cancellationToken)
                    .ConfigureAwait(false) is null)
            {
                return new SaveShopResult.ValidationFailed(
                    $"L’objet {listing.ItemId:N} doit exister dans le catalogue publié.");
            }
        }

        Guid id;
        long newRevision;
        if (request.ShopId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return new SaveShopResult.Conflict(0);
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ShopWorkspaceSession.Clone(request.Definition);
            definition.Id = id;
            _drafts[id] = new DraftRecord(
                id,
                definition,
                newRevision,
                ContentPublishStatus.Draft,
                null);
        }
        else
        {
            if (!_drafts.TryGetValue(existing, out var current))
            {
                return new SaveShopResult.Conflict(0);
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return new SaveShopResult.Conflict(current.Revision);
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ShopWorkspaceSession.Clone(request.Definition);
            definition.Id = id;
            _drafts[id] = current with
            {
                Definition = definition,
                Revision = newRevision,
                Status = ContentPublishStatus.Draft,
            };
        }

        long? publishedRevision = null;
        if (request.Intent == SaveContentIntent.Publish)
        {
            var draft = _drafts[id];
            publishedRevision = newRevision;
            var snapshot = new PublishedRecord(
                id,
                ShopWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _snapshots[(id, publishedRevision.Value)] = snapshot;
            _publishedTips[id] = snapshot;
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return new SaveShopResult.Success(newRevision, id, publishedRevision);
    }

    public Task<StoredShop?> LoadByIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(shopId, out var draft))
        {
            return Task.FromResult<StoredShop?>(null);
        }

        return Task.FromResult<StoredShop?>(new StoredShop
        {
            ShopId = draft.Id,
            Definition = ShopWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredShop?> LoadPublishedByIdAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        if (!_publishedTips.TryGetValue(shopId, out var published))
        {
            return Task.FromResult<StoredShop?>(null);
        }

        return Task.FromResult<StoredShop?>(new StoredShop
        {
            ShopId = published.Id,
            Definition = ShopWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ShopCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (d.Definition.Description?.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ShopCatalogEntry
            {
                ShopId = d.Id,
                Name = d.Definition.Name,
                ListingCount = d.Definition.Listings.Count,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ShopCatalogEntry>>(list);
    }

    public Task<DeleteShopResult> DeleteAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryRemove(shopId, out _))
        {
            return Task.FromResult<DeleteShopResult>(new DeleteShopResult.NotFound());
        }

        _publishedTips.TryRemove(shopId, out _);
        foreach (var key in _snapshots.Keys.Where(key => key.ShopId == shopId))
        {
            _snapshots.TryRemove(key, out _);
        }

        return Task.FromResult<DeleteShopResult>(new DeleteShopResult.Success());
    }

    public Task<IReadOnlyList<ShopDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _publishedTips.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ShopWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ShopDefinition>>(list);
    }

    public Task<bool> IsItemReferencedAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var referenced = _drafts.Values.Any(
                draft => draft.Definition.Listings.Any(listing => listing.ItemId == itemId))
            || _snapshots.Values.Any(
                snapshot => snapshot.Definition.Listings.Any(listing => listing.ItemId == itemId));
        return Task.FromResult(referenced);
    }

    private sealed record DraftRecord(
        Guid Id,
        ShopDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ShopDefinition Definition, long Revision);
}
