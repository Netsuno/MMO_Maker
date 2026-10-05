using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveComposedTilesetRequest
{
    public Guid? TilesetId { get; init; }

    public required ComposedTilesetDefinition Definition { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveComposedTilesetResult
{
    public sealed record Success(long NewRevision, Guid TilesetId, long? PublishedRevision = null) : SaveComposedTilesetResult;

    public sealed record Conflict(long CurrentRevision) : SaveComposedTilesetResult;

    public sealed record ValidationFailed(string Error) : SaveComposedTilesetResult;

    public sealed record PersistenceFailed(string Error) : SaveComposedTilesetResult;

    public sealed record NotDurable(string Message) : SaveComposedTilesetResult;
}

public abstract record DeleteComposedTilesetResult
{
    public sealed record Success : DeleteComposedTilesetResult;

    public sealed record NotFound : DeleteComposedTilesetResult;

    public sealed record PersistenceFailed(string Error) : DeleteComposedTilesetResult;
}

public sealed class StoredComposedTileset
{
    public required Guid TilesetId { get; init; }

    public required ComposedTilesetDefinition Definition { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public sealed class ComposedTilesetCatalogEntry
{
    public required Guid TilesetId { get; init; }

    public required string Name { get; init; }

    public required string LogicalPath { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }

    public int TileCount { get; init; }
}

public interface IComposedTilesetRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveComposedTilesetResult> SaveAsync(SaveComposedTilesetRequest request, CancellationToken cancellationToken = default);

    Task<StoredComposedTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComposedTilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteComposedTilesetResult> DeleteAsync(Guid tilesetId, CancellationToken cancellationToken = default);
}

/// <summary>Tilesets composés publiés. Un brouillon n’est pas listé ici.</summary>
public interface IPublishedComposedTilesetCatalog
{
    Task<IReadOnlyList<ComposedTilesetDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

public sealed class EmptyPublishedComposedTilesetCatalog : IPublishedComposedTilesetCatalog
{
    public static readonly EmptyPublishedComposedTilesetCatalog Instance = new();

    public Task<IReadOnlyList<ComposedTilesetDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ComposedTilesetDefinition>>(Array.Empty<ComposedTilesetDefinition>());
}
