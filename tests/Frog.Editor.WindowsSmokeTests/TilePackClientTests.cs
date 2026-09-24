using System.Drawing;
using System.IO;
using System.Net;
using System.Text.Json;

using Frog.Client.Assets;
using Frog.Client.Config;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

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
    public void Render_TileAssetId_BlitsVerifiedPixels_SheetMapStays32()
    {
        var red = TileAsset.FromStraightRgba(Solid(255, 0, 0, 255));
        var lookup = new MemoryTileAssetLookup(new[] { red });
        var map = MapFormat.CreateTileAssetMap("zone", 1, 1);
        var ground = new Layer { LayerType = LayerType.Ground, Visible = true };
        ground.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, AssetId = red.Id });
        map.Layers.Add(ground);

        Assert.Equal(48, MapViewRenderer.MapTileSizePixels(map));
        var cache = new Dictionary<TileAssetId, Bitmap>();
        try
        {
            using var painted = MapViewRenderer.Render(
                map,
                new Dictionary<string, (float, float)>(),
                localUsername: null,
                localCenterXPx: -1000,
                localCenterYPx: -1000,
                tilesetBitmaps: null,
                tileAssets: lookup,
                tileAssetBitmaps: cache);
            Assert.Equal(48, painted.Width);
            Assert.Equal(48, painted.Height);
            var pixel = painted.GetPixel(24, 24);
            Assert.Equal(255, pixel.R);
            Assert.Equal(0, pixel.G);
            Assert.Equal(0, pixel.B);
            Assert.True(cache.ContainsKey(red.Id));
        }
        finally
        {
            foreach (var bitmap in cache.Values)
            {
                bitmap.Dispose();
            }
        }

        var missing = MapFormat.CreateTileAssetMap("vide", 1, 1);
        var emptyLayer = new Layer { LayerType = LayerType.Ground, Visible = true };
        emptyLayer.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, AssetId = red.Id });
        missing.Layers.Add(emptyLayer);
        using var unpainted = MapViewRenderer.Render(
            missing,
            new Dictionary<string, (float, float)>(),
            localUsername: null,
            localCenterXPx: -1000,
            localCenterYPx: -1000,
            tilesetBitmaps: null,
            tileAssets: new MemoryTileAssetLookup(Array.Empty<TileAsset>()));
        Assert.Equal(48, unpainted.Width);
        Assert.Equal(Color.FromArgb(120, 160, 100).ToArgb(), unpainted.GetPixel(24, 24).ToArgb());

        var sheet = new Map { Name = "feuille", Width = 1, Height = 1 };
        var sheetLayer = new Layer { LayerType = LayerType.Ground, Visible = true };
        sheetLayer.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Ground, TilesetId = 1, SrcX = 0, SrcY = 0 });
        sheet.Layers.Add(sheetLayer);
        Assert.Equal(32, MapViewRenderer.MapTileSizePixels(sheet));
        using var sheetBmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(sheetBmp))
        {
            g.Clear(Color.Blue);
        }

        using var sheetPaint = MapViewRenderer.Render(
            sheet,
            new Dictionary<string, (float, float)>(),
            localUsername: null,
            localCenterXPx: -1000,
            localCenterYPx: -1000,
            tilesetBitmaps: new Dictionary<int, Bitmap> { [1] = sheetBmp },
            tileAssets: lookup);
        Assert.Equal(32, sheetPaint.Width);
        Assert.Equal(32, sheetPaint.Height);
        Assert.Equal(Color.Blue.ToArgb(), sheetPaint.GetPixel(8, 8).ToArgb());
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
