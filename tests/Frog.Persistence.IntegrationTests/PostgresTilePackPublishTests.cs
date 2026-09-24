using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Server.Config;
using Frog.Server.Content;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Frog.Persistence.IntegrationTests;

public sealed class TilePackModelTests
{
    [Fact]
    public void RelationalNames_MatchTileAssetCatalogMigration()
    {
        var options = new DbContextOptionsBuilder<FrogDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=frog;Username=frog;Password=x")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new FrogDbContext(options);

        var tiles = db.Model.FindEntityType(typeof(ContentTileEntity));
        Assert.NotNull(tiles);
        Assert.Equal("content", tiles!.GetSchema());
        Assert.Equal("tiles", tiles.GetTableName());
        Assert.Equal("png_bytes", Column(tiles, nameof(ContentTileEntity.PngBytes)));
        Assert.Equal("content_bytes_len", Column(tiles, nameof(ContentTileEntity.ContentBytesLen)));
        Assert.Equal("tile_asset_id", Column(tiles, nameof(ContentTileEntity.TileAssetId)));
        Assert.Equal("width_px", Column(tiles, nameof(ContentTileEntity.WidthPx)));

        var packs = db.Model.FindEntityType(typeof(ContentTilePackEntity))!;
        Assert.Equal("tile_packs", packs.GetTableName());
        Assert.Equal("content", packs.GetSchema());
        Assert.Equal("ed25519_signature", Column(packs, nameof(ContentTilePackEntity.Ed25519Signature)));
        Assert.Equal("ed25519_public_key_id", Column(packs, nameof(ContentTilePackEntity.Ed25519PublicKeyId)));
        Assert.Equal("frogpack_sha256", Column(packs, nameof(ContentTilePackEntity.FrogpackSha256)));
        Assert.Equal("frogpack_bytes_len", Column(packs, nameof(ContentTilePackEntity.FrogpackBytesLen)));
        Assert.Equal("published_at_utc", Column(packs, nameof(ContentTilePackEntity.PublishedAtUtc)));

        var entries = db.Model.FindEntityType(typeof(ContentTilePackEntryEntity))!;
        Assert.Equal("tile_pack_entries", entries.GetTableName());
        Assert.Equal("entry_meta_json", Column(entries, nameof(ContentTilePackEntryEntity.EntryMetaJson)));
        Assert.Null(db.Model.FindEntityType("Frog.Persistence.PostgreSql.Entities.PlayerTileUnlockEntity"));
    }

    private static string Column(IEntityType entity, string property)
    {
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        return entity.FindProperty(property)!.GetColumnName(store)!;
    }
}

[Collection("PostgresIsolated")]
public sealed class PostgresTilePackPublishTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresTilePackPublishTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Publish_IsImmutable_AndHttpManifestReturnsPack()
    {
        var keys = FrogPackKeys.Generate();
        var options = new TilePackOptions
        {
            PublicKeyHex = Convert.ToHexString(keys.PublicKey).ToLowerInvariant(),
            PrivateSeedHex = Convert.ToHexString(keys.PrivateSeed).ToLowerInvariant(),
            AdminToken = "pg-token",
        };
        using var gate = CreateGate();
        var repository = new PostgresTilePackRepository(gate);
        var service = new TilePackPublishService(repository, Options.Create(options));
        var red = Solid(200, 10, 10, 255);
        var green = Solid(10, 180, 20, 255);

        var published = Assert.IsType<TilePackPublishResult.Published>(
            await service.PublishTilesAsync("world", "1", new[] { green, red }));
        Assert.Equal(48, published.Manifest.TileSizePixels);
        Assert.Equal((ushort)11, published.Manifest.ProtocolVersion);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);

        await using (var db = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)))
        {
            Assert.Equal(2, await db.ContentTiles.CountAsync());
            Assert.Equal(2, await db.ContentTilePackEntries.CountAsync());
            var pack = await db.ContentTilePacks.SingleAsync();
            Assert.Equal((short)TilePackStatus.Published, pack.Status);
            Assert.Equal(published.Manifest.FrogpackSha256, pack.FrogpackSha256!.Trim());
            Assert.Equal(9216, await db.ContentTiles.Select(t => t.ContentBytesLen).FirstAsync());
            Assert.Contains("premultiplied-rgba8", await db.ContentTiles.Select(t => t.MetaJson).FirstAsync(), StringComparison.Ordinal);
        }

        var bytes = await service.GetCurrentPackBytesAsync("world");
        Assert.NotNull(bytes);
        Assert.Equal(2, FrogPackReader.Read(bytes, keys.PublicKey).Count);

        var http = new TilePackContentHttp(service, Options.Create(options));
        var manifest = await http.HandleAsync("GET", "/content/tile-packs/current", null, null, ReadOnlyMemory<byte>.Empty, null);
        Assert.Equal(System.Net.HttpStatusCode.OK, manifest.StatusCode);
        Assert.Contains(published.Manifest.FrogpackSha256, System.Text.Encoding.UTF8.GetString(manifest.Body), StringComparison.Ordinal);

        Assert.Equal(TilePackGuardResult.Immutable, await repository.TryMutateManifestAsync(published.Manifest.PackId, "{\"tamper\":true}"));

        await using (var probe = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)))
        {
            var mutateEntries = await Assert.ThrowsAnyAsync<Exception>(() => probe.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE content.tile_pack_entries SET ordinal = ordinal + 1 WHERE pack_id = {published.Manifest.PackId}"));
            Assert.Contains("immutable", mutateEntries.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(await service.YankAsync("world", "1"));
        Assert.Equal(TilePackGuardResult.Immutable, await repository.TryMutateManifestAsync(published.Manifest.PackId, "{\"still\":true}"));
        Assert.Null(await service.GetCurrentManifestAsync(null));

        var again = Assert.IsType<TilePackPublishResult.Published>(
            await service.PublishTilesAsync("world", "2", new[] { red, green }));
        Assert.Equal(published.Manifest.FrogpackSha256, again.Manifest.FrogpackSha256);

        var foreign = FrogPackWriter.Write(new[] { red }, FrogPackKeys.Generate().PrivateSeed);
        Assert.IsType<TilePackPublishResult.Rejected>(await service.PublishUploadedPackAsync("world", "9", foreign));
        await using var check = new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString));
        Assert.Equal(2, await check.ContentTiles.CountAsync());
        Assert.Equal(2, await check.ContentTilePacks.CountAsync());
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static TileAsset Solid(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return TileAsset.FromStraightRgba(bytes);
    }
}
