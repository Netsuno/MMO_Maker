using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;

namespace Frog.Client.Assets;

/// <summary>
/// Télécharge le <c>.frogpack</c> V1 courant, le vérifie avec la clé Ed25519 épinglée,
/// et le garde sous <c>%LocalAppData%\MmoMaker\Content\tile-assets</c>.
/// Pas d’opcode : Hello TCP reste <see cref="FrogWireProtocol.Version"/>.
/// Les déblocages joueur ne filtrent pas le paquet publié.
/// </summary>
public sealed class TilePackClientService : IDisposable
{
    public const int MaxPackBytes = 80 * 1024 * 1024;

    public const string ManifestPath = "/content/tile-packs/current";

    public const string DefaultPackPath = "/content/tile-packs/current.frogpack";

    private const string IndexFileName = "index.json";

    private const string PackFileName = "current.frogpack";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly TilePackClientOptions _options;
    private readonly ITilePackTransport _transport;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITileAssetLookup _lookup = new MemoryTileAssetLookup(Array.Empty<TileAsset>());
    private bool _disposed;

    public TilePackClientService(TilePackClientOptions options)
        : this(options, new HttpTilePackTransport(), TilePackClientOptions.ResolveCacheDirectory(), ownsTransport: true)
    {
    }

    public TilePackClientService(TilePackClientOptions options, ITilePackTransport transport, string cacheDirectory)
        : this(options, transport, cacheDirectory, ownsTransport: false)
    {
    }

    private readonly bool _ownsTransport;

