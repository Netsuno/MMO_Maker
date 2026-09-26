using System;
using System.IO;
using Frog.Application.Maps;
using Xunit;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Tests;

public sealed class MapRegionTests
{
    [Fact]
    public void Paint_RoundTripsSidecar_WithoutChangingFmapV6OrProtocol()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);

        var map = MapFormat.CreateTileAssetMap("Clairière", 4, 3);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var before = MapFormat.Write(map);

        Assert.True(MapRegionEdit.TryPaint(map, 1, 0, 7, out var error), error);
        Assert.True(MapRegionEdit.TryPaint(map, 2, 1, 63, out error), error);
        Assert.False(MapRegionEdit.TryPaint(map, 1, 0, 7, out error));
        Assert.Null(error);
        Assert.False(MapRegionEdit.TryPaint(map, 1, 0, 64, out error));
        Assert.Contains("0 et 63", error, StringComparison.Ordinal);
        Assert.False(MapRegionEdit.TryPaint(map, 9, 0, 1, out error));
        Assert.Contains("hors", error, StringComparison.Ordinal);
        Assert.Equal("7", MapRegionEdit.OverlayText(7));
        Assert.Equal(string.Empty, MapRegionEdit.OverlayText(0));

        var monster = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        var document = map.Regions!;
        Assert.True(document.TrySetEncounterSteps(24, out error), error);
        Assert.True(document.TryAddEncounter(new MapEncounterDraft
        {
            MonsterId = monster,
            AliasId = 4,
            Label = "Slime",
            Weight = 10,
            RegionsText = "7, 63",
        }, out error), error);
        Assert.True(document.TryAddEncounter(new MapEncounterDraft
        {
            Label = "Patrouille",
            Weight = 3,
            RegionsText = string.Empty,
        }, out error), error);
        Assert.False(document.TryAddEncounter(new MapEncounterDraft { Weight = 1 }, out error));
        Assert.Contains("monstre", error, StringComparison.OrdinalIgnoreCase);

        var onBrush = document.EncountersAt(1, 0);
        Assert.Equal(2, onBrush.Count);
        Assert.Contains(onBrush, entry => entry.Label == "Slime");
        Assert.Contains(onBrush, entry => entry.Label == "Patrouille");
        var plain = document.EncountersAt(0, 0);
        Assert.Single(plain);
        Assert.Equal("Patrouille", plain[0].Label);
        Assert.Equal("Slime · poids 10 · régions 7, 63", MapRegionEdit.FormatEncounter(document.Encounters[0]));
        Assert.Contains(MapRegionLabels.WholeMap, MapRegionEdit.FormatEncounter(document.Encounters[1]), StringComparison.Ordinal);

        var after = MapFormat.Write(map);
        Assert.Equal(before, after);
        Assert.Equal((byte)6, after[4]);
        Assert.Equal(48, map.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.TileAsset, map.GraphicIdentity);
        Assert.Null(MapFormat.Read(after).Regions);

        var dir = Path.Combine(Path.GetTempPath(), "frog-regions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "clairiere.fmap");
            File.WriteAllBytes(path, after);
            MapRegionDocument.WriteForMap(path, map);
            var sidecar = MapRegionDocument.SidecarPath(path);
            Assert.Equal(Path.Combine(dir, "clairiere.regions.json"), sidecar);
            var json = File.ReadAllText(sidecar);
            Assert.Contains("\"region\": 63", json, StringComparison.Ordinal);
            Assert.DoesNotContain("fmap", json, StringComparison.OrdinalIgnoreCase);

            var loadedMap = MapFormat.Read(File.ReadAllBytes(path));
            Assert.True(MapRegionDocument.TryAttach(loadedMap, path, out var attachError), attachError);
            Assert.Equal(48, loadedMap.TileSizePixels);
            Assert.Equal(7, loadedMap.Regions!.Get(1, 0));
            Assert.Equal(63, loadedMap.Regions.Get(2, 1));
            Assert.Equal(0, loadedMap.Regions.Get(0, 0));
            Assert.Equal(24, loadedMap.Regions.EncounterSteps);
            Assert.Equal(monster, loadedMap.Regions.Encounters[0].MonsterId);
            Assert.Equal(4, loadedMap.Regions.Encounters[0].AliasId);
            Assert.Equal(new byte[] { 7, 63 }, loadedMap.Regions.Encounters[0].Regions);
            Assert.Empty(loadedMap.Regions.Encounters[1].Regions);

            Assert.True(MapRegionEdit.TryPaint(loadedMap, 1, 0, 0, out error), error);
            Assert.True(MapRegionEdit.TryPaint(loadedMap, 2, 1, 0, out error), error);
            loadedMap.Regions.TryRemoveEncounter(0, out _);
            loadedMap.Regions.TryRemoveEncounter(0, out _);
            Assert.True(loadedMap.Regions.TrySetEncounterSteps(MapRegionDocument.DefaultEncounterSteps, out error), error);
            Assert.True(loadedMap.Regions.IsEmpty);
            MapRegionDocument.WriteForMap(path, loadedMap);
            Assert.False(File.Exists(sidecar));
            var again = MapFormat.Read(after);
            Assert.True(MapRegionDocument.TryAttach(again, path, out attachError), attachError);
            Assert.Null(again.Regions);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Resize_ClipsAndShiftsRegions_SheetMapStaysV5()
    {
        var sheet = new Map { Name = "Feuille", Width = 4, Height = 3 };
        sheet.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var v5 = new MapSerializer().Serialize(sheet);
        Assert.True(MapRegionEdit.TryPaint(sheet, 3, 2, 2, out var error), error);
        Assert.True(MapRegionEdit.TryPaint(sheet, 0, 0, 1, out error), error);
        Assert.Equal(v5, new MapSerializer().Serialize(sheet));
        Assert.Equal((byte)5, v5[4]);

        Assert.True(MapEditOperations.TryApplyProperties(
            sheet,
            new MapPropertiesEdit { Name = "Feuille", Width = 3, Height = 2, AllowPlayerOverlap = false },
            out error), error);
        Assert.Equal(1, sheet.Regions!.Get(0, 0));
        Assert.Equal(0, sheet.Regions.Get(3, 2));
        Assert.Equal(3, sheet.Regions.Width);
        Assert.Equal(2, sheet.Regions.Height);
        Assert.Equal(0, sheet.TileSizePixels);

        var shifted = MapFormat.CreateTileAssetMap("Décalée", 3, 2);
        shifted.Layers.Add(new Layer { LayerType = LayerType.Ground });
        Assert.True(MapRegionEdit.TryPaint(shifted, 0, 0, 5, out error), error);
        Assert.True(MapResizeShift.TryApply(
            shifted,
            new MapResizeShiftEdit { Width = 4, Height = 3, DeltaX = 1, DeltaY = 1 },
            null,
            null,
            null,
            null,
            out _,
            out error), error);
        Assert.Equal(5, shifted.Regions!.Get(1, 1));
        Assert.Equal(0, shifted.Regions.Get(0, 0));
        Assert.Equal(48, shifted.TileSizePixels);
        Assert.Equal((byte)6, MapFormat.Write(shifted)[4]);
    }

    [Fact]
    public void CorruptSidecar_DoesNotReplaceRegions()
    {
        var map = MapFormat.CreateTileAssetMap("Atelier", 2, 2);
        var dir = Path.Combine(Path.GetTempPath(), "frog-regions-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "atelier.fmap");
            File.WriteAllText(MapRegionDocument.SidecarPath(path), "{ \"formatVersion\": 9 }");
            Assert.False(MapRegionDocument.TryAttach(map, path, out var error));
            Assert.Contains("version", error, StringComparison.OrdinalIgnoreCase);
            Assert.Null(map.Regions);
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
