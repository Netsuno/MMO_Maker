using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveNpcRequest
{
    public Guid? NpcId { get; init; }
    public required NpcDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveNpcResult
{
    public sealed record Success(long NewRevision, Guid NpcId, long? PublishedRevision = null) : SaveNpcResult;
    public sealed record Conflict(long CurrentRevision) : SaveNpcResult;
    public sealed record ValidationFailed(string Error) : SaveNpcResult;
    public sealed record PersistenceFailed(string Error) : SaveNpcResult;
    public sealed record NotDurable(string Message) : SaveNpcResult;
    public sealed record Referenced(string Error) : SaveNpcResult;
}

public abstract record DeleteNpcResult
{
    public sealed record Success : DeleteNpcResult;
    public sealed record NotFound : DeleteNpcResult;
    public sealed record Referenced(string Error) : DeleteNpcResult;
    public sealed record PersistenceFailed(string Error) : DeleteNpcResult;
}

public sealed class StoredNpc
{
    public required Guid NpcId { get; init; }
    public required NpcDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class NpcCatalogEntry
{
    public required Guid NpcId { get; init; }
    public required string Name { get; init; }
    public required NpcKind Kind { get; init; }
    public required string SpriteLogicalPath { get; init; }
    public required int Level { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
    public int? EditorAliasId { get; init; }
}

public interface INpcRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveNpcResult> SaveAsync(SaveNpcRequest request, CancellationToken cancellationToken = default);

    Task<StoredNpc?> LoadByIdAsync(Guid npcId, CancellationToken cancellationToken = default);

    Task<StoredNpc?> LoadPublishedByIdAsync(Guid npcId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NpcCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteNpcResult> DeleteAsync(Guid npcId, CancellationToken cancellationToken = default);

    /// <summary>Indique si un alias NPC entier est référencé par une carte brouillon.</summary>
    Task<bool> IsAliasIdReferencedByMapsAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue NPC/monstres publié uniquement.</summary>
public interface IPublishedNpcCatalog
{
    Task<IReadOnlyList<NpcDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}
