using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Services;

public sealed class MapService
{
    /// <summary>Carte monde par défaut côté API session (sessions démarrent généralement ici).</summary>
    public const int DefaultWorldMapId = 1;

    private readonly MapSerializer _mapSerializer = new();
    private readonly IMapBlobStore _mapBlobStore;
    private readonly ILogger<MapService> _logger;

    /// <summary>Couches monde : fichier / secours puis cartes lazy depuis <see cref="IMapBlobStore"/>.</summary>
    private readonly Dictionary<int, MapChunk> _chunks = new();

    /// <summary>Warps (map source, tuile) → destination.</summary>
    private readonly Dictionary<(int MapId, int X, int Y), (int TargetMapId, int TargetX, int TargetY)> _warps = new();

    public MapService(
        IOptions<WorldMapOptions> worldMapOptions,
        IMapBlobStore mapBlobStore,
        ILogger<MapService> logger)
    {
        _mapBlobStore = mapBlobStore;
        _logger = logger;
        var options = worldMapOptions.Value;

        Map primaryModel;
        var rawPath = options.WorldMapPath;
        var resolved = ResolveMapPath(rawPath);
        if (resolved is not null && File.Exists(resolved))
        {
            try
            {
                var bytesFromFile = File.ReadAllBytes(resolved);
                primaryModel = _mapSerializer.Deserialize(bytesFromFile);
                logger.LogInformation("Carte monde chargee depuis {Path}", resolved);

                RegisterWorldChunkFromModel(
                    DefaultWorldMapId,
                    primaryModel,
                    options.DatabaseFallbackMapId > 0 ? options.DatabaseFallbackMapId : DefaultWorldMapId,
                    fingerprintSerialSourceBytes: bytesFromFile);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Echec de lecture de la carte {Path}, tentative base puis secours.", resolved);
                primaryModel = TryLoadFromDatabaseOrBootstrapWorld(options);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rawPath))
            {
                logger.LogWarning("Fichier carte introuvable ({Raw}), tentative base puis secours.", rawPath);
            }

            primaryModel = TryLoadFromDatabaseOrBootstrapWorld(options);
        }

