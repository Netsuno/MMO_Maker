namespace Frog.Application.Content;

public enum Phase8ContentKind : byte
{
    Dialogue = 1,
    Quest = 2,
    CommonEvent = 3,
    Profession = 4,
    Recipe = 5,
    Region = 6,
    WeatherProfile = 7,
}

public sealed record Phase8SaveContentRequest
{
    public Guid? ContentId { get; init; }

    public Guid? NewId { get; init; }

    public required Phase8ContentKind Kind { get; init; }

    public required string Name { get; init; }

    public int? EditorAliasId { get; init; }

    public required string PayloadJson { get; init; }

    public required long ExpectedRevision { get; init; }

    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public sealed record Phase8ContentSummary(
    Guid Id,
    Phase8ContentKind Kind,
    string Name,
    int? EditorAliasId,
    long Revision,
    ContentPublishStatus Status,
    long? PublishedRevision);

public sealed record Phase8StoredContent(
    Guid Id,
    Phase8ContentKind Kind,
    string Name,
    int? EditorAliasId,
    string PayloadJson,
    long Revision,
    ContentPublishStatus Status,
    long? PublishedRevision);

public abstract record Phase8SaveContentResult
{
    public sealed record Success(long NewRevision, Guid ContentId, long? PublishedRevision) : Phase8SaveContentResult;

    public sealed record Conflict(long CurrentRevision) : Phase8SaveContentResult;

    public sealed record ValidationFailed(string Error) : Phase8SaveContentResult;

    public sealed record PersistenceFailed(string Error) : Phase8SaveContentResult;
}

public abstract record Phase8DeleteContentResult
{
    public sealed record Success : Phase8DeleteContentResult;

    public sealed record NotFound : Phase8DeleteContentResult;

    public sealed record PersistenceFailed(string Error) : Phase8DeleteContentResult;
}

public interface IPhase8ContentEditorRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<Phase8SaveContentResult> SaveAsync(Phase8SaveContentRequest request, CancellationToken cancellationToken = default);

    Task<Phase8StoredContent?> LoadDraftByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Phase8ContentSummary>> ListSummariesAsync(
        Phase8ContentKind kind,
        CancellationToken cancellationToken = default);

    Task<Phase8DeleteContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