    private TilePackClientService(
        TilePackClientOptions options,
        ITilePackTransport transport,
        string cacheDirectory,
        bool ownsTransport)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        _ownsTransport = ownsTransport;
    }

    public ITileAssetLookup Lookup => Volatile.Read(ref _lookup);

    public string? VerifiedContentSha256 { get; private set; }

    public string? VerifiedVersion { get; private set; }

    public int VerifiedTileCount { get; private set; }

    public bool HasVerifiedPack => VerifiedContentSha256 is not null;

    public async Task<TilePackSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await SyncCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
        if (_ownsTransport && _transport is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Vérifie le manifeste et les octets. Échoue fermé : mauvaise signature, mauvaise clé,
    /// bit de chiffrement, hash, taille ≠ 48, protocole ≠ Hello courant.
    /// </summary>
    public static IReadOnlyList<TileAsset> Verify(
        ReadOnlySpan<byte> pack,
        ReadOnlySpan<byte> trustedPublicKey,
        TilePackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.ProtocolVersion != FrogWireProtocol.Version)
        {
            throw new FrogPackRejectedException(
                $"protocolVersion manifeste = {manifest.ProtocolVersion}, Hello client = {FrogWireProtocol.Version}.");
        }

        if (manifest.TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
        {
            throw new FrogPackRejectedException(
                $"tileSizePixels manifeste = {manifest.TileSizePixels}, attendu {TileAssetMetrics.TargetTileSizePixels}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new FrogPackRejectedException("Version de paquet absente.");
        }

        if (pack.Length == 0 || pack.Length > MaxPackBytes)
        {
            throw new FrogPackRejectedException("Taille .frogpack hors limite.");
        }

        var assets = FrogPackReader.Read(pack, trustedPublicKey);
        if (assets.Count != manifest.TileCount)
        {
            throw new FrogPackRejectedException("tileCount du manifeste différent du paquet.");
        }

        var actualSha = ContentSha256Hex(pack);
        if (!HexEquals(actualSha, manifest.FrogpackSha256))
        {
            throw new FrogPackRejectedException("frogpackSha256 du manifeste différent du corps signé.");
        }

        return assets;
    }

    public static string ContentSha256Hex(ReadOnlySpan<byte> pack)
    {
        if (pack.Length < FrogPackFormat.TrailerLength)
        {
            throw new FrogPackRejectedException("frogpack trop court.");
        }

        var body = pack[..^FrogPackFormat.TrailerLength];
        return Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
    }

    private async Task<TilePackSyncResult> SyncCoreAsync(CancellationToken cancellationToken)
    {
        if (!TryDecodePublicKey(_options.PublicKeyHex, out var publicKey, out var keyError))
        {
            ClearVerified();
            return TilePackSyncResult.Rejected(keyError);
        }

        if (!TilePackClientOptions.TryNormalizeBaseUrl(_options.ContentBaseUrl, out var baseUrl))
        {
            ClearVerified();
            return TilePackSyncResult.Rejected("URL de contenu illisible.");
        }

        TilePackHttpGet manifestResponse;
        try
        {
            manifestResponse = await _transport.GetAsync(BuildManifestUri(baseUrl), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return TryOfflineCache(publicKey, ex.Message);
        }

        if (manifestResponse.StatusCode == (int)HttpStatusCode.NotFound)
        {
            ClearVerified();
            DeleteCacheFiles();
            return TilePackSyncResult.Unavailable("Aucun paquet de tuiles publié.");
        }

        if (manifestResponse.StatusCode != (int)HttpStatusCode.OK)
        {
            return TryOfflineCache(publicKey, "Manifeste HTTP " + manifestResponse.StatusCode);
        }

        TilePackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<TilePackManifest>(manifestResponse.Body, JsonOptions);
        }
        catch (JsonException)
        {
            ClearVerified();
            return TilePackSyncResult.Rejected("Manifeste JSON illisible.");
        }

        if (manifest is null)
        {
            ClearVerified();
            return TilePackSyncResult.Rejected("Manifeste vide.");
        }

        if (TryUseCache(manifest, publicKey, out var cached))
        {
            Install(cached, manifest);
            return TilePackSyncResult.FromCache(manifest, cached.Count);
        }

        TilePackHttpGet packResponse;
        try
        {
            packResponse = await _transport.GetAsync(BuildPackUri(baseUrl, manifest), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            ClearVerified();
            return TilePackSyncResult.Unavailable(ex.Message);
        }

        if (packResponse.StatusCode != (int)HttpStatusCode.OK || packResponse.Body.Length == 0)
        {
            ClearVerified();
            return TilePackSyncResult.Unavailable("Téléchargement .frogpack HTTP " + packResponse.StatusCode + ".");
        }

        IReadOnlyList<TileAsset> assets;
        try
        {
            assets = Verify(packResponse.Body, publicKey, manifest);
        }
        catch (Exception ex) when (ex is FrogPackRejectedException or ArgumentException)
        {
            ClearVerified();
            return TilePackSyncResult.Rejected(ex.Message);
        }

        try
        {
            WriteCache(packResponse.Body, manifest);
        }
        catch (IOException ex)
        {
            Install(assets, manifest);
            return new TilePackSyncResult
            {
                Kind = TilePackSyncKind.Downloaded,
                Version = manifest.Version,
                TileCount = assets.Count,
                ContentSha256 = NormalizeHex(manifest.FrogpackSha256),
                Detail = "Vérifié, cache disque non écrit : " + ex.Message,
            };
        }

        Install(assets, manifest);
        return TilePackSyncResult.FromDownload(manifest, assets.Count);
    }

    private TilePackSyncResult TryOfflineCache(byte[] publicKey, string detail)
    {
        if (TryReadIndex(out var index) && TryReadPack(out var pack))
        {
            var manifest = new TilePackManifest
            {
                Version = index.Version,
                TileCount = index.TileCount,
                TileSizePixels = index.TileSizePixels,
                FrogpackSha256 = index.FrogpackSha256,
                ProtocolVersion = index.ProtocolVersion,
            };
            try
            {
                var assets = Verify(pack, publicKey, manifest);
                Install(assets, manifest);
                return TilePackSyncResult.FromCache(manifest, assets.Count, "Hors ligne, dernier paquet vérifié. " + detail);
            }
            catch (Exception ex) when (ex is FrogPackRejectedException or ArgumentException)
            {
                ClearVerified();
                return TilePackSyncResult.Rejected(ex.Message);
            }
        }

        ClearVerified();
        return TilePackSyncResult.Unavailable(detail);
    }

    private bool TryUseCache(
        TilePackManifest manifest,
        byte[] publicKey,
        out IReadOnlyList<TileAsset> assets)
    {
        assets = Array.Empty<TileAsset>();
        if (!TryReadIndex(out var index))
        {
            return false;
        }

        if (!string.Equals(index.Version, manifest.Version, StringComparison.Ordinal)
            || !HexEquals(index.FrogpackSha256, manifest.FrogpackSha256)
            || index.ProtocolVersion != manifest.ProtocolVersion
            || index.TileCount != manifest.TileCount)
        {
            return false;
        }

        if (!TryReadPack(out var pack))
        {
            return false;
        }

        try
        {
            assets = Verify(pack, publicKey, manifest);
            return true;
        }
        catch (Exception ex) when (ex is FrogPackRejectedException or ArgumentException)
        {
            return false;
        }
    }

    private void Install(IReadOnlyList<TileAsset> assets, TilePackManifest manifest)
    {
        var next = new MemoryTileAssetLookup(assets);
        Volatile.Write(ref _lookup, next);
        VerifiedContentSha256 = NormalizeHex(manifest.FrogpackSha256);
        VerifiedVersion = manifest.Version;
        VerifiedTileCount = assets.Count;
    }

    private void ClearVerified()
    {
        Volatile.Write(ref _lookup, new MemoryTileAssetLookup(Array.Empty<TileAsset>()));
        VerifiedContentSha256 = null;
        VerifiedVersion = null;
        VerifiedTileCount = 0;
    }

    private void WriteCache(byte[] pack, TilePackManifest manifest)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var packPath = Path.Combine(_cacheDirectory, PackFileName);
        WriteAtomic(packPath, pack);
        var index = new TilePackCacheIndex
        {
            Version = manifest.Version ?? string.Empty,
            FrogpackSha256 = NormalizeHex(manifest.FrogpackSha256) ?? string.Empty,
            Slug = manifest.Slug,
            TileCount = manifest.TileCount,
            ProtocolVersion = manifest.ProtocolVersion,
            TileSizePixels = manifest.TileSizePixels,
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(index, JsonOptions);
        WriteAtomic(Path.Combine(_cacheDirectory, IndexFileName), json);
    }

    private bool TryReadIndex(out TilePackCacheIndex index)
    {
        index = new TilePackCacheIndex();
        var path = Path.Combine(_cacheDirectory, IndexFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TilePackCacheIndex>(File.ReadAllBytes(path), JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Version) || string.IsNullOrWhiteSpace(parsed.FrogpackSha256))
            {
                return false;
            }

            index = parsed;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return false;
        }
    }

    private bool TryReadPack(out byte[] pack)
    {
        pack = Array.Empty<byte>();
        var path = Path.Combine(_cacheDirectory, PackFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            pack = File.ReadAllBytes(path);
            return pack.Length > 0 && pack.Length <= MaxPackBytes;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private Uri BuildManifestUri(string baseUrl)
    {
        return new Uri(baseUrl + ManifestPath + SlugQuery(_options.Slug));
    }

    private Uri BuildPackUri(string baseUrl, TilePackManifest manifest)
    {
        var path = manifest.DownloadPath;
        if (!string.IsNullOrWhiteSpace(path)
            && path.StartsWith("/content/tile-packs/", StringComparison.Ordinal)
            && !path.Contains("..", StringComparison.Ordinal)
            && !path.Contains("://", StringComparison.Ordinal))
        {
            return new Uri(baseUrl + path);
        }

        return new Uri(baseUrl + DefaultPackPath + SlugQuery(_options.Slug));
    }

    private static string SlugQuery(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        return "?slug=" + Uri.EscapeDataString(slug.Trim());
    }

    private static bool TryDecodePublicKey(string? hex, out byte[] publicKey, out string error)
    {
        publicKey = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(hex))
        {
            error = "Clé publique absente (FROG_TILEPACK_PUBLIC_KEY_HEX). Aucune tuile du paquet n’est peinte.";
            return false;
        }

        try
        {
            publicKey = Convert.FromHexString(hex.Trim());
        }
        catch (FormatException)
        {
            error = "Clé publique Ed25519 illisible.";
            return false;
        }

        if (publicKey.Length != FrogPackFormat.PublicKeyLength)
        {
            error = "Clé publique Ed25519 : 32 octets attendus.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool HexEquals(string? left, string? right)
    {
        var a = NormalizeHex(left);
        var b = NormalizeHex(right);
        return a is not null && b is not null && string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        return hex.Trim().ToLowerInvariant();
    }

    private void DeleteCacheFiles()
    {
        TryDelete(Path.Combine(_cacheDirectory, IndexFileName));
        TryDelete(Path.Combine(_cacheDirectory, PackFileName));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Le prochain sync revérifie ou réécrit.
        }
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        try
        {
            if (File.Exists(path))
            {
                File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try
                {
                    File.Delete(tmp);
                }
                catch (IOException)
                {
                    // leftover temp
                }
            }
        }
    }
}

public interface ITilePackTransport
{
    Task<TilePackHttpGet> GetAsync(Uri url, CancellationToken cancellationToken);
}

public readonly record struct TilePackHttpGet(int StatusCode, byte[] Body);

public sealed class HttpTilePackTransport : ITilePackTransport, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _owns;

    public HttpTilePackTransport(HttpClient? http = null)
    {
        if (http is null)
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _owns = true;
        }
        else
        {
            _http = http;
            _owns = false;
        }
    }

    public async Task<TilePackHttpGet> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var declared = response.Content.Headers.ContentLength;
        if (declared is > TilePackClientService.MaxPackBytes)
        {
            return new TilePackHttpGet((int)response.StatusCode, Array.Empty<byte>());
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > TilePackClientService.MaxPackBytes)
        {
            return new TilePackHttpGet((int)response.StatusCode, Array.Empty<byte>());
        }

        return new TilePackHttpGet((int)response.StatusCode, body);
    }

    public void Dispose()
    {
        if (_owns)
        {
            _http.Dispose();
        }
    }
}

