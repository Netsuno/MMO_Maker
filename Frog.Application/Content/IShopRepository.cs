using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveShopRequest
{
    public Guid? ShopId { get; init; }
    public required ShopDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveShopResult
{
    public sealed record Success(long NewRevision, Guid ShopId, long? PublishedRevision = null) : SaveShopResult;
    public sealed record Conflict(long CurrentRevision) : SaveShopResult;
    public sealed record ValidationFailed(string Error) : SaveShopResult;
    public sealed record PersistenceFailed(string Error) : SaveShopResult;
    public sealed record NotDurable(string Message) : SaveShopResult;
}

public abstract record DeleteShopResult
{
    public sealed record Success : DeleteShopResult;
    public sealed record NotFound : DeleteShopResult;
    public sealed record PersistenceFailed(string Error) : DeleteShopResult;
}

public sealed class StoredShop
{
    public required Guid ShopId { get; init; }
    public required ShopDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ShopCatalogEntry
{
    public required Guid ShopId { get; init; }
    public required string Name { get; init; }
    public required int ListingCount { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IShopRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveShopResult> SaveAsync(SaveShopRequest request, CancellationToken cancellationToken = default);

    Task<StoredShop?> LoadByIdAsync(Guid shopId, CancellationToken cancellationToken = default);

    Task<StoredShop?> LoadPublishedByIdAsync(Guid shopId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteShopResult> DeleteAsync(Guid shopId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue de boutiques publié uniquement.</summary>
public interface IPublishedShopCatalog
{
    Task<IReadOnlyList<ShopDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Références de contenu boutique utilisées pour protéger la suppression d’objets.</summary>
public interface IShopItemReferenceCatalog
{
    Task<bool> IsItemReferencedAsync(Guid itemId, CancellationToken cancellationToken = default);
}
