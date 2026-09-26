using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Session éditeur du document Système (brouillon + publication).</summary>
public sealed class SystemWorkspaceSession
{
    private readonly ISystemRepository _repository;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SystemWorkspaceSession(ISystemRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ContentRepositoryCapabilities Capabilities => _repository.Capabilities;

    public SystemDefinition? Current { get; private set; }

    public long CurrentRevision { get; private set; }

    public ContentPublishStatus CurrentStatus { get; private set; } = ContentPublishStatus.Draft;

    public long? PublishedRevision { get; private set; }

    public bool IsDirty { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            Current = Empty();
            CurrentRevision = 0;
            CurrentStatus = ContentPublishStatus.Draft;
            PublishedRevision = null;
            IsDirty = false;
            return;
        }

        ApplyStored(stored);
    }

    public void MarkDirty() => IsDirty = true;

    public async Task<SaveSystemResult> SaveCurrentAsync(
        SaveContentIntent intent,
        CancellationToken cancellationToken = default)
    {
        var current = Current;
        if (current is null)
        {
            return new SaveSystemResult.ValidationFailed("Aucun document système ouvert.");
        }

        if (!Capabilities.AllowsSave)
        {
            return new SaveSystemResult.NotDurable("Persistance non disponible.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveSystemResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            var result = await _repository
                .SaveAsync(
                    new SaveSystemRequest
                    {
                        Definition = Clone(current),
                        ExpectedRevision = CurrentRevision,
                        Intent = intent,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveSystemResult.Success success)
            {
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

    private void ApplyStored(StoredSystem stored)
    {
        Current = Clone(stored.Definition);
        CurrentRevision = stored.Revision;
        CurrentStatus = stored.Status;
        PublishedRevision = stored.PublishedRevision;
        IsDirty = false;
    }

    internal static SystemDefinition Empty() => new()
    {
        Id = SystemDefinition.SingletonId,
    };

    internal static SystemDefinition Clone(SystemDefinition source) => new()
    {
        Id = source.Id == Guid.Empty ? SystemDefinition.SingletonId : source.Id,
        Switches = source.Switches.Select(CloneSwitch).ToList(),
        Variables = source.Variables.Select(CloneVariable).ToList(),
    };

    private static SystemSwitchEntry CloneSwitch(SystemSwitchEntry source) => new()
    {
        Id = source.Id,
        Label = source.Label,
        Note = source.Note,
    };

    private static SystemVariableEntry CloneVariable(SystemVariableEntry source) => new()
    {
        Id = source.Id,
        Label = source.Label,
        Note = source.Note,
    };
}
