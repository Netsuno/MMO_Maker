using Frog.Core.Constants;

namespace Frog.Application.Content;

/// <summary>
/// Statut <c>content.tile_packs.status</c> : 0 brouillon, 1 publié, 2 retiré.
/// Les lignes publiées ou retirées sont immuables (déclencheur PostgreSQL).
/// </summary>
public enum TilePackStatus : short
{
    Draft = 0,
    Published = 1,
    Yanked = 2,
}

/// <summary>
/// Octets stockés dans <c>content.tiles.png_bytes</c> pour une publication serveur V1 :
/// RGBA8 prémultiplié 48×48 (9216 octets), le buffer dont le SHA-256 est le <c>TileAssetId</c>.
/// </summary>
public static class TilePackPixelEncoding
{
    public const string PremultipliedRgba8 = "premultiplied-rgba8";

    public static string MetaJson { get; } =
        "{\"pixelEncoding\":\"" + PremultipliedRgba8 + "\",\"byteLength\":" + TileAssetMetrics.CanonicalPixelByteCount + "}";
}

public sealed record TilePackTileWrite(string TileAssetId, byte[] NormalizedRgba, string? DisplayName, int Ordinal);

public sealed record StoredTileBlob(string TileAssetId, byte[] Bytes, string? MetaJson, string? DisplayName);

public sealed record PublishedTilePackInfo(
    Guid Id,
    string Slug,
    string Version,
    int EntryCount,
    string FrogpackSha256,
    byte[] Ed25519Signature,
    string? Ed25519PublicKeyId,
    DateTimeOffset PublishedAtUtc);

public sealed record TilePackPublishWrite(
    string Slug,
    string Version,
    string FrogpackSha256,
    byte[] FrogpackBytes,
    byte[] Ed25519Signature,
    string Ed25519PublicKeyId,
    string ManifestJson,
    IReadOnlyList<TilePackTileWrite> Tiles,
    DateTimeOffset PublishedAtUtc);

public abstract record TilePackStoreResult
{
    public sealed record Stored(Guid PackId) : TilePackStoreResult;

    public sealed record Conflict(string Reason) : TilePackStoreResult;

    public sealed record Failed(string Reason) : TilePackStoreResult;
}

/// <summary>Résultat d’une écriture qui doit échouer sur un paquet publié ou retiré.</summary>
public enum TilePackGuardResult
{
    Applied,
    Immutable,
    NotFound,
}

/// <summary>
/// Dépôt <c>content.tiles</c> / <c>tile_packs</c> / <c>tile_pack_entries</c>.
/// Ne filtre jamais sur <c>player.player_tile_unlocks</c> (les déblocages éditeur ne concernent pas la publication).
/// </summary>
public interface ITilePackRepository
{
    Task<PublishedTilePackInfo?> GetCurrentPublishedAsync(string? slug, CancellationToken cancellationToken = default);

    Task<byte[]?> GetCurrentPublishedBytesAsync(string? slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredTileBlob>> ListTilesAsync(CancellationToken cancellationToken = default);

    Task<TilePackStoreResult> PublishAsync(TilePackPublishWrite write, CancellationToken cancellationToken = default);

    Task<bool> TryYankAsync(string slug, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tente de modifier <c>manifest_json</c>. Doit renvoyer <see cref="TilePackGuardResult.Immutable"/>
    /// quand le paquet est publié ou retiré.
    /// </summary>
    Task<TilePackGuardResult> TryMutateManifestAsync(Guid packId, string manifestJson, CancellationToken cancellationToken = default);
}
