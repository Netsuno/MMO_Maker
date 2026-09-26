using System;
using System.IO;
using System.Reflection;
using Frog.Application.Assets;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class PrefabModelAndPlacementTests
{
    [Fact]
    public void BuiltInCatalog_Validates_AndRoundtripsJson()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        Assert.True(PrefabPlacementService.TryValidateCatalog(catalog, out var error), error);
        Assert.Equal(8, catalog.Prefabs.Count);

        var bytes = PrefabCatalogJson.Serialize(catalog);
        var back = PrefabCatalogJson.TryDeserialize(bytes);
        Assert.NotNull(back);
        Assert.True(PrefabPlacementService.TryValidateCatalog(back!, out var error2), error2);
        Assert.Equal(BuiltInPrefabCatalog.SofaId, back!.Prefabs[0].Id);
        Assert.Equal(PrefabFacing.East, back.Prefabs[0].Variants[2].Facing);
    }

    [Fact]
    public void Definition_RejectsTraversalSpriteAndBadId()
    {
        var badId = new PrefabDefinition
        {
            Id = "Sofa!",
            DisplayName = "X",
            Variants = { new PrefabFacingVariant { SpriteFileName = "sofa-south.png", FootprintWidthTiles = 1, FootprintHeightTiles = 1 } },
        };
        Assert.False(PrefabPlacementService.TryValidateDefinition(badId, out _));

        var traversal = new PrefabDefinition
        {
            Id = "sofa",
            DisplayName = "X",
            Variants = { new PrefabFacingVariant { SpriteFileName = "../evil.png", FootprintWidthTiles = 1, FootprintHeightTiles = 1 } },
        };
        Assert.False(PrefabPlacementService.TryValidateDefinition(traversal, out _));
    }

    [Fact]
    public void Footprint_UsesVariantOverride_OrPixels()
    {
        var sofa = BuiltInPrefabCatalog.Create().Prefabs[0];
        Assert.True(PrefabPlacementService.TryResolveVariant(sofa, PrefabFacing.East, out var east));
        Assert.True(PrefabPlacementService.TryResolveFootprint(sofa, east, out var w, out var h));
        Assert.Equal(1, w);
        Assert.Equal(2, h);

        var pxOnly = new PrefabDefinition
        {
            Id = "banner",
            DisplayName = "Bannière",
            WidthPixels = 64,
            HeightPixels = 32,
            Variants = { new PrefabFacingVariant { Facing = PrefabFacing.South, SpriteFileName = "banner.png" } },
        };
        Assert.True(PrefabPlacementService.TryResolveFootprint(pxOnly, pxOnly.Variants[0], out var pw, out var ph));
        Assert.Equal(2, pw);
        Assert.Equal(1, ph);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    [Fact]
    public void Place_Fits_ReplacesOverlap_EraseAt()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        var list = new System.Collections.Generic.List<PrefabPlacement>();

        Assert.False(PrefabPlacementService.TryPlace(list, catalog, "sofa", PrefabFacing.South, 7, 0, mapWidth: 8, mapHeight: 8, out _, out var tooWide));
        Assert.Equal("Empreinte hors carte.", tooWide);

        Assert.True(PrefabPlacementService.TryPlace(list, catalog, "sofa", PrefabFacing.South, 2, 3, 8, 8, out var first, out var err), err);
        Assert.NotNull(first);
        Assert.Single(list);

        Assert.True(PrefabPlacementService.TryPlace(list, catalog, "fence-post", PrefabFacing.South, 3, 3, 8, 8, out _, out var err2), err2);
        Assert.Single(list);
        Assert.Equal(BuiltInPrefabCatalog.FencePostId, list[0].PrefabId);

        Assert.Equal(1, PrefabPlacementService.EraseAt(list, catalog, 3, 3));
        Assert.Empty(list);
    }

    [Fact]
    public void FindAt_AndMove_UpdatesInstanceWithoutPainting()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        var list = new System.Collections.Generic.List<PrefabPlacement>();
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.ChairId, PrefabFacing.East, 1, 1, 8, 8, out var placed, out var err), err);
        Assert.NotNull(placed);
        Assert.Same(placed, PrefabPlacementService.TryFindAt(list, catalog, 1, 1));

        Assert.True(PrefabPlacementService.TryMove(list, catalog, placed!, 4, 5, 8, 8, out var moveErr), moveErr);
        Assert.Equal(4, placed!.TileX);
        Assert.Equal(5, placed.TileY);
        Assert.Same(placed, PrefabPlacementService.TryFindAt(list, catalog, 4, 5));
        Assert.Null(PrefabPlacementService.TryFindAt(list, catalog, 1, 1));
    }

    [Fact]
    public void DuplicateLast_OffsetsByFootprint_SkipsBlockedAndOffMap()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        var list = new System.Collections.Generic.List<PrefabPlacement>();

        Assert.False(PrefabPlacementService.TryDuplicateLast(list, catalog, 8, 8, out _, out var empty));
        Assert.Equal("Aucun objet posé à dupliquer.", empty);

        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.SofaId, PrefabFacing.South, 1, 1, 8, 8, out _, out var err), err);
        Assert.True(PrefabPlacementService.TryDuplicateLast(list, catalog, 8, 8, out var copy, out var dupErr), dupErr);
        Assert.Equal(2, list.Count);
        Assert.NotNull(copy);
        Assert.Equal(BuiltInPrefabCatalog.SofaId, copy!.PrefabId);
        Assert.Equal(PrefabFacing.South, copy.Facing);
        Assert.Equal(3, copy.TileX);
        Assert.Equal(1, copy.TileY);
        Assert.Equal(1, list[0].TileX);
        Assert.Equal(1, list[0].TileY);

        list.Clear();
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.ChestId, PrefabFacing.South, 3, 1, 8, 8, out _, out var chestErr), chestErr);
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.SofaId, PrefabFacing.South, 1, 1, 8, 8, out _, out var again), again);
        Assert.True(PrefabPlacementService.TryDuplicateLast(list, catalog, 8, 8, out var below, out var belowErr), belowErr);
        Assert.Equal(BuiltInPrefabCatalog.SofaId, below!.PrefabId);
        Assert.Equal(1, below.TileX);
        Assert.Equal(2, below.TileY);
        Assert.Equal(3, list.Count);
        Assert.Equal(3, list[0].TileX);
        Assert.Equal(1, list[1].TileX);

        list.Clear();
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.SofaId, PrefabFacing.South, 6, 2, 8, 8, out _, out var edgeErr), edgeErr);
        Assert.True(PrefabPlacementService.TryDuplicateLast(list, catalog, 8, 8, out var dropped, out var dropErr), dropErr);
        Assert.Equal(6, dropped!.TileX);
        Assert.Equal(3, dropped.TileY);
    }

    [Fact]
    public void FacingFallback_UsesFirstVariant()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        Assert.True(PrefabPlacementService.TryGetDefinition(catalog, BuiltInPrefabCatalog.FencePostId, out var post));
        Assert.True(PrefabPlacementService.TryResolveVariant(post, PrefabFacing.East, out var variant));
        Assert.Equal(PrefabFacing.South, variant.Facing);
        Assert.Equal("fence-post.png", variant.SpriteFileName);
    }

    [Fact]
    public void PlacementDocument_Roundtrip()
    {
        var doc = new PrefabPlacementDocument
        {
            DocumentVersion = 1,
            Placements =
            {
                new PrefabPlacement { PrefabId = "sofa", Facing = PrefabFacing.West, TileX = 4, TileY = 1 },
            },
        };
        var back = PrefabPlacementDocumentJson.TryDeserialize(PrefabPlacementDocumentJson.Serialize(doc));
        Assert.NotNull(back);
        var item = Assert.Single(back!.Placements);
        Assert.Equal("sofa", item.PrefabId);
        Assert.Equal(PrefabFacing.West, item.Facing);
        Assert.Equal(4, item.TileX);
        Assert.Equal(1, item.TileY);
    }

    [Fact]
    public void MapPrefabPackage_WritesClientLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"frog-prefab-pkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var catalog = BuiltInPrefabCatalog.Create();
            MapPrefabPackage.WriteSidecars(
                dir,
                ["Starter Meadow"],
                catalog,
                [new PrefabPlacement { PrefabId = "sofa", Facing = PrefabFacing.South, TileX = 1, TileY = 2 }],
                [new PrefabSpriteFile("sofa-south.png", SamplePng.Solid32Coral)]);

            Assert.True(File.Exists(Path.Combine(dir, "Prefabs", "catalog.json")));
            Assert.True(File.Exists(Path.Combine(dir, "Prefabs", "sofa-south.png")));
            Assert.True(File.Exists(Path.Combine(dir, "Maps", "Starter Meadow.prefabs.json")));

            var loaded = PrefabPlacementDocumentJson.TryDeserializeFromFile(
                Path.Combine(dir, "Maps", "Starter Meadow.prefabs.json"));
            Assert.NotNull(loaded);
            var item = Assert.Single(loaded!.Placements);
            Assert.Equal("sofa", item.PrefabId);
            Assert.Equal(1, item.TileX);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ProjectAssetKind_AcceptsPrefabs()
    {
        Assert.True(ProjectAssetKind.IsKnown(ProjectAssetKind.Prefabs));
        Assert.Equal("prefabs", ProjectAssetKind.Prefabs);
    }

    [Fact]
    public void ProtocolAndFmap_Unchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal((byte)6, MapSerializer.TileAssetMapFileFormatVersion);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
    }

    [Fact]
    public void DuplicateSelected_ClonesFields_OffsetsByFootprint_KeepsHelloAndTileSize()
    {
        var catalog = BuiltInPrefabCatalog.Create();
        var list = new System.Collections.Generic.List<PrefabPlacement>();

        Assert.False(PrefabPlacementService.TryDuplicate(list, catalog, source: null, 8, 8, out _, out var none));
        Assert.Equal("Aucun objet sélectionné à dupliquer.", none);

        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.ChestId, PrefabFacing.South, 1, 1, 8, 8, out var chest, out var chestErr), chestErr);
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.SofaId, PrefabFacing.East, 4, 1, 8, 8, out var sofa, out var sofaErr), sofaErr);
        Assert.NotNull(chest);
        Assert.NotNull(sofa);

        var outsider = new PrefabPlacement { PrefabId = BuiltInPrefabCatalog.ChestId, Facing = PrefabFacing.South, TileX = 1, TileY = 1 };
        Assert.False(PrefabPlacementService.TryDuplicate(list, catalog, outsider, 8, 8, out _, out var missing));
        Assert.Equal("Instance introuvable.", missing);

        Assert.True(PrefabPlacementService.TryDuplicate(list, catalog, chest, 8, 8, out var copy, out var dupErr), dupErr);
        Assert.NotNull(copy);
        Assert.Equal(3, list.Count);
        Assert.NotSame(chest, copy);
        Assert.Equal(1, chest!.TileX);
        Assert.Equal(1, chest.TileY);
        Assert.Equal(4, sofa!.TileX);
        Assert.Equal(1, sofa.TileY);
        Assert.Equal(PrefabFacing.East, sofa.Facing);
        Assert.Equal(2, copy!.TileX);
        Assert.Equal(1, copy.TileY);
        AssertSameIdentity(chest, copy);
        var cloned = chest.Clone();
        AssertSameIdentity(chest, cloned);
        Assert.Equal(chest.TileX, cloned.TileX);
        Assert.Equal(chest.TileY, cloned.TileY);
        Assert.NotSame(chest, cloned);

        list.Clear();
        Assert.True(PrefabPlacementService.TryPlace(list, catalog, BuiltInPrefabCatalog.SofaId, PrefabFacing.South, 0, 0, mapWidth: 2, mapHeight: 1, out var blocked, out var blockErr), blockErr);
        Assert.False(PrefabPlacementService.TryDuplicate(list, catalog, blocked, 2, 1, out _, out var noRoom));
        Assert.Equal("Pas de place libre à côté de l’objet sélectionné.", noRoom);
        Assert.Single(list);
        Assert.Equal(0, list[0].TileX);
        Assert.Equal(0, list[0].TileY);

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.True(WireHello.TryParse(WireHello.BuildPayload(), out var hello, out var helloVersion));
        Assert.Equal(WireHello.DefaultMessage, hello);
        Assert.Equal((ushort)11, helloVersion);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((byte)6, MapSerializer.TileAssetMapFileFormatVersion);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
    }

    private static void AssertSameIdentity(PrefabPlacement source, PrefabPlacement copy)
    {
        foreach (var property in typeof(PrefabPlacement).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.Name is nameof(PrefabPlacement.TileX) or nameof(PrefabPlacement.TileY))
            {
                continue;
            }

            Assert.Equal(property.GetValue(source), property.GetValue(copy));
        }
    }
}
