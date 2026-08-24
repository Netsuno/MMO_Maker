using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class SaveSpellRequest
{
    public Guid? SpellId { get; init; }
    public required SpellDefinition Definition { get; init; }
    public required long ExpectedRevision { get; init; }
    public SaveContentIntent Intent { get; init; } = SaveContentIntent.SaveDraft;
}

public abstract record SaveSpellResult
{
    public sealed record Success(long NewRevision, Guid SpellId, long? PublishedRevision = null) : SaveSpellResult;
    public sealed record Conflict(long CurrentRevision) : SaveSpellResult;
    public sealed record ValidationFailed(string Error) : SaveSpellResult;
    public sealed record PersistenceFailed(string Error) : SaveSpellResult;
    public sealed record NotDurable(string Message) : SaveSpellResult;
}

public abstract record DeleteSpellResult
{
    public sealed record Success : DeleteSpellResult;
    public sealed record NotFound : DeleteSpellResult;
    public sealed record Referenced(string Error) : DeleteSpellResult;
    public sealed record PersistenceFailed(string Error) : DeleteSpellResult;
}

public sealed class StoredSpell
{
    public required Guid SpellId { get; init; }
    public required SpellDefinition Definition { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public sealed class SpellCatalogEntry
{
    public required Guid SpellId { get; init; }
    public required string Name { get; init; }
    public required SpellKind Kind { get; init; }
    public required int ManaCost { get; init; }
    public required int CooldownMs { get; init; }
    public required TargetType TargetType { get; init; }
    public required string IconLogicalPath { get; init; }
    public required long Revision { get; init; }
    public required ContentPublishStatus Status { get; init; }
    public long? PublishedRevision { get; init; }
}

public interface ISpellRepository
{
    ContentRepositoryCapabilities Capabilities { get; }

    Task<SaveSpellResult> SaveAsync(SaveSpellRequest request, CancellationToken cancellationToken = default);

    Task<StoredSpell?> LoadByIdAsync(Guid spellId, CancellationToken cancellationToken = default);

    Task<StoredSpell?> LoadPublishedByIdAsync(Guid spellId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpellCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    Task<DeleteSpellResult> DeleteAsync(Guid spellId, CancellationToken cancellationToken = default);
}

/// <summary>Consommation serveur : catalogue de sorts et compétences publié uniquement.</summary>
public interface IPublishedSpellCatalog
{
    Task<IReadOnlyList<SpellDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

public interface IClassSpellReferenceCatalog
{
    Task<bool> IsSpellReferencedAsync(Guid spellId, CancellationToken cancellationToken = default);
}
