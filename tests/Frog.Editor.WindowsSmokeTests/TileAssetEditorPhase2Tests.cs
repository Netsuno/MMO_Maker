using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Services;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Service éditeur TileAsset, sans UI. Le catalogue n’est pas filtré par les déblocages joueur.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class TileAssetEditorPhase2Tests : IDisposable
{
    private const string OpaqueRedHash = "0118296e6c16a0113a31e71a64cac301152e44d9623ca2db92bbbfb166dd22fa";
    private readonly string _store;

    public TileAssetEditorPhase2Tests()
    {
        _store = Path.Combine(Path.GetTempPath(), "frog-tileassets-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_store))
        {
            Directory.Delete(_store, recursive: true);
        }
    }

    [Fact]
    public void Import_DedupesIntoCatalogue_AndDoesNotResizeByDefault()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);

        var catalogue = new TileAssetCatalogue();
        var sheet = Sheet(100, 50, (x, _) => x < 48
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)255, (byte)0, (byte)0, (byte)255));
        var imported = catalogue.ImportStraightRgba(sheet, 100, 50);
        Assert.False(imported.ExplicitResizeApplied);
        Assert.Equal(2, imported.Columns);
        Assert.Equal(1, imported.Rows);
        Assert.Equal(4, imported.DiscardedRightPixels);
        Assert.Equal(2, imported.DiscardedBottomPixels);
        Assert.Equal(1, imported.UniqueAdded);
        Assert.Equal(1, imported.CatalogueCount);
        Assert.Equal(OpaqueRedHash, catalogue.Ids[0].ToHex());

        var again = catalogue.ImportStraightRgba(sheet, 100, 50);
        Assert.Equal(0, again.UniqueAdded);
        Assert.Equal(1, again.UniqueAlreadyPresent);
        Assert.Equal(1, catalogue.Count);
        Assert.Equal(catalogue.Ids, catalogue.Search(null));
        Assert.Equal(catalogue.Ids, catalogue.Search(OpaqueRedHash[..8]));
        Assert.Empty(catalogue.Search("ffffff"));

        var distinct = Sheet(96, 48, (x, _) => x < 48
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255));
        var two = catalogue.ImportStraightRgba(distinct, 96, 48);
        Assert.Equal(1, two.UniqueAdded);
        Assert.Equal(1, two.UniqueAlreadyPresent);
        Assert.Equal(2, catalogue.Count);
        var lookup = catalogue.ToMemoryLookup();
        Assert.True(lookup.TryGet(catalogue.Ids[0], out _));
        Assert.True(lookup.TryGet(catalogue.Ids[1], out _));

        Assert.Throws<ArgumentException>(() => catalogue.ImportStraightRgba(new byte[32 * 32 * 4], 32, 32));
        Assert.Throws<ArgumentException>(() =>
            catalogue.ImportStraightRgba(Sheet(64, 32, (_, _) => ((byte)255, (byte)0, (byte)0, (byte)255)), 64, 32));
    }

    [Fact]
    public void ExplicitResize_IsOptIn_AndMatchesNative48Hash()
    {
        var catalogue = new TileAssetCatalogue();
        var sheet = Sheet(64, 32, (x, _) => x < 32
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)180, (byte)0, (byte)255));
        var imported = catalogue.ImportStraightRgba(sheet, 64, 32, new TileImportOptions
        {
            ExplicitResize = true,
            SourceCellPixels = WorldMetrics.DefaultTileSizePixels,
        });
        Assert.True(imported.ExplicitResizeApplied);
        Assert.Equal(2, imported.Columns);
        Assert.Equal(1, imported.Rows);
        Assert.Equal(2, imported.UniqueAdded);
        Assert.Equal(OpaqueRedHash, catalogue.Ids[0].ToHex());
        Assert.NotEqual(catalogue.Ids[0], catalogue.Ids[1]);
    }

    [Fact]
    public void WorkingTileset_AndCatalogue_RoundTripOnDisk()
    {
        var catalogue = new TileAssetCatalogue { StoreDirectory = _store };
        catalogue.EnsureDefaultWorkingTileset();
        var sheet = Sheet(96, 48, (x, _) => x < 48
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255));
        catalogue.ImportStraightRgba(sheet, 96, 48);
        Assert.True(catalogue.TryCreateWorkingTileset("Sol", out var createError), createError);
        Assert.True(catalogue.TryAddToActiveWorkingTileset(catalogue.Ids[1], out var addError), addError);
        Assert.True(catalogue.TryAddToActiveWorkingTileset(catalogue.Ids[0], out addError), addError);
        Assert.True(catalogue.TryMoveInActiveWorkingTileset(1, 0, out var moveError), moveError);
        Assert.Equal(catalogue.Ids[0], catalogue.ActiveWorkingTileset!.Tiles[0]);
        Assert.Equal(catalogue.Ids[1], catalogue.ActiveWorkingTileset.Tiles[1]);
        catalogue.SaveToDirectory(_store);

        var loaded = new TileAssetCatalogue();
        loaded.LoadFromDirectory(_store);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(catalogue.Ids[0], loaded.Ids[0]);
        Assert.True(loaded.TryGetStraightRgba(loaded.Ids[0], out var rgba));
        Assert.Equal(TileAssetMetrics.CanonicalPixelByteCount, rgba!.Length);
        Assert.True(loaded.ToMemoryLookup().TryGet(loaded.Ids[1], out _));
        var sol = Assert.Single(loaded.WorkingTilesets, set => set.Name == "Sol");
        Assert.Equal(new[] { catalogue.Ids[0], catalogue.Ids[1] }, sol.Tiles);
        Assert.Equal("Sol", loaded.ActiveWorkingTileset?.Name);

        Assert.True(loaded.TryRemoveFromActiveWorkingTileset(1, out var removeError), removeError);
        Assert.Single(loaded.ActiveWorkingTileset!.Tiles);
    }

    [Fact]
    public void PngCodec_RoundTripsStraightRgba_AndImportKeepsHash()
    {
        var red = Solid(255, 0, 0, 255);
        var png = TileAssetPngCodec.Encode(red, 48, 48);
        Assert.True(TileAssetPngCodec.TryDecode(png, out var width, out var height, out var decoded));
        Assert.Equal(48, width);
        Assert.Equal(48, height);
        Assert.Equal(red, decoded);

        var catalogue = new TileAssetCatalogue();
        var imported = catalogue.ImportPng(png);
        Assert.Equal(1, imported.CatalogueCount);
        Assert.Equal(OpaqueRedHash, catalogue.Ids[0].ToHex());
    }

    [Fact]
    public void Paint_WritesMapV6_AndSheetMapsStayV5()
    {
        var catalogue = new TileAssetCatalogue();
        catalogue.ImportStraightRgba(Sheet(96, 48, (x, _) => x < 48
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255)), 96, 48);
        var map = TileAssetMapEditing.CreateMap("Atelier", 2, 1);
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Equal(48, map.TileSizePixels);
        Assert.True(TileAssetMapEditing.TryPaint(
            map,
            0,
            0,
            0,
            TileAssetMapEditing.CreateBrushTile(0, 0, catalogue.Ids[0], TileType.Ground)));
        Assert.True(TileAssetMapEditing.TryPaint(
            map,
            0,
            1,
            0,
            TileAssetMapEditing.CreateBrushTile(1, 0, catalogue.Ids[1], TileType.Ground)));

        var bytes = TileAssetMapEditing.Write(map);
        Assert.Equal((byte)6, bytes[4]);
        Assert.Equal(bytes, TileAssetMapEditing.WriteEditorMap(map));
        var loaded = TileAssetMapEditing.ReadEditorMap(bytes);
        Assert.Equal(TileGraphicIdentity.TileAsset, loaded.GraphicIdentity);
        Assert.Equal(48, loaded.TileSizePixels);
        var tiles = loaded.Layers[0].Tiles.OrderBy(tile => tile.X).ToArray();
        Assert.Equal(catalogue.Ids[0], tiles[0].AssetId);
        Assert.Equal(catalogue.Ids[1], tiles[1].AssetId);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(0, tile.TilesetId);
            Assert.Equal(0, tile.SrcX);
            Assert.Equal(0, tile.SrcY);
        });
        Assert.True(loaded.Validate(out var error), error);

        var sheet = new Map { Name = "Feuille", Width = 2, Height = 1 };
        sheet.Layers.Add(new Layer { LayerType = LayerType.Ground });
        sheet.Layers[0].Tiles.Add(new Tile { X = 0, Y = 0, TilesetId = 3, SrcX = 32, SrcY = 0, Type = TileType.Ground });
        Assert.False(TileAssetMapEditing.TryAdoptTileAssetIdentity(sheet, out var refused));
        Assert.Contains("v5", refused, StringComparison.Ordinal);
        Assert.Equal(TileGraphicIdentity.SheetSource, sheet.GraphicIdentity);
        Assert.Equal((byte)5, TileAssetMapEditing.WriteEditorMap(sheet)[4]);
        Action writeSheet = () => _ = TileAssetMapEditing.Write(sheet);
        Assert.Throws<InvalidDataException>(writeSheet);

        var empty = new Map { Name = "Vide", Width = 1, Height = 1 };
        empty.Layers.Add(new Layer { LayerType = LayerType.Ground });
        Assert.True(TileAssetMapEditing.TryAdoptTileAssetIdentity(empty, out var adoptError), adoptError);
        Assert.Equal(48, empty.TileSizePixels);
        Assert.Equal((byte)6, TileAssetMapEditing.Write(empty)[4]);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
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

    private static byte[] Sheet(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> pixel)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                var i = ((y * width) + x) * 4;
                bytes[i] = r;
                bytes[i + 1] = g;
                bytes[i + 2] = b;
                bytes[i + 3] = a;
            }
        }

        return bytes;
    }
}
