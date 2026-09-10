using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveResourceRequest
{
    public Guid? ResourceId { get; init; }
    public required ResourceDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveResourceResult
{
    public sealed record Success(long NewRevision, Guid ResourceId, long? PublishedRevision = null)
        : SaveResourceResult;
    public sealed record Conflict(long CurrentRevision) : SaveResourceResult;
    public sealed record ValidationFailed(string Error) : SaveResourceResult;
    public sealed record PersistenceFailed(string Error) : SaveResourceResult;
    public sealed record NotDurable(string Message) : SaveResourceResult;
}

public abstract record DeleteResourceResult
{
    public sealed record Success : DeleteResourceResult;
    public sealed record NotFound : DeleteResourceResult;
    public sealed record Referenced(string Error) : DeleteResourceResult;
    public sealed record PersistenceFailed(string Error) : DeleteResourceResult;
}

public sealed class StoredResource
{
    public required Guid ResourceId { get; init; }
    public required ResourceDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ResourceCatalogEntry
{
    public required Guid ResourceId { get; init; }
    public required string Name { get; init; }
    public required string SpriteLogicalPath { get; init; }
    public required int RespawnSeconds { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IResourceRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveResourceResult> SaveAsync(
        SaveResourceRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredResource?> LoadByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);

    Task<StoredResource?> LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteResourceResult> DeleteAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);
}

public interface IPublishedResourceCatalog
{
    Task<IReadOnlyList<ResourceDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<ResourceDefinition?> LoadPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);
}

public interface IResourceItemReferenceCatalog
{
    Task<bool> IsItemReferencedAsync(Guid itemId, CancellationToken cancellationToken = default);
}

public interface IResourceSpawnReferenceCatalog
{
    Task<bool> IsResourceReferencedAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);
}

public sealed class SaveResourceSpawnRequest
{
    public Guid? SpawnId { get; init; }
    public required ResourceSpawnDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveResourceSpawnResult
{
    public sealed record Success(long NewRevision, Guid SpawnId, long? PublishedRevision = null)
        : SaveResourceSpawnResult;
    public sealed record Conflict(long CurrentRevision) : SaveResourceSpawnResult;
    public sealed record ValidationFailed(string Error) : SaveResourceSpawnResult;
    public sealed record PersistenceFailed(string Error) : SaveResourceSpawnResult;
    public sealed record NotDurable(string Message) : SaveResourceSpawnResult;
}

public abstract record DeleteResourceSpawnResult
{
    public sealed record Success : DeleteResourceSpawnResult;
    public sealed record NotFound : DeleteResourceSpawnResult;
    public sealed record PersistenceFailed(string Error) : DeleteResourceSpawnResult;
}

public sealed class StoredResourceSpawn
{
    public required Guid SpawnId { get; init; }
    public required ResourceSpawnDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ResourceSpawnCatalogEntry
{
    public required Guid SpawnId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid ResourceId { get; init; }
    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IResourceSpawnRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveResourceSpawnResult> SaveAsync(
        SaveResourceSpawnRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredResourceSpawn?> LoadByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default);

    Task<StoredResourceSpawn?> LoadPublishedByIdAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceSpawnCatalogEntry>> ListSummariesAsync(
        Guid? mapId = null,
        Guid? resourceId = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteResourceSpawnResult> DeleteAsync(
        Guid spawnId,
        CancellationToken cancellationToken = default);
}

public interface IPublishedResourceSpawnCatalog
{
    Task<IReadOnlyList<ResourceSpawnDefinition>> ListPublishedAsync(
        Guid? mapId = null,
        CancellationToken cancellationToken = default);
}
