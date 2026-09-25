using System;
using System.IO;
using System.Linq;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Tests;

/// <summary>Tri vertical acteurs / butin / frange (Netsun). Protocole 11, tuile monde 32.</summary>
public sealed class WorldDepthTests
{
    [Fact]
    public void Layers_GroundAndMaskStayUnder_FringePaintsAfterActorsOfTheRow()
    {
        Assert.True(WorldDepth.IsBelowActorLayer(LayerType.Ground));
        Assert.True(WorldDepth.IsBelowActorLayer(LayerType.Mask));
        Assert.True(WorldDepth.IsBelowActorLayer(LayerType.Mask2));
        Assert.False(WorldDepth.IsFringeLayer(LayerType.Mask));
        Assert.True(WorldDepth.IsFringeLayer(LayerType.Fringe));
        Assert.True(WorldDepth.IsFringeLayer(LayerType.Fringe2));
        Assert.False(WorldDepth.IsBelowActorLayer(LayerType.Fringe));
        Assert.True(WorldDepth.IsAttributeLayer(LayerType.Attributes));
        Assert.False(WorldDepth.IsBelowActorLayer(LayerType.Attributes));
        Assert.False(WorldDepth.IsFringeLayer(LayerType.Attributes));
    }

    [Fact]
    public void RowSteps_FringeFollowsActorsOfTheSameRow_AndPrecedesTheNextRow()
    {
        var steps = WorldDepth.RowSteps(2);
        Assert.Equal(
            new[]
            {
                new WorldDepth.RowStep(WorldDepth.RowStepKind.ActorsNorthOfMap, -1),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.BelowActors, 0),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.ActorsOnRow, 0),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.Fringe, 0),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.BelowActors, 1),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.ActorsOnRow, 1),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.Fringe, 1),
                new WorldDepth.RowStep(WorldDepth.RowStepKind.ActorsSouthOfMap, 2),
            },
            steps);

        var fringe0 = Array.FindIndex(steps, s => s.Kind == WorldDepth.RowStepKind.Fringe && s.Row == 0);
        var actors0 = Array.FindIndex(steps, s => s.Kind == WorldDepth.RowStepKind.ActorsOnRow && s.Row == 0);
        var below1 = Array.FindIndex(steps, s => s.Kind == WorldDepth.RowStepKind.BelowActors && s.Row == 1);
        Assert.True(actors0 < fringe0 && fringe0 < below1);
    }

    [Fact]
    public void FloorTileIndex_UsesFloor_ForSheet32AndTileAsset48()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(0, WorldDepth.FloorTileIndex(0, 32));
        Assert.Equal(0, WorldDepth.FloorTileIndex(31, 32));
        Assert.Equal(1, WorldDepth.FloorTileIndex(32, 32));
        Assert.Equal(1, WorldDepth.FloorTileIndex(48, 32));
        Assert.Equal(-1, WorldDepth.FloorTileIndex(-1, 32));
        Assert.Equal(-32, WorldDepth.FloorTileIndex(-1000, 32));
        Assert.Equal(-32, WorldDepth.FloorTileIndex(-1000f, 32));

        Assert.Equal(0, WorldDepth.FloorTileIndex(47, 48));
        Assert.Equal(1, WorldDepth.FloorTileIndex(72, 48));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void CompareActors_NorthFirst_LootUnderSameAnchorPlayer_SouthRowLast()
    {
        var keys = new[]
        {
            new WorldDepth.ActorKey(1, 40, WorldDepth.ActorSlot.LocalPlayer, 3),
            new WorldDepth.ActorKey(1, 40, WorldDepth.ActorSlot.Loot, 2),
            new WorldDepth.ActorKey(0, 10, WorldDepth.ActorSlot.RemotePlayer, 1),
            new WorldDepth.ActorKey(2, 80, WorldDepth.ActorSlot.Loot, 0),
        };

        var ordered = keys.OrderBy(k => k, Comparer.Instance).ToArray();
        Assert.Equal(WorldDepth.ActorSlot.RemotePlayer, ordered[0].Slot);
        Assert.Equal(WorldDepth.ActorSlot.Loot, ordered[1].Slot);
        Assert.Equal(1, ordered[1].Row);
        Assert.Equal(WorldDepth.ActorSlot.LocalPlayer, ordered[2].Slot);
        Assert.Equal(2, ordered[3].Row);
    }

    [Fact]
    public void FootprintBaseline_IsTheSouthTileOfThePrefab()
    {
        Assert.Equal(0, WorldDepth.FootprintBaselineRow(0, 1));
        Assert.Equal(3, WorldDepth.FootprintBaselineRow(2, 2));
        Assert.Equal(4, WorldDepth.FootprintBaselineRow(4, 0));
    }

    [Fact]
    public void Renderer_UsesRowSteps_AndKeepsTileAssetBlit()
    {
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("WorldDepth.RowSteps", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldDepth.CompareActors", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldDepth.IsFringeLayer", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldDepth.IsBelowActorLayer", renderer, StringComparison.Ordinal);
        Assert.Contains("TryDrawTileAsset", renderer, StringComparison.Ordinal);
        Assert.Contains("TileAssetDisplayPixels.MapPixelSize", renderer, StringComparison.Ordinal);
        Assert.Contains("groundLootCentersPx", renderer, StringComparison.Ordinal);
        Assert.Contains("groundLootCentersPx: groundLoot", shell, StringComparison.Ordinal);
        Assert.Contains("localPose, localAppearance", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", renderer, StringComparison.Ordinal);
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

    private sealed class Comparer : System.Collections.Generic.IComparer<WorldDepth.ActorKey>
    {
        public static readonly Comparer Instance = new();

        public int Compare(WorldDepth.ActorKey x, WorldDepth.ActorKey y) => WorldDepth.CompareActors(x, y);
    }
}
