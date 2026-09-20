using System;
using System.IO;
using System.Text.Json;
using Frog.Application.Assets;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class PublishedCatalogTilesetsTests
{
    [Fact]
    public void PublishedCatalogWire_TilesetsAdditive_OldPayloadStillDeserializes()
    {
        const string withoutTilesets =
            "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[],\"recipes\":[]}";
        var old = JsonSerializer.Deserialize<PublishedCatalogWire>(withoutTilesets);
        Assert.NotNull(old);
        Assert.Empty(old!.Tilesets);
        Assert.Empty(old.Recipes);

        const string tilesetId = "bbbbbbbb-0004-4000-8000-000000000002";
        var withTilesets =
            "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[],\"recipes\":[],\"tilesets\":[{\"id\":\""
            + tilesetId
            + "\",\"name\":\"Grass\",\"paletteId\":3,\"logicalPath\":\"tiles/grass.png\",\"sha256Hex\":\""
            + new string('a', 64)
            + "\",\"tileSizePixels\":32,\"widthPixels\":32,\"heightPixels\":32}]}";
        var parsed = JsonSerializer.Deserialize<PublishedCatalogWire>(withTilesets);
        Assert.NotNull(parsed);
        var entry = Assert.Single(parsed!.Tilesets);
        Assert.Equal(tilesetId, entry.Id);
        Assert.Equal("Grass", entry.Name);
        Assert.Equal(3, entry.PaletteId);
        Assert.Equal("tiles/grass.png", entry.LogicalPath);
        Assert.Null(entry.PngBase64);
    }

    [Fact]
    public void ProjectAssetTilesetImageSource_ReadsMatchingSha()
    {
        var root = Path.Combine(Path.GetTempPath(), $"frog-tileset-img-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "tiles"));
        var pngPath = Path.Combine(root, "tiles", "grass.png");
        File.WriteAllBytes(pngPath, SamplePng.Solid32Coral);
        try
        {
            var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
            var source = new ProjectAssetTilesetImageSource(root);
            var def = new TilesetDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Grass",
                LogicalPath = "tiles/grass.png",
                TileSizePixels = 32,
                WidthPixels = 32,
                HeightPixels = 32,
                Sha256Hex = sha,
                EditorPaletteId = 1,
            };
            Assert.True(source.TryReadPng(def, out var bytes));
            Assert.Equal(SamplePng.Solid32Coral, bytes);

            def.Sha256Hex = new string('0', 64);
            Assert.False(source.TryReadPng(def, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
