using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class PublishedPrefabClientCoverageTests
{
    [Fact]
    public void Missing_Fails_WhenCatalogHasNoPng()
    {
        var map = MapNamed("Maison");
        var catalog = new PublishedCatalogWire
        {
            Prefabs =
            [
                new PublishedPrefabWireEntry
                {
                    Id = "sofa",
                    DisplayName = "Canapé",
                    Variants =
                    [
                        new PublishedPrefabVariantWire { Facing = "south", SpriteFileName = "sofa-south.png" },
                    ],
                },
            ],
            PrefabMaps =
            [
                new PublishedPrefabMapWireEntry
                {
                    MapName = "Maison",
                    Placements =
                    [
                        new PublishedPrefabPlacementWire { PrefabId = "sofa", Facing = "south", TileX = 0, TileY = 0 },
                    ],
                },
            ],
        };

        var missing = PublishedPrefabClientCoverage.Missing(map, catalog);
        var issue = Assert.Single(missing);
        Assert.Equal("sofa", issue.PrefabId);
        Assert.Contains("image manquante", issue.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_Fails_WhenPlacementPrefabUnknown()
    {
        var map = MapNamed("Maison");
        var catalog = new PublishedCatalogWire
        {
            Prefabs = [],
            PrefabMaps =
            [
                new PublishedPrefabMapWireEntry
                {
                    MapName = "Maison",
                    Placements =
                    [
                        new PublishedPrefabPlacementWire { PrefabId = "ghost-sofa", Facing = "south", TileX = 0, TileY = 0 },
                    ],
                },
            ],
        };

        var missing = PublishedPrefabClientCoverage.Missing(map, catalog);
        Assert.Contains(missing, m => m.PrefabId == "ghost-sofa");
    }

    [Fact]
    public void Missing_Empty_WhenCatalogPngMatchesPlacement()
    {
        var map = MapNamed("Maison");
        var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
        var catalog = CatalogWithSofa(map.Name, sha, Convert.ToBase64String(SamplePng.Solid32Coral));
        Assert.Empty(PublishedPrefabClientCoverage.Missing(map, catalog));
    }

    [Fact]
    public void Materialize_ThenSidecarLayout_CoversMapPrefab()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-prefab-cov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var map = MapNamed("Maison");
            var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
            var catalog = CatalogWithSofa(map.Name, sha, Convert.ToBase64String(SamplePng.Solid32Coral));

            var catalogNoPng = new PublishedCatalogWire
            {
                Prefabs =
                [
                    new PublishedPrefabWireEntry
                    {
                        Id = "sofa",
                        DisplayName = "Canapé",
                        Variants =
                        [
                            new PublishedPrefabVariantWire
                            {
                                Facing = "south",
                                SpriteFileName = "sofa-south.png",
                                Sha256Hex = sha,
                            },
                        ],
                    },
                ],
                PrefabMaps = catalog.PrefabMaps,
            };

            Assert.NotEmpty(PublishedPrefabClientCoverage.Missing(map, catalogNoPng));
            Assert.True(PublishedPrefabCatalogMaterializer.Materialize(catalog, dir) > 0);
            Assert.True(File.Exists(Path.Combine(dir, "Prefabs", "sofa-south.png")));
            Assert.True(File.Exists(Path.Combine(dir, "Maps", "Maison.prefabs.json")));
            Assert.Empty(PublishedPrefabClientCoverage.Missing(map, catalog: null, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task MapPublish_ThenCatalog_CoversPlacedPrefab()
    {
        var repo = new InMemoryMapRepository();
        var map = MapNamed("Salon");
        var document = MapPrefabPersistDocument.Create(
            BuiltInPrefabCatalog.Create(),
            [new PrefabPlacement { PrefabId = BuiltInPrefabCatalog.ChestId, Facing = PrefabFacing.South, TileX = 2, TileY = 2 }],
            [new PrefabSpriteFile("chest.png", SamplePng.Solid32Coral)]);

        var saved = await repo.SaveAsync(new SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = SaveMapIntent.Publish,
            Prefabs = document,
        });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        var published = await repo.LoadPublishedByIdAsync(success.MapId);
        Assert.NotNull(published);
        Assert.NotNull(published!.Prefabs);
        var placed = Assert.Single(published.Prefabs!.Placements);
        Assert.Equal(BuiltInPrefabCatalog.ChestId, placed.PrefabId);
        var sprite = Assert.Single(published.Prefabs.ToSpriteFiles());
        Assert.Equal("chest.png", sprite.FileName);

        var wire = new PublishedCatalogWire
        {
            Prefabs = PublishedPrefabClientCoverage.ToWirePrefabs(published.Prefabs.Catalog, published.Prefabs.ToSpriteFiles()),
            PrefabMaps = [PublishedPrefabClientCoverage.ToWireMap(success.MapId, map.Name, published.Prefabs.Placements)],
        };
        Assert.Empty(PublishedPrefabClientCoverage.Missing(map, wire));
    }

    [Fact]
    public void PlaytestSidecarCatalog_RoundTrip_CoversMap()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-playtest-prefabs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, SidecarPublishedPrefabCatalog.FileName);
            var document = MapPrefabPersistDocument.Create(
                BuiltInPrefabCatalog.Create(),
                [new PrefabPlacement { PrefabId = BuiltInPrefabCatalog.PlantId, Facing = PrefabFacing.South, TileX = 0, TileY = 1 }],
                [new PrefabSpriteFile("plant.png", SamplePng.Solid32Coral)]);
            SidecarPublishedPrefabCatalog.WriteFromDocument(path, Guid.NewGuid(), "Jardin", document);
            var sidecar = new SidecarPublishedPrefabCatalog(path);
            Assert.True(sidecar.TryRead(out var wire));
            Assert.Empty(PublishedPrefabClientCoverage.Missing(MapNamed("Jardin"), wire));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static PublishedCatalogWire CatalogWithSofa(string mapName, string sha, string pngBase64)
        => new()
        {
            Prefabs =
            [
                new PublishedPrefabWireEntry
                {
                    Id = "sofa",
                    DisplayName = "Canapé",
                    FootprintWidthTiles = 2,
                    FootprintHeightTiles = 1,
                    Variants =
                    [
                        new PublishedPrefabVariantWire
                        {
                            Facing = "south",
                            SpriteFileName = "sofa-south.png",
                            FootprintWidthTiles = 2,
                            FootprintHeightTiles = 1,
                            Sha256Hex = sha,
                            PngBase64 = pngBase64,
                        },
                    ],
                },
            ],
            PrefabMaps =
            [
                new PublishedPrefabMapWireEntry
                {
                    MapName = mapName,
                    Placements =
                    [
                        new PublishedPrefabPlacementWire { PrefabId = "sofa", Facing = "south", TileX = 0, TileY = 0 },
                    ],
                },
            ],
        };

    private static Map MapNamed(string name)
    {
        var map = new Map { Width = 8, Height = 8, Name = name };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground, Visible = true });
        return map;
    }
}
