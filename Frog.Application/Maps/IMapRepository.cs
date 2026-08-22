using Frog.Core.Models;

namespace Frog.Application.Maps;

public enum MapPublishStatus : byte
{
    Draft = 0,
    Published = 1,
}

public sealed class SaveMapRequest
{
    /// <summary>Null ou vide = création (identifiant généré) ; identifiant existant = mise à jour.</summary>
    public Guid? MapId { get; init; }
    public required Map Map { get; init; }
    /// <summary>0 pour une création ; sinon la révision brouillon actuellement connue.</summary>
    public required long ExpectedRevision { get; init; }
    public SaveMapIntent Intent { get; init; } = SaveMapIntent.SaveDraft;
    /// <summary>Obsolète — utiliser <see cref="Intent"/>.</summary>
    public MapPublishStatus Status
    {
        get => Intent == SaveMapIntent.Publish ? MapPublishStatus.Published : MapPublishStatus.Draft;
        init => Intent = value == MapPublishStatus.Published ? SaveMapIntent.Publish : SaveMapIntent.SaveDraft;
    }
}

public abstract record SaveMapResult
{
    public sealed record Success(long NewRevision, Guid MapId, long? PublishedRevision = null) : SaveMapResult;
    public sealed record Conflict(long CurrentRevision) : SaveMapResult;
    public sealed record ValidationFailed(string Error) : SaveMapResult;
    public sealed record PersistenceFailed(string Error) : SaveMapResult;
    public sealed record NotDurable(string Message) : SaveMapResult;
}

public sealed class StoredMap
{
    public required Guid MapId { get; init; }
    public required Map Map { get; init; }
    public required long Revision { get; init; }
    public required MapPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

/// <summary>Entrée légère pour l’arbre « monde » de l’éditeur (pas de cellules).</summary>
public sealed class MapCatalogEntry
{
    public required Guid MapId { get; init; }
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required long Revision { get; init; }
    public required MapPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class MapPublicationRecord
{
    public required Guid MapId { get; init; }
    public required long Revision { get; init; }
    public required DateTimeOffset PublishedAtUtc { get; init; }
}

public interface IMapRepository
{
    MapRepositoryCapabilities Capabilities { get; }

    Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default);
    Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default);
    Task<StoredMap?> LoadPublishedByIdAsync(Guid mapId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapPublicationRecord>> ListPublicationHistoryAsync(Guid mapId, CancellationToken cancellationToken = default);
}
