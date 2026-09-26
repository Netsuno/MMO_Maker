using Frog.Core.Models;

namespace Frog.Application.Content;

public sealed class InMemorySystemRepository : ISystemRepository, IPublishedSystemCatalog
{
    private readonly object _gate = new();
    private DraftRecord? _draft;
    private PublishedRecord? _published;

    public InMemorySystemRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<SaveSystemResult> SaveAsync(
        SaveSystemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Capabilities.AllowsSave)
        {
            return Task.FromResult<SaveSystemResult>(
                new SaveSystemResult.NotDurable("Persistance mémoire démo désactivée."));
        }

        if (!request.Definition.Validate(out var error))
        {
            return Task.FromResult<SaveSystemResult>(new SaveSystemResult.ValidationFailed(error!));
        }

        lock (_gate)
        {
            long newRevision;
            if (_draft is null)
            {
                if (request.ExpectedRevision != 0)
                {
                    return Task.FromResult<SaveSystemResult>(new SaveSystemResult.Conflict(0));
                }

                newRevision = 1;
                _draft = new DraftRecord(
                    SystemWorkspaceSession.Clone(request.Definition),
                    newRevision,
                    ContentPublishStatus.Draft,
                    null);
            }
            else
            {
                if (_draft.Revision != request.ExpectedRevision)
                {
                    return Task.FromResult<SaveSystemResult>(new SaveSystemResult.Conflict(_draft.Revision));
                }

                newRevision = _draft.Revision + 1;
                _draft = _draft with
                {
                    Definition = SystemWorkspaceSession.Clone(request.Definition),
                    Revision = newRevision,
                    Status = ContentPublishStatus.Draft,
                };
            }

            long? publishedRevision = null;
            if (request.Intent == SaveContentIntent.Publish)
            {
                publishedRevision = newRevision;
                _published = new PublishedRecord(
                    SystemWorkspaceSession.Clone(_draft.Definition),
                    publishedRevision.Value);
                _draft = _draft with
                {
                    Status = ContentPublishStatus.Published,
                    PublishedRevision = publishedRevision,
                };
            }

            return Task.FromResult<SaveSystemResult>(
                new SaveSystemResult.Success(newRevision, publishedRevision));
        }
    }

    public Task<StoredSystem?> LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_draft is null)
            {
                return Task.FromResult<StoredSystem?>(null);
            }

            return Task.FromResult<StoredSystem?>(ToStored(_draft));
        }
    }

    public Task<StoredSystem?> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_published is null)
            {
                return Task.FromResult<StoredSystem?>(null);
            }

            return Task.FromResult<StoredSystem?>(new StoredSystem
            {
                Definition = SystemWorkspaceSession.Clone(_published.Definition),
                Revision = _published.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = _published.Revision,
            });
        }
    }

    public async Task<SystemDefinition?> LoadPublishedDefinitionAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await LoadPublishedAsync(cancellationToken).ConfigureAwait(false);
        return stored?.Definition;
    }

    private static StoredSystem ToStored(DraftRecord draft) => new()
    {
        Definition = SystemWorkspaceSession.Clone(draft.Definition),
        Revision = draft.Revision,
        Status = draft.Status,
        PublishedRevision = draft.PublishedRevision,
    };

    private sealed record DraftRecord(
        SystemDefinition Definition,
        long Revision,
        ContentPublishStatus Status,
        long? PublishedRevision);

    private sealed record PublishedRecord(SystemDefinition Definition, long Revision);
}
