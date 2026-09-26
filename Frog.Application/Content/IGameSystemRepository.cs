using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveGameSystemRequest
{
    public Guid? SystemId { get; init; }

    public required GameSystemDefinition Definition { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveGameSystemResult
{
    public sealed record Success(long NewRevision, Guid SystemId, long? PublishedRevision = null) : SaveGameSystemResult;

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

public sealed class StoredGameSystem
{
    public required Guid SystemId { get; init; }

    public required GameSystemDefinition Definition { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public sealed class GameSystemCatalogEntry
{
    public required Guid SystemId { get; init; }

    public required string Name { get; init; }

    public required string Title { get; init; }

    public required string CurrencyUnit { get; init; }

    public required int SwitchCount { get; init; }

    public required int VariableCount { get; init; }

    public required int PartyCount { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public interface IGameSystemRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveGameSystemResult> SaveAsync(
        SaveGameSystemRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredGameSystem?> LoadByIdAsync(Guid systemId, CancellationToken cancellationToken = default);

    Task<StoredGameSystem?> LoadPublishedByIdAsync(Guid systemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteGameSystemResult> DeleteAsync(Guid systemId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation : fiches Système publiées (noms d’interrupteurs, variables, groupe).</summary>
public interface IPublishedGameSystemCatalog
{
    Task<IReadOnlyList<GameSystemDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}
