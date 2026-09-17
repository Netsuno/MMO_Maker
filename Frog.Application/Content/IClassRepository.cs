using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveClassRequest
{
    public Guid? ClassId { get; init; }
    public required ClassDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveClassResult
{
    public sealed record Success(long NewRevision, Guid ClassId, long? PublishedRevision = null) : SaveClassResult;
    public sealed record Conflict(long CurrentRevision) : SaveClassResult;
    public sealed record ValidationFailed(string Error) : SaveClassResult;
    public sealed record PersistenceFailed(string Error) : SaveClassResult;
    public sealed record NotDurable(string Message) : SaveClassResult;
}

public abstract record DeleteClassResult
{
    public sealed record Success : DeleteClassResult;
    public sealed record NotFound : DeleteClassResult;
    public sealed record PersistenceFailed(string Error) : DeleteClassResult;
}

public sealed class StoredClass
{
    public required Guid ClassId { get; init; }
    public required ClassDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class ClassCatalogEntry
{
    public required Guid ClassId { get; init; }
    public required string Name { get; init; }
    public required int BaseHp { get; init; }
    public required int BaseMp { get; init; }
    public Guid? StartingSpellId { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface IClassRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveClassResult> SaveAsync(SaveClassRequest request, CancellationToken cancellationToken = default);

    Task<StoredClass?> LoadByIdAsync(Guid classId, CancellationToken cancellationToken = default);

    Task<StoredClass?> LoadPublishedByIdAsync(Guid classId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteClassResult> DeleteAsync(Guid classId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue de classes publié uniquement.</summary>
public interface IPublishedClassCatalog
{
    Task<IReadOnlyList<ClassDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}
