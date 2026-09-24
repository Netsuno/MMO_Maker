using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Server.Config;
using Frog.Server.Content;

using Microsoft.Extensions.Options;

using Xunit;

namespace Frog.Tests;

public sealed class TilePackPublishTests
{
    [Fact]
    public async Task Publish_SignsVerifies_AndManifestStaysHello11()
    {
        var keys = FrogPackKeys.Generate();
        var (service, repo, _) = Create(keys);
        var red = Solid(255, 0, 0, 255);
        var blue = Solid(0, 0, 255, 255);

        var published = Assert.IsType<TilePackPublishResult.Published>(
            await service.PublishTilesAsync("world", "1", new[] { blue, red, blue }, new[] { "blue", "red", "blue-dup" }));

        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, published.Manifest.TileSizePixels);
        Assert.Equal((ushort)11, published.Manifest.ProtocolVersion);
        Assert.Equal(2, published.Manifest.TileCount);
        Assert.Equal(64, published.Manifest.FrogpackSha256.Length);
        Assert.Equal(128, published.Manifest.Ed25519Signature.Length);
        Assert.DoesNotContain(Enum.GetNames<PacketId>(), name => name.Contains("TilePack", StringComparison.Ordinal));

        var bytes = await service.GetCurrentPackBytesAsync("world");
        Assert.NotNull(bytes);
        var loaded = FrogPackReader.Read(bytes, keys.PublicKey);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(new[] { red.Id, blue.Id }.OrderBy(id => id).Select(id => id.ToHex()), loaded.Select(t => t.Id.ToHex()));

        var manifest = await service.GetCurrentManifestAsync(null);
        Assert.Equal(published.Manifest.FrogpackSha256, manifest!.FrogpackSha256);
        Assert.Equal("/content/tile-packs/current.frogpack?slug=world", manifest.DownloadPath);

        var http = new TilePackContentHttp(service, Options.Create(OptionsFor(keys)));
        var response = await http.HandleAsync("GET", "/content/tile-packs/current?slug=world", null, null, ReadOnlyMemory<byte>.Empty, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal(11, document.RootElement.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(48, document.RootElement.GetProperty("tileSizePixels").GetInt32());
        Assert.Equal(published.Manifest.FrogpackSha256, document.RootElement.GetProperty("frogpackSha256").GetString());
        Assert.Equal("published", document.RootElement.GetProperty("status").GetString());

        Assert.Equal(TilePackGuardResult.Immutable, await repo.TryMutateManifestAsync(published.Manifest.PackId, "{\"tamper\":true}"));
        Assert.Equal(published.Manifest.FrogpackSha256, (await service.GetCurrentManifestAsync("world"))!.FrogpackSha256);

        Assert.True(await service.YankAsync("world", "1"));
        Assert.Equal(TilePackGuardResult.Immutable, await repo.TryMutateManifestAsync(published.Manifest.PackId, "{\"tamper\":true}"));
        Assert.Null(await service.GetCurrentManifestAsync(null));
        Assert.IsType<TilePackPublishResult.Rejected>(await service.PublishTilesAsync("world", "1", new[] { red }));

        var again = Assert.IsType<TilePackPublishResult.Published>(
            await service.PublishTilesAsync("world", "2", new[] { red, blue }));
        Assert.Equal(published.Manifest.FrogpackSha256, again.Manifest.FrogpackSha256);
        Assert.Equal(2, (await repo.ListTilesAsync()).Count);
    }

    [Fact]
    public async Task Upload_RejectsBadSignature_WrongKey_AndEncryption_WithoutStoring()
    {
        var keys = FrogPackKeys.Generate();
        var other = FrogPackKeys.Generate();
        var (service, _, _) = Create(keys);
        var red = Solid(255, 0, 0, 255);

        var foreign = FrogPackWriter.Write(new[] { red }, other.PrivateSeed);
        var wrongKey = Assert.IsType<TilePackPublishResult.Rejected>(
            await service.PublishUploadedPackAsync("world", "1", foreign));
        Assert.Contains("Clé", wrongKey.Reason, StringComparison.Ordinal);
        Assert.Null(await service.GetCurrentManifestAsync(null));

        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);
        var badSignature = file.ToArray();
        badSignature[^1] ^= 0x01;
        var signatureError = Assert.IsType<TilePackPublishResult.Rejected>(
            await service.PublishUploadedPackAsync("world", "1", badSignature));
        Assert.Contains("Signature", signatureError.Reason, StringComparison.Ordinal);

        var encrypted = file.ToArray();
        encrypted[6] = (byte)FrogPackFormat.FlagEncrypted;
        var cryptoError = Assert.IsType<TilePackPublishResult.Rejected>(
            await service.PublishUploadedPackAsync("world", "1", encrypted));
        Assert.Contains("Chiffrement", cryptoError.Reason, StringComparison.Ordinal);
        Assert.Null(await service.GetCurrentManifestAsync(null));
        Assert.Empty(await service.GetCurrentPackBytesAsync(null) is null
            ? Array.Empty<byte>()
            : await service.GetCurrentPackBytesAsync(null) ?? Array.Empty<byte>());
    }

