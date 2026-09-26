using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur Système : catalogue + brouillon courant (interrupteur, variable ou options).</summary>
public sealed class GameSystemWorkspaceSession
{
    private readonly IGameSystemRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public GameSystemWorkspaceSession(IGameSystemRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public IReadOnlyList<GameSystemCatalogEntry> Catalog { get; private set; } = Array.Empty<GameSystemCatalogEntry>();

    public GameSystemEntryDefinition? Current { get; private set; }

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public string? SearchFilter { get; set; }

    public ContentPublishStatus? StatusFilter { get; set; }

    /// <summary>Quand il est fixé, la session ne liste et n’enregistre que ce type.</summary>
    public GameSystemEntryKind? KindFilter { get; set; }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        Catalog = await _repository
            .ListSummariesAsync(SearchFilter, StatusFilter, KindFilter, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        var kind = KindFilter ?? GameSystemEntryKind.Switch;
        return await _repository.ListKeysAsync(kind, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> OpenAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        if (entryId == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadByIdAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        ApplyStored(stored);
        return true;
    }

    public void AdoptNewDraft(GameSystemEntryDefinition definition)
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

    public GameSystemEntryDefinition DuplicateCurrent(IEnumerable<string> usedKeys)
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucune entrée système ouverte.");
        }

        if (Current.Kind == GameSystemEntryKind.Options)
        {
            throw new InvalidOperationException("Les options du projet ne se dupliquent pas.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        var used = usedKeys.Append(Current.Key);
        copy.Key = GameSystemEntryDefinition.AllocateCopyKey(Current.Key, used);
        copy.Label = Current.Label + " (copie)";
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

    public async Task<SaveGameSystemResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current is null)
        {
            return new SaveGameSystemResult.ValidationFailed("Aucune entrée système ouverte.");
        }

        ApplyKindFilter(current);

        if (!Capabilities.AllowsSave)
        {
            return new SaveGameSystemResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveGameSystemResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveGameSystemRequest
                    {
                        EntryId = CurrentId,
                        Definition = Clone(current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveGameSystemResult.Success success)
            {
                CurrentId = success.EntryId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.EntryId;
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

    public async Task<DeleteGameSystemResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new DeleteGameSystemResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is DeleteGameSystemResult.Success)
        {
            ClearCurrent();
            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void ApplyKindFilter(GameSystemEntryDefinition definition)
    {
        if (KindFilter is GameSystemEntryKind kind)
        {
            definition.Kind = kind;
        }

        if (definition.Kind == GameSystemEntryKind.Options)
        {
            definition.Key = string.Empty;
            return;
        }

        definition.Key = definition.Key.Trim();
        definition.StartingBgmAsset = string.Empty;
        definition.StartingBgmVolume = MapAudioTrack.DefaultVolume;
        definition.StartingBgmFadeMs = 0;
    }

    private void ApplyStored(StoredGameSystemEntry stored)
    {
        Current = Clone(stored.Definition);
        CurrentId = stored.EntryId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    public static GameSystemEntryDefinition Clone(GameSystemEntryDefinition source) => new()
    {
        Id = source.Id,
        Kind = source.Kind,
        Key = source.Key,
        Label = source.Label,
        Note = source.Note,
        StartingBgmAsset = source.StartingBgmAsset,
        StartingBgmVolume = source.StartingBgmVolume,
        StartingBgmFadeMs = source.StartingBgmFadeMs,
    };
}
