using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemoryItemRepository : IItemRepository, IPublishedItemCatalog
{
    private readonly ConcurrentDictionary<Guid, DraftRecord> _drafts = new();
    private readonly ConcurrentDictionary<Guid, PublishedRecord> _published = new();
    private IShopItemReferenceCatalog? _shopReferences;

    public InMemoryItemRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    internal void RegisterShopReferences(IShopItemReferenceCatalog shopReferences)
    {
        _shopReferences = shopReferences ?? throw new ArgumentNullException(nameof(shopReferences));
    }

    public Task<SaveItemResult> SaveAsync(
        SaveItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveItemResult>(
                new SaveItemResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveItemResult>(new SaveItemResult.ValidationFailed(error!));
        }

        Guid id;
        long newRevision;
        if (request.ItemId is not Guid existing || existing == Guid.Empty)
        {
            if (request.ExpectedRevision != 0)
            {
                return Task.FromResult<SaveItemResult>(new SaveItemResult.Conflict(0));
            }

            id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
            newRevision = 1;
            var definition = ItemWorkspaceSession.Clone(request.Definition);
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
                return Task.FromResult<SaveItemResult>(new SaveItemResult.Conflict(0));
            }

            if (current.Revision != request.ExpectedRevision)
            {
                return Task.FromResult<SaveItemResult>(
                    new SaveItemResult.Conflict(current.Revision));
            }

            id = existing;
            newRevision = current.Revision + 1;
            var definition = ItemWorkspaceSession.Clone(request.Definition);
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
            _published[id] = new PublishedRecord(
                id,
                ItemWorkspaceSession.Clone(draft.Definition),
                publishedRevision.Value);
            _drafts[id] = draft with
            {
                Status = ContentPublishStatus.Published,
                PublishedRevision = publishedRevision,
            };
        }

        return Task.FromResult<SaveItemResult>(
            new SaveItemResult.Success(newRevision, id, publishedRevision));
    }

    public Task<StoredItem?> LoadByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (!_drafts.TryGetValue(itemId, out var draft))
        {
            return Task.FromResult<StoredItem?>(null);
        }

        return Task.FromResult<StoredItem?>(new StoredItem
        {
            ItemId = draft.Id,
            Definition = ItemWorkspaceSession.Clone(draft.Definition),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        });
    }

    public Task<StoredItem?> LoadPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (!_published.TryGetValue(itemId, out var published))
        {
            return Task.FromResult<StoredItem?>(null);
        }

        return Task.FromResult<StoredItem?>(new StoredItem
        {
            ItemId = published.Id,
            Definition = ItemWorkspaceSession.Clone(published.Definition),
            Revision = published.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = published.Revision,
        });
    }

    public Task<IReadOnlyList<ItemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<DraftRecord> query = _drafts.Values;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.IconLogicalPath.Contains(search, StringComparison.OrdinalIgnoreCase)
                || d.Definition.Kind.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var list = query
            .OrderBy(d => d.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ItemCatalogEntry
            {
                ItemId = d.Id,
                Name = d.Definition.Name,
                Kind = d.Definition.Kind,
                IconLogicalPath = d.Definition.IconLogicalPath,
                MaxStack = d.Definition.MaxStack,
                BuyPrice = d.Definition.BuyPrice,
                SellPrice = d.Definition.SellPrice,
                Revision = d.Revision,
                Status = d.Status,
                PublishedRevision = d.PublishedRevision,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ItemCatalogEntry>>(list);
    }

    public async Task<DeleteItemResult> DeleteAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (_shopReferences is not null
            && await _shopReferences.IsItemReferencedAsync(itemId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new DeleteItemResult.Referenced(
                "L’objet est référencé par un brouillon ou un snapshot publié de boutique.");
        }

        if (!_drafts.TryRemove(itemId, out _))
        {
            return new DeleteItemResult.NotFound();
        }

        _published.TryRemove(itemId, out _);
        return new DeleteItemResult.Success();
    }

    public Task<IReadOnlyList<ItemDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var list = _published.Values
            .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => ItemWorkspaceSession.Clone(p.Definition))
            .ToList();
        return Task.FromResult<IReadOnlyList<ItemDefinition>>(list);
    }

    async Task<ItemDefinition?> IPublishedItemCatalog.LoadPublishedByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var stored = await LoadPublishedByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
        return stored is null ? null : ItemWorkspaceSession.Clone(stored.Definition);
    }

    private sealed record DraftRecord(
        Guid Id,
        ItemDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, ItemDefinition Definition, long Revision);
}
