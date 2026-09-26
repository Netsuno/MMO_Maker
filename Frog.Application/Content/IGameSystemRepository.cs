using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveGameSystemRequest
{
    public Guid? EntryId { get; init; }
    public required GameSystemEntryDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveGameSystemResult
{
    public sealed record Success(long NewRevision, Guid EntryId, long? PublishedRevision = null) : SaveGameSystemResult;
    public sealed record Conflict(long CurrentRevision) : SaveGameSystemResult;
    public sealed record ValidationFailed(string Error) : SaveGameSystemResult;
    public sealed record PersistenceFailed(string Error) : SaveGameSystemResult;
    public sealed record NotDurable(string Message) : SaveGameSystemResult;
}

public abstract record DeleteGameSystemResult
{
    public sealed record Success : DeleteGameSystemResult;
    public sealed record NotFound : DeleteGameSystemResult;
    public sealed record PersistenceFailed(string Error) : DeleteGameSystemResult;
}

public sealed class StoredGameSystemEntry
{
    public required Guid EntryId { get; init; }
    public required GameSystemEntryDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class GameSystemCatalogEntry
{
    public required Guid EntryId { get; init; }
    public required GameSystemEntryKind Kind { get; init; }
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IGameSystemRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveGameSystemResult> SaveAsync(SaveGameSystemRequest request, CancellationToken cancellationToken = default);

    Task<StoredGameSystemEntry?> LoadByIdAsync(Guid entryId, CancellationToken cancellationToken = default);

    Task<StoredGameSystemEntry?> LoadPublishedByIdAsync(Guid entryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListKeysAsync(
        GameSystemEntryKind kind,
        CancellationToken cancellationToken = default);

    Task<DeleteGameSystemResult> DeleteAsync(Guid entryId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation : catalogue système publié (interrupteurs, variables, options).</summary>
public interface IPublishedGameSystemCatalog
{
    Task<IReadOnlyList<GameSystemEntryDefinition>> ListPublishedAsync(
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default);
}
