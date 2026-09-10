using Frog.Core.Models;

namespace Frog.Application.Content;

public enum ContentPublishStatus : byte
{
    Draft = 0,
    Published = 1,
}

public enum SaveContentIntent
{
    SaveDraft = 0,
    Publish = 1,
}

public sealed class ContentRepositoryCapabilities
{
    public required bool IsDurablePersistence { get; init; }
    public required bool AllowsSave { get; init; }
    public required string DisplayLabel { get; init; }

    public static ContentRepositoryCapabilities PostgreSql { get; } = new()
    {
        IsDurablePersistence = true,
        AllowsSave = true,
        DisplayLabel = "PostgreSQL",
    };

    public static ContentRepositoryCapabilities InMemoryTest { get; } = new()
    {
        IsDurablePersistence = false,
        AllowsSave = true,
        DisplayLabel = "mémoire (test)",
    };

    public static ContentRepositoryCapabilities InMemoryDemo { get; } = new()
    {
        IsDurablePersistence = false,
        AllowsSave = false,
        DisplayLabel = "mémoire (démo — non persistant)",
    };
}

public sealed class SaveTilesetRequest
{
    public Guid? TilesetId { get; init; }
    public required TilesetDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveTilesetResult
{
    public sealed record Success(long NewRevision, Guid TilesetId, long? PublishedRevision = null) : SaveTilesetResult;
    public sealed record Conflict(long CurrentRevision) : SaveTilesetResult;
    public sealed record ValidationFailed(string Error) : SaveTilesetResult;
    public sealed record PersistenceFailed(string Error) : SaveTilesetResult;
    public sealed record NotDurable(string Message) : SaveTilesetResult;
    public sealed record Referenced(string Error) : SaveTilesetResult;
}

public abstract record DeleteTilesetResult
{
    public sealed record Success : DeleteTilesetResult;
    public sealed record NotFound : DeleteTilesetResult;
    public sealed record Referenced(string Error) : DeleteTilesetResult;
    public sealed record PersistenceFailed(string Error) : DeleteTilesetResult;
}

public sealed class StoredTileset
{
    public required Guid TilesetId { get; init; }
    public required TilesetDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class TilesetCatalogEntry
{
    public required Guid TilesetId { get; init; }
    public required string Name { get; init; }
    public required string LogicalPath { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
    public int? EditorPaletteId { get; init; }
}

public interface ITilesetRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveTilesetResult> SaveAsync(SaveTilesetRequest request, CancellationToken cancellationToken = default);

    Task<StoredTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default);

    Task<StoredTileset?> LoadPublishedByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteTilesetResult> DeleteAsync(Guid tilesetId, CancellationToken cancellationToken = default);

    /// <summary>Indique si un EditorPaletteId est référencé par au moins une carte (brouillon).</summary>
    Task<bool> IsPaletteIdReferencedByMapsAsync(int editorPaletteId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue publié uniquement.</summary>
public interface IPublishedTilesetCatalog
{
    Task<IReadOnlyList<TilesetDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}
