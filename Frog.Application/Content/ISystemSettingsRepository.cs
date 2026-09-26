using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveSystemSettingsRequest
{
    /// <summary>Null ou vide = première création du document unique.</summary>
    public Guid? SettingsId { get; init; }

    public required SystemDefinition Definition { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveSystemSettingsResult
{
    public sealed record Success(long NewRevision, Guid SettingsId, long? PublishedRevision = null) : SaveSystemSettingsResult;

    public sealed record Conflict(long CurrentRevision) : SaveSystemSettingsResult;

    public sealed record ValidationFailed(string Error) : SaveSystemSettingsResult;

    public sealed record PersistenceFailed(string Error) : SaveSystemSettingsResult;

    public sealed record NotDurable(string Message) : SaveSystemSettingsResult;
}

public sealed class StoredSystemSettings
{
    public required Guid SettingsId { get; init; }

    public required SystemDefinition Definition { get; init; }

    public required long Revision { get; init; }

    public required ContentPublishStatus Status { get; init; }

    public long? PublishedRevision { get; init; }
}

/// <summary>Document projet Système : brouillon, publication. Une seule ligne.</summary>
public interface ISystemSettingsRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveSystemSettingsResult> SaveAsync(
        SaveSystemSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredSystemSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task<StoredSystemSettings?> LoadPublishedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Consommation : paramètres Système publiés, ou null si jamais publiés.</summary>
public interface IPublishedSystemSettings
{
    Task<SystemDefinition?> LoadPublishedAsync(CancellationToken cancellationToken = default);
}
