using System.Drawing;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
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

        // Feet-anchor a 32×32 sprite on the south row so the full body stays on the 2×2 bitmap.
        var localCx = tw / 2f;
        var otherCx = tw + tw / 2f;
        var feetCy = tw + tw / 2f;
        var others = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["other"] = (otherCx, feetCy),
        };

        using var play = MapViewRenderer.Render(
            map,
            others,
            localUsername: "self",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null);

        var interior = play.GetPixel(tw + tw / 2, 8);
        Assert.Equal(groundArgb, interior.ToArgb());

        var seamAwayFromPlayers = play.GetPixel(tw, 8);
        Assert.Equal(groundArgb, seamAwayFromPlayers.ToArgb());

        var oldGoldArgb = Color.FromArgb(240, 200, 60).ToArgb();
        var bodyY = (int)feetCy - 14;
        var selfPixel = play.GetPixel((int)localCx, bodyY);
        Assert.NotEqual(groundArgb, selfPixel.ToArgb());
        Assert.NotEqual(oldGoldArgb, selfPixel.ToArgb());
        Assert.True(
            selfPixel.B > selfPixel.R && selfPixel.B > selfPixel.G,
            $"local player body should be blue armor, not a gold ellipse, got {selfPixel}");

        var otherPixel = play.GetPixel((int)otherCx, bodyY);
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
    public void PlayerWorldSprite_IsThirtyTwoSquare_NotGold()
    {
        var sprite = PlayerWorldAssets.Sprite;
        Assert.Equal(32, sprite.Width);
        Assert.Equal(32, sprite.Height);
        Assert.Equal(32, PlayerWorldAssets.NativeSize);
        Assert.Equal(1, PlayerWorldAssets.DrawScale);
        var center = sprite.GetPixel(16, 16);
        Assert.NotEqual(Color.FromArgb(240, 200, 60).ToArgb(), center.ToArgb());
        Assert.True(center.A == 255 && center.B > center.R, $"expected blue armor center, got {center}");
    }

    [Fact]
    public void WalkPose_ComposesBodyAndHead_FourDirsStayThirtyTwo()
    {
        Assert.Equal(96, PlayerWorldAssets.WalkSheetWidth);
        Assert.Equal(128, PlayerWorldAssets.WalkSheetHeight);

        var idle = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown);
        Assert.Equal(32, idle.Width);
        Assert.Equal(32, idle.Height);
        var idleCenter = idle.GetPixel(16, 16);
        Assert.True(idleCenter.A == 255 && idleCenter.B > idleCenter.R, $"idle south should stay blue, got {idleCenter}");

        var leftWalk = PlayerWorldAssets.FrameFor(new PlayerSpritePose(Direction.Left, Walking: true, ElapsedMs: 0));
        var upIdle = PlayerWorldAssets.FrameFor(new PlayerSpritePose(Direction.Up, Walking: false));
        Assert.Equal(32, leftWalk.Width);
        Assert.Equal(32, upIdle.Width);
        Assert.True(FramesDiffer(idle, leftWalk), "left walk frame must differ from south idle");
        Assert.True(FramesDiffer(idle, upIdle), "up idle frame must differ from south idle");

        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var localCx = tw / 2f;
        var feetCy = tw + tw / 2f;
        using var play = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "self",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null,
            localPose: new PlayerSpritePose(Direction.Right, Walking: true, ElapsedMs: 280));

        var bodyY = (int)feetCy - 14;
        var selfPixel = play.GetPixel((int)localCx, bodyY);
        Assert.NotEqual(Color.FromArgb(120, 160, 100).ToArgb(), selfPixel.ToArgb());
        Assert.NotEqual(Color.FromArgb(240, 200, 60).ToArgb(), selfPixel.ToArgb());
        Assert.True(selfPixel.B > selfPixel.R, $"walk pose must still draw blue armor, got {selfPixel}");
    }

    [Fact]
    public void NpcAndMonsterWalkPose_DrawFeetAnchored_StayThirtyTwoAndNotGold()
    {
        Assert.Equal(96, WorldEntityAssets.WalkSheetWidth);
        Assert.Equal(128, WorldEntityAssets.WalkSheetHeight);
        Assert.Equal(32, WorldEntityAssets.NativeSize);
        Assert.Equal(1, WorldEntityAssets.DrawScale);

        var npcIdle = WorldEntityAssets.FrameFor(WorldEntityKind.Npc, WorldSpritePose.IdleDown);
        var npcWalk = WorldEntityAssets.FrameFor(
            WorldEntityKind.Npc,
            new WorldSpritePose(Direction.Right, Walking: true, ElapsedMs: 0));
        var monsterIdle = WorldEntityAssets.FrameFor(WorldEntityKind.Monster, WorldSpritePose.IdleDown);
        var monsterWalk = WorldEntityAssets.FrameFor(
            WorldEntityKind.Monster,
            new WorldSpritePose(Direction.Left, Walking: true, ElapsedMs: 280));
        Assert.Equal(32, npcIdle.Width);
        Assert.Equal(32, monsterIdle.Height);
        Assert.True(FramesDiffer(npcIdle, npcWalk), "NPC walk frame must differ from south idle");
        Assert.True(FramesDiffer(monsterIdle, monsterWalk), "monster walk frame must differ from south idle");

        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var npcCx = tw / 2f;
        var monsterCx = tw + tw / 2f;
        var feetCy = tw + tw / 2f;
        var npcs = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["villager"] = (npcCx, feetCy),
        };
        var monsters = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["slime"] = (monsterCx, feetCy),
        };
        var npcPoses = new Dictionary<string, WorldSpritePose>(StringComparer.OrdinalIgnoreCase)
        {
            ["villager"] = new WorldSpritePose(Direction.Down, Walking: true, ElapsedMs: 0),
        };
        var monsterPoses = new Dictionary<string, WorldSpritePose>(StringComparer.OrdinalIgnoreCase)
        {
            ["slime"] = new WorldSpritePose(Direction.Right, Walking: true, ElapsedMs: 140),
        };

        using var play = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            npcCentersPx: npcs,
            npcPoses: npcPoses,
            monsterCentersPx: monsters,
            monsterPoses: monsterPoses);

        var groundArgb = GroundFill.ToArgb();
        var oldGoldArgb = Color.FromArgb(240, 200, 60).ToArgb();
        var bodyY = (int)feetCy - 14;
        var npcPixel = play.GetPixel((int)npcCx, bodyY);
        var monsterPixel = play.GetPixel((int)monsterCx, bodyY);
        Assert.NotEqual(groundArgb, npcPixel.ToArgb());
        Assert.NotEqual(groundArgb, monsterPixel.ToArgb());
        Assert.NotEqual(oldGoldArgb, npcPixel.ToArgb());
        Assert.NotEqual(oldGoldArgb, monsterPixel.ToArgb());
        Assert.NotEqual(npcPixel.ToArgb(), monsterPixel.ToArgb());
        Assert.True(
            monsterPixel.G > monsterPixel.R && monsterPixel.G > monsterPixel.B,
            $"monster slime should read green, got {monsterPixel}");
    }

    [Fact]
    public void Render_DrawsGroundLootBagAboveGroundTile()
    {
        var map = CreateTwoByTwoGround();
        var emptyOthers = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase);
        const int lootX = 16;
        const int lootY = 16;

        using var bare = MapViewRenderer.Render(
            map,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null);
        var groundArgb = GroundFill.ToArgb();
        Assert.Equal(groundArgb, bare.GetPixel(lootX, lootY).ToArgb());

        using var withLoot = MapViewRenderer.Render(
            map,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            groundLootCentersPx: [(lootX, lootY)]);

        var bag = withLoot.GetPixel(lootX, lootY);
        Assert.Equal(UiTheme.AccentGoldDim.ToArgb(), bag.ToArgb());
        Assert.NotEqual(groundArgb, bag.ToArgb());
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

    private static bool FramesDiffer(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return true;
        }

        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb())
                {
                    return true;
                }
            }
        }

        return false;
    }
}
