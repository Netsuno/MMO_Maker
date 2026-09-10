using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresMapEventRepository
    : IMapEventRepository, IPublishedMapEventCatalog, IPublishedMapEventPlacementCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresMapEventRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public Task<SaveMapEventResult> SaveAsync(SaveMapEventRequest request, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (!request.Definition.Validate(out var error))
            {
                return new SaveMapEventResult.ValidationFailed(error ?? "Événement invalide.");
            }

            if (!MapEventPagesCodec.TryDeserializePages(
                    MapEventPagesCodec.SerializePages(request.Definition.Pages),
                    out _,
                    out error))
            {
                return new SaveMapEventResult.ValidationFailed(error ?? "Pages invalides.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveMapEventResult.ValidationFailed("Une opération d'enregistrement est déjà en cours.");
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

    private async Task<SaveMapEventResult> SaveCoreAsync(
        FrogDbContext db,
        SaveMapEventRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.EventId is not Guid eventId || eventId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveMapEventResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = MapEventPersistenceMapper.ToEntity(request.Definition, id, now);
                db.MapEventDefinitions.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var entity = await db.MapEventDefinitions
                    .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return new SaveMapEventResult.ValidationFailed("Événement introuvable.");
                }

                if (entity.Revision != request.ExpectedRevision)
                {
                    return new SaveMapEventResult.Conflict(entity.Revision);
                }

                MapEventPersistenceMapper.ApplyDefinition(entity, request.Definition, now);
                entity.Revision++;
                entity.Status = ContentPublishStatus.Draft;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = entity.Revision;
                savedId = eventId;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveMapEventResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            var current = request.EventId is Guid id && id != Guid.Empty
                ? await ReadRevisionAsync(db, id, cancellationToken).ConfigureAwait(false)
                : 0;
            return new SaveMapEventResult.Conflict(current);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveMapEventResult.PersistenceFailed(ex.Message);
        }
    }

    private static async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken ct) =>
        await db.MapEventDefinitions.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.Revision)
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

    private static async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        MapEventDefinitionEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.MapEventPublishedSnapshots.Add(new MapEventPublishedSnapshotEntity
        {
            Id = snapshotId,
            EventDefinitionId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            CatalogSlug = entity.CatalogSlug,
            EditorAliasId = entity.EditorAliasId,
            PagesJson = entity.PagesJson,
        });
        db.MapEventPublicationHistory.Add(new MapEventPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            EventDefinitionId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.MapEventDefinitions
            .Where(e => e.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.Status, ContentPublishStatus.Published)
                    .SetProperty(e => e.PublishedRevision, entity.Revision)
                    .SetProperty(e => e.PublishedSnapshotId, snapshotId)
                    .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public Task<StoredMapEvent?> LoadByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<StoredMapEvent?>(async (db, ct) =>
        {
            var entity = await db.MapEventDefinitions.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : MapEventPersistenceMapper.ToStored(entity);
        }, cancellationToken);

    public Task<StoredMapEvent?> LoadPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<StoredMapEvent?>(async (db, ct) =>
        {
            var tip = await db.MapEventDefinitions.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.MapEventPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredMapEvent
            {
                EventId = snapshot.EventDefinitionId,
                Definition = MapEventPersistenceMapper.ToDomain(new MapEventDefinitionEntity
                {
                    Id = snapshot.EventDefinitionId,
                    Name = snapshot.Name,
                    CatalogSlug = snapshot.CatalogSlug,
                    EditorAliasId = snapshot.EditorAliasId,
                    PagesJson = snapshot.PagesJson,
                }),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken);

    public Task<IReadOnlyList<MapEventCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<MapEventCatalogEntry>>(async (db, ct) =>
        {
            var query = db.MapEventDefinitions.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(e =>
                    EF.Functions.ILike(e.Name, $"%{value}%")
                    || (e.CatalogSlug != null && EF.Functions.ILike(e.CatalogSlug, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(e => e.Status == status);
            }

            var rows = await query.OrderBy(e => e.Name).ToListAsync(ct).ConfigureAwait(false);
            return rows.Select(e =>
            {
                MapEventPagesCodec.TryDeserializePages(e.PagesJson, out var pages, out _);
                return new MapEventCatalogEntry
                {
                    EventId = e.Id,
                    Name = e.Name,
                    CatalogSlug = e.CatalogSlug,
                    Revision = e.Revision,
                    Status = e.Status,
                    PublishedRevision = e.PublishedRevision,
                    EditorAliasId = e.EditorAliasId,
                    PageCount = pages.Count,
                };
            }).ToList();
        }, cancellationToken);

    public Task<DeleteMapEventResult> DeleteAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<DeleteMapEventResult>(async (db, ct) =>
        {
            if (await IsReferencedByMapPlacementsAsync(eventId, ct).ConfigureAwait(false))
            {
                return new DeleteMapEventResult.Referenced("Événement référencé par des placements carte.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await db.MapEventPublicationHistory.Where(h => h.EventDefinitionId == eventId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.MapEventPublishedSnapshots.Where(s => s.EventDefinitionId == eventId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.MapEventDefinitions.Where(e => e.Id == eventId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteMapEventResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteMapEventResult.PersistenceFailed(ex.Message);
            }
        }, cancellationToken);

    public Task<bool> IsReferencedByMapPlacementsAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
            await db.MapEventPlacements.AsNoTracking()
                .AnyAsync(p => p.EventDefinitionId == eventId, ct)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<IReadOnlyList<MapEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<MapEventDefinition>>(async (db, ct) =>
        {
            var ids = await db.MapEventDefinitions.AsNoTracking()
                .Where(e => e.PublishedSnapshotId != null)
                .Select(e => e.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (ids.Count == 0)
            {
                return Array.Empty<MapEventDefinition>();
            }

            var snapshots = await db.MapEventPublishedSnapshots.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(s => MapEventPersistenceMapper.ToDomain(new MapEventDefinitionEntity
            {
                Id = s.EventDefinitionId,
                Name = s.Name,
                CatalogSlug = s.CatalogSlug,
                EditorAliasId = s.EditorAliasId,
                PagesJson = s.PagesJson,
            })).ToList();
        }, cancellationToken);

    public Task<MapEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<MapEventDefinition?>(async (db, ct) =>
        {
            var stored = await LoadPublishedByIdAsync(eventId, ct).ConfigureAwait(false);
            return stored?.Definition;
        }, cancellationToken);

    public Task<MapEventDefinition?> TryGetPublishedByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<MapEventDefinition?>(async (db, ct) =>
        {
            var snapshot = await (
                    from e in db.MapEventDefinitions.AsNoTracking()
                    where e.EditorAliasId == editorAliasId && e.PublishedSnapshotId != null
                    join s in db.MapEventPublishedSnapshots.AsNoTracking()
                        on e.PublishedSnapshotId equals s.Id
                    select s)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return MapEventPersistenceMapper.ToDomain(new MapEventDefinitionEntity
            {
                Id = snapshot.EventDefinitionId,
                Name = snapshot.Name,
                CatalogSlug = snapshot.CatalogSlug,
                EditorAliasId = snapshot.EditorAliasId,
                PagesJson = snapshot.PagesJson,
            });
        }, cancellationToken);

    public Task<IReadOnlyList<MapEventWireEntry>> GetPlacementsForRuntimeMapAsync(
        int runtimeMapId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<MapEventWireEntry>>(async (db, ct) =>
        {
            var binding = await db.RuntimeMapBindings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.RuntimeMapId == runtimeMapId, ct)
                .ConfigureAwait(false);
            if (binding is null)
            {
                return Array.Empty<MapEventWireEntry>();
            }

            var map = await db.Maps.AsNoTracking()
                .Where(m => m.Id == binding.MapId && m.PublishedSnapshotId != null)
                .Select(m => new { m.PublishedSnapshotId })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (map?.PublishedSnapshotId is not Guid snapshotId)
            {
                return Array.Empty<MapEventWireEntry>();
            }

            var placements = await db.MapPublishedEventPlacements.AsNoTracking()
                .Where(p => p.SnapshotId == snapshotId)
                .OrderBy(p => p.TileY).ThenBy(p => p.TileX)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (placements.Count == 0)
            {
                return Array.Empty<MapEventWireEntry>();
            }

            var eventIds = placements.Select(p => p.EventDefinitionId).Distinct().ToArray();
            var catalogSnapshots = await (
                    from e in db.MapEventDefinitions.AsNoTracking()
                    where eventIds.Contains(e.Id) && e.PublishedSnapshotId != null
                    join s in db.MapEventPublishedSnapshots.AsNoTracking()
                        on e.PublishedSnapshotId equals s.Id
                    select s)
                .ToDictionaryAsync(s => s.EventDefinitionId, ct)
                .ConfigureAwait(false);

            var list = new List<MapEventWireEntry>(placements.Count);
            foreach (var placement in placements)
            {
                if (!catalogSnapshots.TryGetValue(placement.EventDefinitionId, out var catalog))
                {
                    continue;
                }

                list.Add(MapEventPersistenceMapper.ToWireEntry(
                    placement,
                    catalog,
                    StableWireId(placement.Id)));
            }

            return list;
        }, cancellationToken);

    internal static long StableWireId(Guid id)
    {
        var bytes = id.ToByteArray();
        return BitConverter.ToInt64(bytes, 0);
    }

    internal static int StableCatalogWireId(Guid id, int? editorAliasId) =>
        editorAliasId ?? (BitConverter.ToInt32(id.ToByteArray(), 0) & 0x7FFFFFFF);
}