        _warps.TrimExcess();
        _chunks.TrimExcess();
    }

    private Map TryLoadFromDatabaseOrBootstrapWorld(WorldMapOptions options)
    {
        var blobId = options.DatabaseFallbackMapId;
        if (blobId <= 0)
        {
            return RegisterStandaloneSampleWorldChunk(DefaultWorldMapId);
        }

        if (_mapBlobStore.TryGet(blobId, out var bytes, out _, out _))
        {
            try
            {
                var map = _mapSerializer.Deserialize(bytes);
                _logger.LogInformation("Carte monde chargee depuis MariaDB frog_map id={BlobId}", blobId);
                RegisterWorldChunkFromModel(
                    DefaultWorldMapId,
                    map,
                    blobId,
                    fingerprintSerialSourceBytes: bytes);
                return map;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "frog_map id={BlobId} illisible, carte de secours.", blobId);
            }
        }

        return RegisterStandaloneSampleWorldChunk(blobId);
    }

    /// <summary>Starter Meadow hors DB : blobs sérialisés en mémoire pour cohérence hash.</summary>
    private Map RegisterStandaloneSampleWorldChunk(int headLookupBlobIdForFingerprint)
    {
        var map = MapSamples.StarterMeadow(DefaultWorldMapId);
        var bytes = _mapSerializer.Serialize(map);
        RegisterWorldChunkFromModel(
            DefaultWorldMapId,
            map,
            headLookupBlobIdForFingerprint: headLookupBlobIdForFingerprint,
            fingerprintSerialSourceBytes: bytes);
        return map;
    }

    private void RegisterWorldChunkFromModel(
        int worldLogicalMapId,
        Map model,
        int headLookupBlobIdForFingerprint,
        byte[] fingerprintSerialSourceBytes)
    {
        var sha = SHA256.HashData(fingerprintSerialSourceBytes);
        long fingerprintRevision = 1;
        if (_mapBlobStore.TryGetHead(headLookupBlobIdForFingerprint, out var headRev, out var headHex))
        {
            Span<byte> decoded = stackalloc byte[32];
            if (TryDecodeHexSha256(headHex.AsSpan(), decoded) && CryptographicOperations.FixedTimeEquals(sha, decoded))
            {
                fingerprintRevision = headRev;
            }
        }

        UpsertChunkFromParts(worldLogicalMapId, model, (byte[])fingerprintSerialSourceBytes.Clone(), sha, fingerprintRevision);
    }

    private void UpsertChunkFromParts(int mapId, Map model, byte[] serializedCanon, byte[] sha256Binary, long fingerprintRevision)
    {
        if (_chunks.ContainsKey(mapId))
        {
            StripWarpsForMap(mapId);
        }

        var chunk = new MapChunk(model, serializedCanon, sha256Binary, fingerprintRevision, IndexBlockedTiles(model));
        _chunks[mapId] = chunk;
        AddWarpEntries(mapId, model);
    }

    private void StripWarpsForMap(int sourceMapId)
    {
        var keys = _warps.Keys.Where(k => k.MapId == sourceMapId).ToArray();
        foreach (var key in keys)
        {
            _warps.Remove(key);
        }
    }

    /// <returns><see langword="false"/> si le blob est absent ou illisible.</returns>
    public bool TryEnsureMapLoaded(int mapId)
    {
        if (_chunks.ContainsKey(mapId))
        {
            return true;
        }

        if (!_mapBlobStore.TryGet(mapId, out var blobBytes, out var blobRevFromGet, out var shaHexClaimed))
        {
            _logger.LogDebug("frog_map absent pour id={MapId}", mapId);
            return false;
        }

        Map model;
        try
        {
            model = _mapSerializer.Deserialize(blobBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible deserialiser frog_map id={MapId}", mapId);
            return false;
        }

        var serializedCanon = (byte[])blobBytes.Clone();
        var shaFingerprint = SHA256.HashData(serializedCanon);
        Span<byte> claimed = stackalloc byte[32];
        long fingerprintRev = 1;
        if (TryDecodeHexSha256(shaHexClaimed.AsSpan(), claimed) &&
            CryptographicOperations.FixedTimeEquals(shaFingerprint, claimed))
        {
            fingerprintRev = ResolveHeadRevision(mapId, shaFingerprint);
            if (fingerprintRev == 1 && blobRevFromGet > 0)
            {
                fingerprintRev = blobRevFromGet;
            }
        }

        UpsertChunkFromParts(mapId, model, serializedCanon, shaFingerprint, fingerprintRev);

        _logger.LogInformation(
            "Carte chargee a la demande id={MapId} revisionFingerPrint={FingerprintRev}",
            mapId,
            fingerprintRev);

        return true;
    }

    private long ResolveHeadRevision(int mapId, ReadOnlySpan<byte> fingerprintShaBinary)
    {
        if (!_mapBlobStore.TryGetHead(mapId, out var rev, out var hexFromHead))
        {
            return 1;
        }

        Span<byte> decodedHead = stackalloc byte[32];
        return TryDecodeHexSha256(hexFromHead.AsSpan(), decodedHead) &&
               CryptographicOperations.FixedTimeEquals(fingerprintShaBinary, decodedHead)
            ? rev
            : 1;
    }

    private static HashSet<(int X, int Y)> IndexBlockedTiles(Map map)
        => MapCollision.IndexBlockedTiles(map);

    private void AddWarpEntries(int sourceMapId, Map map)
    {
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type != TileType.Warp)
                {
                    continue;
                }

                var destinationMap =
                    tile.WarpTargetMapId == 0 ? DefaultWorldMapId : tile.WarpTargetMapId;
                _warps[(sourceMapId, tile.X, tile.Y)] = (destinationMap, tile.WarpTargetX, tile.WarpTargetY);
            }
        }
    }

    public bool TryGetWarpDestination(int mapId, int tileX, int tileY, out int targetMapId, out int targetX, out int targetY)
    {
        if (!_warps.TryGetValue((mapId, tileX, tileY), out var dest))
        {
            targetMapId = 0;
            targetX = 0;
            targetY = 0;
            return false;
        }

        targetMapId = dest.TargetMapId;
        targetX = dest.TargetX;
        targetY = dest.TargetY;
        return true;
    }

    public byte[] CloneSerializedBlob(int mapId)
    {
        if (!_chunks.TryGetValue(mapId, out var chunk))
        {
            throw new KeyNotFoundException($"Carte {mapId} non chargee.");
        }

        return (byte[])chunk.SerializedCanon.Clone();
    }

    /// <inheritdoc cref="CloneSerializedBlob"/>
    /// <remarks>Garde une signature utilisée avant multi-maps (<paramref name="sessionId"/> ignoré).</remarks>
    public byte[] GetSerializedMapForSession(Guid sessionId, int mapId)
    {
        _ = sessionId;
        return CloneSerializedBlob(mapId);
    }

    /// <summary>Blob carte monde par défaut (compat tests / appels historiques).</summary>
    public byte[] GetSerializedMapForSession(Guid sessionId)
        => GetSerializedMapForSession(sessionId, DefaultWorldMapId);

    public long GetFingerprintRevision(int mapId)
        => _chunks.TryGetValue(mapId, out var c)
            ? c.FingerprintRevision
            : throw new KeyNotFoundException($"Carte {mapId} non chargee.");

    public ReadOnlySpan<byte> GetFingerprintSha256(int mapId)
        => !_chunks.TryGetValue(mapId, out var chunk)
            ? throw new KeyNotFoundException($"Carte {mapId} non chargee.")
            : chunk.Sha256;

    public ReadOnlySpan<byte> WorldFingerprintSha256 => GetFingerprintSha256(DefaultWorldMapId);

    public long WorldMapFingerprintRevision => GetFingerprintRevision(DefaultWorldMapId);

    public (int Width, int Height) GetDefaultMapBounds()
    {
        if (!TryGetMapBounds(DefaultWorldMapId, out var w, out var h))
        {
            throw new InvalidOperationException("Carte monde par defaut non initialisee.");
        }

        return (w, h);
    }

    public bool TryMatchMapFingerprint(int mapId, long clientRevision, ReadOnlySpan<byte> clientSha256)
    {
        if (clientSha256.Length != 32 || !_chunks.TryGetValue(mapId, out var chunk))
        {
            return false;
        }

        return chunk.FingerprintRevision == clientRevision &&
               CryptographicOperations.FixedTimeEquals(chunk.Sha256, clientSha256);
    }

    public bool AllowsPlayerOverlapOnMap(int mapId)
        => _chunks.TryGetValue(mapId, out var c) && c.Model.AllowPlayerOverlap;

    public bool TryGetMapBounds(int mapId, out int width, out int height)
    {
        width = height = 0;
        return _chunks.TryGetValue(mapId, out var chunk) &&
               (width = chunk.Model.Width) > 0 &&
               (height = chunk.Model.Height) > 0;
    }

    public bool IsBlocked(int mapId, int x, int y)
        => _chunks.TryGetValue(mapId, out var chunk) && chunk.BlockedTiles.Contains((x, y));

    /// <summary>True si le cercle (centre pixels) intersecte au moins une tuile bloquée.</summary>
    public bool IsBlockedForPlayerCircle(
        int mapId,
        int centerPixelX,
        int centerPixelY,
        int radiusPixels,
        int tileSizePixels = WorldMetrics.DefaultTileSizePixels)
    {
        if (!_chunks.TryGetValue(mapId, out var chunk))
        {
            return true;
        }

        return MapCollision.IsBlockedForPlayerCircle(
            chunk.Model,
            chunk.BlockedTiles,
            centerPixelX,
            centerPixelY,
            radiusPixels,
            tileSizePixels);
    }

    public bool IsWarpCell(int mapId, int x, int y)
        => _warps.ContainsKey((mapId, x, y));

    private static bool TryDecodeHexSha256(ReadOnlySpan<char> hex, Span<byte> destination32)
    {
        hex = hex.Trim();
        if (hex.Length != 64 || destination32.Length != 32)
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromHexString(hex.ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length != 32)
        {
            return false;
        }

        decoded.CopyTo(destination32);
        return true;
    }

    private static string? ResolveMapPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    /// <remarks>Représente un blob carte chargé avec son hash aligné synchro.</remarks>
    private sealed class MapChunk(
        Map model,
        byte[] serializedCanon,
        byte[] sha256,
        long fingerprintRevision,
        HashSet<(int X, int Y)> blockedTiles)
    {
        public Map Model { get; } = model;
        public byte[] SerializedCanon { get; } = serializedCanon;
        public byte[] Sha256 { get; } = sha256;
        public long FingerprintRevision { get; } = fingerprintRevision;
        public HashSet<(int X, int Y)> BlockedTiles { get; } = blockedTiles;
    }
}
