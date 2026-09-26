using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur Système : catalogue d'interrupteurs ou de variables + brouillon courant.</summary>
public sealed class SystemFlagWorkspaceSession
{
    private readonly ISystemFlagRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SystemFlagWorkspaceSession(ISystemFlagRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<SystemFlagCatalogEntry> Catalog { get; private set; } = Array.Empty<SystemFlagCatalogEntry>();

    public SystemFlagDefinition? Current { get; private set; }

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public string? SearchFilter { get; set; }

    public ContentPublishStatus? StatusFilter { get; set; }

    /// <summary>
    /// Quand il est fixé, la session ne liste et n'enregistre que ce type
    /// (fiche Système : interrupteurs ou variables).
    /// </summary>
    public SystemFlagKind? KindFilter { get; set; }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await _repository
            .ListSummariesAsync(SearchFilter, StatusFilter, KindFilter, cancellationToken)
            .ConfigureAwait(false);
        if (KindFilter is SystemFlagKind kind)
        {
            catalog = catalog.Where(entry => entry.Kind == kind).ToArray();
        }

        Catalog = catalog;
    }

    public async Task<bool> OpenAsync(Guid flagId, CancellationToken cancellationToken = default)
    {
        if (flagId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(flagId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        if (KindFilter is SystemFlagKind kind && stored.Definition.Kind != kind)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(SystemFlagDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        var draft = Clone(definition);
        ApplyKindFilter(draft);
        Current = draft;
        CurrentId = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = true;
    }

    public SystemFlagDefinition DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucun interrupteur ou variable ouvert.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Label = WithCopySuffix(Current.Label, " (copie)", SystemFlagDefinition.MaxLabelLength);
        copy.Key = WithCopySuffix(Current.Key, "_copie", SystemFlagDefinition.MaxKeyLength);
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public void ClearCurrent()
    {
        Current = null;
        CurrentId = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = false;
    }

    public async Task<SaveSystemFlagResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current is null)
        {
            return new SaveSystemFlagResult.ValidationFailed("Aucun interrupteur ou variable ouvert.");
        }

        ApplyKindFilter(current);

        if (!Capabilities.AllowsSave)
        {
            return new SaveSystemFlagResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveSystemFlagResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveSystemFlagRequest
                    {
                        FlagId = CurrentId,
                        Definition = Clone(current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveSystemFlagResult.Success success)
            {
                CurrentId = success.FlagId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.FlagId;
                if (intent == SaveContentIntent.Publish)
                {
                    CurrentStatus = ContentPublishStatus.Published;
                    PublishedRevision = success.PublishedRevision;
                }
                else
                {
                    CurrentStatus = ContentPublishStatus.Draft;
                }

                IsDirty = false;
                await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    public async Task<DeleteSystemFlagResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteSystemFlagResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteSystemFlagResult.Success)
        {
            ClearCurrent();
            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void ApplyKindFilter(SystemFlagDefinition definition)
    {
        if (KindFilter is SystemFlagKind kind)
        {
            definition.Kind = kind;
        }
    }

    private void ApplyStored(StoredSystemFlag stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.FlagId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    internal static SystemFlagDefinition Clone(SystemFlagDefinition source) => new()
    {
        Id = source.Id,
        Kind = source.Kind,
        Key = source.Key,
        Label = source.Label,
        Note = source.Note,
    };

    private static string WithCopySuffix(string value, string suffix, int maxLength)
    {
        var room = Math.Max(0, maxLength - suffix.Length);
        var stem = value.Length > room ? value[..room].TrimEnd() : value;
        var copy = stem + suffix;
        return copy.Length > maxLength ? copy[..maxLength] : copy;
    }
}
