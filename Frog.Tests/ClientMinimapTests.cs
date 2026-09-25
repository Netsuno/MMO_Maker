using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>Minicarte client : tuiles voisines, sans opcode. Hello 11, tuile 32.</summary>
public sealed class ClientMinimapTests
{
    [Fact]
    public void Hello_And_WorldTile_Stay_Put()
    {
        Assert.Equal(11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(13, MinimapWindow.WindowSpanTiles);
        Assert.Equal(1, MinimapWindow.WindowSpanTiles % 2);
        Assert.Equal(MinimapWindow.NearbyRadiusTiles, MinimapWindow.WindowSpanTiles / 2);
    }

    [Fact]
    public void Pixels_Use_Default_Tile_Not_Declared_48()
    {
        Assert.Equal((2, 1), MinimapWindow.TileFromPixels((2 * 32) + 15, 40, 20, 20));
        Assert.Equal((1, 0), MinimapWindow.TileFromPixels(63, 0, 20, 20));
        Assert.Equal((2, 0), MinimapWindow.TileFromPixels(64, 0, 20, 20));
        Assert.Equal((0, 0), MinimapWindow.TileFromPixels(-8, -4, 20, 20));
        Assert.Equal((1, 0), MinimapWindow.TileFromPixels(48, 0, 8, 8));
        Assert.Equal((19, 19), MinimapWindow.TileFromPixels(10_000, 10_000, 20, 20));
    }

    [Fact]
    public void Window_Centers_Player_And_Shows_Block_Warp_And_Void()
    {
        var map = MapSamples.StarterMeadow(Guid.Empty);
        map.TileSizePixels = 48;
        var index = MinimapWindow.Build(map);
        var (tx, ty) = MinimapWindow.TileFromPixels((8 * 32) + 4, 4 * 32, map.Width, map.Height);
        Assert.Equal((8, 4), (tx, ty));

        var window = MinimapWindow.Sample(index, tx, ty);
        var span = MinimapWindow.WindowSpanTiles;
        var radius = MinimapWindow.NearbyRadiusTiles;
        Assert.Equal(span * span, window.Length);
        Assert.Equal(MinimapWindow.CellKind.Ground, window[(radius * span) + radius]);

        var blockCol = 5 - tx + radius;
        var blockRow = 5 - ty + radius;
        Assert.Equal(MinimapWindow.CellKind.Block, window[(blockRow * span) + blockCol]);
        Assert.Equal(MinimapWindow.CellKind.Block, window[(blockRow * span) + (blockCol + 2)]);

        var warpCol = 3 - tx + radius;
        var warpRow = 3 - ty + radius;
        Assert.Equal(MinimapWindow.CellKind.Warp, window[(warpRow * span) + warpCol]);

        var edge = MinimapWindow.Sample(index, 0, 0);
        Assert.Equal(MinimapWindow.CellKind.Outside, edge[0]);
        Assert.Equal(MinimapWindow.CellKind.Ground, edge[(radius * span) + radius]);
    }

    [Fact]
    public void Attribute_Warp_And_Block_Are_Read_From_Loaded_Map()
    {
        var map = new Map { Name = "Grotte", Width = 8, Height = 8 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        var attrs = new Layer { LayerType = LayerType.Attributes };
        attrs.Tiles.Add(new Tile { X = 2, Y = 2, Type = TileType.Warp });
        attrs.Tiles.Add(new Tile { X = 3, Y = 2, Type = TileType.Block });
        map.Layers.Add(attrs);

        var window = MinimapWindow.Sample(MinimapWindow.Build(map), 2, 2);
        var span = MinimapWindow.WindowSpanTiles;
        var radius = MinimapWindow.NearbyRadiusTiles;
        Assert.Equal(MinimapWindow.CellKind.Warp, window[(radius * span) + radius]);
        Assert.Equal(MinimapWindow.CellKind.Block, window[(radius * span) + (radius + 1)]);
        Assert.Equal(MinimapWindow.CellKind.Outside, window[0]);
        Assert.Equal(MinimapWindow.CellKind.Open, window[(radius * span) + (radius - 1)]);
    }

    [Fact]
    public void Hidden_Visual_Layer_Is_Skipped_Block_Still_Wins()
    {
        var map = new Map { Name = "Masqué", Width = 4, Height = 4 };
        var hidden = new Layer { LayerType = LayerType.Ground, Visible = false };
        hidden.Tiles.Add(new Tile { X = 1, Y = 1, Type = TileType.Warp });
        var shown = new Layer { LayerType = LayerType.Mask };
        shown.Tiles.Add(new Tile { X = 1, Y = 1, Type = TileType.Ground });
        shown.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Block });
        map.Layers.Add(hidden);
        map.Layers.Add(shown);

        var index = MinimapWindow.Build(map);
        Assert.Equal(MinimapWindow.CellKind.Ground, index.At(1, 1));
        Assert.Equal(MinimapWindow.CellKind.Block, index.At(0, 0));
        Assert.Equal(MinimapWindow.CellKind.Open, index.At(2, 2));
    }

    [Fact]
    public void Client_Module_Reads_Window_Without_New_Protocol()
    {
        var root = RepoRoot();
        var module = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "HudMinimapModule.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var metrics = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "WorldMetrics.cs"));
        var hello = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));

        Assert.Contains("MinimapWindow", module, StringComparison.Ordinal);
        Assert.Contains("Tuiles autour du joueur", module, StringComparison.Ordinal);
        Assert.Contains("base(\"Carte\")", module, StringComparison.Ordinal);
        Assert.Contains("· tuile ", module, StringComparison.Ordinal);
        Assert.DoesNotContain("MapViewRenderer.Render", module, StringComparison.Ordinal);
        Assert.DoesNotContain("AccentGold", module, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", module, StringComparison.Ordinal);
        Assert.Contains("_hudMinimap.RebuildCache(map);", shell, StringComparison.Ordinal);
        Assert.Contains("_hudMinimap.SetPlayerPixel(", shell, StringComparison.Ordinal);
        Assert.Contains("public const int DefaultTileSizePixels = 32;", metrics, StringComparison.Ordinal);
        Assert.Contains("public const ushort Version = 11;", hello, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
