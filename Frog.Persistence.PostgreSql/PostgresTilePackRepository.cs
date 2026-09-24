using Frog.Application.Content;
using Frog.Persistence.PostgreSql.Entities;

using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.PostgreSql;

/// <summary>
/// Écrit <c>content.tiles</c>, <c>tile_packs</c> et <c>tile_pack_entries</c>.
/// Le paquet est inséré en brouillon, les entrées ensuite, puis le statut passe à publié :
/// le déclencheur interdit de modifier les entrées une fois le statut publié.
/// <c>player.player_tile_unlocks</c> n’est pas lu.
/// </summary>
public sealed class PostgresTilePackRepository : ITilePackRepository
{
    private readonly FrogDbContextGate _gate;

    public PostgresTilePackRepository(FrogDbContextGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public Task<PublishedTilePackInfo?> GetCurrentPublishedAsync(string? slug, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await CurrentQuery(db, slug)
                .Select(p => new
                {
                    p.Id,
                    p.Slug,
                    p.Version,
                    p.EntryCount,
                    p.FrogpackSha256,
                    p.Ed25519Signature,
                    p.Ed25519PublicKeyId,
                    p.PublishedAtUtc,
                })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (row?.FrogpackSha256 is null || row.Ed25519Signature is null || row.PublishedAtUtc is null)
            {
                return null;
            }

            return new PublishedTilePackInfo(
                row.Id,
                row.Slug,
                row.Version,
                row.EntryCount,
                row.FrogpackSha256.Trim().ToLowerInvariant(),
                row.Ed25519Signature,
                row.Ed25519PublicKeyId?.Trim(),
                row.PublishedAtUtc.Value);
        }, cancellationToken);