public sealed class TilePackManifest
{
    public string? Slug { get; set; }

    public string? Version { get; set; }

    public int TileCount { get; set; }

    public int TileSizePixels { get; set; }

    public string? FrogpackSha256 { get; set; }

    public string? Ed25519Signature { get; set; }

    public string? DownloadPath { get; set; }

    public int ProtocolVersion { get; set; }
}

public sealed class TilePackCacheIndex
{
    public string Version { get; set; } = string.Empty;

    public string FrogpackSha256 { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public int TileCount { get; set; }

    public int ProtocolVersion { get; set; }

    public int TileSizePixels { get; set; }
}

public enum TilePackSyncKind
{
    Cached,
    Downloaded,
    Rejected,
    Unavailable,
}

public sealed class TilePackSyncResult
{
    public TilePackSyncKind Kind { get; init; }

    public string? Version { get; init; }

    public int TileCount { get; init; }

    public string? ContentSha256 { get; init; }

    public string Detail { get; init; } = string.Empty;

    public static TilePackSyncResult FromCache(TilePackManifest manifest, int tileCount, string? detail = null)
        => new()
        {
            Kind = TilePackSyncKind.Cached,
            Version = manifest.Version,
            TileCount = tileCount,
            ContentSha256 = manifest.FrogpackSha256,
            Detail = detail ?? string.Empty,
        };

    public static TilePackSyncResult FromDownload(TilePackManifest manifest, int tileCount)
        => new()
        {
            Kind = TilePackSyncKind.Downloaded,
            Version = manifest.Version,
            TileCount = tileCount,
            ContentSha256 = manifest.FrogpackSha256,
            Detail = string.Empty,
        };

    public static TilePackSyncResult Rejected(string detail)
        => new() { Kind = TilePackSyncKind.Rejected, Detail = detail };

    public static TilePackSyncResult Unavailable(string detail)
        => new() { Kind = TilePackSyncKind.Unavailable, Detail = detail };
}
