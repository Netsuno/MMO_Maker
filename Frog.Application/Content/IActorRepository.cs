using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveActorRequest
{
    public Guid? ActorId { get; init; }
    public required ActorDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveActorResult
{
    public sealed record Success(long NewRevision, Guid ActorId, long? PublishedRevision = null) : SaveActorResult;
    public sealed record Conflict(long CurrentRevision) : SaveActorResult;
    public sealed record ValidationFailed(string Error) : SaveActorResult;
    public sealed record PersistenceFailed(string Error) : SaveActorResult;
    public sealed record NotDurable(string Message) : SaveActorResult;
}

public abstract record DeleteActorResult
{
    public sealed record Success : DeleteActorResult;
    public sealed record NotFound : DeleteActorResult;
    public sealed record PersistenceFailed(string Error) : DeleteActorResult;
}

public sealed class StoredActor
{
    public required Guid ActorId { get; init; }
    public required ActorDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ActorCatalogEntry
{
    public required Guid ActorId { get; init; }
    public required string Name { get; init; }
    public Guid? ClassId { get; init; }
    public required int BaseHp { get; init; }
    public required int BaseMp { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IActorRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveActorResult> SaveAsync(SaveActorRequest request, CancellationToken cancellationToken = default);

    Task<StoredActor?> LoadByIdAsync(Guid actorId, CancellationToken cancellationToken = default);

    Task<StoredActor?> LoadPublishedByIdAsync(Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActorCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteActorResult> DeleteAsync(Guid actorId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation : catalogue de héros publié uniquement.</summary>
public interface IPublishedActorCatalog
{
    Task<IReadOnlyList<ActorDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Empêche de supprimer une classe encore liée à un héros.</summary>
public interface IActorClassReferenceCatalog
{
    Task<bool> IsClassReferencedAsync(Guid classId, CancellationToken cancellationToken = default);
}

/// <summary>Empêche de supprimer un objet encore posé comme équipement de départ.</summary>
public interface IActorItemReferenceCatalog
{
    Task<bool> IsItemReferencedAsync(Guid itemId, CancellationToken cancellationToken = default);
}
