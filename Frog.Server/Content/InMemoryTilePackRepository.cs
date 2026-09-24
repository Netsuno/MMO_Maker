using Frog.Application.Content;

namespace Frog.Server.Content;

/// <summary>
/// Dépôt mémoire pour les tests et le repli hors PostgreSQL. Imite l’immutabilité publié/retiré.
/// Les déblocages joueur n’existent pas ici.
/// </summary>
public sealed class InMemoryTilePackRepository : ITilePackRepository
{
    private readonly object _gate = new();
    private readonly List<Pack> _packs = new();
    private readonly Dictionary<string, StoredTileBlob> _tiles = new(StringComparer.Ordinal);

    public Task<PublishedTilePackInfo?> GetCurrentPublishedAsync(string? slug, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Project(FindCurrent(slug)));
        }
    }

    public Task<byte[]?> GetCurrentPublishedBytesAsync(string? slug, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var pack = FindCurrent(slug);
            return Task.FromResult(pack?.Bytes is { Length: > 0 } bytes ? bytes.ToArray() : null);
        }
    }

    public Task<IReadOnlyList<StoredTileBlob>> ListTilesAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StoredTileBlob> copy = _tiles.Values
                .Select(t => t with { Bytes = t.Bytes.ToArray() })
                .ToArray();
            return Task.FromResult(copy);
        }
    }

    public Task<TilePackStoreResult> PublishAsync(TilePackPublishWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (_gate)
        {
            if (_packs.Any(p => p.Slug == write.Slug && p.Version == write.Version && p.Status != TilePackStatus.Draft))
            {
                return Task.FromResult<TilePackStoreResult>(
                    new TilePackStoreResult.Conflict("slug+version déjà publié ou retiré."));
            }

            if (_packs.Any(p => p.Status == TilePackStatus.Published
                                && string.Equals(p.Sha256, write.FrogpackSha256, StringComparison.Ordinal)))
            {
                return Task.FromResult<TilePackStoreResult>(
                    new TilePackStoreResult.Conflict("frogpack_sha256 déjà publié."));
            }

            foreach (var tile in write.Tiles)
            {
                if (_tiles.TryGetValue(tile.TileAssetId, out var existing)
                    && !existing.Bytes.AsSpan().SequenceEqual(tile.NormalizedRgba))
                {
                    return Task.FromResult<TilePackStoreResult>(
                        new TilePackStoreResult.Failed("Octets de tuile divergents pour " + tile.TileAssetId + "."));
                }
            }

            _packs.RemoveAll(p => p.Slug == write.Slug && p.Version == write.Version && p.Status == TilePackStatus.Draft);
            foreach (var tile in write.Tiles)
            {
                _tiles[tile.TileAssetId] = new StoredTileBlob(
                    tile.TileAssetId,
                    tile.NormalizedRgba.ToArray(),
                    TilePackPixelEncoding.MetaJson,
                    tile.DisplayName);
            }

            var id = Guid.NewGuid();
            _packs.Add(new Pack(
                id,
                write.Slug,
                write.Version,
                TilePackStatus.Published,
                write.FrogpackSha256,
                write.FrogpackBytes.ToArray(),
                write.Ed25519Signature.ToArray(),
                write.Ed25519PublicKeyId,
                write.Tiles.Count,
                write.ManifestJson,
                write.PublishedAtUtc));
            return Task.FromResult<TilePackStoreResult>(new TilePackStoreResult.Stored(id));
        }
    }

    public Task<bool> TryYankAsync(string slug, string version, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var index = _packs.FindIndex(p => p.Slug == slug && p.Version == version && p.Status == TilePackStatus.Published);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            var pack = _packs[index];
            _packs[index] = pack with { Status = TilePackStatus.Yanked };
            return Task.FromResult(true);
        }
    }

    public Task<TilePackGuardResult> TryMutateManifestAsync(
        Guid packId,
        string manifestJson,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var index = _packs.FindIndex(p => p.Id == packId);
            if (index < 0)
            {
                return Task.FromResult(TilePackGuardResult.NotFound);
            }

            var pack = _packs[index];
            if (pack.Status is TilePackStatus.Published or TilePackStatus.Yanked)
            {
                return Task.FromResult(TilePackGuardResult.Immutable);
            }

            _packs[index] = pack with { Manifest = manifestJson };
            return Task.FromResult(TilePackGuardResult.Applied);
        }
    }

    private Pack? FindCurrent(string? slug)
    {
        return _packs
            .Where(p => p.Status == TilePackStatus.Published && (slug is null || p.Slug == slug))
            .OrderByDescending(p => p.PublishedAtUtc)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();
    }

    private static PublishedTilePackInfo? Project(Pack? pack)
    {
        if (pack is null || string.IsNullOrEmpty(pack.Sha256))
        {
            return null;
        }

        return new PublishedTilePackInfo(
            pack.Id,
            pack.Slug,
            pack.Version,
            pack.EntryCount,
            pack.Sha256,
            pack.Signature.ToArray(),
            pack.PublicKeyId,
            pack.PublishedAtUtc);
    }

    private sealed record Pack(
        Guid Id,
        string Slug,
        string Version,
        TilePackStatus Status,
        string Sha256,
        byte[] Bytes,
        byte[] Signature,
        string? PublicKeyId,
        int EntryCount,
        string Manifest,
        DateTimeOffset PublishedAtUtc);
}
