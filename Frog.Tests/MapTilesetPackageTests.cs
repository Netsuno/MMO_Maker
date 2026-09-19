using System;
using System.IO;
using Frog.Application.Assets;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapTilesetPackageTests
{
    [Fact]
    public void CollectUsedTilesetIds_IgnoresZero()
    {
        var map = new Map { Width = 2, Height = 1, Name = "Pack" };
        var layer = new Layer { LayerType = LayerType.Ground };
        layer.Tiles.Add(new Tile { X = 0, Y = 0, TilesetId = 4, Type = TileType.Ground });
        layer.Tiles.Add(new Tile { X = 1, Y = 0, TilesetId = 0, Type = TileType.Ground });
        map.Layers.Add(layer);

        var ids = MapTilesetPackage.CollectUsedTilesetIds(map);
        Assert.Equal(new[] { 4 }, ids);
    }

    [Fact]
    public void WriteSidecars_WritesClientLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-tileset-pkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            MapTilesetPackage.WriteSidecars(
                dir,
                ["Starter Meadow"],
                [new MapTilesetFile(7, SamplePng.Solid32Coral)]);

            Assert.True(File.Exists(Path.Combine(dir, "Tilesets", "7.png")));
            Assert.True(File.Exists(Path.Combine(dir, "Tilesets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(dir, "Maps", "Starter Meadow.tilesets.json")));

            var man = TilesetManifestJson.TryDeserializeFromFile(Path.Combine(dir, "Tilesets", "manifest.json"));
            Assert.NotNull(man);
            var entry = Assert.Single(man!.Entries);
            Assert.Equal(7, entry.Id);
            Assert.Equal("7.png", entry.FileName);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PngImageHeader_ReadsSample()
    {
        Assert.True(PngImageHeader.TryRead(SamplePng.Solid32Coral, out var w, out var h));
        Assert.Equal(32, w);
        Assert.Equal(32, h);
        Assert.False(PngImageHeader.TryRead([0x00, 0x01], out _, out _));
    }
}