    [Fact]
    public async Task Authoring_RefusesMismatchedSeed_AndEmptyPack()
    {
        var pinned = FrogPackKeys.Generate();
        var other = FrogPackKeys.Generate();
        var options = OptionsFor(pinned);
        options.PrivateSeedHex = Convert.ToHexString(other.PrivateSeed).ToLowerInvariant();
        Assert.False(options.TryValidate(out var error));
        Assert.Contains("épinglée", error, StringComparison.Ordinal);

        var service = new TilePackPublishService(new InMemoryTilePackRepository(), Options.Create(options));
        var rejected = Assert.IsType<TilePackPublishResult.Rejected>(
            await service.PublishTilesAsync("world", "1", new[] { Solid(1, 2, 3, 255) }));
        Assert.Contains("épinglée", rejected.Reason, StringComparison.Ordinal);

        var (good, _, _) = Create(pinned);
        var empty = Assert.IsType<TilePackPublishResult.Rejected>(
            await good.PublishTilesAsync("world", "1", Array.Empty<TileAsset>()));
        Assert.Contains("tuile", empty.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PngFolder_AndHttpDownload_RoundTrip()
    {
        var keys = FrogPackKeys.Generate();
        var root = Path.Combine(Path.GetTempPath(), "frog-tilepack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var red = SolidPixels(255, 0, 0, 255);
            var blue = SolidPixels(0, 0, 255, 255);
            await File.WriteAllBytesAsync(Path.Combine(root, "red.png"), PngRgba8.Encode(48, 48, red));
            var sheet = new byte[96 * 48 * 4];
            for (var y = 0; y < 48; y++)
            {
                red.AsSpan(y * 48 * 4, 48 * 4).CopyTo(sheet.AsSpan(((y * 96) + 0) * 4, 48 * 4));
                blue.AsSpan(y * 48 * 4, 48 * 4).CopyTo(sheet.AsSpan(((y * 96) + 48) * 4, 48 * 4));
            }

            var decoded = PngRgba8.Decode(PngRgba8.Encode(48, 48, red));
            Assert.Equal(red, decoded.StraightRgba);

            await File.WriteAllBytesAsync(Path.Combine(root, "sheet.png"), PngRgba8.Encode(96, 48, sheet));
            await File.WriteAllBytesAsync(Path.Combine(root, "too-small.png"), PngRgba8.Encode(32, 32, new byte[32 * 32 * 4]));

            var (service, _, options) = Create(keys);
            options.PngImportRoot = root;
            var tooSmall = Assert.IsType<TilePackPublishResult.Rejected>(
                await service.PublishPngDirectoryAsync("world", "bad", root, enforceImportRoot: false));
            Assert.Contains("too-small.png", tooSmall.Reason, StringComparison.Ordinal);

            File.Delete(Path.Combine(root, "too-small.png"));
            var published = Assert.IsType<TilePackPublishResult.Published>(
                await service.PublishPngDirectoryAsync("world", "1", root, enforceImportRoot: true));
            Assert.Equal(2, published.Manifest.TileCount);

            var outside = Assert.IsType<TilePackPublishResult.Rejected>(
                await service.PublishPngDirectoryAsync("world", "9", Path.GetTempPath(), enforceImportRoot: true));
            Assert.Contains("PngImportRoot", outside.Reason, StringComparison.Ordinal);

            var http = new TilePackContentHttp(service, Options.Create(options));
            var denied = await http.HandleAsync("POST", "/content/tile-packs", "application/json", null, Encoding.UTF8.GetBytes("{}"), null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var port = FreePort();
            await using var server = TilePackContentServer.Start(http, "127.0.0.1", port);
            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port + "/") };
            var manifest = await client.GetStringAsync("content/tile-packs/current?slug=world");
            using var document = JsonDocument.Parse(manifest);
            Assert.Equal(11, document.RootElement.GetProperty("protocolVersion").GetInt32());
            Assert.Equal(published.Manifest.FrogpackSha256, document.RootElement.GetProperty("frogpackSha256").GetString());
            var pack = await client.GetByteArrayAsync("content/tile-packs/current.frogpack?slug=world");
            Assert.Equal(2, FrogPackReader.Read(pack, keys.PublicKey).Count);

            var request = new HttpRequestMessage(HttpMethod.Get, "content/tile-packs/current.frogpack?slug=world");
            request.Headers.TryAddWithoutValidation("If-None-Match", "\"" + published.Manifest.FrogpackSha256 + "\"");
            var notModified = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HttpPost_PublishesStraightRgba_AndCatalogueRepublish()
    {
        var keys = FrogPackKeys.Generate();
        var (service, _, options) = Create(keys);
        var http = new TilePackContentHttp(service, Options.Create(options));
        var rgba = SolidPixels(10, 20, 30, 255);
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            slug = "world",
            version = "1",
            tiles = new[] { new { rgbaBase64 = Convert.ToBase64String(rgba), displayName = "moss" } },
        });
        var response = await http.HandleAsync(
            "POST",
            "/content/tile-packs",
            "application/json",
            options.AdminToken,
            body,
            null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(await service.YankAsync("world", "1"));
        var catalogue = Assert.IsType<TilePackPublishResult.Published>(
            await service.PublishStoredCatalogueAsync("world", "2"));
        Assert.Equal(1, catalogue.Manifest.TileCount);
        var loaded = FrogPackReader.Read((await service.GetCurrentPackBytesAsync("world"))!, keys.PublicKey);
        Assert.Equal(TileAsset.FromStraightRgba(rgba).Id, loaded[0].Id);
    }

    [Fact]
    public void KeygenLines_MatchPinnedPublicKey_AndCliParsesPublish()
    {
        var lines = TilePackCli.KeygenLines();
        var publicHex = lines[0].Split('=', 2)[1];
        var seedHex = lines[1].Split('=', 2)[1];
        Assert.Equal(64, publicHex.Length);
        Assert.Equal(
            publicHex,
            Convert.ToHexString(FrogPackKeys.PublicKeyFromSeed(Convert.FromHexString(seedHex))).ToLowerInvariant());
        Assert.Contains("11", lines[2], StringComparison.Ordinal);

        Assert.True(TilePackCli.TryParsePublish(
            new[] { "--tilepack-publish", "--slug", "world", "--version", "1", "--folder", "tiles", "--urls", "http://localhost" },
            out var parsed,
            out var hostArgs,
            out var error));
        Assert.Null(error);
        Assert.Equal("tiles", parsed.Folder);
        Assert.Equal(new[] { "--urls", "http://localhost" }, hostArgs);
        Assert.False(TilePackCli.TryParsePublish(new[] { "--tilepack-publish", "--slug", "world" }, out _, out _, out error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Host_ResolvesTilePackServices_WithoutOpeningContentPort()
    {
        using var host = Frog.Server.FrogServerHostFactory.CreateHostBuilder(
            null,
            new PlaytestRuntimeOptions { Enabled = true }).Build();
        Assert.NotNull(host.Services.GetService(typeof(TilePackPublishService)));
        Assert.NotNull(host.Services.GetService(typeof(ITilePackRepository)));
        var options = (IOptions<TilePackOptions>)host.Services.GetService(typeof(IOptions<TilePackOptions>))!;
        Assert.False(options.Value.Enabled);
    }

    [Fact]
    public void Appsettings_KeepsContentChannelDisabled()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir!.FullName, "Frog.Server", "appsettings.json"));
        Assert.Contains("\"TilePack\"", json, StringComparison.Ordinal);
        Assert.Contains("\"enabled\": false", json, StringComparison.Ordinal);
    }

    private static (TilePackPublishService Service, InMemoryTilePackRepository Repository, TilePackOptions Options) Create(
        FrogPackKeys.Ed25519KeyPair keys)
    {
        var options = OptionsFor(keys);
        var repository = new InMemoryTilePackRepository();
        return (new TilePackPublishService(repository, Options.Create(options)), repository, options);
    }

    private static TilePackOptions OptionsFor(FrogPackKeys.Ed25519KeyPair keys) => new()
    {
        PublicKeyHex = Convert.ToHexString(keys.PublicKey).ToLowerInvariant(),
        PrivateSeedHex = Convert.ToHexString(keys.PrivateSeed).ToLowerInvariant(),
        AdminToken = "dev-token",
        Enabled = false,
    };

    private static TileAsset Solid(byte r, byte g, byte b, byte a)
        => TileAsset.FromStraightRgba(SolidPixels(r, g, b, a));

    private static byte[] SolidPixels(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return bytes;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
