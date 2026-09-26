using System;
using System.IO;
using System.Linq;

using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Tests;

using Xunit;

namespace Frog.Tests;

public sealed class TileAssetFlagsTests
{
    [Fact]
    public void EditorModel_RoundTripsPassagePriorityBushCounterDamage()
    {
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);
        Assert.Equal(1, TileAssetFlagTable.FormatVersion);

        var table = new TileAssetFlagTable();
        var wall = Id(1);
        var grass = Id(2);
        var counter = Id(3);

        var edited = TileAssetFlags.Default
            .WithPassage(TilePassageDirection.North, false)
            .WithPassage(TilePassageDirection.West, false)
            .WithPriority(3)
            .WithBush(true)
            .WithCounter(false)
            .WithDamage(true);
        table.Set(wall, TileAssetFlags.Blocked);
        table.Set(grass, edited);
        table.Set(counter, TileAssetFlags.Default.WithCounter(true).WithPriority(1));
        table.Set(Id(4), TileAssetFlags.Default);

        Assert.Equal(3, table.Count);
        Assert.True(table.Get(wall).BlocksAllPassage);
        Assert.True(table.Get(Id(9)).IsDefault);
        Assert.False(table.Get(grass).Allows(TilePassageDirection.North));
        Assert.True(table.Get(grass).Allows(TilePassageDirection.South));
        Assert.True(table.Get(grass).Allows(TilePassageDirection.East));
        Assert.False(table.Get(grass).Allows(TilePassageDirection.West));
        Assert.Equal(3, table.Get(grass).Priority);
        Assert.True(table.Get(grass).Bush);
        Assert.True(table.Get(grass).Damage);
        Assert.False(table.Get(grass).Counter);
        Assert.False(TileAssetFlags.CanStep(table.Get(grass), TileAssetFlags.Default, TilePassageDirection.North));
        Assert.True(TileAssetFlags.CanStep(table.Get(grass), TileAssetFlags.Default, TilePassageDirection.East));
        Assert.False(TileAssetFlags.CanStep(TileAssetFlags.Default, table.Get(wall), TilePassageDirection.South));

        var json = table.ToJson();
        Assert.DoesNotContain(Id(4).ToHex(), json, StringComparison.Ordinal);
        var loaded = TileAssetFlagTable.FromJson(json);
        Assert.Equal(table.Get(wall), loaded.Get(wall));
        Assert.Equal(table.Get(grass), loaded.Get(grass));
        Assert.Equal(table.Get(counter), loaded.Get(counter));
        Assert.Equal(loaded.ToJson(), TileAssetFlagTable.FromJson(loaded.ToJson()).ToJson());

        var partial = $$"""
            {
              "version": 1,
              "tiles": {
                "{{grass.ToHex()}}": { "priority": 2, "bush": true }
              }
            }
            """;
        var fromPartial = TileAssetFlagTable.FromJson(partial);
        var partialFlags = fromPartial.Get(grass);
        Assert.True(partialFlags.PassageNorth);
        Assert.True(partialFlags.PassageSouth);
        Assert.True(partialFlags.PassageEast);
        Assert.True(partialFlags.PassageWest);
        Assert.Equal(2, partialFlags.Priority);
        Assert.True(partialFlags.Bush);
        Assert.False(partialFlags.Counter);
        Assert.False(partialFlags.Damage);

        Assert.Throws<InvalidDataException>(() => TileAssetFlagTable.FromJson("""{ "version": 2, "tiles": {} }"""));
        Assert.Throws<InvalidDataException>(() => TileAssetFlagTable.FromJson(
            $$"""{ "version": 1, "tiles": { "{{grass.ToHex()}}": { "priority": 6 } } }"""));
        Assert.Throws<ArgumentException>(() => table.Set(TileAssetId.None, TileAssetFlags.Blocked));
        Assert.Throws<ArgumentOutOfRangeException>(() => TileAssetFlags.Default.WithPriority(6));

