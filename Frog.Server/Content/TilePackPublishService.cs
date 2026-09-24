using System.Security.Cryptography;
using System.Text.Json;

using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;
using Frog.Server.Config;

using Microsoft.Extensions.Options;

namespace Frog.Server.Content;

/// <summary>
/// Assemble, signe et vérifie un <c>.frogpack</c> V1 complet avant d’écrire les lignes PostgreSQL.
/// Échoue fermé : <see cref="FrogPackReader"/> et la clé publique épinglée. Pas de delta, pas de chiffrement.
/// <see cref="FrogWireProtocol.Version"/> reste 11.
/// </summary>
public sealed class TilePackPublishService
{
    public const int MaxPackBytes = 80 * 1024 * 1024;

    private readonly ITilePackRepository _repository;
    private readonly IOptions<TilePackOptions> _options;
    private readonly TimeProvider _clock;

    public TilePackPublishService(
        ITilePackRepository repository,
        IOptions<TilePackOptions> options,
        TimeProvider? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<TilePackPublishResult> PublishTilesAsync(
        string slug,
        string version,
        IReadOnlyList<TileAsset> tiles,
        IReadOnlyList<string?>? displayNames = null,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.TryGetPrivateSeed(out var seed))
        {
            return new TilePackPublishResult.Rejected("Graine privée Ed25519 absente : impossible de signer.");
        }

        if (!options.TryGetPinnedPublicKey(out var pinned))
        {
            return new TilePackPublishResult.Rejected("Clé publique Ed25519 épinglée absente.");
        }

        if (!CryptographicOperations.FixedTimeEquals(pinned, FrogPackKeys.PublicKeyFromSeed(seed)))
        {
            return new TilePackPublishResult.Rejected("La graine privée ne correspond pas à la clé publique épinglée.");
        }

        byte[] file;
        try
        {
            file = FrogPackWriter.Write(tiles, seed);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            return new TilePackPublishResult.Rejected(ex.Message);
        }

        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (displayNames is not null)
        {
            for (var i = 0; i < tiles.Count && i < displayNames.Count; i++)
            {
                var id = tiles[i].Id.ToHex();
                if (!names.ContainsKey(id))
                {
                    names[id] = SanitizeDisplayName(displayNames[i]);
                }
            }
        }

        return await CommitVerifiedAsync(slug, version, file, pinned, names, cancellationToken).ConfigureAwait(false);
    }

    public Task<TilePackPublishResult> PublishUploadedPackAsync(
        string slug,
        string version,
        ReadOnlyMemory<byte> frogpack,
        CancellationToken cancellationToken = default)
    {
        if (frogpack.Length == 0 || frogpack.Length > MaxPackBytes)
        {
            return Task.FromResult<TilePackPublishResult>(
                new TilePackPublishResult.Rejected("Paquet .frogpack vide ou trop grand."));
        }

        var options = _options.Value;
        if (!options.TryGetPinnedPublicKey(out var pinned))
        {
            return Task.FromResult<TilePackPublishResult>(
                new TilePackPublishResult.Rejected("Clé publique Ed25519 épinglée absente."));
        }

        return CommitVerifiedAsync(slug, version, frogpack.ToArray(), pinned, displayNames: null, cancellationToken);
    }

