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
    private static readonly Color GroundFill = Color.FromArgb(120, 160, 100);

    [Fact]
    public void Render_Default_HasNoTileGrid_PlayersAndTilesRemain()
    {
        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var groundArgb = GroundFill.ToArgb();
        var emptyOthers = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase);

        using var emptyPlay = MapViewRenderer.Render(
            map,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null);

        Assert.Equal(map.Width * tw, emptyPlay.Width);
        Assert.Equal(map.Height * tw, emptyPlay.Height);

        for (var y = 0; y < emptyPlay.Height; y++)
        {
            for (var x = 0; x < emptyPlay.Width; x++)
            {
                Assert.Equal(groundArgb, emptyPlay.GetPixel(x, y).ToArgb());
            }
        }

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

        var interior = play.GetPixel(tw + tw / 2, tw + tw / 2);
        Assert.Equal(groundArgb, interior.ToArgb());

        var seamAwayFromPlayers = play.GetPixel(tw, tw + tw / 2);
        Assert.Equal(groundArgb, seamAwayFromPlayers.ToArgb());

        var oldGoldArgb = Color.FromArgb(240, 200, 60).ToArgb();
        var selfPixel = play.GetPixel(tw / 2, tw / 2);
        Assert.NotEqual(groundArgb, selfPixel.ToArgb());
        Assert.NotEqual(oldGoldArgb, selfPixel.ToArgb());
        Assert.True(
            selfPixel.G > selfPixel.R && selfPixel.G > selfPixel.B,
            $"local player center should be olive tunic, not a gold ellipse, got {selfPixel}");

        var otherPixel = play.GetPixel(tw + tw / 2, tw / 2);
        Assert.NotEqual(groundArgb, otherPixel.ToArgb());
        Assert.NotEqual(selfPixel.ToArgb(), otherPixel.ToArgb());
        Assert.True(otherPixel.B > otherPixel.R, $"other player should stay blue-tinted, got {otherPixel}");

        using var debug = MapViewRenderer.Render(
            map,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            showTileGrid: true);

        var gridPixels = 0;
        for (var x = 0; x < debug.Width; x++)
        {
            if (debug.GetPixel(x, 0).ToArgb() != groundArgb)
            {
                gridPixels++;
            }

            if (debug.GetPixel(x, tw).ToArgb() != groundArgb)
            {
                gridPixels++;
            }
        }

        for (var y = 0; y < debug.Height; y++)
        {
            if (debug.GetPixel(0, y).ToArgb() != groundArgb)
            {
                gridPixels++;
            }

            if (debug.GetPixel(tw, y).ToArgb() != groundArgb)
            {
                gridPixels++;
            }
        }

        Assert.True(gridPixels > 0, "showTileGrid:true must paint at least one tile-seam pixel");
    }

    [Fact]
    public void PlayerWorldSprite_IsSixteenSquare_NotGold()
    {
        var sprite = PlayerWorldAssets.Sprite;
        Assert.Equal(16, sprite.Width);
        Assert.Equal(16, sprite.Height);
        Assert.Equal(2, PlayerWorldAssets.DrawScale);
        var center = sprite.GetPixel(8, 8);
        Assert.NotEqual(Color.FromArgb(240, 200, 60).ToArgb(), center.ToArgb());
        Assert.True(center.A == 255 && center.G > center.R, $"expected olive tunic center, got {center}");
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
