using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresMapObjectRepository : IMapObjectRepository, IPublishedMapObjectCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresMapObjectRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public Task<SaveMapObjectResult> SaveAsync(
        SaveMapObjectRequest request,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<SaveMapObjectResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Definition.Validate(out var error))
            {
                return new SaveMapObjectResult.ValidationFailed(error ?? "Objet de carte invalide.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveMapObjectResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
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

    private async Task<SaveMapObjectResult> SaveCoreAsync(
        FrogDbContext db,
        SaveMapObjectRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;
            var definition = Normalize(request.Definition);

            if (request.MapObjectId is not Guid mapObjectId || mapObjectId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveMapObjectResult.Conflict(0);
                }

                if (await PathTakenAsync(db, definition.LogicalPath, excludeId: null, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveMapObjectResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                if (await PlacementTakenAsync(db, definition.PlacementId, excludeId: null, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveMapObjectResult.ValidationFailed("Identifiant de placement déjà utilisé.");
                }

                var id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
                var entity = ToEntity(definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.MapObjects.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, definition.PngBytes, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (await PathTakenAsync(db, definition.LogicalPath, mapObjectId, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveMapObjectResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                if (await PlacementTakenAsync(db, definition.PlacementId, mapObjectId, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveMapObjectResult.ValidationFailed("Identifiant de placement déjà utilisé.");
                }

                var updatedRows = await db.MapObjects
                    .Where(t => t.Id == mapObjectId && t.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(t => t.Revision, request.ExpectedRevision + 1)
                            .SetProperty(t => t.Name, definition.Name)
                            .SetProperty(t => t.LogicalPath, definition.LogicalPath)
                            .SetProperty(t => t.PlacementId, definition.PlacementId)
                            .SetProperty(t => t.FootprintWidthTiles, definition.FootprintWidthTiles)
                            .SetProperty(t => t.FootprintHeightTiles, definition.FootprintHeightTiles)
                            .SetProperty(t => t.Width, definition.WidthPixels)
                            .SetProperty(t => t.Height, definition.HeightPixels)
                            .SetProperty(t => t.Sha256Hex, definition.Sha256Hex.ToUpperInvariant())
                            .SetProperty(t => t.Status, ContentPublishStatus.Draft)
                            .SetProperty(t => t.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveMapObjectResult.Conflict(
                        await ReadRevisionAsync(db, mapObjectId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = mapObjectId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.MapObjects.AsNoTracking()
                        .FirstAsync(t => t.Id == mapObjectId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(
                            db, entity, now, definition.PngBytes, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveMapObjectResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveMapObjectResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        MapObjectEntity entity,
        DateTimeOffset now,
        byte[]? pngBytes,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        byte[]? persistPng = pngBytes is { Length: > 0 } ? pngBytes : null;
        if (persistPng is null && entity.PublishedSnapshotId is Guid previousSnapId)
        {
            var previous = await db.MapObjectPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == previousSnapId, cancellationToken)
                .ConfigureAwait(false);
            if (previous?.PngBytes is { Length: > 0 } kept)
            {
                persistPng = kept;
            }
        }

        db.MapObjectPublishedSnapshots.Add(new MapObjectPublishedSnapshotEntity
        {
            Id = snapshotId,
            MapObjectId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            LogicalPath = entity.LogicalPath,
            PlacementId = entity.PlacementId,
            FootprintWidthTiles = entity.FootprintWidthTiles,
            FootprintHeightTiles = entity.FootprintHeightTiles,
            Width = entity.Width,
            Height = entity.Height,
            Sha256Hex = entity.Sha256Hex,
            PngBytes = persistPng,
        });
        db.MapObjectPublicationHistory.Add(new MapObjectPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            MapObjectId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.MapObjects
            .Where(t => t.Id == entity.Id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.Status, ContentPublishStatus.Published)
                    .SetProperty(t => t.PublishedRevision, entity.Revision)
                    .SetProperty(t => t.PublishedSnapshotId, snapshotId)
                    .SetProperty(t => t.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public Task<StoredMapObject?> LoadByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<StoredMapObject?>(async (db, ct) =>
        {
            var entity = await db.MapObjects.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == mapObjectId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken);

    public Task<StoredMapObject?> LoadPublishedByIdAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<StoredMapObject?>(async (db, ct) =>
        {
            var tip = await db.MapObjects.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == mapObjectId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapId)
            {
                return null;
            }

            var snap = await db.MapObjectPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == snapId, ct)
                .ConfigureAwait(false);
            return snap is null
                ? null
                : new StoredMapObject
                {
                    MapObjectId = snap.MapObjectId,
                    Definition = FromSnapshot(snap),
                    Revision = snap.Revision,
                    Status = ContentPublishStatus.Published,
                    PublishedRevision = snap.Revision,
                };
        }, cancellationToken);

    public Task<IReadOnlyList<MapObjectCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<MapObjectCatalogEntry>>(async (db, ct) =>
        {
            var q = db.MapObjects.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(t => EF.Functions.ILike(t.Name, $"%{s}%")
                                 || EF.Functions.ILike(t.LogicalPath, $"%{s}%")
                                 || EF.Functions.ILike(t.PlacementId, $"%{s}%"));
            }

            if (statusFilter is { } st)
            {
                q = q.Where(t => t.Status == st);
            }

            return await q
                .OrderBy(t => t.Name)
                .Select(t => new MapObjectCatalogEntry
                {
                    MapObjectId = t.Id,
                    Name = t.Name,
                    LogicalPath = t.LogicalPath,
                    PlacementId = t.PlacementId,
                    Revision = t.Revision,
                    Status = t.Status,
                    PublishedRevision = t.PublishedRevision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    public Task<DeleteMapObjectResult> DeleteAsync(Guid mapObjectId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<DeleteMapObjectResult>(async (db, ct) =>
        {
            var exists = await db.MapObjects.AsNoTracking().AnyAsync(t => t.Id == mapObjectId, ct).ConfigureAwait(false);
            if (!exists)
            {
                return new DeleteMapObjectResult.NotFound();
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await db.MapObjectPublicationHistory.Where(h => h.MapObjectId == mapObjectId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.MapObjectPublishedSnapshots.Where(s => s.MapObjectId == mapObjectId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.MapObjects.Where(t => t.Id == mapObjectId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteMapObjectResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteMapObjectResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken);

    public Task<IReadOnlyList<MapObjectDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<MapObjectDefinition>>(async (db, ct) =>
        {
            var tips = await db.MapObjects.AsNoTracking()
                .Where(t => t.PublishedSnapshotId != null)
                .Select(t => t.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<MapObjectDefinition>();
            }

            var snaps = await db.MapObjectPublishedSnapshots.AsNoTracking()
                .Where(s => tips.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snaps.Select(FromSnapshot).ToList();
        }, cancellationToken);

    private static async Task<bool> PathTakenAsync(FrogDbContext db, string path, Guid? excludeId, CancellationToken ct)
        => await db.MapObjects.AsNoTracking()
            .AnyAsync(t => t.LogicalPath == path && (excludeId == null || t.Id != excludeId), ct)
            .ConfigureAwait(false);

    private static async Task<bool> PlacementTakenAsync(FrogDbContext db, string placementId, Guid? excludeId, CancellationToken ct)
        => await db.MapObjects.AsNoTracking()
            .AnyAsync(t => t.PlacementId == placementId && (excludeId == null || t.Id != excludeId), ct)
            .ConfigureAwait(false);

    private static async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken ct)
    {
        var rev = await db.MapObjects.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (long?)t.Revision)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return rev ?? 0;
    }

    private static MapObjectDefinition Normalize(MapObjectDefinition def)
    {
        def.Name = def.Name.Trim();
        def.LogicalPath = def.LogicalPath.Trim().Replace('\\', '/');
        def.PlacementId = def.PlacementId.Trim();
        def.Sha256Hex = def.Sha256Hex.Trim();
        return def;
    }

    private static MapObjectEntity ToEntity(MapObjectDefinition def, Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Name = def.Name.Trim(),
        LogicalPath = def.LogicalPath.Trim().Replace('\\', '/'),
        PlacementId = def.PlacementId.Trim(),
        FootprintWidthTiles = def.FootprintWidthTiles,
        FootprintHeightTiles = def.FootprintHeightTiles,
        Width = def.WidthPixels,
        Height = def.HeightPixels,
        Sha256Hex = def.Sha256Hex.ToUpperInvariant(),
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredMapObject ToStored(MapObjectEntity e) => new()
    {
        MapObjectId = e.Id,
        Definition = new MapObjectDefinition
        {
            Id = e.Id,
            Name = e.Name,
            LogicalPath = e.LogicalPath,
            PlacementId = e.PlacementId,
            FootprintWidthTiles = e.FootprintWidthTiles,
            FootprintHeightTiles = e.FootprintHeightTiles,
            WidthPixels = e.Width,
            HeightPixels = e.Height,
            Sha256Hex = e.Sha256Hex,
        },
        Revision = e.Revision,
        Status = e.Status,
        PublishedRevision = e.PublishedRevision,
    };

    private static MapObjectDefinition FromSnapshot(MapObjectPublishedSnapshotEntity s) => new()
    {
        Id = s.MapObjectId,
        Name = s.Name,
        LogicalPath = s.LogicalPath,
        PlacementId = s.PlacementId,
        FootprintWidthTiles = s.FootprintWidthTiles,
        FootprintHeightTiles = s.FootprintHeightTiles,
        WidthPixels = s.Width,
        HeightPixels = s.Height,
        Sha256Hex = s.Sha256Hex,
        PngBytes = s.PngBytes,
    };

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance objet de carte.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
