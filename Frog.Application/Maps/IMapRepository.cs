using Frog.Core.Models;

namespace Frog.Application.Maps;

public enum MapPublishStatus : byte
{
    Draft = 0,
    Published = 1,
}

public sealed class SaveMapRequest
{
    public required int LegacyId { get; init; }
    public required Map Map { get; init; }
    /// <summary>0 pour une création ; sinon la révision actuellement connue.</summary>
    public required long ExpectedRevision { get; init; }
    public MapPublishStatus Status { get; init; } = MapPublishStatus.Draft;
}

public abstract record SaveMapResult
{
    public sealed record Success(long NewRevision) : SaveMapResult;
    public sealed record Conflict(long CurrentRevision) : SaveMapResult;
    public sealed record ValidationFailed(string Error) : SaveMapResult;
}

public sealed class StoredMap
{
    public required int LegacyId { get; init; }
    public required Map Map { get; init; }
    public required long Revision { get; init; }
    public required MapPublishStatus Status { get; init; }
}

public interface IMapRepository
{
    Task<SaveMapResult> SaveAsync(SaveMapRequest request, CancellationToken cancellationToken = default);
    Task<StoredMap?> LoadByLegacyIdAsync(int legacyId, CancellationToken cancellationToken = default);
}
