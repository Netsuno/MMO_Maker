using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveMapObjectRequest
{
    public Guid? MapObjectId { get; init; }
    public required MapObjectDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveMapObjectResult
{
    public sealed record Success(long NewRevision, Guid MapObjectId, long? PublishedRevision = null) : SaveMapObjectResult;
    public sealed record Conflict(long CurrentRevision) : SaveMapObjectResult;
    public sealed record ValidationFailed(string Error) : SaveMapObjectResult;
    public sealed record PersistenceFailed(string Error) : SaveMapObjectResult;
    public sealed record NotDurable(string Message) : SaveMapObjectResult;
}

public abstract record DeleteMapObjectResult
{
    public sealed record Success : DeleteMapObjectResult;
    public sealed record NotFound : DeleteMapObjectResult;
    public sealed record PersistenceFailed(string Error) : DeleteMapObjectResult;
}

public sealed class StoredMapObject
{
    public required Guid MapObjectId { get; init; }
    public required MapObjectDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class MapObjectCatalogEntry
{
    public required Guid MapObjectId { get; init; }
    public required string Name { get; init; }
    public required string LogicalPath { get; init; }
    public required string PlacementId { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IMapObjectRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveMapObjectResult> SaveAsync(SaveMapObjectRequest request, CancellationToken cancellationToken = default);

    Task<StoredMapObject?> LoadByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default);

    Task<StoredMapObject?> LoadPublishedByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MapObjectCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteMapObjectResult> DeleteAsync(Guid mapObjectId, CancellationToken cancellationToken = default);
}

/// <summary>Objets de carte publiés, consommés par la palette de placement.</summary>
public interface IPublishedMapObjectCatalog
{
    Task<IReadOnlyList<MapObjectDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

public sealed class EmptyPublishedMapObjectCatalog : IPublishedMapObjectCatalog
{
    public static readonly EmptyPublishedMapObjectCatalog Instance = new();

    public Task<IReadOnlyList<MapObjectDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MapObjectDefinition>>(Array.Empty<MapObjectDefinition>());
}
