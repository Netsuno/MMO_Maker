using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>Sac inventaire : filtre Arme / Armure / Objet et équipement rapide (Netsun). Protocole 11.</summary>
public sealed class InventoryBagFilterTests
{
    [Fact]
    public void Labels_AreArmeArmureObjet_AndProtocolStays11()
    {
        Assert.Equal("Arme", InventoryBagFilter.Label(InventoryBagCategory.Weapon));
        Assert.Equal("Armure", InventoryBagFilter.Label(InventoryBagCategory.Armor));
        Assert.Equal("Objet", InventoryBagFilter.Label(InventoryBagCategory.Item));
        Assert.Equal("Tout", InventoryBagFilter.Label(InventoryBagCategory.All));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
    }

    [Fact]
    public void Includes_KeepsOnlyTheChosenCategory()
    {
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.All, ItemType.Weapon));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.All, ItemType.Armor));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.All, ItemType.Consumable));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.All, null));

        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Weapon, ItemType.Weapon));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Weapon, ItemType.Armor));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Weapon, ItemType.Consumable));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Weapon, null));

        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Armor, ItemType.Armor));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Armor, ItemType.Weapon));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Armor, ItemType.Key));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Armor, null));

        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Consumable));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Key));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Quest));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Unknown));
        Assert.True(InventoryBagFilter.Includes(InventoryBagCategory.Item, null));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Weapon));
        Assert.False(InventoryBagFilter.Includes(InventoryBagCategory.Item, ItemType.Armor));
    }

    [Fact]
    public void BagSurfaces_ExposeCategoryButtons_AndQuickEquip()
    {
        var root = RepoRoot();
        var inventory = File.ReadAllText(Path.Combine(root, "Frog.Client", "Controls", "InventoryPanel.cs"));
        var sheet = File.ReadAllText(Path.Combine(root, "Frog.Client", "Controls", "CharacterSheetPanel.cs"));
        var buttons = File.ReadAllText(Path.Combine(root, "Frog.Client", "Controls", "InventoryBagCategoryButtons.cs"));
        var help = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "HelpForm.cs"));
        var filter = File.ReadAllText(Path.Combine(root, "Frog.Core", "Gameplay", "InventoryBagFilter.cs"));

        Assert.Contains("InventoryBagCategoryButtons", inventory, StringComparison.Ordinal);
        Assert.Contains("InventoryBagFilter.Includes", inventory, StringComparison.Ordinal);
        Assert.Contains("_list.DoubleClick", inventory, StringComparison.Ordinal);
        Assert.Contains("InventoryBagCategoryButtons", sheet, StringComparison.Ordinal);
        Assert.Contains("InventoryBagFilter.Includes", sheet, StringComparison.Ordinal);
        Assert.Contains("_bagList.DoubleClick", sheet, StringComparison.Ordinal);
        Assert.Contains("AccessibleName = \"Filtre \"", buttons, StringComparison.Ordinal);
        Assert.Contains("InventoryBagCategory.All", buttons, StringComparison.Ordinal);
        Assert.Contains("Arme, Armure et Objet filtrent l'inventaire et le sac", help, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", filter, StringComparison.Ordinal);
        Assert.Contains("Hello reste 11", filter, StringComparison.Ordinal);
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