    public async Task<TilePackPublishResult> PublishPngDirectoryAsync(
        string slug,
        string version,
        string directory,
        bool enforceImportRoot,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveDirectory(directory, enforceImportRoot, out var fullPath, out var error))
        {
            return new TilePackPublishResult.Rejected(error ?? "Dossier refusé.");
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(fullPath, "*.png", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new TilePackPublishResult.Rejected(ex.Message);
        }

        Array.Sort(files, StringComparer.Ordinal);
        var tiles = new List<TileAsset>();
        var names = new List<string?>();
        foreach (var file in files)
        {
            byte[] png;
            try
            {
                png = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new TilePackPublishResult.Rejected(ex.Message);
            }

            (int width, int height, byte[] rgba) decoded;
            try
            {
                decoded = PngRgba8.Decode(png);
            }
            catch (InvalidDataException ex)
            {
                return new TilePackPublishResult.Rejected(Path.GetFileName(file) + " : " + ex.Message);
            }

            IReadOnlyList<TileAsset> sliced;
            try
            {
                sliced = Slice(decoded.width, decoded.height, decoded.rgba);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
            {
                return new TilePackPublishResult.Rejected(Path.GetFileName(file) + " : " + ex.Message);
            }

            var display = SanitizeDisplayName(Path.GetFileNameWithoutExtension(file));
            foreach (var tile in sliced)
            {
                tiles.Add(tile);
                names.Add(display);
            }
        }

        if (tiles.Count == 0)
        {
            return new TilePackPublishResult.Rejected("Aucun PNG 48×48 dans le dossier.");
        }

        return await PublishTilesAsync(slug, version, tiles, names, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TilePackPublishResult> PublishStoredCatalogueAsync(
        string slug,
        string version,
        CancellationToken cancellationToken = default)
    {
        var stored = await _repository.ListTilesAsync(cancellationToken).ConfigureAwait(false);
        if (stored.Count == 0)
        {
            return new TilePackPublishResult.Rejected("Catalogue de tuiles vide.");
        }

        var tiles = new List<TileAsset>(stored.Count);
        var names = new List<string?>(stored.Count);
        foreach (var row in stored)
        {
            TileAsset asset;
            try
            {
                asset = DecodeStored(row);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
            {
                return new TilePackPublishResult.Rejected(row.TileAssetId + " : " + ex.Message);
            }

            tiles.Add(asset);
            names.Add(SanitizeDisplayName(row.DisplayName));
        }

        return await PublishTilesAsync(slug, version, tiles, names, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TilePackClientManifest?> GetCurrentManifestAsync(string? slug, CancellationToken cancellationToken = default)
    {
        var info = await _repository.GetCurrentPublishedAsync(NormalizeSlugFilter(slug), cancellationToken)
            .ConfigureAwait(false);
        return info is null ? null : ToManifest(info);
    }

    public Task<byte[]?> GetCurrentPackBytesAsync(string? slug, CancellationToken cancellationToken = default)
        => _repository.GetCurrentPublishedBytesAsync(NormalizeSlugFilter(slug), cancellationToken);

    public Task<bool> YankAsync(string slug, string version, CancellationToken cancellationToken = default)
    {
        if (!TilePackNames.IsSlug(slug) || !TilePackNames.IsVersion(version))
        {
            return Task.FromResult(false);
        }

        return _repository.TryYankAsync(slug, version, cancellationToken);
    }

    private async Task<TilePackPublishResult> CommitVerifiedAsync(
        string slug,
        string version,
        byte[] file,
        byte[] pinnedPublicKey,
        IReadOnlyDictionary<string, string?>? displayNames,
        CancellationToken cancellationToken)
    {
        if (!TilePackNames.IsSlug(slug) || !TilePackNames.IsVersion(version))
        {
            return new TilePackPublishResult.Rejected("slug ou version refusé.");
        }

        if (file.Length == 0 || file.Length > MaxPackBytes)
        {
            return new TilePackPublishResult.Rejected("Paquet .frogpack vide ou trop grand.");
        }

        IReadOnlyList<TileAsset> verified;
        try
        {
            verified = FrogPackReader.Read(file, pinnedPublicKey);
        }
        catch (FrogPackRejectedException ex)
        {
            return new TilePackPublishResult.Rejected(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return new TilePackPublishResult.Rejected(ex.Message);
        }

        if (verified.Count == 0)
        {
            return new TilePackPublishResult.Rejected("Un paquet publié V1 contient au moins une tuile.");
        }

        var sha = BodySha256Hex(file);
        var signature = file.AsSpan(file.Length - FrogPackFormat.SignatureLength, FrogPackFormat.SignatureLength).ToArray();
        if (signature.Length != FrogPackFormat.SignatureLength)
        {
            return new TilePackPublishResult.Rejected("Signature Ed25519 de longueur inattendue.");
        }

        var rows = new List<TilePackTileWrite>(verified.Count);
        var ids = new string[verified.Count];
        for (var i = 0; i < verified.Count; i++)
        {
            var id = verified[i].Id.ToHex();
            ids[i] = id;
            displayNames ??= EmptyNames.Instance;
            displayNames.TryGetValue(id, out var display);
            rows.Add(new TilePackTileWrite(id, verified[i].NormalizedRgba, SanitizeDisplayName(display), i));
        }

        var manifestJson = JsonSerializer.Serialize(new
        {
            format = "frogpack-v1",
            tileSizePixels = TileAssetMetrics.TargetTileSizePixels,
            tileCount = verified.Count,
            protocolVersion = FrogWireProtocol.Version,
            tileAssetIds = ids,
        });

        var stored = await _repository.PublishAsync(
            new TilePackPublishWrite(
                slug,
                version,
                sha,
                file,
                signature,
                Convert.ToHexString(pinnedPublicKey).ToLowerInvariant(),
                manifestJson,
                rows,
                _clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        return stored switch
        {
            TilePackStoreResult.Stored success => new TilePackPublishResult.Published(
                (await GetCurrentManifestAsync(slug, cancellationToken).ConfigureAwait(false))
                ?? ToManifest(new PublishedTilePackInfo(
                    success.PackId,
                    slug,
                    version,
                    verified.Count,
                    sha,
                    signature,
                    Convert.ToHexString(pinnedPublicKey).ToLowerInvariant(),
                    _clock.GetUtcNow()))),
            TilePackStoreResult.Conflict conflict => new TilePackPublishResult.Rejected(conflict.Reason),
            TilePackStoreResult.Failed failed => new TilePackPublishResult.Rejected(failed.Reason),
            _ => new TilePackPublishResult.Rejected("Publication refusée."),
        };
    }

    private bool TryResolveDirectory(string directory, bool enforceImportRoot, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(directory))
        {
            error = "Dossier manquant.";
            return false;
        }

        var options = _options.Value;
        if (enforceImportRoot)
        {
            if (string.IsNullOrWhiteSpace(options.PngImportRoot))
            {
                error = "TilePack:PngImportRoot est requis pour publier un dossier via HTTP.";
                return false;
            }

            var root = Path.GetFullPath(options.PngImportRoot);
            var combined = Path.GetFullPath(Path.Combine(root, directory));
            if (!IsUnderRoot(root, combined))
            {
                error = "Le dossier sort de PngImportRoot.";
                return false;
            }

            fullPath = combined;
        }
        else
        {
            fullPath = Path.GetFullPath(directory);
            if (!string.IsNullOrWhiteSpace(options.PngImportRoot))
            {
                var root = Path.GetFullPath(options.PngImportRoot);
                if (!IsUnderRoot(root, fullPath))
                {
                    error = "Le dossier sort de PngImportRoot.";
                    return false;
                }
            }
        }

        if (!Directory.Exists(fullPath))
        {
            error = "Dossier introuvable.";
            return false;
        }

        return true;
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return candidate.Equals(root, StringComparison.Ordinal)
               || candidate.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static TilePackClientManifest ToManifest(PublishedTilePackInfo info)
    {
        var query = "?slug=" + Uri.EscapeDataString(info.Slug);
        return new TilePackClientManifest(
            info.Id,
            info.Slug,
            info.Version,
            info.EntryCount,
            TileAssetMetrics.TargetTileSizePixels,
            info.FrogpackSha256.Trim().ToLowerInvariant(),
            Convert.ToHexString(info.Ed25519Signature).ToLowerInvariant(),
            (info.Ed25519PublicKeyId ?? string.Empty).Trim().ToLowerInvariant(),
            "/content/tile-packs/current.frogpack" + query,
            FrogWireProtocol.Version,
            info.PublishedAtUtc);
    }

    private static string? NormalizeSlugFilter(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();

    private static string BodySha256Hex(byte[] file)
    {
        var bodyLength = file.Length - FrogPackFormat.TrailerLength;
        return Convert.ToHexString(file.AsSpan(bodyLength, FrogPackFormat.ContentHashLength)).ToLowerInvariant();
    }

    private static TileAsset DecodeStored(StoredTileBlob row)
    {
        if (!Frog.Core.Maps.TileAssetId.TryParse(row.TileAssetId.Trim(), out var id))
        {
            throw new InvalidDataException("TileAssetId illisible.");
        }

        if (row.Bytes.Length >= 8 && row.Bytes[0] == 0x89 && row.Bytes[1] == 0x50)
        {
            var decoded = PngRgba8.Decode(row.Bytes);
            var sliced = Slice(decoded.Width, decoded.Height, decoded.StraightRgba);
            if (sliced.Count != 1 || sliced[0].Id != id)
            {
                throw new InvalidDataException("PNG catalogue incohérent avec le TileAssetId.");
            }

            return sliced[0];
        }

        return TileAsset.FromNormalizedRgba(row.Bytes, id);
    }

    private static IReadOnlyList<TileAsset> Slice(int width, int height, byte[] straightRgba)
    {
        if (width == TileAssetMetrics.TargetTileSizePixels && height == TileAssetMetrics.TargetTileSizePixels)
        {
            return [TileAsset.FromStraightRgba(straightRgba)];
        }

        var sheet = TileSheetSlicer.Slice(straightRgba, width, height);
        return sheet.UniqueAssets;
    }

    private static string? SanitizeDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 120)
        {
            trimmed = trimmed[..120];
        }

        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch))
            {
                return null;
            }
        }

        return trimmed;
    }

    private sealed class EmptyNames : IReadOnlyDictionary<string, string?>
    {
        public static readonly EmptyNames Instance = new();

        public string? this[string key] => throw new KeyNotFoundException();

        public IEnumerable<string> Keys => Array.Empty<string>();

        public IEnumerable<string?> Values => Array.Empty<string?>();

        public int Count => 0;

        public bool ContainsKey(string key) => false;

        public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
        {
            yield break;
        }

        public bool TryGetValue(string key, out string? value)
        {
            value = null;
            return false;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public sealed record TilePackClientManifest(
    Guid PackId,
    string Slug,
    string Version,
    int TileCount,
    int TileSizePixels,
    string FrogpackSha256,
    string Ed25519Signature,
    string Ed25519PublicKeyId,
    string DownloadPath,
    ushort ProtocolVersion,
    DateTimeOffset PublishedAtUtc);

public abstract record TilePackPublishResult
{
    public sealed record Published(TilePackClientManifest Manifest) : TilePackPublishResult;

    public sealed record Rejected(string Reason) : TilePackPublishResult;
}

public static class TilePackNames
{
    public static bool IsSlug(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length > 120 || !IsSlugLead(slug[0]))
        {
            return false;
        }

        foreach (var ch in slug)
        {
            if (!IsSlugLead(ch) && ch is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsVersion(string? version)
    {
        if (string.IsNullOrEmpty(version) || version.Length > 64 || !char.IsAsciiLetterOrDigit(version[0]))
        {
            return false;
        }

        foreach (var ch in version)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '_' or '+' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSlugLead(char ch)
        => char.IsAsciiDigit(ch) || (char.IsAsciiLetter(ch) && char.IsLower(ch));
}
