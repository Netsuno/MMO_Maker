using System;
using System.IO;
using System.Linq;
using Frog.Core.Maps;
using Frog.Legacy;
using Xunit;

namespace Frog.Tests;

public sealed class LegacyFccMapReaderTests
{
    private static string FixturesMapsDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "fixtures", "legacy", "maps");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("fixtures/legacy/maps introuvable.");
        }
    }

    [Fact]
    public void Read_Map1_HeaderAndWarp()
    {
        var path = Path.Combine(FixturesMapsDir, "map1.fcc");
        var result = new LegacyFccMapReader().Read(path);

        Assert.True(result.Report.Success, string.Join("; ", result.Report.Errors));
        Assert.Equal("95e09b2b794b4d3bdb8903b83fdd41abe7769204f00556a3240b13506e781f3b", result.Report.Sha256Hex);
        Assert.NotNull(result.Map);
        Assert.Equal(31, result.Map!.Width);
        Assert.Equal(31, result.Map.Height);
        Assert.StartsWith("Carte de test", result.Map.Name, StringComparison.Ordinal);
        Assert.Equal(22, result.Revision);
        Assert.Equal(2, result.Down);

        var attrs = result.Map.Layers.Single(l => l.LayerType == Frog.Core.Enums.LayerType.Attributes);
        var warp = attrs.Tiles.Single(t => t.Type == Frog.Core.Enums.TileType.Warp && t.X == 5 && t.Y == 14);
        Assert.Equal(MapSamples.RuntimeMapIdToGuid(2), warp.WarpTargetMapId);
        Assert.Equal(7, warp.WarpTargetX);
        Assert.Equal(17, warp.WarpTargetY);

        Assert.Contains(attrs.Tiles, t => t.Type == Frog.Core.Enums.TileType.Block);
        Assert.NotEmpty(result.Report.Unsupported); // ITEM/SHOP etc. présents sur map1
    }

    [Theory]
    [InlineData("map1.fcc")]
    [InlineData("map2.fcc")]
    [InlineData("map3.fcc")]
    public void Read_ValidFixtures_Succeed(string fileName)
    {
        var path = Path.Combine(FixturesMapsDir, fileName);
        var result = new LegacyFccMapReader().Read(path);
        Assert.True(result.Report.Success, string.Join("; ", result.Report.Errors));
        Assert.NotNull(result.Map);
        Assert.True(result.Map!.Validate(out var err), err);
    }

    [Fact]
    public void Read_Truncated_FailsWithError()
    {
        var path = Path.Combine(FixturesMapsDir, "map1_truncated.fcc");
        var result = new LegacyFccMapReader().Read(path);
        Assert.False(result.Report.Success);
        Assert.Null(result.Map);
        Assert.NotEmpty(result.Report.Errors);
    }
}
