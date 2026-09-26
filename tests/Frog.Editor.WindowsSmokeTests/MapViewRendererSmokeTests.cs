using System.Drawing;
using Frog.Application.Maps;
using Frog.Client.UI;
using Frog.Core.Chat;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;
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

        // Nameplates occupy the band above the 32px sprite. Ground samples stay below the feet.
        var belowFeetY = (int)feetCy + 4;
        var interior = play.GetPixel(tw + tw / 2, belowFeetY);
        Assert.Equal(groundArgb, interior.ToArgb());

        var seamAwayFromPlayers = play.GetPixel(tw, belowFeetY);
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
    public void AttackAndDeathPose_FourDirsStayThirtyTwo_NotGold()
    {
        var idle = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown);
        var attack = PlayerWorldAssets.FrameFor(
            new PlayerSpritePose(Direction.Right, Walking: false, Action: SpriteAction.Attack, ActionElapsedMs: 90));
        var death = PlayerWorldAssets.FrameFor(
            new PlayerSpritePose(Direction.Down, Walking: false, Action: SpriteAction.Death, ActionElapsedMs: 280));
        Assert.Equal(32, attack.Width);
        Assert.Equal(32, attack.Height);
        Assert.Equal(32, death.Width);
        Assert.Equal(32, death.Height);
        Assert.True(FramesDiffer(idle, attack), "attack frame must differ from south idle");
        Assert.True(FramesDiffer(idle, death), "death frame must differ from south idle");
        Assert.True(FramesDiffer(attack, death), "attack and death must not share a cell");
        var strike = attack.GetPixel(16, 16);
        Assert.NotEqual(Color.FromArgb(240, 200, 60).ToArgb(), strike.ToArgb());

        var npcIdle = WorldEntityAssets.FrameFor(WorldEntityKind.Npc, WorldSpritePose.IdleDown);
        var npcDeath = WorldEntityAssets.FrameFor(
            WorldEntityKind.Npc,
            new WorldSpritePose(Direction.Left, Walking: false, Action: SpriteAction.Death, ActionElapsedMs: 0));
        var monsterIdle = WorldEntityAssets.FrameFor(WorldEntityKind.Monster, WorldSpritePose.IdleDown);
        var monsterAttack = WorldEntityAssets.FrameFor(
            WorldEntityKind.Monster,
            new WorldSpritePose(Direction.Up, Walking: false, Action: SpriteAction.Attack, ActionElapsedMs: 0));
        Assert.Equal(32, npcDeath.Width);
        Assert.Equal(32, monsterAttack.Height);
        Assert.True(FramesDiffer(npcIdle, npcDeath), "NPC death frame must differ from south idle");
        Assert.True(FramesDiffer(monsterIdle, monsterAttack), "monster attack frame must differ from south idle");
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
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

    [Fact]
    public void Render_FringeCoversActorsOnItsRow_AndYieldsToActorsSouth()
    {
        const int redId = 7;
        using var red = new Bitmap(WorldMetrics.DefaultTileSizePixels, WorldMetrics.DefaultTileSizePixels);
        using (var g = Graphics.FromImage(red))
        {
            g.Clear(Color.FromArgb(255, 0, 0));
        }

        var tiles = new Dictionary<int, Bitmap> { [redId] = red };
        var emptyOthers = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase);
        var tw = WorldMetrics.DefaultTileSizePixels;
        var fringeNorth = CreateColumn(height: 2, LayerType.Fringe, upperRow: 0, redId);

        using var southOfFringe = MapViewRenderer.Render(
            fringeNorth,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: tw / 2f,
            localCenterYPx: tw + (tw / 2f),
            tilesetBitmaps: tiles);
        var headInNorthernFringe = southOfFringe.GetPixel(tw / 2, 20);
        Assert.True(
            headInNorthernFringe.B > headInNorthernFringe.R && headInNorthernFringe.B > headInNorthernFringe.G,
            $"player south of a fringe tile paints in front of it, got {headInNorthernFringe}");
        Assert.NotEqual(Color.FromArgb(255, 0, 0).ToArgb(), headInNorthernFringe.ToArgb());

        using var onFringe = MapViewRenderer.Render(
            fringeNorth,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: tw / 2f,
            localCenterYPx: tw / 2f,
            tilesetBitmaps: tiles);
        var bodyUnderFringe = onFringe.GetPixel(tw / 2, 2);
        Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), bodyUnderFringe.ToArgb());

        var mask = CreateColumn(height: 2, LayerType.Mask, upperRow: 0, redId);
        using var onMask = MapViewRenderer.Render(
            mask,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: tw / 2f,
            localCenterYPx: tw / 2f,
            tilesetBitmaps: tiles);
        var bodyOverMask = onMask.GetPixel(tw / 2, 2);
        Assert.True(
            bodyOverMask.B > bodyOverMask.R,
            $"mask stays under the actor of the same row, got {bodyOverMask}");
        Assert.NotEqual(Color.FromArgb(255, 0, 0).ToArgb(), bodyOverMask.ToArgb());

        using var lootUnderFringe = MapViewRenderer.Render(
            fringeNorth,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: tiles,
            groundLootCentersPx: [(tw / 2, tw / 2)]);
        Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), lootUnderFringe.GetPixel(tw / 2, tw / 2).ToArgb());

        // Sac centré à y=34 (rangée 1) : son bord nord recouvre la frange de la rangée 0.
        const int lootY = 34;
        using var lootSouth = MapViewRenderer.Render(
            fringeNorth,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: tiles,
            groundLootCentersPx: [(tw / 2, lootY)]);
        Assert.Equal(UiTheme.AccentGoldDim.ToArgb(), lootSouth.GetPixel(tw / 2, 30).ToArgb());
    }

    [Fact]
    public void Render_PlaytestPlacedNpc_PaintsMarkerOnItsTile()
    {
        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var npc = MapPlacedEntityEdit.Create(MapPlacedKind.Npc, 1, 0, 1);
        npc.Name = "Garde";
        var emptyOthers = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase);
        using var painted = MapViewRenderer.Render(
            map,
            emptyOthers,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            playtestPlacedEntities: new[] { npc });

        var marker = painted.GetPixel(tw + (tw / 2), tw / 2);
        Assert.NotEqual(GroundFill.ToArgb(), marker.ToArgb());
        Assert.True(marker.B > marker.R, $"PNJ playtest should read as a blue marker, got {marker}");

        var otherTile = painted.GetPixel(tw / 2, tw + (tw / 2));
        Assert.Equal(GroundFill.ToArgb(), otherTile.ToArgb());
    }

    [Fact]
    public void NameplateLabels_KeepFrenchNames_SkipIdsAndNonNpcEvents()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal("Élodie", NameplatePainter.FormatLabel("  Élodie  "));
        Assert.Null(NameplatePainter.FormatLabel("   "));
        Assert.Null(NameplatePainter.FormatLabel("aaaaaaaa-0002-4000-8000-000000000001"));
        Assert.Equal(NameplatePainter.MaxLabelChars, NameplatePainter.FormatLabel(new string('é', 40))!.Length);
        Assert.EndsWith("…", NameplatePainter.FormatLabel(new string('é', 40)));

        Assert.Equal("Élodie", NameplatePainter.LabelForNpcMapEvent("pnj_elodie", "Élodie"));
        Assert.Equal("elodie", NameplatePainter.LabelForNpcMapEvent("pnj_elodie", "  "));
        Assert.Null(NameplatePainter.LabelForNpcMapEvent("coffre_bois", "Coffre"));
        Assert.Null(NameplatePainter.LabelForNpcMapEvent("demo_interact", "Interaction démo"));
        Assert.False(NameplatePainter.IsNpcMapEvent("porte_nord"));
        Assert.True(NameplatePainter.IsNpcMapEvent("PNJ_Gardien"));
    }

    [Fact]
    public void Render_DrawsNameplatesAbovePlayersAndNpcs_NotMonstersOrChests()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var ground = GroundFill.ToArgb();
        var feetCy = tw + (tw / 2f);
        var localCx = tw / 2f;
        var otherCx = tw + (tw / 2f);
        var empty = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase);

        using var unnamed = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null);
        Assert.Equal(0, CountNotGround(unnamed, 0, 0, unnamed.Width, 14, ground));

        using var named = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Hélène"] = (otherCx, feetCy),
            },
            localUsername: "compte",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null,
            localDisplayName: "Élodie");
        Assert.True(CountNotGround(named, 0, 0, tw, 14, ground) > 4, "local display name should paint above the sprite");
        Assert.True(CountNotGround(named, tw, 0, tw, 14, ground) > 4, "remote username should paint above the sprite");
        var bodyY = (int)feetCy - 14;
        Assert.NotEqual(ground, named.GetPixel((int)localCx, bodyY).ToArgb());
        Assert.Equal(ground, named.GetPixel((int)localCx, (int)feetCy + 4).ToArgb());

        using var npc = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            npcCentersPx: new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Marchand"] = (localCx, feetCy),
            });
        Assert.True(CountNotGround(npc, 0, 0, tw, 14, ground) > 4, "NPC name should paint above the sprite");

        using var monster = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            monsterCentersPx: new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
            {
                ["slime"] = (localCx, feetCy),
            });
        Assert.Equal(0, CountNotGround(monster, 0, 0, tw, 14, ground));

        using var chest = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            mapEvents:
            [
                new MapEventWireEntry
                {
                    Slug = "coffre_bois",
                    DisplayName = "Coffre",
                    TileX = 0,
                    TileY = 1,
                    TriggerKind = MapEventTriggerKinds.Interact,
                },
            ]);
        Assert.Equal(0, CountNotGround(chest, 0, tw - 16, tw, 14, ground));

        using var talkingNpc = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            mapEvents:
            [
                new MapEventWireEntry
                {
                    Slug = "pnj_elodie",
                    DisplayName = "Élodie",
                    TileX = 0,
                    TileY = 1,
                    TriggerKind = MapEventTriggerKinds.Interact,
                },
            ]);
        Assert.True(
            CountNotGround(talkingNpc, 0, tw - 16, tw, 14, ground) > 4,
            "pnj_ event display name should paint above the tile");

        var placed = MapPlacedEntityEdit.Create(MapPlacedKind.Npc, 0, 1, 1);
        placed.Name = "Garde";
        using var playtestNpc = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            playtestPlacedEntities: [placed]);
        Assert.True(CountNotGround(playtestNpc, 0, tw - 16, tw, 14, ground) > 4, "playtest NPC name should paint above the tile");

        var spawn = MapPlacedEntityEdit.Create(MapPlacedKind.Spawn, 0, 1, 1);
        spawn.Name = "Héros";
        using var playtestSpawn = MapViewRenderer.Render(
            map,
            empty,
            localUsername: null,
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            playtestPlacedEntities: [spawn]);
        Assert.Equal(0, CountNotGround(playtestSpawn, 0, tw - 16, tw, 14, ground));
    }

    [Fact]
    public void Render_TileAsset48_NameplateStaysAbove32Sprite()
    {
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        var map = new Map
        {
            Name = "v6",
            Width = 2,
            Height = 2,
            GraphicIdentity = TileGraphicIdentity.TileAsset,
            TileSizePixels = TileAssetMetrics.TargetTileSizePixels,
        };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        var tw = MapViewRenderer.MapTileSizePixels(map);
        Assert.Equal(48, tw);
        var feetCy = tw + (tw / 2f);
        var localCx = tw / 2f;
        using var named = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "compte",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null,
            localDisplayName: "Élodie");

        var spriteTop = (int)MathF.Round(feetCy - 32f + 1f);
        var groundArgb = GroundFill.ToArgb();
        Assert.True(
            CountNotGround(named, 0, spriteTop - 16, tw, 14, groundArgb) > 4,
            "48px tiles still place the name above the 32px sprite");

        // Sheet row 12 is the face (255,229,229). The armor row matches the other
        // smoke checks: feet - 14, which is sprite row 17 (blue, B > R).
        var face = named.GetPixel((int)localCx, spriteTop + 12);
        Assert.Equal(Color.FromArgb(255, 229, 229).ToArgb(), face.ToArgb());

        var body = named.GetPixel((int)localCx, (int)feetCy - 14);
        Assert.Equal(spriteTop + 17, (int)feetCy - 14);
        Assert.NotEqual(groundArgb, body.ToArgb());
        Assert.True(body.B > body.R, $"armor under the nameplate stays the sprite, got {body}");
    }

    [Fact]
    public void Render_DrawsExpressionBubblesAboveLocalAndRemote_NotGold()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var tw = WorldMetrics.DefaultTileSizePixels;
        var map = new Map { Name = "bubbles", Width = 2, Height = 4 };
        var groundLayer = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                groundLayer.Tiles.Add(new Tile { X = x, Y = y, Type = TileType.Ground });
            }
        }

        map.Layers.Add(groundLayer);

        var feetY = (3 * tw) + (tw / 2f);
        var localX = tw / 2f;
        var remoteX = tw + (tw / 2f);
        var others = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bob"] = (remoteX, feetY),
        };
        var panel = UiTheme.BgPanel.ToArgb();
        var gold = UiTheme.AccentGold.ToArgb();
        var spriteTop = (int)feetY - PlayerWorldAssets.NativeSize + 1;

        using var plain = MapViewRenderer.Render(
            map,
            others,
            localUsername: "Ada",
            localCenterXPx: localX,
            localCenterYPx: feetY,
            tilesetBitmaps: null);
        Assert.Equal(0, CountArgb(plain, 0, 0, plain.Width, spriteTop, panel));
        Assert.Equal(0, CountArgb(plain, 0, 0, plain.Width, spriteTop, gold));

        using var painted = MapViewRenderer.Render(
            map,
            others,
            localUsername: "Ada",
            localCenterXPx: localX,
            localCenterYPx: feetY,
            tilesetBitmaps: null,
            expressionBubbles:
            [
                new ExpressionBubbleBoard.Visible("ada", ":-)", 1f),
                new ExpressionBubbleBoard.Visible("Bob", "Salut", 1f),
            ]);

        Assert.True(
            CountArgb(painted, (int)localX - 24, 0, 48, spriteTop, panel) > 4,
            "local bubble should use the panel chrome above the name");
        Assert.True(
            CountArgb(painted, (int)remoteX - 24, 0, 48, spriteTop, panel) > 4,
            "remote bubble should use the panel chrome above the name");
        Assert.Equal(0, CountArgb(painted, 0, 0, painted.Width, spriteTop, gold));

        var body = painted.GetPixel((int)localX, spriteTop + 17);
        Assert.True(body.B > body.R, $"bubble stays above the sprite, got {body}");

        using var hidden = MapViewRenderer.Render(
            map,
            others,
            localUsername: "Ada",
            localCenterXPx: localX,
            localCenterYPx: feetY,
            tilesetBitmaps: null,
            expressionBubbles: [new ExpressionBubbleBoard.Visible("Zoe", ":-)", 1f)]);
        Assert.Equal(0, CountArgb(hidden, 0, 0, hidden.Width, spriteTop, panel));
    }

    private static int CountArgb(Bitmap bmp, int x, int y, int width, int height, int argb)
    {
        var count = 0;
        var x1 = Math.Min(bmp.Width, x + width);
        var y1 = Math.Min(bmp.Height, y + height);
        for (var py = Math.Max(0, y); py < y1; py++)
        {
            for (var px = Math.Max(0, x); px < x1; px++)
            {
                if (bmp.GetPixel(px, py).ToArgb() == argb)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountNotGround(Bitmap bmp, int x, int y, int width, int height, int groundArgb)
    {
        var count = 0;
        var x1 = Math.Min(bmp.Width, x + width);
        var y1 = Math.Min(bmp.Height, y + height);
        for (var py = Math.Max(0, y); py < y1; py++)
        {
            for (var px = Math.Max(0, x); px < x1; px++)
            {
                if (bmp.GetPixel(px, py).ToArgb() != groundArgb)
                {
                    count++;
                }
            }
        }

        return count;
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

    private static Map CreateColumn(int height, LayerType upper, int upperRow, int tilesetId)
    {
        var map = new Map { Name = "depth", Width = 1, Height = height };
        var ground = new Layer { LayerType = LayerType.Ground, Visible = true };
        for (var y = 0; y < height; y++)
        {
            ground.Tiles.Add(new Tile { X = 0, Y = y, Type = TileType.Ground });
        }

        var top = new Layer { LayerType = upper, Visible = true };
        top.Tiles.Add(new Tile
        {
            X = 0,
            Y = upperRow,
            Type = TileType.Ground,
            TilesetId = tilesetId,
            SrcX = 0,
            SrcY = 0,
        });
        map.Layers.Add(ground);
        map.Layers.Add(top);
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
