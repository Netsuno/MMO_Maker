using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveSystemRequest
{
    public required SystemDefinition Definition { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveSystemResult
{
    public sealed record Success(long NewRevision, long? PublishedRevision = null) : SaveSystemResult;

    public sealed record Conflict(long CurrentRevision) : SaveSystemResult;

    public sealed record ValidationFailed(string Error) : SaveSystemResult;

    public sealed record PersistenceFailed(string Error) : SaveSystemResult;

    public sealed record NotDurable(string Message) : SaveSystemResult;
}

public sealed class StoredSystem
{
    public required SystemDefinition Definition { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

public interface ISystemRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveSystemResult> SaveAsync(SaveSystemRequest request, CancellationToken cancellationToken = default);

    Task<StoredSystem?> LoadAsync(CancellationToken cancellationToken = default);

    Task<StoredSystem?> LoadPublishedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Consommation éditeur : dernier document système publié (libellés d’événements).</summary>
public interface IPublishedSystemCatalog
{
    Task<SystemDefinition?> LoadPublishedDefinitionAsync(CancellationToken cancellationToken = default);
}
