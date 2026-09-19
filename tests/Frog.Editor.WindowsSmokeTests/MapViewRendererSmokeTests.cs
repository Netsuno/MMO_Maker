using System.Drawing;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>GameWorldView : pas de grille tuile en jeu (Netsun). Flag debug défaut off.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapViewRendererSmokeTests
{
    [Fact]
    public void Render_Default_HasNoTileGrid_PlayersAndTilesRemain()
    {
        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var others = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["other"] = (tw + tw / 2f, tw / 2f),
        };

        using var play = MapViewRenderer.Render(
            map,
            others,
            localUsername: "self",
            localCenterXPx: tw / 2f,
            localCenterYPx: tw / 2f,
            tilesetBitmaps: null);

        Assert.Equal(map.Width * tw, play.Width);
        Assert.Equal(map.Height * tw, play.Height);

        var interior = play.GetPixel(tw + tw / 2, tw + tw / 2);
        var seam = play.GetPixel(tw, tw / 2);
        Assert.Equal(interior, seam);

        var selfPixel = play.GetPixel(tw / 2, tw / 2);
        Assert.NotEqual(interior, selfPixel);

        var otherPixel = play.GetPixel(tw + tw / 2, tw / 2);
        Assert.NotEqual(interior, otherPixel);

        using var debug = MapViewRenderer.Render(
            map,
            others,
            localUsername: "self",
            localCenterXPx: tw / 2f,
            localCenterYPx: tw / 2f,
            tilesetBitmaps: null,
            showTileGrid: true);

        var debugSeam = debug.GetPixel(tw, tw / 2);
        var debugInterior = debug.GetPixel(tw + tw / 2, tw + tw / 2);
        Assert.Equal(interior, debugInterior);
        Assert.NotEqual(debugInterior, debugSeam);
    }

    private static Map CreateTwoByTwoGround()
    {
        var map = new Map { Name = "GridOff", Width = 2, Height = 2 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
