using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresTilesetRepository : ITilesetRepository, IPublishedTilesetCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit (publication).</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresTilesetRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveTilesetResult> SaveAsync(
        SaveTilesetRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveTilesetResult>(async (db, ct) =>
        {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Definition.Validate(out var error))
        {
            return new SaveTilesetResult.ValidationFailed(error ?? "Tileset invalide.");
        }

        if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return new SaveTilesetResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            return await SaveCoreAsync(db, request, ct).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveTilesetResult> SaveCoreAsync(FrogDbContext db, SaveTilesetRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.TilesetId is not Guid tilesetId || tilesetId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveTilesetResult.Conflict(0);
                }

                if (await PathTakenAsync(db, request.Definition.LogicalPath, excludeId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new SaveTilesetResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                if (request.Definition.EditorPaletteId is int palette
                    && await PaletteTakenAsync(db, palette, excludeId: null, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveTilesetResult.ValidationFailed("EditorPaletteId déjà utilisé.");
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.Tilesets.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;

                if (request.Intent == SaveContentIntent.Publish)
                {
                    publishedRevision = await PublishSnapshotAsync(db, entity, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }
            else
            {
                if (await PathTakenAsync(db, request.Definition.LogicalPath, tilesetId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new SaveTilesetResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                if (request.Definition.EditorPaletteId is int palette
                    && await PaletteTakenAsync(db, palette, tilesetId, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveTilesetResult.ValidationFailed("EditorPaletteId déjà utilisé.");
                }

                var updatedRows = await db.Tilesets
                    .Where(t => t.Id == tilesetId && t.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(t => t.Revision, request.ExpectedRevision + 1)
                            .SetProperty(t => t.Name, request.Definition.Name)
                            .SetProperty(t => t.LogicalPath, request.Definition.LogicalPath)
                            .SetProperty(t => t.TileSizePixels, request.Definition.TileSizePixels)
                            .SetProperty(t => t.Width, request.Definition.WidthPixels)
                            .SetProperty(t => t.Height, request.Definition.HeightPixels)
                            .SetProperty(t => t.Sha256Hex, request.Definition.Sha256Hex.ToUpperInvariant())
                            .SetProperty(t => t.EditorPaletteId, request.Definition.EditorPaletteId)
                            .SetProperty(t => t.Status, ContentPublishStatus.Draft)
                            .SetProperty(t => t.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveTilesetResult.Conflict(
                        await ReadRevisionAsync(db, tilesetId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = tilesetId;

                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.Tilesets.AsNoTracking()
                        .FirstAsync(t => t.Id == tilesetId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, entity, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            if (TestBeforeCommitAsync is not null)
            {
                await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveTilesetResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveTilesetResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(FrogDbContext db, TilesetEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        var snapshot = new TilesetPublishedSnapshotEntity
        {
            Id = snapshotId,
            TilesetId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            LogicalPath = entity.LogicalPath,
            TileSizePixels = entity.TileSizePixels,
            Width = entity.Width,
            Height = entity.Height,
            Sha256Hex = entity.Sha256Hex,
            EditorPaletteId = entity.EditorPaletteId,
        };
        db.TilesetPublishedSnapshots.Add(snapshot);
        db.TilesetPublicationHistory.Add(new TilesetPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            TilesetId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.Tilesets
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

    public async Task<StoredTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredTileset?>(async (db, ct) =>
        {
        var entity = await db.Tilesets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tilesetId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredTileset?> LoadPublishedByIdAsync(
        Guid tilesetId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredTileset?>(async (db, ct) =>
        {
        var tip = await db.Tilesets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tilesetId, ct)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapId)
        {
            return null;
        }

        var snap = await db.TilesetPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapId, ct)
            .ConfigureAwait(false);
        if (snap is null)
        {
            return null;
        }

        return new StoredTileset
        {
            TilesetId = snap.TilesetId,
            Definition = FromSnapshot(snap),
            Revision = snap.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = snap.Revision,
        };
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<TilesetCatalogEntry>>(async (db, ct) =>
        {
        var q = db.Tilesets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => EF.Functions.ILike(t.Name, $"%{s}%")
                             || EF.Functions.ILike(t.LogicalPath, $"%{s}%"));
        }

        if (statusFilter is { } st)
        {
            q = q.Where(t => t.Status == st);
        }

        return await q
            .OrderBy(t => t.Name)
            .Select(t => new TilesetCatalogEntry
            {
                TilesetId = t.Id,
                Name = t.Name,
                LogicalPath = t.LogicalPath,
                Revision = t.Revision,
                Status = t.Status,
                PublishedRevision = t.PublishedRevision,
                EditorPaletteId = t.EditorPaletteId,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteTilesetResult> DeleteAsync(
        Guid tilesetId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteTilesetResult>(async (db, ct) =>
        {
        var entity = await db.Tilesets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tilesetId, ct)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return new DeleteTilesetResult.NotFound();
        }

        if (entity.EditorPaletteId is int palette
            && await IsPaletteIdReferencedByMapsAsync(palette, ct).ConfigureAwait(false))
        {
            return new DeleteTilesetResult.Referenced(
                $"Tileset référencé par des cartes (palette {palette}).");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await db.TilesetPublicationHistory.Where(h => h.TilesetId == tilesetId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.TilesetPublishedSnapshots.Where(s => s.TilesetId == tilesetId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.Tilesets.Where(t => t.Id == tilesetId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new DeleteTilesetResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteTilesetResult.PersistenceFailed(Sanitize(ex.Message));
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsPaletteIdReferencedByMapsAsync(
        int editorPaletteId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<bool>(async (db, ct) =>
        {
        // layers_json est jsonb : caster en text pour LIKE (évite like_escape(jsonb)).
        var needleA = $"%\"tilesetId\":{editorPaletteId}%";
        var needleB = $"%\"tilesetId\": {editorPaletteId}%";
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT EXISTS(
              SELECT 1 FROM world.map_cells
              WHERE layers_json::text LIKE @a OR layers_json::text LIKE @b
            )
            """;
        var pA = cmd.CreateParameter();
        pA.ParameterName = "a";
        pA.Value = needleA;
        cmd.Parameters.Add(pA);
        var pB = cmd.CreateParameter();
        pB.ParameterName = "b";
        pB.Value = needleB;
        cmd.Parameters.Add(pB);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is bool flag && flag;
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TilesetDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<TilesetDefinition>>(async (db, ct) =>
        {
        var tips = await db.Tilesets.AsNoTracking()
            .Where(t => t.PublishedSnapshotId != null)
            .Select(t => t.PublishedSnapshotId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (tips.Count == 0)
        {
            return Array.Empty<TilesetDefinition>();
        }

        var snaps = await db.TilesetPublishedSnapshots.AsNoTracking()
            .Where(s => tips.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return snaps.Select(FromSnapshot).ToList();
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> PathTakenAsync(FrogDbContext db, string path, Guid? excludeId, CancellationToken ct)
        => await db.Tilesets.AsNoTracking()
            .AnyAsync(
                t => t.LogicalPath == path && (excludeId == null || t.Id != excludeId),
                ct)
            .ConfigureAwait(false);

    private async Task<bool> PaletteTakenAsync(FrogDbContext db, int palette, Guid? excludeId, CancellationToken ct)
        => await db.Tilesets.AsNoTracking()
            .AnyAsync(
                t => t.EditorPaletteId == palette && (excludeId == null || t.Id != excludeId),
                ct)
            .ConfigureAwait(false);

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken ct)
    {
        var rev = await db.Tilesets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (long?)t.Revision)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return rev ?? 0;
    }

    private static TilesetEntity ToEntity(TilesetDefinition def, Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Name = def.Name.Trim(),
        LogicalPath = def.LogicalPath.Trim().Replace('\\', '/'),
        TileSizePixels = def.TileSizePixels,
        Width = def.WidthPixels,
        Height = def.HeightPixels,
        Sha256Hex = def.Sha256Hex.ToUpperInvariant(),
        EditorPaletteId = def.EditorPaletteId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredTileset ToStored(TilesetEntity e) => new()
    {
        TilesetId = e.Id,
        Definition = new TilesetDefinition
        {
            Id = e.Id,
            Name = e.Name,
            LogicalPath = e.LogicalPath,
            TileSizePixels = e.TileSizePixels,
            WidthPixels = e.Width,
            HeightPixels = e.Height,
            Sha256Hex = e.Sha256Hex,
            EditorPaletteId = e.EditorPaletteId,
        },
        Revision = e.Revision,
        Status = e.Status,
        PublishedRevision = e.PublishedRevision,
    };

    private static TilesetDefinition FromSnapshot(TilesetPublishedSnapshotEntity s) => new()
    {
        Id = s.TilesetId,
        Name = s.Name,
        LogicalPath = s.LogicalPath,
        TileSizePixels = s.TileSizePixels,
        WidthPixels = s.Width,
        HeightPixels = s.Height,
        Sha256Hex = s.Sha256Hex,
        EditorPaletteId = s.EditorPaletteId,
    };

    private static string Sanitize(string message)
    {
        // Ne jamais remonter de chaînes de connexion / secrets.
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance tileset.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