        var reopened = TileAssetFlags.Blocked
            .WithPassage(TilePassageDirection.East, true)
            .WithPassage(TilePassageDirection.West, true);
        table.Set(wall, reopened);
        Assert.False(table.Get(wall).BlocksAllPassage);
        Assert.True(table.Get(wall).Allows(TilePassageDirection.East));
        table.Set(wall, TileAssetFlags.Default);
        Assert.False(table.TryGetExplicit(wall, out _));
    }

    [Fact]
    public void Sidecar_AndMapCollision_Use48TileFlags_WithoutTouchingFmap()
    {
        var wall = Id(7);
        var grass = Id(8);
        var counter = Id(9);
        var map = SampleMap(wall, grass, counter);
        var flags = new TileAssetFlagTable();
        flags.Set(wall, TileAssetFlags.Blocked.WithPriority(5));
        flags.Set(grass, TileAssetFlags.Default.WithPassage(TilePassageDirection.North, false).WithBush(true).WithDamage(true));
        flags.Set(counter, TileAssetFlags.Default.WithCounter(true).WithPriority(2));
        flags.Set(Id(10), TileAssetFlags.Default.WithPriority(4));
        flags.Set(Id(99), TileAssetFlags.Blocked);
        map.TileFlags = flags;

        var withFlags = MapFormat.Write(map);
        map.TileFlags = null;
        var without = MapFormat.Write(map);
        Assert.Equal(without, withFlags);
        Assert.Equal((byte)6, withFlags[4]);
        map.TileFlags = flags;

        var blocked = MapCollision.IndexBlockedTiles(map);
        Assert.Contains((1, 0), blocked);
        Assert.DoesNotContain((0, 0), blocked);
        Assert.Contains((2, 0), blocked);

        Assert.False(MapCollision.AllowsTileStep(map, 0, 0, 1, 0));
        Assert.True(MapCollision.AllowsTileStep(map, 0, 0, 0, 1));
        Assert.False(MapCollision.CellAllows(map, 0, 0, TilePassageDirection.North));
        Assert.True(MapCollision.CellAllows(map, 0, 0, TilePassageDirection.South));
        Assert.True(MapCollision.CellIsBush(map, 0, 0));
        Assert.True(MapCollision.CellDealsDamage(map, 0, 0));
        Assert.False(MapCollision.CellIsCounter(map, 0, 0));
        Assert.Equal(4, MapCollision.CellPriority(map, 0, 0));
        Assert.True(MapCollision.TryCounterTarget(map, 0, 1, TilePassageDirection.East, out var tx, out var ty));
        Assert.Equal(2, tx);
        Assert.Equal(1, ty);
        Assert.False(MapCollision.AllowsPixelMove(map, 20, 10, 60, 10, 48));
        Assert.True(MapCollision.AllowsPixelMove(map, 20, 10, 30, 10, 48));

        map.TileFlags = null;
        Assert.False(MapCollision.IndexBlockedTiles(map).Contains((1, 0)));
        Assert.True(MapCollision.AllowsPixelMove(map, 20, 10, 60, 10, 48));
        Assert.Contains((2, 0), MapCollision.IndexBlockedTiles(map));

        var dir = Path.Combine(Path.GetTempPath(), "frog-tileflags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "atelier.fmap");
            File.WriteAllBytes(path, without);
            var projected = flags.Project(map);
            Assert.Equal(4, projected.Count);
            Assert.False(projected.TryGetExplicit(Id(99), out _));
            projected.SaveSidecar(path);
            Assert.Equal(Path.Combine(dir, "atelier.tileflags.json"), TileAssetFlagTable.SidecarPath(path));
            var sidecar = TileAssetFlagTable.TryLoadSidecar(path);
            Assert.NotNull(sidecar);
            Assert.True(sidecar!.Get(wall).BlocksAllPassage);
            Assert.True(sidecar.Get(grass).Bush);
            map.TileFlags = sidecar;
            var service = MapTestHelpers.CreateMapService(path);
            Assert.True(service.IsBlocked(1, 1, 0));
            Assert.False(service.IsBlocked(1, 0, 0));
            Assert.True(service.IsBlocked(1, 2, 0));
            Assert.False(service.AllowsFlaggedPixelMove(1, 20, 10, 60, 10, TileAssetMetrics.TargetTileSizePixels));
            Assert.True(service.AllowsFlaggedPixelMove(1, 20, 10, 30, 10, TileAssetMetrics.TargetTileSizePixels));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Labels_AndStatus_StayFrench_Hello11_Tiles48()
    {
        Assert.Equal("Passage (4 dir.)", TileAssetFlagLabels.Passage);
        Assert.Equal("Nord", TileAssetFlagLabels.North);
        Assert.Equal("Sud", TileAssetFlagLabels.South);
        Assert.Equal("Est", TileAssetFlagLabels.East);
        Assert.Equal("Ouest", TileAssetFlagLabels.West);
        Assert.Equal("Tout ouvert", TileAssetFlagLabels.OpenAll);
        Assert.Equal("Tout bloqué", TileAssetFlagLabels.BlockAll);
        Assert.Equal("Priorité", TileAssetFlagLabels.Priority);
        Assert.Equal("Buisson", TileAssetFlagLabels.Bush);
        Assert.Equal("Comptoir", TileAssetFlagLabels.Counter);
        Assert.Equal("Dégâts", TileAssetFlagLabels.Damage);
        Assert.Contains("48", TileAssetFlagLabels.Empty, StringComparison.Ordinal);

        var root = RepoRoot();
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Controls", "TileAssetFlagsPanel.cs"));
        Assert.Contains("TileAssetFlagLabels.Passage", panel, StringComparison.Ordinal);
        Assert.Contains("TileAssetFlagLabels.Bush", panel, StringComparison.Ordinal);
        Assert.Contains("TileAssetFlagLabels.Counter", panel, StringComparison.Ordinal);
        Assert.Contains("TileAssetFlagLabels.Damage", panel, StringComparison.Ordinal);
        Assert.Contains("TileAssetFlagLabels.Priority", panel, StringComparison.Ordinal);

        var status = File.ReadAllText(Path.Combine(root, "docs", "progress", "editor-tile-flags", "STATUS.md"));
        Assert.Contains("**Propriétaire** | Netsun", status, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", status, StringComparison.Ordinal);
        Assert.Contains("48×48", status, StringComparison.Ordinal);
        Assert.Contains("reste 11", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", status, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    private static Map SampleMap(TileAssetId wall, TileAssetId grass, TileAssetId counter)
    {
        var map = MapFormat.CreateTileAssetMap("Drapeaux", 3, 2);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        map.Layers[0].Tiles.Add(new Tile { X = 0, Y = 0, AssetId = grass, Type = TileType.Ground });
        map.Layers[0].Tiles.Add(new Tile { X = 1, Y = 0, AssetId = wall, Type = TileType.Ground });
        map.Layers[0].Tiles.Add(new Tile { X = 2, Y = 0, Type = TileType.Block });
        map.Layers[0].Tiles.Add(new Tile { X = 1, Y = 1, AssetId = counter, Type = TileType.Ground });
        map.Layers[1].Tiles.Add(new Tile { X = 0, Y = 0, AssetId = Id(10), Type = TileType.Ground });
        Assert.True(map.Validate(out var error), error);
        return map;
    }

    private static TileAssetId Id(byte mark)
    {
        var bytes = new byte[TileAssetId.ByteLength];
        bytes[0] = mark;
        bytes[^1] = 0x48;
        return TileAssetId.FromHashBytes(bytes);
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

        throw new InvalidOperationException("Racine du dépôt introuvable.");
    }
}
