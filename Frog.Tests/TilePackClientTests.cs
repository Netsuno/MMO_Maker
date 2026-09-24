using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Frog.Client.Assets;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

using Xunit;

namespace Frog.Tests;

public sealed class TilePackClientTests
{
    [Fact]
    public void HelloAndWorldTileSize_StayPut()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
    }

    [Fact]
    public async Task Sync_RejectsBadSignature_WrongKey_Encryption_AndHashMismatch()
    {
        var keys = FrogPackKeys.Generate();
        var other = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(200, 10, 10, 255));
        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);
        var cache = NewCacheDir();
        try
        {
            var badSignature = (byte[])file.Clone();
            badSignature[^1] ^= 0x01;
            var rejected = await SyncAsync(keys.PublicKey, ManifestFor(file, 1), badSignature, cache);
            Assert.Equal(TilePackSyncKind.Rejected, rejected.Result.Kind);
            Assert.Contains("Ed25519", rejected.Result.Detail, StringComparison.Ordinal);
            Assert.False(rejected.Service.Lookup.TryGet(red.Id, out _));
            Assert.False(File.Exists(Path.Combine(cache, "index.json")));
            rejected.Service.Dispose();

            var encrypted = (byte[])file.Clone();
            encrypted[6] = (byte)FrogPackFormat.FlagEncrypted;
            var crypto = await SyncAsync(keys.PublicKey, ManifestFor(file, 1), encrypted, cache);
            Assert.Equal(TilePackSyncKind.Rejected, crypto.Result.Kind);
            Assert.Contains("V1", crypto.Result.Detail, StringComparison.Ordinal);
            Assert.False(crypto.Service.Lookup.TryGet(red.Id, out _));
            crypto.Service.Dispose();

            var wrongKey = await SyncAsync(other.PublicKey, ManifestFor(file, 1), file, cache);
            Assert.Equal(TilePackSyncKind.Rejected, wrongKey.Result.Kind);
            Assert.False(wrongKey.Service.HasVerifiedPack);
            wrongKey.Service.Dispose();

            var manifest = ManifestFor(file, 1);
            manifest.FrogpackSha256 = new string('a', 64);
            var hash = await SyncAsync(keys.PublicKey, manifest, file, cache);
            Assert.Equal(TilePackSyncKind.Rejected, hash.Result.Kind);
            Assert.Contains("frogpackSha256", hash.Result.Detail, StringComparison.Ordinal);
            Assert.False(hash.Service.Lookup.TryGet(red.Id, out _));
            Assert.False(File.Exists(Path.Combine(cache, "current.frogpack")));
            hash.Service.Dispose();
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public async Task Sync_MissingKey_DoesNotDownloadOrPaint()
    {
        var keys = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(1, 2, 3, 255));
        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);
        var transport = new ScriptedTransport(_ => new TilePackHttpGet((int)HttpStatusCode.OK, file));
        var cache = NewCacheDir();
        try
        {
            var service = new TilePackClientService(
                new TilePackClientOptions { ContentBaseUrl = "http://127.0.0.1:6080", PublicKeyHex = "" },
                transport,
                cache);
            var result = await service.SyncAsync();
            Assert.Equal(TilePackSyncKind.Rejected, result.Kind);
            Assert.Empty(transport.Paths);
            Assert.False(service.Lookup.TryGet(red.Id, out _));
            service.Dispose();
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public async Task Sync_CacheHit_SkipsPackDownload_AndResolvesId()
    {
        var keys = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(255, 0, 0, 255));
        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);
        var manifest = ManifestFor(file, 1);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var downloads = 0;
        var transport = new ScriptedTransport(url =>
        {
            if (url.AbsolutePath.EndsWith(".frogpack", StringComparison.Ordinal))
            {
                downloads++;
                return new TilePackHttpGet((int)HttpStatusCode.OK, file);
            }

            return new TilePackHttpGet((int)HttpStatusCode.OK, manifestBytes);
        });
        var cache = NewCacheDir();
        try
        {
            var service = Service(keys.PublicKey, transport, cache);
            var first = await service.SyncAsync();
            Assert.Equal(TilePackSyncKind.Downloaded, first.Kind);
            Assert.Equal(1, downloads);
            Assert.True(service.Lookup.TryGet(red.Id, out var found));
            Assert.Equal(red.NormalizedRgba, found!.NormalizedRgba);
            Assert.True(File.Exists(Path.Combine(cache, "index.json")));
            Assert.True(File.Exists(Path.Combine(cache, "current.frogpack")));

            var second = await service.SyncAsync();
            Assert.Equal(TilePackSyncKind.Cached, second.Kind);
            Assert.Equal(1, downloads);
            Assert.True(service.Lookup.TryGet(red.Id, out _));
            Assert.Equal(11, manifest.ProtocolVersion);
            service.Dispose();
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public void RenderPath_ResolvesTileAssetIdToPixels_SheetMapStays32()
    {
        var red = TileAsset.FromStraightRgba(Solid(255, 0, 0, 255));
        var lookup = new MemoryTileAssetLookup(new[] { red });
        var map = MapFormat.CreateTileAssetMap("zone", 1, 1);
        var ground = new Layer { LayerType = LayerType.Ground, Visible = true };
        ground.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, AssetId = red.Id });
        map.Layers.Add(ground);

        Assert.Equal(48, TileAssetDisplayPixels.MapPixelSize(map));
        Assert.True(TileAssetDisplayPixels.TryGetPixel(lookup, ground.Tiles[0].AssetId, 24, 24, out var r, out var g, out var b, out var a));
        Assert.Equal(255, r);
        Assert.Equal(0, g);
        Assert.Equal(0, b);
        Assert.Equal(255, a);

        var empty = new MemoryTileAssetLookup(Array.Empty<TileAsset>());
        Assert.False(TileAssetDisplayPixels.TryGetPixel(empty, red.Id, 24, 24, out _, out _, out _, out _));

        var sheet = new Map { Name = "feuille", Width = 1, Height = 1, GraphicIdentity = TileGraphicIdentity.SheetSource };
        Assert.Equal(32, TileAssetDisplayPixels.MapPixelSize(sheet));
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);

        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("TryDrawTileAsset", renderer, StringComparison.Ordinal);
        Assert.Contains("tileAssets: _tilePacks.Lookup", shell, StringComparison.Ordinal);
        Assert.Contains("TileAssetDisplayPixels.MapPixelSize", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldMetrics.DefaultTileSizePixels", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_NotFound_DropsVerifiedCache()
    {
        var keys = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(9, 9, 9, 255));
        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);
        var manifest = ManifestFor(file, 1);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var calls = 0;
        var transport = new ScriptedTransport(url =>
        {
            calls++;
            if (calls == 1)
            {
                return new TilePackHttpGet((int)HttpStatusCode.OK, manifestBytes);
            }

            if (url.AbsolutePath.EndsWith(".frogpack", StringComparison.Ordinal))
            {
                return new TilePackHttpGet((int)HttpStatusCode.OK, file);
            }

            return new TilePackHttpGet((int)HttpStatusCode.NotFound, Array.Empty<byte>());
        });
        var cache = NewCacheDir();
        try
        {
            var service = Service(keys.PublicKey, transport, cache);
            var first = await service.SyncAsync();
            Assert.Equal(TilePackSyncKind.Downloaded, first.Kind);
            Assert.True(File.Exists(Path.Combine(cache, "index.json")));

            var yanked = await service.SyncAsync();
            Assert.Equal(TilePackSyncKind.Unavailable, yanked.Kind);
            Assert.False(service.Lookup.TryGet(red.Id, out _));
            Assert.False(File.Exists(Path.Combine(cache, "index.json")));
            Assert.False(File.Exists(Path.Combine(cache, "current.frogpack")));
            service.Dispose();
        }
        finally
        {
            TryDelete(cache);
        }
    }

    [Fact]
    public void Options_EnvOverridesSettings_AndCacheLivesUnderMmoMakerContent()
    {
        var previousUrl = Environment.GetEnvironmentVariable(TilePackClientOptions.ContentBaseUrlEnvironmentVariable);
        var previousKey = Environment.GetEnvironmentVariable(TilePackClientOptions.PublicKeyHexEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(TilePackClientOptions.ContentBaseUrlEnvironmentVariable, "http://content.example:6080/ignored");
            Environment.SetEnvironmentVariable(TilePackClientOptions.PublicKeyHexEnvironmentVariable, "ab");
            var resolved = TilePackClientOptions.Resolve(new UserSettings
            {
                TilePackContentBaseUrl = "http://127.0.0.1:9",
                TilePackPublicKeyHex = "ff",
            });
            Assert.Equal("http://content.example:6080", resolved.ContentBaseUrl);
            Assert.Equal("ab", resolved.PublicKeyHex);
            Assert.EndsWith(Path.Combine("MmoMaker", "Content", "tile-assets"), TilePackClientOptions.DefaultCacheDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable(TilePackClientOptions.ContentBaseUrlEnvironmentVariable, previousUrl);
            Environment.SetEnvironmentVariable(TilePackClientOptions.PublicKeyHexEnvironmentVariable, previousKey);
        }
    }

    private static async Task<(TilePackClientService Service, TilePackSyncResult Result)> SyncAsync(
        byte[] publicKey,
        TilePackManifest manifest,
        byte[] pack,
        string cache)
    {
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var transport = new ScriptedTransport(url =>
        {
            var body = url.AbsolutePath.EndsWith(".frogpack", StringComparison.Ordinal) ? pack : manifestBytes;
            return new TilePackHttpGet((int)HttpStatusCode.OK, body);
        });
        var service = Service(publicKey, transport, cache);
        var result = await service.SyncAsync();
        return (service, result);
    }

    private static TilePackClientService Service(byte[] publicKey, ITilePackTransport transport, string cache)
        => new(
            new TilePackClientOptions
            {
                ContentBaseUrl = "http://127.0.0.1:6080",
                PublicKeyHex = Convert.ToHexString(publicKey).ToLowerInvariant(),
            },
            transport,
            cache);

    private static TilePackManifest ManifestFor(byte[] pack, int tileCount)
    {
        var sha = TilePackClientService.ContentSha256Hex(pack);
        return new TilePackManifest
        {
            Slug = "world",
            Version = "1",
            TileCount = tileCount,
            TileSizePixels = 48,
            FrogpackSha256 = sha,
            ProtocolVersion = FrogWireProtocol.Version,
            DownloadPath = "/content/tile-packs/current.frogpack?slug=world",
        };
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
    {
        var buffer = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = r;
            buffer[i + 1] = g;
            buffer[i + 2] = b;
            buffer[i + 3] = a;
        }

        return buffer;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }

    private static string NewCacheDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "frog-tilepack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path ?? throw new InvalidOperationException("Cache temporaire introuvable.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // ignore
        }
    }

    private sealed class ScriptedTransport : ITilePackTransport
    {
        private readonly Func<Uri, TilePackHttpGet> _next;

        public ScriptedTransport(Func<Uri, TilePackHttpGet> next) => _next = next;

        public List<string> Paths { get; } = new();

        public Task<TilePackHttpGet> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            Paths.Add(url.AbsolutePath);
            return Task.FromResult(_next(url));
        }
    }
}
