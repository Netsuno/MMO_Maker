using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresComposedTilesetRepository : IComposedTilesetRepository, IPublishedComposedTilesetCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresComposedTilesetRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public Task<SaveComposedTilesetResult> SaveAsync(
        SaveComposedTilesetRequest request,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<SaveComposedTilesetResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = Clone(request.Definition);
            if (definition.Id == Guid.Empty)
            {
                definition.Id = Guid.NewGuid();
            }

            definition.Normalize();
            if (!definition.Validate(out var error))
            {
                return new SaveComposedTilesetResult.ValidationFailed(error ?? "Tileset composé invalide.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveComposedTilesetResult.ValidationFailed("Une opération d’enregistrement est déjà en cours.");
            }

            try
            {
                return await SaveCoreAsync(db, request, definition, ct).ConfigureAwait(false);
            }
            finally
            {
                _saveGate.Release();
            }
        }, cancellationToken);

    private async Task<SaveComposedTilesetResult> SaveCoreAsync(
        FrogDbContext db,
        SaveComposedTilesetRequest request,
        ComposedTilesetDefinition definition,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tileError = await UpsertTilesAsync(db, definition, now, cancellationToken).ConfigureAwait(false);
            if (tileError is not null)
            {
                db.ChangeTracker.Clear();
                return new SaveComposedTilesetResult.ValidationFailed(tileError);
            }

            var membersJson = ComposedTilesetMemberCodec.Write(definition.Tiles);
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.TilesetId is not Guid tilesetId || tilesetId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    db.ChangeTracker.Clear();
                    return new SaveComposedTilesetResult.Conflict(0);
                }

                if (await PathTakenAsync(db, definition.LogicalPath, excludeId: null, cancellationToken).ConfigureAwait(false))
                {
                    db.ChangeTracker.Clear();
                    return new SaveComposedTilesetResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                var id = definition.Id;
                db.ComposedTilesets.Add(new ComposedTilesetEntity
                {
                    Id = id,
                    Name = definition.Name,
                    LogicalPath = definition.LogicalPath,
                    MembersJson = membersJson,
                    Status = ContentPublishStatus.Draft,
                    Revision = 1,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, id, 1, definition, membersJson, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (await PathTakenAsync(db, definition.LogicalPath, tilesetId, cancellationToken).ConfigureAwait(false))
                {
                    db.ChangeTracker.Clear();
                    return new SaveComposedTilesetResult.ValidationFailed("Chemin logique déjà utilisé.");
                }

                var updatedRows = await db.ComposedTilesets
                    .Where(t => t.Id == tilesetId && t.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(t => t.Revision, request.ExpectedRevision + 1)
                            .SetProperty(t => t.Name, definition.Name)
                            .SetProperty(t => t.LogicalPath, definition.LogicalPath)
                            .SetProperty(t => t.MembersJson, membersJson)
                            .SetProperty(t => t.Status, ContentPublishStatus.Draft)
                            .SetProperty(t => t.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    db.ChangeTracker.Clear();
                    return new SaveComposedTilesetResult.Conflict(
                        await ReadRevisionAsync(db, tilesetId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = tilesetId;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, tilesetId, newRevision, definition, membersJson, now, cancellationToken)
                        .ConfigureAwait(false)
                    : null;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveComposedTilesetResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveComposedTilesetResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        Guid tilesetId,
        long revision,
        ComposedTilesetDefinition definition,
        string membersJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ComposedTilesetPublishedSnapshots.Add(new ComposedTilesetPublishedSnapshotEntity
        {
            Id = snapshotId,
            ComposedTilesetId = tilesetId,
            Revision = revision,
            PublishedAtUtc = now,
            Name = definition.Name,
            LogicalPath = definition.LogicalPath,
            MembersJson = membersJson,
        });
        db.ComposedTilesetPublicationHistory.Add(new ComposedTilesetPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ComposedTilesetId = tilesetId,
            SnapshotId = snapshotId,
            Revision = revision,
            PublishedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await db.ComposedTilesets
            .Where(t => t.Id == tilesetId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.Status, ContentPublishStatus.Published)
                    .SetProperty(t => t.PublishedRevision, revision)
                    .SetProperty(t => t.PublishedSnapshotId, snapshotId)
                    .SetProperty(t => t.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);
        return revision;
    }

    public Task<StoredComposedTileset?> LoadByIdAsync(Guid tilesetId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<StoredComposedTileset?>(async (db, ct) =>
        {
            var entity = await db.ComposedTilesets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tilesetId, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return null;
            }

            var definition = await HydrateAsync(db, entity.Id, entity.Name, entity.LogicalPath, entity.MembersJson, ct)
                .ConfigureAwait(false);
            return new StoredComposedTileset
            {
                TilesetId = entity.Id,
                Definition = definition,
                Revision = entity.Revision,
                Status = entity.Status,
                PublishedRevision = entity.PublishedRevision,
            };
        }, cancellationToken);

    public Task<IReadOnlyList<ComposedTilesetCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<ComposedTilesetCatalogEntry>>(async (db, ct) =>
        {
            var q = db.ComposedTilesets.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(t => EF.Functions.ILike(t.Name, $"%{s}%") || EF.Functions.ILike(t.LogicalPath, $"%{s}%"));
            }

            if (statusFilter is { } st)
            {
                q = q.Where(t => t.Status == st);
            }

            var rows = await q.OrderBy(t => t.Name).ToListAsync(ct).ConfigureAwait(false);
            return rows.Select(t => new ComposedTilesetCatalogEntry
            {
                TilesetId = t.Id,
                Name = t.Name,
                LogicalPath = t.LogicalPath,
                Revision = t.Revision,
                Status = t.Status,
                PublishedRevision = t.PublishedRevision,
                TileCount = ComposedTilesetMemberCodec.Read(t.MembersJson).Count,
            }).ToList();
        }, cancellationToken);

    public Task<DeleteComposedTilesetResult> DeleteAsync(Guid tilesetId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<DeleteComposedTilesetResult>(async (db, ct) =>
        {
            var exists = await db.ComposedTilesets.AsNoTracking().AnyAsync(t => t.Id == tilesetId, ct).ConfigureAwait(false);
            if (!exists)
            {
                return new DeleteComposedTilesetResult.NotFound();
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await db.ComposedTilesetPublicationHistory.Where(h => h.ComposedTilesetId == tilesetId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.ComposedTilesetPublishedSnapshots.Where(s => s.ComposedTilesetId == tilesetId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.ComposedTilesets.Where(t => t.Id == tilesetId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteComposedTilesetResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteComposedTilesetResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken);

    public Task<IReadOnlyList<ComposedTilesetDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<ComposedTilesetDefinition>>(async (db, ct) =>
        {
            var tips = await db.ComposedTilesets.AsNoTracking()
                .Where(t => t.PublishedSnapshotId != null)
                .Select(t => t.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<ComposedTilesetDefinition>();
            }

            var snaps = await db.ComposedTilesetPublishedSnapshots.AsNoTracking()
                .Where(s => tips.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var list = new List<ComposedTilesetDefinition>(snaps.Count);
            foreach (var snap in snaps)
            {
                list.Add(await HydrateAsync(db, snap.ComposedTilesetId, snap.Name, snap.LogicalPath, snap.MembersJson, ct)
                    .ConfigureAwait(false));
            }

            return list;
        }, cancellationToken);

    private static async Task<string?> UpsertTilesAsync(
        FrogDbContext db,
        ComposedTilesetDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, ComposedTileRef>(StringComparer.Ordinal);
        foreach (var tile in definition.Tiles)
        {
            distinct.TryAdd(tile.TileAssetId, tile);
        }

        var ids = distinct.Keys.ToArray();
        var existing = await db.ContentTiles
            .Where(t => ids.Contains(t.TileAssetId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingById = existing.ToDictionary(t => t.TileAssetId.Trim().ToLowerInvariant(), StringComparer.Ordinal);
        foreach (var pair in distinct)
        {
            if (existingById.TryGetValue(pair.Key, out var row)
                && !row.PngBytes.AsSpan().SequenceEqual(pair.Value.NormalizedRgba))
            {
                return "Pixels divergents pour la tuile " + pair.Key + ".";
            }
        }

        foreach (var pair in distinct)
        {
            if (existingById.ContainsKey(pair.Key))
            {
                continue;
            }

            db.ContentTiles.Add(new ContentTileEntity
            {
                TileAssetId = pair.Key,
                Id = Guid.NewGuid(),
                PngBytes = pair.Value.NormalizedRgba.ToArray(),
                WidthPx = (short)TileAssetMetrics.TargetTileSizePixels,
                HeightPx = (short)TileAssetMetrics.TargetTileSizePixels,
                DisplayName = pair.Value.DisplayName,
                Tags = Array.Empty<string>(),
                MetaJson = TilePackPixelEncoding.MetaJson,
                ContentBytesLen = pair.Value.NormalizedRgba.Length,
                CreatedAtUtc = now,
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<ComposedTilesetDefinition> HydrateAsync(
        FrogDbContext db,
        Guid id,
        string name,
        string logicalPath,
        string membersJson,
        CancellationToken cancellationToken)
    {
        var members = ComposedTilesetMemberCodec.Read(membersJson);
        var ids = members.Select(m => m.Id.Trim().ToLowerInvariant()).Where(m => m.Length > 0).Distinct().ToArray();
        var rows = ids.Length == 0
            ? new List<ContentTileEntity>()
            : await db.ContentTiles.AsNoTracking()
                .Where(t => ids.Contains(t.TileAssetId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var byId = rows.ToDictionary(t => t.TileAssetId.Trim().ToLowerInvariant(), StringComparer.Ordinal);
        var definition = new ComposedTilesetDefinition
        {
            Id = id,
            Name = name,
            LogicalPath = logicalPath,
        };
        foreach (var member in members)
        {
            var key = member.Id.Trim().ToLowerInvariant();
            byId.TryGetValue(key, out var row);
            definition.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = key,
                DisplayName = member.Name,
                NormalizedRgba = row?.PngBytes is { Length: > 0 } bytes ? bytes.ToArray() : Array.Empty<byte>(),
            });
        }

        return definition;
    }

    private static async Task<bool> PathTakenAsync(FrogDbContext db, string path, Guid? excludeId, CancellationToken ct)
        => await db.ComposedTilesets.AsNoTracking()
            .AnyAsync(t => t.LogicalPath == path && (excludeId == null || t.Id != excludeId), ct)
            .ConfigureAwait(false);

    private static async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken ct)
    {
        var rev = await db.ComposedTilesets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (long?)t.Revision)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return rev ?? 0;
    }

    private static ComposedTilesetDefinition Clone(ComposedTilesetDefinition src)
    {
        var copy = new ComposedTilesetDefinition
        {
            Id = src.Id,
            Name = src.Name,
            LogicalPath = src.LogicalPath,
        };
        foreach (var tile in src.Tiles)
        {
            if (tile is null)
            {
                continue;
            }

            copy.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = tile.TileAssetId,
                DisplayName = tile.DisplayName,
                NormalizedRgba = tile.NormalizedRgba is { Length: > 0 } bytes ? bytes.ToArray() : Array.Empty<byte>(),
            });
        }

        return copy;
    }

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance du tileset composé.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
