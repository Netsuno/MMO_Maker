using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveMapEventRequest
{
    public Guid? EventId { get; init; }
    public required MapEventDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveMapEventResult
{
    public sealed record Success(long NewRevision, Guid EventId, long? PublishedRevision = null) : SaveMapEventResult;
    public sealed record Conflict(long CurrentRevision) : SaveMapEventResult;
    public sealed record ValidationFailed(string Error) : SaveMapEventResult;
    public sealed record PersistenceFailed(string Error) : SaveMapEventResult;
    public sealed record Referenced(string Error) : SaveMapEventResult;
}

public abstract record DeleteMapEventResult
{
    public sealed record Success : DeleteMapEventResult;
    public sealed record NotFound : DeleteMapEventResult;
    public sealed record Referenced(string Error) : DeleteMapEventResult;
    public sealed record PersistenceFailed(string Error) : DeleteMapEventResult;
}

public sealed class StoredMapEvent
{
    public required Guid EventId { get; init; }
    public required MapEventDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class MapEventCatalogEntry
{
    public required Guid EventId { get; init; }
    public required string Name { get; init; }
    public string? CatalogSlug { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
    public int? EditorAliasId { get; init; }
    public int PageCount { get; init; }
}

public interface IMapEventRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveMapEventResult> SaveAsync(SaveMapEventRequest request, CancellationToken cancellationToken = default);

    Task<StoredMapEvent?> LoadByIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<StoredMapEvent?> LoadPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MapEventCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteMapEventResult> DeleteAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<bool> IsReferencedByMapPlacementsAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Catalogue événements publiés pour le runtime serveur.</summary>
public interface IPublishedMapEventCatalog
{
    Task<IReadOnlyList<MapEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<MapEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<MapEventDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default);
}
