using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveSystemFlagRequest
{
    public Guid? FlagId { get; init; }

    public required SystemFlagDefinition Definition { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveSystemFlagResult
{
    public sealed record Success(long NewRevision, Guid FlagId, long? PublishedRevision = null) : SaveSystemFlagResult;

    public sealed record Conflict(long CurrentRevision) : SaveSystemFlagResult;

    public sealed record ValidationFailed(string Error) : SaveSystemFlagResult;

    public sealed record PersistenceFailed(string Error) : SaveSystemFlagResult;

    public sealed record NotDurable(string Message) : SaveSystemFlagResult;
}

public abstract record DeleteSystemFlagResult
{
    public sealed record Success : DeleteSystemFlagResult;

    public sealed record NotFound : DeleteSystemFlagResult;

    public sealed record PersistenceFailed(string Error) : DeleteSystemFlagResult;
}

public sealed class StoredSystemFlag
{
    public required Guid FlagId { get; init; }

    public required SystemFlagDefinition Definition { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public sealed class SystemFlagCatalogEntry
{
    public required Guid FlagId { get; init; }

    public required SystemFlagKind Kind { get; init; }

    public required string Key { get; init; }

    public required string Label { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public interface ISystemFlagRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveSystemFlagResult> SaveAsync(SaveSystemFlagRequest request, CancellationToken cancellationToken = default);

    Task<StoredSystemFlag?> LoadByIdAsync(Guid flagId, CancellationToken cancellationToken = default);

    Task<StoredSystemFlag?> LoadPublishedByIdAsync(Guid flagId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemFlagCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        SystemFlagKind? kindFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteSystemFlagResult> DeleteAsync(Guid flagId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation : catalogue publié des interrupteurs et variables.</summary>
public interface IPublishedSystemFlagCatalog
{
    Task<IReadOnlyList<SystemFlagDefinition>> ListPublishedAsync(
        SystemFlagKind? kind = null,
        CancellationToken cancellationToken = default);
}
