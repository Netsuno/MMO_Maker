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
    public void CreateMerged_KeepsPersistedCatalogAndPng_WhenLocalCacheLacksThem()
    {
        var previous = MapPrefabPersistDocument.Create(
            new PrefabCatalog
            {
                Prefabs =
                {
                    new PrefabDefinition
                    {
                        Id = "custom-lamp",
                        DisplayName = "Lampe",
                        FootprintWidthTiles = 1,
                        FootprintHeightTiles = 1,
                        Variants =
                        {
                            new PrefabFacingVariant { Facing = PrefabFacing.South, SpriteFileName = "custom-lamp.png" },
                        },
                    },
                },
            },
            [new PrefabPlacement { PrefabId = "custom-lamp", Facing = PrefabFacing.South, TileX = 1, TileY = 1 }],
            [new PrefabSpriteFile("custom-lamp.png", SamplePng.Solid32Coral)]);

        var localOnly = MapPrefabPersistDocument.Create(
            BuiltInPrefabCatalog.Create(),
            previous.Placements,
            sprites: []);
        Assert.DoesNotContain(localOnly.Catalog.Prefabs, p => p.Id == "custom-lamp");
        Assert.Empty(localOnly.Sprites);

        var merged = MapPrefabPersistDocument.CreateMerged(
            BuiltInPrefabCatalog.Create(),
            previous.Placements,
            localSprites: [],
            previous);
        Assert.Contains(merged.Catalog.Prefabs, p => p.Id == "custom-lamp");
        var sprite = Assert.Single(merged.ToSpriteFiles());
        Assert.Equal("custom-lamp.png", sprite.FileName);
        Assert.Equal(SamplePng.Solid32Coral, sprite.PngBytes);
    }

    [Fact]
    public void PrefabEditorRestore_PrefersUnsavedWorkstateOverPersistedPackage()
    {
        Assert.Equal(
            PrefabEditorRestore.Source.UnsavedWorkstate,
            PrefabEditorRestore.Choose(workspaceDirty: true, workstatePresent: true, persistedPackagePresent: true));
        Assert.Equal(
            PrefabEditorRestore.Source.PersistedPackage,
            PrefabEditorRestore.Choose(workspaceDirty: false, workstatePresent: true, persistedPackagePresent: true));
        Assert.Equal(
            PrefabEditorRestore.Source.DiskWorkstate,
            PrefabEditorRestore.Choose(workspaceDirty: false, workstatePresent: true, persistedPackagePresent: false));
        Assert.Equal(
            PrefabEditorRestore.Source.Empty,
            PrefabEditorRestore.Choose(workspaceDirty: true, workstatePresent: false, persistedPackagePresent: false));
    }

    [Fact]
    public void Materialize_DuplicateMapNames_UsesMapIdSidecars()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-prefab-dup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var idA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var idB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var sha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
            var png = Convert.ToBase64String(SamplePng.Solid32Coral);
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
                            new PublishedPrefabVariantWire
                            {
                                Facing = "south",
                                SpriteFileName = "sofa-south.png",
                                Sha256Hex = sha,
                                PngBase64 = png,
                            },
                        ],
                    },
                    new PublishedPrefabWireEntry
                    {
                        Id = "chest",
                        DisplayName = "Coffre",
                        Variants =
                        [
                            new PublishedPrefabVariantWire
                            {
                                Facing = "south",
                                SpriteFileName = "chest.png",
                                Sha256Hex = sha,
                            },
                        ],
                    },
                ],
                PrefabMaps =
                [
                    new PublishedPrefabMapWireEntry
                    {
                        MapId = idA.ToString("D"),
                        MapName = "Maison",
                        RuntimeMapId = 7,
                        Placements =
                        [
                            new PublishedPrefabPlacementWire { PrefabId = "sofa", Facing = "south", TileX = 0, TileY = 0 },
                        ],
                    },
                    new PublishedPrefabMapWireEntry
                    {
                        MapId = idB.ToString("D"),
                        MapName = "Maison",
                        RuntimeMapId = 8,
                        Placements =
                        [
                            new PublishedPrefabPlacementWire { PrefabId = "chest", Facing = "south", TileX = 1, TileY = 1 },
                        ],
                    },
                ],
            };

            Assert.True(PublishedPrefabCatalogMaterializer.Materialize(catalog, dir) > 0);
            Assert.True(File.Exists(Path.Combine(dir, "Maps", MapPrefabPackage.PlacementSidecarFileName("Maison", idA))));
            Assert.True(File.Exists(Path.Combine(dir, "Maps", MapPrefabPackage.PlacementSidecarFileName("Maison", idB))));
            Assert.False(File.Exists(Path.Combine(dir, "Maps", "Maison.prefabs.json")));

            var map = MapNamed("Maison");
            Assert.Empty(PublishedPrefabClientCoverage.Missing(map, catalog, mapId: idA));
            var missingB = PublishedPrefabClientCoverage.Missing(map, catalog, mapId: idB);
            Assert.Contains(missingB, m => m.PrefabId == "chest");

            Assert.True(PublishedPrefabClientCoverage.TryMatchPrefabMap(catalog, "Maison", out var matchedA, mapId: idA));
            Assert.Equal("sofa", Assert.Single(matchedA.Placements).PrefabId);
            Assert.True(PublishedPrefabClientCoverage.TryMatchPrefabMap(catalog, "Maison", out var matchedB, runtimeMapId: 8));
            Assert.Equal("chest", Assert.Single(matchedB.Placements).PrefabId);
            Assert.False(PublishedPrefabClientCoverage.TryMatchPrefabMap(catalog, "Maison", out _));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
