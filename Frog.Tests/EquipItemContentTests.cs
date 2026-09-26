using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Fiches Armes et Armures (Données de jeu). Même dépôt que les objets,
/// types Weapon et Armor, emplacements paperdoll déjà en jeu.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class EquipItemContentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndEquipSheetsExist()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(EquipmentSlotKind.Weapon, (EquipmentSlotKind)1);
        Assert.Equal(EquipmentSlotKind.Armor, (EquipmentSlotKind)2);
        Assert.Equal(PaperdollLayer.Armor, (PaperdollLayer)2);
        Assert.Equal(PaperdollLayer.Weapon, (PaperdollLayer)5);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "EquipItemEditorPanel.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Armes\"", form, StringComparison.Ordinal);
        Assert.Contains("\"Armures\"", form, StringComparison.Ordinal);
        Assert.Contains("EquipItemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("ItemType.Weapon", form, StringComparison.Ordinal);
        Assert.Contains("ItemType.Armor", form, StringComparison.Ordinal);
        Assert.Contains("Emplacement", panel, StringComparison.Ordinal);
        Assert.Contains("EquipPaperdollSlot", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("amber", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planche", panel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PaperdollSlots_ReuseWeaponAndArmorOnly()
    {
        Assert.True(EquipPaperdollSlot.TryResolve(ItemType.Weapon, out var weaponSlot, out var weaponLayer));
        Assert.Equal(EquipmentSlotKind.Weapon, weaponSlot);
        Assert.Equal(PaperdollLayer.Weapon, weaponLayer);
        Assert.Equal(weaponSlot, CharacterSheetGear.ServerSlot(weaponLayer));
        Assert.True(CharacterSheetGear.TypeFits(weaponSlot, ItemType.Weapon));
        Assert.False(CharacterSheetGear.TypeFits(weaponSlot, ItemType.Armor));
        Assert.Equal("Arme", EquipPaperdollSlot.French(ItemType.Weapon));

        Assert.True(EquipPaperdollSlot.TryResolve(ItemType.Armor, out var armorSlot, out var armorLayer));
        Assert.Equal(EquipmentSlotKind.Armor, armorSlot);
        Assert.Equal(PaperdollLayer.Armor, armorLayer);
        Assert.Equal(armorSlot, CharacterSheetGear.ServerSlot(armorLayer));
        Assert.True(CharacterSheetGear.TypeFits(armorSlot, ItemType.Armor));
        Assert.False(CharacterSheetGear.TypeFits(armorSlot, ItemType.Weapon));
        Assert.Equal("Armure", EquipPaperdollSlot.French(ItemType.Armor));

        foreach (var kind in new[] { ItemType.Unknown, ItemType.Consumable, ItemType.Key, ItemType.Quest })
        {
            Assert.False(EquipPaperdollSlot.TryResolve(kind, out _, out _));
            Assert.Equal("—", EquipPaperdollSlot.French(kind));
        }
    }

    [Theory]
    [InlineData(ItemType.Weapon, "Épée courte")]
    [InlineData(ItemType.Armor, "Cotte de mailles")]
    public async Task KindFilter_ListsOnlyThatEquipType_AndForcesKindOnSave(ItemType equipKind, string name)
    {
        var repository = new InMemoryItemRepository();
        Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(new SaveItemRequest
        {
            Definition = Definition("Potion", ItemType.Consumable, 20, 10, 4),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
        var otherKind = equipKind == ItemType.Weapon ? ItemType.Armor : ItemType.Weapon;
        Assert.IsType<SaveItemResult.Success>(await repository.SaveAsync(new SaveItemRequest
        {
            Definition = Definition("Autre pièce", otherKind, 1, 1, 1),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var session = new ItemWorkspaceSession(repository) { KindFilter = equipKind };
        session.AdoptNewDraft(Definition(name, ItemType.Consumable, 1, 80, 20));
        Assert.Equal(equipKind, session.Current!.Kind);
        session.Current.Description = "Forge du village.";
        session.Current.BuyPrice = 150;
        session.Current.SellPrice = 40;

        var published = Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        await session.RefreshCatalogAsync();
        var entry = Assert.Single(session.Catalog);
        Assert.Equal(name, entry.Name);
        Assert.Equal(equipKind, entry.Kind);
        Assert.Equal(150, entry.BuyPrice);
        Assert.Equal(40, entry.SellPrice);

        var stored = await repository.LoadPublishedByIdAsync(published.ItemId);
        Assert.Equal(equipKind, stored!.Definition.Kind);
        Assert.Equal("Forge du village.", stored.Definition.Description);
        Assert.Equal(150, stored.Definition.BuyPrice);
        Assert.Equal(40, stored.Definition.SellPrice);
        Assert.True(EquipPaperdollSlot.TryResolve(stored.Definition.Kind, out _, out _));

        var unfiltered = new ItemWorkspaceSession(repository);
        await unfiltered.RefreshCatalogAsync();
        Assert.Equal(3, unfiltered.Catalog.Count);

        Assert.False(await session.OpenAsync(
            unfiltered.Catalog.Single(item => item.Kind == ItemType.Consumable).ItemId));
        Assert.Equal(published.ItemId, session.CurrentId);
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTrip_AndClassReferenceBlocksDelete()
    {
        var repository = new InMemoryItemRepository();
        var session = new ItemWorkspaceSession(repository) { KindFilter = ItemType.Weapon };
        session.AdoptNewDraft(Definition("Frappe", ItemType.Weapon, 1, 0, 0));
        var draft = Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.SellPrice = 12;
        session.MarkDirty();
        var published = Assert.IsType<SaveItemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.ItemId, published.ItemId);
        Assert.Equal(12, (await repository.LoadPublishedByIdAsync(published.ItemId))!.Definition.SellPrice);

        session.DuplicateCurrent();
        Assert.Equal(ItemType.Weapon, session.Current!.Kind);
        Assert.Contains("(copie)", session.Current.Name, StringComparison.Ordinal);
        Assert.IsType<SaveItemResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        var classes = new InMemoryClassRepository(new InMemorySpellRepository(), items: repository);
        var definition = ClassWorkspaceSessionTests.CreateDefinition("Guerrier");
        definition.DefaultWeaponItemId = published.ItemId;
        Assert.IsType<SaveClassResult.Success>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var blocked = new ItemWorkspaceSession(repository) { KindFilter = ItemType.Weapon };
        Assert.True(await blocked.OpenAsync(published.ItemId));
        Assert.IsType<DeleteItemResult.Referenced>(await blocked.DeleteCurrentAsync());
        Assert.NotNull(await repository.LoadByIdAsync(published.ItemId));

        var armors = new ItemWorkspaceSession(repository) { KindFilter = ItemType.Armor };
        await armors.RefreshCatalogAsync();
        Assert.Empty(armors.Catalog);
        Assert.False(await armors.OpenAsync(published.ItemId));
        Assert.Null(armors.Current);
    }

    private static ItemDefinition Definition(
        string name,
        ItemType kind,
        int maxStack,
        int buyPrice,
        int sellPrice) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = maxStack,
        BuyPrice = buyPrice,
        SellPrice = sellPrice,
        Description = "Description de test",
    };

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

        throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }
}
