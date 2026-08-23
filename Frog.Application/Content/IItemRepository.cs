using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveItemRequest
{
    public Guid? ItemId { get; init; }
    public required ItemDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveItemResult
{
    public sealed record Success(long NewRevision, Guid ItemId, long? PublishedRevision = null) : SaveItemResult;
    public sealed record Conflict(long CurrentRevision) : SaveItemResult;
    public sealed record ValidationFailed(string Error) : SaveItemResult;
    public sealed record PersistenceFailed(string Error) : SaveItemResult;
    public sealed record NotDurable(string Message) : SaveItemResult;
}

public abstract record DeleteItemResult
{
    public sealed record Success : DeleteItemResult;
    public sealed record NotFound : DeleteItemResult;
    public sealed record PersistenceFailed(string Error) : DeleteItemResult;
}

public sealed class StoredItem
{
    public required Guid ItemId { get; init; }
    public required ItemDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ItemCatalogEntry
{
    public required Guid ItemId { get; init; }
    public required string Name { get; init; }
    public required ItemType Kind { get; init; }
    public required string IconLogicalPath { get; init; }
    public required int MaxStack { get; init; }
    public required int BuyPrice { get; init; }
    public required int SellPrice { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IItemRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveItemResult> SaveAsync(SaveItemRequest request, CancellationToken cancellationToken = default);

    Task<StoredItem?> LoadByIdAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<StoredItem?> LoadPublishedByIdAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteItemResult> DeleteAsync(Guid itemId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue d’objets publié uniquement.</summary>
public interface IPublishedItemCatalog
{
    Task<IReadOnlyList<ItemDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}
