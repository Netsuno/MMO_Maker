using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class PublishedTilesetClientCoverageTests
{
    [Fact]
    public void MissingTilesetIds_Fails_WhenCatalogHasNoPng()
    {
        var map = MapWithTileset(7);
        var catalog = new PublishedCatalogWire
        {
            Tilesets =
            [
                new PublishedTilesetWireEntry
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = "Ghost",
                    PaletteId = 7,
                    LogicalPath = "tiles/ghost.png",
                    Sha256Hex = new string('a', 64),
                },
            ],
        };

        var missing = PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog);
        Assert.Equal(new[] { 7 }, missing);
    }

    [Fact]
    public void MissingTilesetIds_Fails_WhenPaletteDoesNotMatchMap()
    {
        var map = MapWithTileset(3);
        var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
        var catalog = new PublishedCatalogWire
        {
            Tilesets =
            [
                new PublishedTilesetWireEntry
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = "Coral",
                    PaletteId = 9,
                    Sha256Hex = sha,
                    PngBase64 = Convert.ToBase64String(SamplePng.Solid32Coral),
                },
            ],
        };

        Assert.Equal(new[] { 3 }, PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog));
    }

    [Fact]
    public void MissingTilesetIds_Empty_WhenCatalogPngMatchesMapId()
    {
        var map = MapWithTileset(4);
        var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
        var catalog = new PublishedCatalogWire
        {
            Tilesets =
            [
                new PublishedTilesetWireEntry
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = "Coral",
                    PaletteId = 4,
                    Sha256Hex = sha,
                    PngBase64 = Convert.ToBase64String(SamplePng.Solid32Coral),
                },
            ],
        };

        Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog));
    }

    [Fact]
    public void Materialize_ThenSidecarLayout_CoversMapTileset()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-tileset-cov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var map = MapWithTileset(12);
            var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
            var catalog = new PublishedCatalogWire
            {
                Tilesets =
                [
                    new PublishedTilesetWireEntry
                    {
                        Id = Guid.NewGuid().ToString("D"),
                        Name = "Coral",
                        PaletteId = 12,
                        Sha256Hex = sha,
                        PngBase64 = Convert.ToBase64String(SamplePng.Solid32Coral),
                    },
                ],
            };

            Assert.Equal(new[] { 12 }, PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog: null));
            Assert.Equal(1, PublishedTilesetCatalogMaterializer.Materialize(catalog, dir));
            Assert.True(File.Exists(Path.Combine(dir, "Tilesets", "12.png")));
            Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(map, catalog, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task MapPublishSync_ThenCatalog_IncludesPngForMapTilesetId()
    {
        var repo = new InMemoryTilesetRepository();
        var map = MapWithTileset(5);
        var files = new[] { new MapTilesetFile(5, SamplePng.Solid32Coral) };

        var publishedIds = await MapPublishedTilesetSync.PublishUsedAsync(repo, map, files);
        Assert.Single(publishedIds);

        var listed = await repo.ListPublishedAsync();
        var def = Assert.Single(listed);
        Assert.Equal(5, def.EditorPaletteId);
        Assert.NotNull(def.PngBytes);
        Assert.Equal(SamplePng.Solid32Coral, def.PngBytes);

        Assert.True(EmbeddedPublishedTilesetImageSource.Instance.TryReadPng(def, out var png));
        var wire = new PublishedCatalogWire
        {
            Tilesets =
            [
                new PublishedTilesetWireEntry
                {
                    Id = def.Id.ToString("D"),
                    Name = def.Name,
                    PaletteId = def.EditorPaletteId ?? 0,
                    LogicalPath = def.LogicalPath,
                    Sha256Hex = def.Sha256Hex,
                    PngBase64 = Convert.ToBase64String(png),
                },
            ],
        };
        Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(map, wire));
    }

    [Fact]
    public async Task Hydrator_RebuildsCacheFiles_FromPublishedGuidAndPalette()
    {
        var repo = new InMemoryTilesetRepository();
        var def = new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Hydra",
            LogicalPath = "tiles/hydra.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral),
            EditorPaletteId = 8,
            PngBytes = SamplePng.Solid32Coral,
        };
        await repo.SaveAsync(new SaveTilesetRequest
        {
            Definition = def,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        var map = MapWithTileset(8);
        var files = PublishedTilesetCacheHydrator.CollectPngFiles(
            map,
            await repo.ListPublishedAsync(),
            EmbeddedPublishedTilesetImageSource.Instance);
        var file = Assert.Single(files);
        Assert.Equal(8, file.Id);
        Assert.Equal(SamplePng.Solid32Coral, file.PngBytes);
    }

    [Fact]
    public void PlaytestSidecarCatalog_RoundTrip_CoversMap()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-playtest-tiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, SidecarPublishedTilesetCatalog.FileName);
            SidecarPublishedTilesetCatalog.WriteFromFiles(
                path,
                [new MapTilesetFile(2, SamplePng.Solid32Coral)]);
            var sidecar = new SidecarPublishedTilesetCatalog(path);
            Assert.True(sidecar.TryRead(out var wire));
            Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(MapWithTileset(2), wire));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Alignment_UsesEditorPalette_ThenSha()
    {
        var def = new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "A",
            LogicalPath = "tiles/a.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral),
            EditorPaletteId = 15,
        };
        Assert.Equal(15, TilesetPaletteAlignment.ResolveClientTilesetId(def, [1, 15]));

        def.EditorPaletteId = null;
        var shaMap = TilesetPaletteAlignment.BuildShaToPalette([new MapTilesetFile(6, SamplePng.Solid32Coral)]);
        Assert.Equal(6, TilesetPaletteAlignment.ResolveClientTilesetId(def, [6], shaMap));
    }

    private static Map MapWithTileset(int tilesetId)
    {
        var map = new Map { Width = 2, Height = 1, Name = "Coverage" };
        var layer = new Layer { LayerType = LayerType.Ground, Visible = true };
        layer.Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            TilesetId = tilesetId,
            SrcX = 0,
            SrcY = 0,
            Type = TileType.Ground,
        });
        map.Layers.Add(layer);
        return map;
    }
}
