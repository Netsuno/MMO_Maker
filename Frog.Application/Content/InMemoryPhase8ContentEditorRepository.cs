namespace Frog.Application.Content;

/// <summary>Dépôt Phase 8 en mémoire pour smoke / tests (pas de PostgreSQL).</summary>
public sealed class InMemoryPhase8ContentEditorRepository : IPhase8ContentEditorRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Phase8StoredContent> _items = new();

    public InMemoryPhase8ContentEditorRepository(ContentRepositoryCapabilities? capabilities = null)
    {
        Capabilities = capabilities ?? ContentRepositoryCapabilities.InMemoryTest;
    }

    public ContentRepositoryCapabilities Capabilities { get; }

    public Task<Phase8SaveContentResult> SaveAsync(
        Phase8SaveContentRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Task.FromResult<Phase8SaveContentResult>(
                new Phase8SaveContentResult.ValidationFailed("Nom requis."));
        }

        lock (_sync)
        {
            Guid id;
            long newRevision;
            long? publishedRevision;

            if (request.ContentId is Guid existingId && existingId != Guid.Empty)
            {
                if (!_items.TryGetValue(existingId, out var current))
                {
                    return Task.FromResult<Phase8SaveContentResult>(
                        new Phase8SaveContentResult.ValidationFailed("Contenu introuvable."));
                }

                if (current.Revision != request.ExpectedRevision)
                {
                    return Task.FromResult<Phase8SaveContentResult>(
                        new Phase8SaveContentResult.Conflict(current.Revision));
                }

                id = existingId;
                newRevision = current.Revision + 1;
                publishedRevision = current.PublishedRevision;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    publishedRevision = newRevision;
                }

                _items[id] = current with
                {
                    Name = request.Name.Trim(),
                    EditorAliasId = request.EditorAliasId,
                    PayloadJson = request.PayloadJson,
                    Revision = newRevision,
                    Status = request.Intent == SaveContentIntent.Publish
                        ? ContentPublishStatus.Published
                        : ContentPublishStatus.Draft,
                    PublishedRevision = publishedRevision,
                };
            }
            else
            {
                id = request.NewId ?? Guid.NewGuid();
                newRevision = 1;
                publishedRevision = request.Intent == SaveContentIntent.Publish ? 1L : null;
                _items[id] = new Phase8StoredContent(
                    id,
                    request.Kind,
                    request.Name.Trim(),
                    request.EditorAliasId,
                    request.PayloadJson,
                    newRevision,
                    request.Intent == SaveContentIntent.Publish
                        ? ContentPublishStatus.Published
                        : ContentPublishStatus.Draft,
                    publishedRevision);
            }

            return Task.FromResult<Phase8SaveContentResult>(
                new Phase8SaveContentResult.Success(newRevision, id, publishedRevision));
        }
    }

    public Task<Phase8StoredContent?> LoadDraftByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);
        }
    }

    public Task<IReadOnlyList<Phase8ContentSummary>> ListSummariesAsync(
        Phase8ContentKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var rows = _items.Values
                .Where(i => i.Kind == kind)
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(i => new Phase8ContentSummary(
                    i.Id,
                    i.Kind,
                    i.Name,
                    i.EditorAliasId,
                    i.Revision,
                    i.Status,
                    i.PublishedRevision))
                .ToList();
            return Task.FromResult<IReadOnlyList<Phase8ContentSummary>>(rows);
        }
    }

    public Task<Phase8DeleteContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_items.Remove(id))
            {
                return Task.FromResult<Phase8DeleteContentResult>(new Phase8DeleteContentResult.NotFound());
            }

            return Task.FromResult<Phase8DeleteContentResult>(new Phase8DeleteContentResult.Success());
        }
    }
}
