using System.Drawing;
using Frog.Client.Controls;
using Frog.Client.Models;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Equipped paperdoll overlays composite in draw order and hide when empty (Netsun).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class PaperdollOverlaySmokeTests
{
    private static readonly Color Tunic = Color.FromArgb(255, 186, 122, 64);
    private static readonly Color Hat = Color.FromArgb(255, 196, 48, 72);
    private static readonly Color Armor = Color.FromArgb(255, 42, 138, 78);
    private static readonly Color Blade = Color.FromArgb(255, 214, 224, 232);

    [Fact]
    public void Frame_UnequippedHidesOverlays_EquippedPaintsHatOnHead()
    {
        var bare = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown);
        Assert.False(Contains(bare, Tunic));
        Assert.False(Contains(bare, Hat));
        Assert.False(Contains(bare, Armor));
        Assert.False(Contains(bare, Blade));

        var hatOnly = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, PaperdollOverlaySet.FromItems(null, null, Guid.NewGuid()));
        Assert.True(Contains(hatOnly, Hat));
        Assert.False(Contains(hatOnly, Armor));
        Assert.False(Contains(hatOnly, Blade));
        Assert.True(CountReplaced(bare, hatOnly, Hat) >= 10, "hat must cover existing head pixels");

        var armorOnly = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, PaperdollOverlaySet.FromItems(null, Guid.NewGuid(), null));
        Assert.True(Contains(armorOnly, Armor));
        Assert.False(Contains(armorOnly, Hat));
        Assert.True(CountReplaced(bare, armorOnly, Armor) >= 8, "armor must cover the torso, not replace the head");

        var weaponOnly = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, PaperdollOverlaySet.FromItems(Guid.NewGuid(), null, null));
        Assert.True(Contains(weaponOnly, Blade));
        Assert.False(Contains(weaponOnly, Hat));
        Assert.False(Contains(weaponOnly, Armor));

        var geared = PlayerWorldAssets.FrameFor(
            PlayerSpritePose.IdleDown,
            PaperdollOverlaySet.FromItems(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(Contains(geared, Hat) && Contains(geared, Armor) && Contains(geared, Blade));
        Assert.Equal(bare.GetPixel(16, 30).ToArgb(), geared.GetPixel(16, 30).ToArgb());

        var cleared = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown, PaperdollOverlaySet.None);
        Assert.False(Contains(cleared, Hat));
        Assert.False(Contains(cleared, Tunic));
        Assert.Equal(bare.GetPixel(16, 16).ToArgb(), cleared.GetPixel(16, 16).ToArgb());
    }

    [Fact]
    public void Frame_TunicClothShowsUnderArmor_AndHidesWhenEmpty()
    {
        var bare = PlayerWorldAssets.FrameFor(PlayerSpritePose.IdleDown);
        var tunicOnly = PlayerWorldAssets.FrameFor(
            PlayerSpritePose.IdleDown,
            PaperdollOverlaySet.FromItems(null, null, null, Guid.NewGuid()));
        Assert.True(Contains(tunicOnly, Tunic));
        Assert.False(Contains(tunicOnly, Armor));
        Assert.False(Contains(tunicOnly, Hat));
        Assert.True(CountReplaced(bare, tunicOnly, Tunic) >= 12, "tunic must cover the torso cloth");
        Assert.Equal(bare.GetPixel(16, 4).ToArgb(), tunicOnly.GetPixel(16, 4).ToArgb());
        Assert.Equal(bare.GetPixel(16, 30).ToArgb(), tunicOnly.GetPixel(16, 30).ToArgb());

        var both = PlayerWorldAssets.FrameFor(
            PlayerSpritePose.IdleDown,
            PaperdollOverlaySet.FromItems(null, Guid.NewGuid(), null, Guid.NewGuid()));
        Assert.True(Contains(both, Tunic), "cloth hem stays visible under the plate");
        Assert.True(Contains(both, Armor));
        Assert.True(CountReplaced(tunicOnly, both, Armor) >= 8, "armor is drawn after the tunic");

        var walk = PlayerWorldAssets.FrameFor(
            new PlayerSpritePose(Direction.Left, Walking: true),
            PaperdollOverlaySet.FromItems(null, null, null, Guid.NewGuid()));
        Assert.True(Contains(walk, Tunic));
        Assert.Equal(32, walk.Width);
        Assert.Equal(32, walk.Height);
    }

    [Fact]
    public void EquipmentService_MapsServerSlots_AndKeepsLocalHat()
    {
        var weapon = Guid.NewGuid();
        var armor = Guid.NewGuid();
        var equipped = new Equipment(weapon, armor, Equipment.LocalHeadwearItemId);
        var shown = EquipmentService.ToOverlaySet(equipped);
        Assert.True(shown.Weapon && shown.Armor && shown.Hat);
        Assert.False(shown.Tunic);
        Assert.True(shown.IsLayerVisible(PaperdollLayer.Body));
        Assert.True(shown.IsLayerVisible(PaperdollLayer.Head));

        var afterUnequip = equipped.WithServerLoadout(new InventorySnapshotWire());
        var hidden = EquipmentService.ToOverlaySet(afterUnequip);
        Assert.False(hidden.Weapon);
        Assert.False(hidden.Armor);
        Assert.True(hidden.Hat);

        var withTunic = new Equipment(weapon, armor, Equipment.LocalHeadwearItemId, Equipment.LocalTunicItemId);
        var shownTunic = EquipmentService.ToOverlaySet(withTunic);
        Assert.True(shownTunic.Tunic && shownTunic.Hat && shownTunic.Weapon && shownTunic.Armor);
        var tunicKept = EquipmentService.ToOverlaySet(withTunic.WithServerLoadout(new InventorySnapshotWire()));
        Assert.True(tunicKept.Hat && tunicKept.Tunic);
        Assert.False(tunicKept.Weapon || tunicKept.Armor);

        var offhandOnly = new Equipment(null, null, null, null, Guid.NewGuid());
        Assert.True(offhandOnly.IsOccupied(EquipmentSlot.Offhand));
        var offhandVisual = EquipmentService.ToOverlaySet(offhandOnly);
        Assert.False(offhandVisual.Weapon || offhandVisual.Armor || offhandVisual.Hat || offhandVisual.Tunic);

        Assert.True(EquipmentSlotMapping.TryGetServerSlot(EquipmentSlot.Weapon, out var weaponSlot));
        Assert.Equal(EquipmentSlotKind.Weapon, weaponSlot);
        Assert.True(EquipmentSlotMapping.TryGetServerSlot(EquipmentSlot.Armor, out var armorSlot));
        Assert.Equal(EquipmentSlotKind.Armor, armorSlot);
        Assert.False(EquipmentSlotMapping.TryGetServerSlot(EquipmentSlot.Headwear, out _));
        Assert.False(EquipmentSlotMapping.TryGetServerSlot(EquipmentSlot.Tunic, out _));
    }

    [Fact]
    public void Map_LocalAppearanceShowsHat_OtherPlayersStayBare()
    {
        var map = CreateTwoByTwoGround();
        var tw = WorldMetrics.DefaultTileSizePixels;
        var feetCy = tw + (tw / 2f);
        var localCx = tw / 2f;
        var otherCx = tw + (tw / 2f);
        var hat = PaperdollOverlaySet.FromItems(null, null, Guid.NewGuid());
        var others = new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase)
        {
            ["other"] = (otherCx, feetCy),
        };

        using var local = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "self",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null,
            localAppearance: hat);
        Assert.True(Contains(local, Hat), "local hat overlay should be visible");

        using var bare = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "self",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null);
        Assert.False(Contains(bare, Hat));

        using var othersOnly = MapViewRenderer.Render(
            map,
            others,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            localAppearance: hat);
        Assert.False(Contains(othersOnly, Hat), "other players have no equipment on the wire");

        var tunic = PaperdollOverlaySet.FromItems(null, null, null, Guid.NewGuid());
        using var localTunic = MapViewRenderer.Render(
            map,
            new Dictionary<string, (float CxPx, float CyPx)>(StringComparer.OrdinalIgnoreCase),
            localUsername: "self",
            localCenterXPx: localCx,
            localCenterYPx: feetCy,
            tilesetBitmaps: null,
            localAppearance: tunic);
        Assert.True(Contains(localTunic, Tunic), "local tunic overlay should be visible");
        Assert.False(Contains(localTunic, Armor));

        using var othersTunic = MapViewRenderer.Render(
            map,
            others,
            localUsername: "self",
            localCenterXPx: -1000f,
            localCenterYPx: -1000f,
            tilesetBitmaps: null,
            localAppearance: tunic);
        Assert.False(Contains(othersTunic, Tunic), "other players have no tunic on the wire");
    }

    [Fact]
    public void EquipmentPanel_CasqueLocal_UsesFrenchCopy()
    {
        StaTestRunner.Run(() =>
        {
            using var panel = new EquipmentPanel();
            Assert.Equal("Casque: —", panel.HeadwearLabelTextForTest);
            Assert.Equal("Porter le casque", panel.HeadwearButtonTextForTest);
            var worn = false;
            panel.LocalHeadwearChanged += value => worn = value;
            panel.ClickToggleHeadwearForTest();
            Assert.True(worn);
            Assert.Equal("Casque: porté (local)", panel.HeadwearLabelTextForTest);
            Assert.Equal("Retirer le casque", panel.HeadwearButtonTextForTest);
            panel.ResetLocalHeadwear();
            Assert.Equal("Casque: —", panel.HeadwearLabelTextForTest);
            Assert.Equal("Porter le casque", panel.HeadwearButtonTextForTest);
        });
    }

    [Fact]
    public void EquipmentPanel_TuniqueLocale_UsesFrenchCopy()
    {
        StaTestRunner.Run(() =>
        {
            using var panel = new EquipmentPanel();
            Assert.Equal("Tunique: —", panel.TunicLabelTextForTest);
            Assert.Equal("Porter la tunique", panel.TunicButtonTextForTest);
            var worn = false;
            panel.LocalTunicChanged += value => worn = value;
            panel.ClickToggleTunicForTest();
            Assert.True(worn);
            Assert.Equal("Tunique: portée (local)", panel.TunicLabelTextForTest);
            Assert.Equal("Retirer la tunique", panel.TunicButtonTextForTest);
            panel.ResetLocalTunic();
            Assert.Equal("Tunique: —", panel.TunicLabelTextForTest);
            Assert.Equal("Porter la tunique", panel.TunicButtonTextForTest);
            Assert.Equal("Casque: —", panel.HeadwearLabelTextForTest);
        });
    }

    private static int CountReplaced(Bitmap before, Bitmap after, Color marker)
    {
        var n = 0;
        var argb = marker.ToArgb();
        for (var y = 0; y < before.Height; y++)
        {
            for (var x = 0; x < before.Width; x++)
            {
                if (before.GetPixel(x, y).A == 255 && after.GetPixel(x, y).ToArgb() == argb)
                {
                    n++;
                }
            }
        }

        return n;
    }

    private static bool Contains(Bitmap bitmap, Color marker)
    {
        var argb = marker.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == argb)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Map CreateTwoByTwoGround()
    {
        var map = new Map { Name = "Paperdoll", Width = 2, Height = 2 };
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
