using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur du document unique Système (brouillon + publication).</summary>
public sealed class SystemSettingsWorkspaceSession
{
    private readonly ISystemSettingsRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SystemSettingsWorkspaceSession(ISystemSettingsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public bool CanPersist => Capabilities.IsDurablePersistence;

    public SystemDefinition Current { get; private set; } = SystemDefinition.CreateDefault();

    public Guid? CurrentId { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            Current = SystemDefinition.CreateDefault();
            CurrentId = null;
            CurrentRevision = 0;
            CurrentStatus = ContentPublishStatus.Draft;
            PublishedRevision = null;
            IsDirty = false;
            return;
        }

        ApplyStored(stored);
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    public async Task<SaveSystemSettingsResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (!Capabilities.AllowsSave)
        {
            return new SaveSystemSettingsResult.NotDurable("Persistance non disponible.");
        }

        if (!SystemDefinition.TryCanonicalize(Current, out var canonical, out var error))
        {
            return new SaveSystemSettingsResult.ValidationFailed(error ?? "Paramètres système invalides.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveSystemSettingsResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveSystemSettingsRequest
                    {
                        SettingsId = CurrentId,
                        Definition = canonical.Copy(),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveSystemSettingsResult.Success success)
            {
                Current = canonical.Copy();
                Current.Id = success.SettingsId;
                CurrentId = success.SettingsId;
                CurrentRevision = success.NewRevision;
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
            }

            return result;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void ApplyStored(StoredSystemSettings stored)
    {
        Current = stored.Definition.Copy();
        CurrentId = stored.SettingsId;
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }
}
