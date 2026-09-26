using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemorySystemSettingsRepository : ISystemSettingsRepository, IPublishedSystemSettings
{
    private readonly IPublishedActorCatalog? _actors;
    private readonly IMapRepository? _maps;
    private readonly object _gate = new();
    private DraftRecord? _draft;
    private PublishedRecord? _published;

    public InMemorySystemSettingsRepository(
        IPublishedActorCatalog? actors = null,
        IMapRepository? maps = null,
        ContentRepositoryCapabilities? capabilities = null)
    {
        _actors = actors;
        _maps = maps;
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public async Task<SaveSystemSettingsResult> SaveAsync(
        SaveSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return new SaveSystemSettingsResult.NotDurable("Persistance mémoire démo désactivée.");
        }

        if (!SystemDefinition.TryCanonicalize(request.Definition, out var definition, out var error))
        {
            return new SaveSystemSettingsResult.ValidationFailed(error ?? "Paramètres système invalides.");
        }

        var referenceError = await SystemSettingsReferenceChecks
            .ValidateAsync(definition, _actors, _maps, cancellationToken)
            .ConfigureAwait(false);
        if (referenceError is not null)
        {
            return new SaveSystemSettingsResult.ValidationFailed(referenceError);
        }

        lock (_gate)
        {
            long newRevision;
            if (request.SettingsId is not Guid existing || existing == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveSystemSettingsResult.Conflict(0);
                }

                if (_draft is not null)
                {
                    return new SaveSystemSettingsResult.Conflict(_draft.Revision);
                }

                newRevision = 1;
                _draft = new DraftRecord(
                    SystemDefinition.SingletonId,
                    definition.Copy(),
                    newRevision,
                    ContentPublishStatus.Draft,
                    null);
            }
            else
            {
                if (existing != SystemDefinition.SingletonId || _draft is null || _draft.Id != existing)
                {
                    return new SaveSystemSettingsResult.Conflict(_draft?.Revision ?? 0);
                }

                if (_draft.Revision != request.ExpectedRevision)
                {
                    return new SaveSystemSettingsResult.Conflict(_draft.Revision);
                }

                newRevision = _draft.Revision + 1;
                _draft = _draft with
                {
                    Definition = definition.Copy(),
                    Revision = newRevision,
                    Status = ContentPublishStatus.Draft,
                };
            }

            long? publishedRevision = null;
            if (request.Intent == SaveContentIntent.Publish)
            {
                publishedRevision = newRevision;
                _published = new PublishedRecord(_draft.Id, definition.Copy(), newRevision);
                _draft = _draft with
                {
                    Status = ContentPublishStatus.Published,
                    PublishedRevision = publishedRevision,
                };
            }

            return new SaveSystemSettingsResult.Success(newRevision, _draft.Id, publishedRevision);
        }
    }

    public Task<StoredSystemSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(ToStored(_draft));
        }
    }

    public Task<StoredSystemSettings?> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_published is null)
            {
                return Task.FromResult<StoredSystemSettings?>(null);
            }

            return Task.FromResult<StoredSystemSettings?>(new StoredSystemSettings
            {
                SettingsId = _published.Id,
                Definition = _published.Definition.Copy(),
                Revision = _published.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = _published.Revision,
            });
        }
    }

    Task<SystemDefinition?> IPublishedSystemSettings.LoadPublishedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_published?.Definition.Copy());
        }
    }

    private static StoredSystemSettings? ToStored(DraftRecord? draft)
    {
        if (draft is null)
        {
            return null;
        }

        return new StoredSystemSettings
        {
            SettingsId = draft.Id,
            Definition = draft.Definition.Copy(),
            Revision = draft.Revision,
            Status = draft.Status,
            PublishedRevision = draft.PublishedRevision,
        };
    }

    private sealed record DraftRecord(
        Guid Id,
        SystemDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(Guid Id, SystemDefinition Definition, long Revision);
}
