using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Client.Assets;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Content;

using Microsoft.Extensions.Options;

using Xunit;

namespace Frog.Tests;

public sealed class ComposedTilesetTests
{
    [Fact]
    public void Hello_And_ContentTileSize_StayPut_SheetFormKeeps32()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var form = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        Assert.Contains("Taille tuile", form, StringComparison.Ordinal);
        Assert.Contains("Value = 32", form, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", form, StringComparison.Ordinal);

        var panel = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Editor", "Forms", "GameData", "ComposedTilesetEditorPanel.cs"));
        Assert.DoesNotContain("TileSheetSlicer", panel, StringComparison.Ordinal);
        Assert.Contains("ne découpe pas une feuille", panel, StringComparison.Ordinal);
        Assert.Contains("Tuiles composées", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Draft_StaysOutOfPlacement_Published_KeepsChosenTileOrder()
    {
        var repo = new InMemoryComposedTilesetRepository(ContentRepositoryCapabilities.InMemoryTest);
        var grass = Tile("Herbe", 10, 140, 30);
        var water = Tile("Eau", 20, 40, 180);
        var definition = NewSet("Sol", grass, water);

        var session = new ComposedTilesetWorkspaceSession(repo);
        session.AdoptNewDraft(definition);
        var draft = Assert.IsType<SaveComposedTilesetResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Empty(await repo.ListPublishedAsync());

        var local = new WorkingTileset { Name = "Tileset de travail" };
        local.Tiles.Add(grass.Id);
        var before = ComposedTilesetPlacement.Merge(new[] { local }, await repo.ListPublishedAsync());
        Assert.Single(before);
        Assert.Equal("Tileset de travail", before[0].Name);
        Assert.Equal(Guid.Empty, before[0].ServerTilesetId);

        var reopened = new ComposedTilesetWorkspaceSession(repo);
        Assert.True(await reopened.OpenAsync(draft.TilesetId));
        Assert.Equal(new[] { grass.Id.ToHex(), water.Id.ToHex() }, reopened.Current!.Tiles.Select(t => t.TileAssetId));
        Assert.Equal(ContentPublishStatus.Draft, reopened.CurrentStatus);

        var published = Assert.IsType<SaveComposedTilesetResult.Success>(
            await reopened.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.NotNull(published.PublishedRevision);

        var again = new ComposedTilesetWorkspaceSession(repo);
        Assert.True(await again.OpenAsync(draft.TilesetId));
        Assert.Equal(ContentPublishStatus.Published, again.CurrentStatus);
        Assert.Equal(grass.NormalizedRgba, again.Current!.Tiles[0].NormalizedRgba);
        Assert.Equal(water.NormalizedRgba, again.Current.Tiles[1].NormalizedRgba);

        var palette = ComposedTilesetPlacement.Merge(new[] { local }, await repo.ListPublishedAsync());
        Assert.Equal(2, palette.Count);
        Assert.Equal("Tileset de travail", palette[0].Name);
        var placed = Assert.Single(palette, set => set.ServerTilesetId == draft.TilesetId);
        Assert.Equal(new[] { grass.Id, water.Id }, placed.Tiles);
    }

    [Fact]
    public void ClientLoad_RejectsRawCatalog_AcceptsSignedPack()
    {
        var keys = FrogPackKeys.Generate();
        var grass = Tile("Herbe", 1, 2, 3);
        var water = Tile("Eau", 4, 5, 6);
        var set = Definition("Rive", grass, water);
        var signed = ComposedTilesetPackWriter.Write(new[] { set }, keys.PrivateSeed);

        var raw = Encoding.UTF8.GetBytes(
            "{\"tilesets\":[{\"id\":\"" + set.Id + "\",\"name\":\"Rive\",\"paletteId\":1,\"tileSizePixels\":32}]}");
        var rejected = Assert.Throws<FrogPackRejectedException>(() => ComposedTilesetClientLoader.Load(raw, keys.PublicKey));
        Assert.Contains("Catalogue brut", rejected.Message, StringComparison.Ordinal);
        Assert.Equal((byte)'C', signed[0]);
        Assert.NotEqual((byte)'{', signed[0]);

        var loaded = ComposedTilesetClientLoader.Load(signed, keys.PublicKey);
        var only = Assert.Single(loaded);
        Assert.Equal("Rive", only.Name);
        Assert.Equal(new[] { grass.Id.ToHex(), water.Id.ToHex() }, only.Tiles.Select(t => t.TileAssetId));
        Assert.Equal(grass.NormalizedRgba, only.Tiles[0].NormalizedRgba);
        Assert.Equal(water.NormalizedRgba, only.Tiles[1].NormalizedRgba);
        Assert.Equal("Herbe", only.Tiles[0].DisplayName);

        var wrongProtocol = (byte[])signed.Clone();
        wrongProtocol[8] = 12;
        var protocol = Assert.Throws<FrogPackRejectedException>(() => ComposedTilesetClientLoader.Load(wrongProtocol, keys.PublicKey));
        Assert.Contains("11", protocol.Message, StringComparison.Ordinal);

        var encrypted = (byte[])signed.Clone();
        encrypted[6] = (byte)ComposedTilesetPackFormat.FlagEncrypted;
        var crypto = Assert.Throws<FrogPackRejectedException>(() => ComposedTilesetClientLoader.Load(encrypted, keys.PublicKey));
        Assert.Contains("refusé", crypto.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_RejectsRawCatalogPayload_AndAcceptsSignedBody()
    {
        var keys = FrogPackKeys.Generate();
        var grass = Tile("Herbe", 9, 9, 9);
        var set = Definition("Pré", grass);
        var signed = ComposedTilesetPackWriter.Write(new[] { set }, keys.PrivateSeed);
        var raw = Encoding.UTF8.GetBytes("{\"tilesets\":[]}");
        var calls = 0;
        var transport = new ScriptedTransport(_ =>
        {
            calls++;
            var body = calls == 1 ? raw : signed;
            return new TilePackHttpGet((int)HttpStatusCode.OK, body);
        });
        var loader = new ComposedTilesetClientLoader(ClientOptions(keys), transport);
        var rejected = await loader.SyncAsync();
        Assert.Equal(ComposedTilesetLoadKind.Rejected, rejected.Kind);
        Assert.Contains("Catalogue brut", rejected.Detail, StringComparison.Ordinal);
        Assert.Empty(loader.Tilesets);
        Assert.Equal(ComposedTilesetPackFormat.HttpPath, Assert.Single(transport.Paths));

        var loaded = await loader.SyncAsync();
        Assert.Equal(ComposedTilesetLoadKind.Loaded, loaded.Kind);
        Assert.Equal(grass.Id.ToHex(), Assert.Single(Assert.Single(loader.Tilesets).Tiles).TileAssetId);
        loader.Dispose();
    }

    [Fact]
    public async Task HttpLoad_IsSignedBytes_DraftIsAbsent()
    {
        var keys = FrogPackKeys.Generate();
        var repo = new InMemoryComposedTilesetRepository(ContentRepositoryCapabilities.InMemoryTest);
        var grass = Tile("Herbe", 200, 10, 10);
        var water = Tile("Eau", 10, 10, 200);
        var definition = Definition("Berge", grass, water);
        var created = Assert.IsType<SaveComposedTilesetResult.Success>(await repo.SaveAsync(new SaveComposedTilesetRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var load = new ComposedTilesetProtectedLoad(repo, Options.Create(ServerOptions(keys)));
        var http = new TilePackContentHttp(
            new TilePackPublishService(new InMemoryTilePackRepository(), Options.Create(ServerOptions(keys))),
            Options.Create(ServerOptions(keys)),
            load);
        var draftResponse = await http.HandleAsync(
            "GET",
            ComposedTilesetPackFormat.HttpPath,
            null,
            null,
            ReadOnlyMemory<byte>.Empty,
            null);
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        Assert.Equal("application/octet-stream", draftResponse.ContentType);
        Assert.NotEqual((byte)'{', draftResponse.Body[0]);
        Assert.Empty(ComposedTilesetClientLoader.Load(draftResponse.Body, keys.PublicKey));

        Assert.IsType<SaveComposedTilesetResult.Success>(await repo.SaveAsync(new SaveComposedTilesetRequest
        {
            TilesetId = definition.Id,
            Definition = definition,
            ExpectedRevision = created.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        var publishedResponse = await http.HandleAsync(
            "GET",
            ComposedTilesetPackFormat.HttpPath,
            null,
            null,
            ReadOnlyMemory<byte>.Empty,
            null);
        var loaded = ComposedTilesetClientLoader.Load(publishedResponse.Body, keys.PublicKey);
        var only = Assert.Single(loaded);
        Assert.Equal(new[] { grass.Id.ToHex(), water.Id.ToHex() }, only.Tiles.Select(t => t.TileAssetId));
    }

    private static TilePackClientOptions ClientOptions(FrogPackKeys.Ed25519KeyPair keys) => new()
    {
        ContentBaseUrl = "http://127.0.0.1:6080",
        PublicKeyHex = Convert.ToHexString(keys.PublicKey).ToLowerInvariant(),
    };

    private static TilePackOptions ServerOptions(FrogPackKeys.Ed25519KeyPair keys) => new()
    {
        PublicKeyHex = Convert.ToHexString(keys.PublicKey).ToLowerInvariant(),
        PrivateSeedHex = Convert.ToHexString(keys.PrivateSeed).ToLowerInvariant(),
        Enabled = false,
    };

    private static ComposedTilesetDefinition NewSet(string name, params TileAsset[] tiles)
    {
        var definition = Definition(name, tiles);
        return definition;
    }

    private static ComposedTilesetDefinition Definition(string name, params TileAsset[] tiles)
    {
        var id = Guid.NewGuid();
        var definition = new ComposedTilesetDefinition
        {
            Id = id,
            Name = name,
            LogicalPath = "tiles/composed/" + id.ToString("N") + ".tileset",
        };
        foreach (var tile in tiles)
        {
            definition.Tiles.Add(new ComposedTileRef
            {
                TileAssetId = tile.Id.ToHex(),
                DisplayName = tile == tiles[0] ? "Herbe" : "Eau",
                NormalizedRgba = tile.NormalizedRgba.ToArray(),
            });
        }

        Assert.True(definition.Validate(out var error), error);
        return definition;
    }

    private static TileAsset Tile(string _, byte r, byte g, byte b)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = 255;
        }

        return TileAsset.FromStraightRgba(bytes);
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

    private sealed class ScriptedTransport : ITilePackTransport
    {
        private readonly Func<Uri, TilePackHttpGet> _next;

        public ScriptedTransport(Func<Uri, TilePackHttpGet> next) => _next = next;

        public System.Collections.Generic.List<string> Paths { get; } = new();

        public Task<TilePackHttpGet> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            Paths.Add(url.AbsolutePath);
            return Task.FromResult(_next(url));
        }
    }
}
