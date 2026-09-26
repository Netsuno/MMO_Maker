using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>
/// Session éditeur du catalogue Système (interrupteurs ou variables).
/// Persistance : dépôt Phase 8 existant (brouillon / publication), un kind par emplacement.
/// </summary>
public sealed class SystemCatalogWorkspaceSession
{
    private readonly IPhase8ContentEditorRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SystemCatalogWorkspaceSession(IPhase8ContentEditorRepository repository, SystemCatalogSlot slot)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Slot = slot;
        Kind = slot == SystemCatalogSlot.Variable
            ? Phase8ContentKind.NamedVariable
            : Phase8ContentKind.NamedSwitch;
    }

    public SystemCatalogSlot Slot { get; }

    public Phase8ContentKind Kind { get; }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public IReadOnlyList<SystemCatalogListItem> Catalog { get; private set; } = Array.Empty<SystemCatalogListItem>();

    public SystemCatalogEntry? Current { get; private set; }

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public string? SearchFilter { get; set; }

    public ContentPublishStatus? StatusFilter { get; set; }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await _repository.ListSummariesAsync(Kind, cancellationToken).ConfigureAwait(false);
        var rows = new List<SystemCatalogListItem>(summaries.Count);
        foreach (var summary in summaries)
        {
            if (StatusFilter is ContentPublishStatus status && summary.Status != status)
            {
                continue;
            }

            var stored = await _repository.LoadDraftByIdAsync(summary.Id, cancellationToken).ConfigureAwait(false);
            if (stored is null || !SystemCatalogCodec.TryRead(stored.PayloadJson, out var entry, out _))
            {
                continue;
            }

            if (!MatchesSearch(entry))
            {
                continue;
            }

            rows.Add(new SystemCatalogListItem(
                summary.Id,
                entry.Key,
                string.IsNullOrWhiteSpace(entry.Label) ? summary.Name : entry.Label,
                entry.Note ?? string.Empty,
                summary.Status,
                summary.Revision,
                summary.PublishedRevision));
        }

        Catalog = rows
            .OrderBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<bool> OpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return false;
        }

        var stored = await _repository.LoadDraftByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.Kind != Kind || !SystemCatalogCodec.TryRead(stored.PayloadJson, out var entry, out _))
        {
            return false;
        }

        ApplyStored(stored, entry);
        return true;
    }

    public void AdoptNewDraft(SystemCatalogEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty)
        {
            definition.Id = Guid.NewGuid();
        }

        Current = Clone(definition);
        CurrentId = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = true;
    }

    public SystemCatalogEntry DuplicateCurrent()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Aucune entrée ouverte.");
        }

        var copy = Clone(Current);
        copy.Id = Guid.NewGuid();
        copy.Label = Current.Label + " (copie)";
        copy.Key = CopyKey(Current.Key);
        AdoptNewDraft(copy);
        return Current!;
    }

    public void MarkDirty() => IsDirty = true;

    public async Task DiscardEditsAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is Guid id)
        {
            await OpenAsync(id, cancellationToken).ConfigureAwait(false);
            return;
        }

        Current = null;
        CurrentRevision = 0;
        CurrentStatus = ContentPublishStatus.Draft;
        PublishedRevision = null;
        IsDirty = false;
    }

    public async Task<Phase8SaveContentResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current is null)
        {
            return new Phase8SaveContentResult.ValidationFailed("Aucune entrée ouverte.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new Phase8SaveContentResult.ValidationFailed("Persistance non disponible.");
        }

        current.Normalize();
        if (!current.Validate(Slot, out var error))
        {
            return new Phase8SaveContentResult.ValidationFailed(error ?? "Entrée invalide.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new Phase8SaveContentResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var duplicate = await FindDuplicateKeyAsync(current.Key, CurrentId, cancellationToken).ConfigureAwait(false);
            if (duplicate is not null)
            {
                return new Phase8SaveContentResult.ValidationFailed(duplicate);
            }

            var result = await _repository.SaveAsync(
                new Phase8SaveContentRequest
                {
                    ContentId = CurrentId,
                    NewId = current.Id,
                    Kind = Kind,
                    Name = current.Label,
                    PayloadJson = SystemCatalogCodec.Serialize(current),
                    ExpectedRevision = CurrentRevision,
                    Intent = intent,
                },
                cancellationToken).ConfigureAwait(false);

            if (result is Phase8SaveContentResult.Success success)
            {
                CurrentId = success.ContentId;
                CurrentRevision = success.NewRevision;
                Current!.Id = success.ContentId;
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

    public async Task<Phase8DeleteContentResult> DeleteCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentId is not Guid id)
        {
            return new Phase8DeleteContentResult.NotFound();
        }

        var result = await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result is Phase8DeleteContentResult.Success)
        {
            Current = null;
            CurrentId = null;
            CurrentRevision = 0;
            CurrentStatus = ContentPublishStatus.Draft;
            PublishedRevision = null;
            IsDirty = false;
            await RefreshCatalogAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<string?> FindDuplicateKeyAsync(string key, Guid? selfId, CancellationToken cancellationToken)
    {
        var summaries = await _repository.ListSummariesAsync(Kind, cancellationToken).ConfigureAwait(false);
        foreach (var summary in summaries)
        {
            if (selfId is Guid id && summary.Id == id)
            {
                continue;
            }

            var stored = await _repository.LoadDraftByIdAsync(summary.Id, cancellationToken).ConfigureAwait(false);
            if (stored is null || !SystemCatalogCodec.TryRead(stored.PayloadJson, out var entry, out _))
            {
                continue;
            }

            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
            {
                return "Cet identifiant est déjà utilisé.";
            }
        }

        return null;
    }

    private bool MatchesSearch(SystemCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(SearchFilter))
        {
            return true;
        }

        var query = SearchFilter.Trim();
        return entry.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
               || (entry.Note ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyStored(Phase8StoredContent stored, SystemCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Label))
        {
            entry.Label = stored.Name;
        }

        entry.Id = stored.Id;
        Current = Clone(entry);
        CurrentId = stored.Id;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    private static SystemCatalogEntry Clone(SystemCatalogEntry source) => new()
    {
        Id = source.Id,
        Key = source.Key,
        Label = source.Label,
        Note = source.Note,
    };

    private static string CopyKey(string key)
    {
        const string suffix = "_copie";
        var candidate = (key ?? string.Empty) + suffix;
        var max = Frog.Core.Character.CharacterPayloadWorldFlags.MaxKeyUtf8Bytes;
        if (System.Text.Encoding.UTF8.GetByteCount(candidate) <= max)
        {
            return candidate;
        }

        var room = Math.Max(0, max - suffix.Length);
        var trimmed = key ?? string.Empty;
        if (trimmed.Length > room)
        {
            trimmed = trimmed[..room];
        }

        return trimmed + suffix;
    }
}

public sealed record SystemCatalogListItem(
    Guid Id,
    string Key,
    string Label,
    string Note,
    ContentPublishStatus Status,
    long Revision,
    long? PublishedRevision);