    public Task<byte[]?> GetCurrentPublishedBytesAsync(string? slug, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var bytes = await CurrentQuery(db, slug)
                .Select(p => p.FrogpackBytes)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            return bytes is { Length: > 0 } ? bytes : null;
        }, cancellationToken);

    public Task<IReadOnlyList<StoredTileBlob>> ListTilesAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyList<StoredTileBlob>>(async (db, ct) =>
        {
            var rows = await db.ContentTiles.AsNoTracking()
                .Select(t => new { t.TileAssetId, t.PngBytes, t.MetaJson, t.DisplayName })
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows
                .Select(t => new StoredTileBlob(t.TileAssetId.Trim().ToLowerInvariant(), t.PngBytes, t.MetaJson, t.DisplayName))
                .ToArray();
        }, cancellationToken);

    public Task<TilePackStoreResult> PublishAsync(TilePackPublishWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return _gate.ExecuteAsync(async (db, ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var result = await PublishCoreAsync(db, write, ct).ConfigureAwait(false);
                if (result is TilePackStoreResult.Stored)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    db.ChangeTracker.Clear();
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return MapFailure(ex);
            }
        }, cancellationToken);
    }

    public Task<bool> TryYankAsync(string slug, string version, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var pack = await db.ContentTilePacks
                .FirstOrDefaultAsync(p => p.Slug == slug && p.Version == version && p.Status == (short)TilePackStatus.Published, ct)
                .ConfigureAwait(false);
            if (pack is null)
            {
                return false;
            }

            pack.Status = (short)TilePackStatus.Yanked;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task<TilePackGuardResult> TryMutateManifestAsync(
        Guid packId,
        string manifestJson,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var pack = await db.ContentTilePacks.FirstOrDefaultAsync(p => p.Id == packId, ct).ConfigureAwait(false);
            if (pack is null)
            {
                return TilePackGuardResult.NotFound;
            }

            pack.ManifestJson = manifestJson;
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return TilePackGuardResult.Applied;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && IsImmutableViolation(ex))
            {
                db.ChangeTracker.Clear();
                return TilePackGuardResult.Immutable;
            }
        }, cancellationToken);

    private static IQueryable<ContentTilePackEntity> CurrentQuery(FrogDbContext db, string? slug)
    {
        var query = db.ContentTilePacks.AsNoTracking().Where(p => p.Status == (short)TilePackStatus.Published);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            query = query.Where(p => p.Slug == slug);
        }

        return query
            .OrderByDescending(p => p.PublishedAtUtc)
            .ThenByDescending(p => p.Id);
    }

    private static async Task<TilePackStoreResult> PublishCoreAsync(
        FrogDbContext db,
        TilePackPublishWrite write,
        CancellationToken cancellationToken)
    {
        if (write.Tiles.Count == 0 || write.FrogpackBytes.Length == 0 || write.Ed25519Signature.Length != 64)
        {
            return new TilePackStoreResult.Failed("Paquet publié incomplet.");
        }

        var prior = await db.ContentTilePacks.AsNoTracking()
            .Where(p => p.Slug == write.Slug && p.Version == write.Version)
            .Select(p => new { p.Id, p.Status })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (prior is not null && prior.Status != (short)TilePackStatus.Draft)
        {
            return new TilePackStoreResult.Conflict("slug+version déjà publié ou retiré.");
        }

        var shaTaken = await db.ContentTilePacks.AsNoTracking()
            .AnyAsync(
                p => p.Status == (short)TilePackStatus.Published && p.FrogpackSha256 == write.FrogpackSha256,
                cancellationToken)
            .ConfigureAwait(false);
        if (shaTaken)
        {
            return new TilePackStoreResult.Conflict("frogpack_sha256 déjà publié.");
        }

        var ids = write.Tiles.Select(t => t.TileAssetId).ToArray();
        var existing = await db.ContentTiles
            .Where(t => ids.Contains(t.TileAssetId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingById = existing.ToDictionary(t => t.TileAssetId.Trim().ToLowerInvariant(), StringComparer.Ordinal);
        foreach (var tile in write.Tiles)
        {
            if (existingById.TryGetValue(tile.TileAssetId, out var row)
                && !row.PngBytes.AsSpan().SequenceEqual(tile.NormalizedRgba))
            {
                return new TilePackStoreResult.Failed("Octets de tuile divergents pour " + tile.TileAssetId + ".");
            }
        }

        if (prior is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM content.tile_pack_entries WHERE pack_id = {prior.Id}",
                cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM content.tile_packs WHERE id = {prior.Id} AND status = 0",
                cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }

        var now = write.PublishedAtUtc;
        foreach (var tile in write.Tiles)
        {
            if (existingById.ContainsKey(tile.TileAssetId))
            {
                continue;
            }

            db.ContentTiles.Add(new ContentTileEntity
            {
                TileAssetId = tile.TileAssetId,
                Id = Guid.NewGuid(),
                PngBytes = tile.NormalizedRgba,
                WidthPx = 48,
                HeightPx = 48,
                DisplayName = tile.DisplayName,
                Tags = Array.Empty<string>(),
                MetaJson = TilePackPixelEncoding.MetaJson,
                ContentBytesLen = tile.NormalizedRgba.Length,
                CreatedAtUtc = now,
            });
        }

        var packId = Guid.NewGuid();
        db.ContentTilePacks.Add(new ContentTilePackEntity
        {
            Id = packId,
            Slug = write.Slug,
            Version = write.Version,
            Status = (short)TilePackStatus.Draft,
            FrogpackSha256 = write.FrogpackSha256,
            FrogpackBytes = write.FrogpackBytes,
            FrogpackBytesLen = write.FrogpackBytes.Length,
            Ed25519Signature = write.Ed25519Signature,
            Ed25519PublicKeyId = write.Ed25519PublicKeyId,
            EntryCount = write.Tiles.Count,
            ManifestJson = write.ManifestJson,
            CreatedAtUtc = now,
            PublishedAtUtc = null,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var tile in write.Tiles)
        {
            db.ContentTilePackEntries.Add(new ContentTilePackEntryEntity
            {
                PackId = packId,
                TileAssetId = tile.TileAssetId,
                Ordinal = tile.Ordinal,
                EntryMetaJson = "{}",
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var pack = await db.ContentTilePacks.FirstAsync(p => p.Id == packId, cancellationToken).ConfigureAwait(false);
        pack.Status = (short)TilePackStatus.Published;
        pack.PublishedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TilePackStoreResult.Stored(packId);
    }

    private static TilePackStoreResult MapFailure(Exception ex)
    {
        if (TryPostgres(ex, out var pg))
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return new TilePackStoreResult.Conflict("Contrainte d’unicité tile pack (slug+version ou sha publié).");
            }

            if (pg.SqlState == PostgresErrorCodes.CheckViolation)
            {
                return new TilePackStoreResult.Failed("Forme de paquet refusé par PostgreSQL.");
            }

            if (IsImmutableViolation(ex))
            {
                return new TilePackStoreResult.Conflict(Trim(pg.MessageText));
            }
        }

        return new TilePackStoreResult.Failed(Sanitize(ex.Message));
    }

    private static bool IsImmutableViolation(Exception ex)
        => TryPostgres(ex, out var pg)
           && (pg.SqlState == PostgresErrorCodes.RaiseException
               || pg.MessageText.Contains("immutable", StringComparison.OrdinalIgnoreCase));

    private static bool TryPostgres(Exception ex, out PostgresException postgres)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException found)
            {
                postgres = found;
                return true;
            }
        }

        postgres = null!;
        return false;
    }

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance tile pack.";
        }

        return Trim(message);
    }

    private static string Trim(string message) => message.Length > 240 ? message[..240] : message;
}
