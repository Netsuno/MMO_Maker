using Frog.Core.Models;

namespace Frog.Application.Maps;

public enum MapPublishStatus : byte
{
    Draft = 0,
    Published = 1,
}

public sealed class SaveMapRequest
{
    /// <summary>Null pour création (id généré) ; sinon identité canonique de la carte.</summary>
    public Guid? MapId { get; init; }
    public required Map Map { get; init; }
    /// <summary>0 pour une création ; sinon la révision actuellement connue.</summary>
    public required long ExpectedRevision { get; init; }
    public MapPublishStatus Status { get; init; } = MapPublishStatus.Draft;
}

public abstract record SaveMapResult
{
    public sealed record Success(long NewRevision, Guid MapId) : SaveMapResult;
    public sealed record Conflict(long CurrentRevision) : SaveMapResult;
    public sealed record ValidationFailed(string Error) : SaveMapResult;
}

public sealed class StoredMap
{
    public required Guid MapId { get; init; }
    public required Map Map { get; init; }
    public required long Revision { get; init; }
    public required MapPublishStatus Status { get; init; }
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
}

public interface IMapRepository
{
    Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default);
    Task<StoredMap?> LoadByIdAsync(Guid mapId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapCatalogEntry>> ListSummariesAsync(CancellationToken cancellationToken = default);
}
