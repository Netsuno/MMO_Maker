using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

/// <summary>Catalogues Phase 8 publiés PostgreSQL (source de vérité production).</summary>
public sealed class PostgresPhase8PublishedCatalogs
    : IPublishedDialogueCatalog,
        IPublishedQuestCatalog,
        IPublishedCommonEventCatalog,
        IPublishedProfessionCatalog,
        IPublishedRecipeCatalog,
        IPublishedRegionCatalog,
        IPublishedWeatherCatalog,
        IPhase8ContentEditorRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresPhase8PublishedCatalogs(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public Task<IReadOnlyList<DialogueDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        ListPublishedKindAsync<DialogueDefinition>(
            Phase8ContentKind.Dialogue,
            Phase8ContentCodec.TryDeserializeDialogue,
            cancellationToken);

    public Task<DialogueDefinition?> TryGetPublishedByIdAsync(Guid dialogueId, CancellationToken cancellationToken = default) =>
        TryGetPublishedKindByIdAsync<DialogueDefinition>(dialogueId, Phase8ContentKind.Dialogue, Phase8ContentCodec.TryDeserializeDialogue, cancellationToken);

    public Task<DialogueDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default) =>
        TryGetPublishedKindByAliasAsync<DialogueDefinition>(editorAliasId, Phase8ContentKind.Dialogue, Phase8ContentCodec.TryDeserializeDialogue, cancellationToken);

    Task<IReadOnlyList<QuestDefinition>> IPublishedQuestCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        ListPublishedKindAsync<QuestDefinition>(Phase8ContentKind.Quest, Phase8ContentCodec.TryDeserializeQuest, cancellationToken);

    Task<QuestDefinition?> IPublishedQuestCatalog.TryGetPublishedByIdAsync(Guid questId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByIdAsync<QuestDefinition>(questId, Phase8ContentKind.Quest, Phase8ContentCodec.TryDeserializeQuest, cancellationToken);

    Task<QuestDefinition?> IPublishedQuestCatalog.TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByAliasAsync<QuestDefinition>(editorAliasId, Phase8ContentKind.Quest, Phase8ContentCodec.TryDeserializeQuest, cancellationToken);

    Task<IReadOnlyList<CommonEventDefinition>> IPublishedCommonEventCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        ListPublishedKindAsync<CommonEventDefinition>(
            Phase8ContentKind.CommonEvent,
            Phase8ContentCodec.TryDeserializeCommonEvent,
            cancellationToken);

    Task<CommonEventDefinition?> IPublishedCommonEventCatalog.TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByIdAsync<CommonEventDefinition>(eventId, Phase8ContentKind.CommonEvent, Phase8ContentCodec.TryDeserializeCommonEvent, cancellationToken);

    Task<CommonEventDefinition?> IPublishedCommonEventCatalog.TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByAliasAsync<CommonEventDefinition>(editorAliasId, Phase8ContentKind.CommonEvent, Phase8ContentCodec.TryDeserializeCommonEvent, cancellationToken);

    Task<IReadOnlyList<ProfessionDefinition>> IPublishedProfessionCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        ListPublishedKindAsync<ProfessionDefinition>(
            Phase8ContentKind.Profession,
            Phase8ContentCodec.TryDeserializeProfession,
            cancellationToken);

    Task<ProfessionDefinition?> IPublishedProfessionCatalog.TryGetPublishedByIdAsync(Guid professionId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByIdAsync<ProfessionDefinition>(professionId, Phase8ContentKind.Profession, Phase8ContentCodec.TryDeserializeProfession, cancellationToken);

    Task<IReadOnlyList<RecipeDefinition>> IPublishedRecipeCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        ListPublishedKindAsync<RecipeDefinition>(Phase8ContentKind.Recipe, Phase8ContentCodec.TryDeserializeRecipe, cancellationToken);

    Task<RecipeDefinition?> IPublishedRecipeCatalog.TryGetPublishedByIdAsync(Guid recipeId, CancellationToken cancellationToken) =>
        TryGetPublishedKindByIdAsync<RecipeDefinition>(recipeId, Phase8ContentKind.Recipe, Phase8ContentCodec.TryDeserializeRecipe, cancellationToken);

    Task<IReadOnlyList<RegionDefinition>> IPublishedRegionCatalog.ListPublishedAsync(CancellationToken cancellationToken) =>
        ListPublishedKindAsync<RegionDefinition>(Phase8ContentKind.Region, Phase8ContentCodec.TryDeserializeRegion, cancellationToken);

    Task<RegionDefinition?> IPublishedRegionCatalog.TryGetRegionForTileAsync(
        int mapId,
        int tileX,
        int tileY,
        CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            var snapshots = await db.Phase8ContentPublishedSnapshots.AsNoTracking()
                .Where(s => s.Kind == (byte)Phase8ContentKind.Region)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            RegionDefinition? best = null;
            foreach (var snap in snapshots)
            {
                if (!Phase8ContentCodec.TryDeserializeRegion(snap.PayloadJson, out var region, out _))
                {
                    continue;
                }

                if (region.MapId == mapId && region.ContainsTile(tileX, tileY))
                {
                    best = region;
                }
            }

            return best;
        }, cancellationToken);

    Task<WeatherProfileDefinition?> IPublishedWeatherCatalog.TryGetPublishedByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken) =>
        TryGetPublishedKindByIdAsync<WeatherProfileDefinition>(profileId, Phase8ContentKind.WeatherProfile, Phase8ContentCodec.TryDeserializeWeather, cancellationToken);

    public Task<Phase8SaveContentResult> SaveAsync(Phase8SaveContentRequest request, CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new Phase8SaveContentResult.ValidationFailed("Une opération d'enregistrement est déjà en cours.");
            }

            try
            {
                return await SaveCoreAsync(db, request, ct).ConfigureAwait(false);
            }
            finally
            {
                _saveGate.Release();
            }
        }, cancellationToken);

    public Task<Phase8StoredContent?> LoadDraftByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.Phase8ContentDefinitions.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken);

    public Task<IReadOnlyList<Phase8ContentSummary>> ListSummariesAsync(
        Phase8ContentKind kind,
        CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync<IReadOnlyList<Phase8ContentSummary>>(async (db, ct) =>
        {
            var rows = await db.Phase8ContentDefinitions.AsNoTracking()
                .Where(e => e.Kind == (byte)kind)
                .OrderBy(e => e.Name)
                .Select(e => new Phase8ContentSummary(
                    e.Id,
                    (Phase8ContentKind)e.Kind,
                    e.Name,
                    e.EditorAliasId,
                    e.Revision,
                    e.Status,
                    e.PublishedRevision))
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows;
        }, cancellationToken);

    public Task<Phase8DeleteContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync<Phase8DeleteContentResult>(async (db, ct) =>
        {
            try
            {
                var exists = await db.Phase8ContentDefinitions.AsNoTracking()
                    .AnyAsync(e => e.Id == id, ct)
                    .ConfigureAwait(false);
                if (!exists)
                {
                    return new Phase8DeleteContentResult.NotFound();
                }

                await db.Phase8ContentPublicationHistory
                    .Where(h => h.ContentDefinitionId == id)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
                await db.Phase8ContentPublishedSnapshots
                    .Where(s => s.ContentDefinitionId == id)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
                await db.Phase8ContentDefinitions
                    .Where(e => e.Id == id)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
                return new Phase8DeleteContentResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new Phase8DeleteContentResult.PersistenceFailed(ex.Message);
            }
        }, cancellationToken);

    private async Task<Phase8SaveContentResult> SaveCoreAsync(
        FrogDbContext db,
        Phase8SaveContentRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (request.Intent == SaveContentIntent.Publish
            && request.Kind == Phase8ContentKind.CommonEvent)
        {
            var cycleError = await ValidateCommonEventCyclesAsync(db, request, cancellationToken)
                .ConfigureAwait(false);
            if (cycleError is not null)
            {
                return new Phase8SaveContentResult.ValidationFailed(cycleError);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payloadJson = request.PayloadJson;
            if (request.ContentId is not Guid id || id == Guid.Empty)
            {
                id = request.NewId ?? Guid.NewGuid();
                db.Phase8ContentDefinitions.Add(new Phase8ContentDefinitionEntity
                {
                    Id = id,
                    Kind = (byte)request.Kind,
                    Name = request.Name.Trim(),
                    EditorAliasId = request.EditorAliasId,
                    PayloadJson = payloadJson,
                    Revision = 1,
                    Status = ContentPublishStatus.Draft,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                long? pubRev = null;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    pubRev = await PublishSnapshotAsync(db, id, request.Kind, 1, request.Name, request.EditorAliasId, payloadJson, now, cancellationToken)
                        .ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new Phase8SaveContentResult.Success(1, id, pubRev);
            }

            var updated = await db.Phase8ContentDefinitions
                .Where(e => e.Id == id && e.Revision == request.ExpectedRevision)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.Revision, request.ExpectedRevision + 1)
                        .SetProperty(e => e.Name, request.Name.Trim())
                        .SetProperty(e => e.EditorAliasId, request.EditorAliasId)
                        .SetProperty(e => e.PayloadJson, payloadJson)
                        .SetProperty(e => e.Status, ContentPublishStatus.Draft)
                        .SetProperty(e => e.UpdatedAtUtc, now),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updated == 0)
            {
                var current = await db.Phase8ContentDefinitions.AsNoTracking()
                    .Where(e => e.Id == id)
                    .Select(e => e.Revision)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                return new Phase8SaveContentResult.Conflict(current);
            }

            var newRevision = request.ExpectedRevision + 1;
            long? publishedRevision = null;
            if (request.Intent == SaveContentIntent.Publish)
            {
                publishedRevision = await PublishSnapshotAsync(
                        db,
                        id,
                        request.Kind,
                        newRevision,
                        request.Name,
                        request.EditorAliasId,
                        payloadJson,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new Phase8SaveContentResult.Success(newRevision, id, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new Phase8SaveContentResult.PersistenceFailed(ex.Message);
        }
    }

    private static async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        Guid contentId,
        Phase8ContentKind kind,
        long revision,
        string name,
        int? editorAliasId,
        string payloadJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.Phase8ContentPublishedSnapshots.Add(new Phase8ContentPublishedSnapshotEntity
        {
            Id = snapshotId,
            ContentDefinitionId = contentId,
            Kind = (byte)kind,
            Revision = revision,
            PublishedAtUtc = now,
            Name = name.Trim(),
            EditorAliasId = editorAliasId,
            PayloadJson = payloadJson,
        });
        db.Phase8ContentPublicationHistory.Add(new Phase8ContentPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ContentDefinitionId = contentId,
            SnapshotId = snapshotId,
            Revision = revision,
            PublishedAtUtc = now,
        });
        await db.Phase8ContentDefinitions
            .Where(e => e.Id == contentId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.Status, ContentPublishStatus.Published)
                    .SetProperty(e => e.PublishedRevision, revision)
                    .SetProperty(e => e.PublishedSnapshotId, snapshotId)
                    .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return revision;
    }

    private Task<IReadOnlyList<T>> ListPublishedKindAsync<T>(
        Phase8ContentKind kind,
        TryDeserialize<T> deserialize,
        CancellationToken cancellationToken) =>
        _gate.ExecuteAsync<IReadOnlyList<T>>(async (db, ct) =>
        {
            var rows = await db.Phase8ContentPublishedSnapshots.AsNoTracking()
                .Where(s => s.Kind == (byte)kind)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var list = new List<T>();
            foreach (var row in rows)
            {
                if (deserialize(row.PayloadJson, out var item, out _))
                {
                    list.Add(item);
                }
            }

            return list;
        }, cancellationToken);

    private Task<T?> TryGetPublishedKindByIdAsync<T>(
        Guid id,
        Phase8ContentKind kind,
        TryDeserialize<T> deserialize,
        CancellationToken cancellationToken) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.Phase8ContentPublishedSnapshots.AsNoTracking()
                .Where(s => s.Kind == (byte)kind && s.ContentDefinitionId == id)
                .OrderByDescending(s => s.Revision)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (row is null)
            {
                return default;
            }

            return deserialize(row.PayloadJson, out var item, out _) ? item : default;
        }, cancellationToken);

    private Task<T?> TryGetPublishedKindByAliasAsync<T>(
        int editorAliasId,
        Phase8ContentKind kind,
        TryDeserialize<T> deserialize,
        CancellationToken cancellationToken) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.Phase8ContentPublishedSnapshots.AsNoTracking()
                .Where(s => s.Kind == (byte)kind && s.EditorAliasId == editorAliasId)
                .OrderByDescending(s => s.Revision)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (row is null)
            {
                return default;
            }

            return deserialize(row.PayloadJson, out var item, out _) ? item : default;
        }, cancellationToken);

    private static Phase8StoredContent ToStored(Phase8ContentDefinitionEntity entity) =>
        new(entity.Id, (Phase8ContentKind)entity.Kind, entity.Name, entity.EditorAliasId, entity.PayloadJson, entity.Revision, entity.Status, entity.PublishedRevision);

    private async Task<string?> ValidateCommonEventCyclesAsync(
        FrogDbContext db,
        Phase8SaveContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Phase8ContentCodec.TryDeserializeCommonEvent(request.PayloadJson, out var publishing, out var deserializeError))
        {
            return deserializeError ?? "Événement commun invalide.";
        }

        var contentId = request.ContentId is Guid id && id != Guid.Empty
            ? id
            : request.NewId ?? publishing.Id;
        if (contentId == Guid.Empty)
        {
            contentId = Guid.NewGuid();
        }

        publishing.Id = contentId;
        if (string.IsNullOrWhiteSpace(publishing.Name))
        {
            publishing.Name = request.Name;
        }

        if (publishing.EditorAliasId is null && request.EditorAliasId is int alias)
        {
            publishing.EditorAliasId = alias;
        }

        var rows = await db.Phase8ContentPublishedSnapshots.AsNoTracking()
            .Where(s => s.Kind == (byte)Phase8ContentKind.CommonEvent)
            .OrderByDescending(s => s.Revision)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byContent = new Dictionary<Guid, CommonEventDefinition>();
        foreach (var row in rows)
        {
            if (byContent.ContainsKey(row.ContentDefinitionId))
            {
                continue;
            }

            if (Phase8ContentCodec.TryDeserializeCommonEvent(row.PayloadJson, out var existing, out _))
            {
                existing.Id = row.ContentDefinitionId;
                if (existing.EditorAliasId is null && row.EditorAliasId is int publishedAlias)
                {
                    existing.EditorAliasId = publishedAlias;
                }

                byContent[row.ContentDefinitionId] = existing;
            }
        }

        byContent[contentId] = publishing;
        return CommonEventCycleDetector.DetectCycles(byContent.Values.ToList());
    }

    private delegate bool TryDeserialize<T>(string json, out T value, out string? error);
}
