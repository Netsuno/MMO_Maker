using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.Controls;
using Frog.Client.Models;
using Frog.Client.Services;
using Frog.Client.UI;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Fiche perso open/close and paperdoll slot labels (Netsun).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class CharacterSheetSmokeTests
{
    private static readonly Color Tunic = Color.FromArgb(255, 186, 122, 64);
    private static readonly Color Hat = Color.FromArgb(255, 196, 48, 72);
    private static readonly Color Armor = Color.FromArgb(255, 42, 138, 78);
    private static readonly Color Blade = Color.FromArgb(255, 214, 224, 232);

    [Fact]
    public void CharacterSheet_OpensAndCloses_FromMenuAndKey()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-fiche-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.LayoutGameHudForTest();
                Assert.False(form.WindowLayerVisibleForTest);
                Assert.Contains("Fiche", form.GameplayTabsForTest.TabPages.Cast<TabPage>().Select(p => p.Text));

                form.PressCharacterSheetKeyForTest();
                Assert.False(form.WindowLayerVisibleForTest, "C does nothing before play");

                form.InvokeHudMenuCommandForTest(HudMenuCommand.Character);
                Assert.True(form.WindowLayerVisibleForTest, "Perso opens the sheet");
                Assert.True(form.IsCharacterSheetTabSelectedForTest);
                Assert.Equal("Fiche perso", form.WindowChromeForTest.Title);
                Assert.Equal(
                    new[] { "Corps", "Tunique", "Armure", "Tête", "Casque", "Arme" },
                    form.CharacterSheetForTest.SlotLabelsForTest.ToArray());
                Assert.Equal("de base", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Body));
                Assert.Equal("de base", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Head));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Tunic));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Armor));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Hat));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Weapon));
                Assert.True(form.CharacterSheetForTest.SlotOccupiedForTest(PaperdollLayer.Body));
                Assert.True(form.CharacterSheetForTest.SlotOccupiedForTest(PaperdollLayer.Head));
                Assert.False(form.CharacterSheetForTest.SlotOccupiedForTest(PaperdollLayer.Tunic));

                form.InvokeHudMenuCommandForTest(HudMenuCommand.Character);
                Assert.False(form.WindowLayerVisibleForTest, "Perso again closes the sheet");

                form.InvokeHudMenuCommandForTest(HudMenuCommand.Inventory);
                Assert.True(form.WindowLayerVisibleForTest);
                Assert.Equal("Inventaire", form.WindowChromeForTest.Title);
                form.InvokeHudMenuCommandForTest(HudMenuCommand.Character);
                Assert.True(form.WindowLayerVisibleForTest, "Perso switches from Inventaire");
                Assert.Equal("Fiche perso", form.WindowChromeForTest.Title);
                Assert.True(form.IsCharacterSheetTabSelectedForTest);
                form.InvokeHudMenuCommandForTest(HudMenuCommand.Character);
                Assert.False(form.WindowLayerVisibleForTest);

                form.ShowPlayingHudForTest();
                form.ActiveControl = form.ChatTextBoxForTest;
                Assert.True(InputService.IsTextInputFocus(form.ActiveControl), "chat must count as text focus");
                form.PressCharacterSheetKeyForTest();
                Assert.False(form.WindowLayerVisibleForTest, "C in chat does not open the sheet");

                form.ActiveControl = form.MapScrollForTest;
                Assert.False(InputService.IsTextInputFocus(form.ActiveControl));
                form.PressCharacterSheetKeyForTest();
                Assert.True(form.WindowLayerVisibleForTest, "C opens the sheet in play");
                Assert.Equal("Fiche perso", form.WindowChromeForTest.Title);
                Assert.Equal("Netsun · Niv —", form.CharacterSheetForTest.IdentityTextForTest);
                form.PressCharacterSheetKeyForTest();
                Assert.False(form.WindowLayerVisibleForTest, "C closes the sheet");

                form.WindowChromeForTest.CloseButtonForTest.PerformClick();
                form.InvokeHudMenuCommandForTest(HudMenuCommand.Character);
                Assert.True(form.WindowLayerVisibleForTest);
                form.WindowChromeForTest.CloseButtonForTest.PerformClick();
                Assert.False(form.WindowLayerVisibleForTest, "red X closes the sheet");
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }

    [Fact]
    public void CharacterSheet_SlotsFollowLocalPaperdoll_AndPreviewUsesLayers()
    {
        StaTestRunner.Run(() =>
        {
            using var sheet = new CharacterSheetPanel();
            var bare = sheet.RenderPreviewForTest();
            try
            {
                Assert.False(Contains(bare, Tunic));
                Assert.False(Contains(bare, Hat));
                Assert.False(Contains(bare, Armor));
                Assert.False(Contains(bare, Blade));
            }
            finally
            {
                bare.Dispose();
            }

            var weapon = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
            var armor = Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
            sheet.ApplyLoadout(
                new Equipment(weapon, armor, Equipment.LocalHeadwearItemId, Equipment.LocalTunicItemId),
                id => id == weapon ? "Épée" : id == armor ? "Cuirasse" : id.ToString("N"),
                "Netsun",
                4);

            Assert.Equal("Netsun · Niv 4", sheet.IdentityTextForTest);
            Assert.Equal("de base", sheet.SlotDetailForTest(PaperdollLayer.Body));
            Assert.Equal("portée", sheet.SlotDetailForTest(PaperdollLayer.Tunic));
            Assert.Equal("Cuirasse", sheet.SlotDetailForTest(PaperdollLayer.Armor));
            Assert.Equal("de base", sheet.SlotDetailForTest(PaperdollLayer.Head));
            Assert.Equal("porté", sheet.SlotDetailForTest(PaperdollLayer.Hat));
            Assert.Equal("Épée", sheet.SlotDetailForTest(PaperdollLayer.Weapon));
            Assert.True(sheet.SlotOccupiedForTest(PaperdollLayer.Tunic));
            Assert.True(sheet.SlotOccupiedForTest(PaperdollLayer.Hat));
            Assert.True(sheet.SlotOccupiedForTest(PaperdollLayer.Weapon));
            Assert.True(sheet.SlotOccupiedForTest(PaperdollLayer.Armor));

            var geared = sheet.RenderPreviewForTest();
            try
            {
                Assert.True(Contains(geared, Tunic), "preview composites the tunic");
                Assert.True(Contains(geared, Armor), "preview composites the armor");
                Assert.True(Contains(geared, Hat), "preview composites the hat");
                Assert.True(Contains(geared, Blade), "preview composites the weapon");
            }
            finally
            {
                geared.Dispose();
            }

            var hatIcon = PlayerWorldAssets.LayerIcon(PlayerSpriteSlot.Hat);
            var tunicIcon = PlayerWorldAssets.LayerIcon(PlayerSpriteSlot.Tunic);
            var armorIcon = PlayerWorldAssets.LayerIcon(PlayerSpriteSlot.Armor);
            var weaponIcon = PlayerWorldAssets.LayerIcon(PlayerSpriteSlot.Weapon);
            Assert.True(Contains(hatIcon, Hat));
            Assert.False(Contains(hatIcon, Tunic));
            Assert.False(Contains(hatIcon, Blade));
            Assert.True(Contains(tunicIcon, Tunic));
            Assert.False(Contains(tunicIcon, Armor));
            Assert.True(Contains(armorIcon, Armor));
            Assert.False(Contains(armorIcon, Hat));
            Assert.True(Contains(weaponIcon, Blade));
            Assert.False(Contains(weaponIcon, Hat));
            Assert.Equal(PlayerWorldAssets.NativeSize, hatIcon.Width);
            Assert.Equal(PlayerWorldAssets.NativeSize, hatIcon.Height);

            sheet.ApplyLoadout(Equipment.Empty, null, null, null);
            Assert.Equal("— · Niv —", sheet.IdentityTextForTest);
            Assert.Equal("—", sheet.SlotDetailForTest(PaperdollLayer.Tunic));
            Assert.Equal("—", sheet.SlotDetailForTest(PaperdollLayer.Weapon));
            Assert.False(sheet.SlotOccupiedForTest(PaperdollLayer.Weapon));
        });
    }

    [Fact]
    public void CharacterSheet_SlotClick_TogglesLocalTunicOnTheShell()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-fiche-click-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                var weapon = Guid.Parse("cccccccc-dddd-4eee-8fff-000000000001");
                form.OnInventorySnapshotForTest(new InventorySnapshotWire { EquippedWeaponItemId = weapon });
                Assert.Equal(weapon.ToString("N")[..8], form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Weapon));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Armor));

                form.CharacterSheetForTest.ClickSlotForTest(PaperdollLayer.Tunic);
                Assert.Equal("portée", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Tunic));
                Assert.Equal("Tunique: portée (local)", form.EquipmentPanelForTest.TunicLabelTextForTest);
                form.CharacterSheetForTest.ClickSlotForTest(PaperdollLayer.Hat);
                Assert.Equal("porté", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Hat));
                Assert.Equal("Casque: porté (local)", form.EquipmentPanelForTest.HeadwearLabelTextForTest);

                form.CharacterSheetForTest.ClickSlotForTest(PaperdollLayer.Tunic);
                form.CharacterSheetForTest.ClickSlotForTest(PaperdollLayer.Hat);
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Tunic));
                Assert.Equal("—", form.CharacterSheetForTest.SlotDetailForTest(PaperdollLayer.Hat));
                Assert.Equal("Tunique: —", form.EquipmentPanelForTest.TunicLabelTextForTest);
                Assert.Equal("Casque: —", form.EquipmentPanelForTest.HeadwearLabelTextForTest);
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }

    [Fact]
    public void CharacterSheet_BagEquipAndSlotClick_RaiseServerGearCommands()
    {
        StaTestRunner.Run(() =>
        {
            using var host = new Form { Size = new Size(420, 640) };
            var sheet = new CharacterSheetPanel { Dock = DockStyle.Fill };
            host.Controls.Add(sheet);
            host.Show();
            byte? equipped = null;
            EquipmentSlotKind? unequipped = null;
            var tunic = false;
            sheet.EquipRequested += slot => equipped = slot;
            sheet.UnequipRequested += slot => unequipped = slot;
            sheet.ToggleTunicRequested += () => tunic = true;

            var weapon = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
            var armor = Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");
            var potion = Guid.Parse("cccccccc-dddd-4eee-8fff-111111111111");
            sheet.ApplyBag(
                new InventorySnapshotWire
                {
                    Slots =
                    [
                        new InventorySlotWire { SlotIndex = 0, ItemId = potion, Quantity = 3 },
                        new InventorySlotWire { SlotIndex = 2, ItemId = weapon, Quantity = 1 },
                        new InventorySlotWire { SlotIndex = 4, ItemId = armor, Quantity = 1 },
                    ],
                },
                id => id == weapon ? "Épée" : id == armor ? "Cuirasse" : "Potion",
                id => id == weapon ? ItemType.Weapon : id == armor ? ItemType.Armor : ItemType.Consumable);

            Assert.Equal(3, sheet.BagCountForTest);
            Assert.Equal("[0] Potion ×3", sheet.BagTextAtForTest(0));
            Assert.Equal("[2] Épée · Arme ×1", sheet.BagTextAtForTest(1));
            Assert.Equal("[4] Cuirasse · Armure ×1", sheet.BagTextAtForTest(2));
            Assert.False(sheet.EquipBagEnabledForTest);

            equipped = null;
            sheet.SelectBagBySlotForTest(2);
            Assert.True(sheet.EquipBagEnabledForTest);
            sheet.ClickEquipBagForTest();
            Assert.Equal((byte)2, equipped);

            equipped = null;
            sheet.SelectBagBySlotForTest(4);
            Assert.True(sheet.EquipBagEnabledForTest);
            sheet.ClickEquipBagForTest();
            Assert.Equal((byte)4, equipped);

            equipped = null;
            sheet.ClearBagSelectionForTest();
            Assert.False(sheet.EquipBagEnabledForTest);
            sheet.ClickSlotForTest(PaperdollLayer.Weapon);
            Assert.Equal((byte)2, equipped);
            Assert.Null(unequipped);

            sheet.ApplyLoadout(
                new Equipment(weapon, null),
                id => id == weapon ? "Épée" : id.ToString("N"),
                "Netsun",
                2);
            equipped = null;
            sheet.ClickSlotForTest(PaperdollLayer.Weapon);
            Assert.Equal(EquipmentSlotKind.Weapon, unequipped);
            Assert.Null(equipped);

            unequipped = null;
            sheet.SelectBagBySlotForTest(4);
            sheet.ClickSlotForTest(PaperdollLayer.Armor);
            Assert.Equal((byte)4, equipped);
            Assert.Null(unequipped);

            equipped = null;
            sheet.ClickSlotForTest(PaperdollLayer.Tunic);
            Assert.True(tunic);
            Assert.Null(equipped);
            Assert.Null(unequipped);
            host.Close();
        });
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
}
