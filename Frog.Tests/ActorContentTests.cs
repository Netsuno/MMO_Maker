using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Character;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue héros (Données de jeu). Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class ActorContentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndHeroesCategoryExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(CharacterStatsWire.MinStat, ActorDefinition.MinStat);
        Assert.Equal(CharacterStatsWire.MaxStat, ActorDefinition.MaxStat);
        Assert.Equal(EquipmentSlotKind.Weapon, (EquipmentSlotKind)1);
        Assert.Equal(EquipmentSlotKind.Armor, (EquipmentSlotKind)2);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Héros\"", form, StringComparison.Ordinal);
        Assert.Contains("ActorEditorPanel", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsBadFaceLookStatsAndEmptyIds()
    {
        var actor = Sample("Visage");
        actor.FaceLogicalPath = @"faces\hero.png";
        Assert.False(actor.Validate(out _));

        actor = Sample("Look");
        actor.Body = CharacterLook.BodyCount;
        Assert.False(actor.Validate(out _));

        actor = Sample("Stat");
        actor.Luck = ActorDefinition.MaxStat + 1;
        Assert.False(actor.Validate(out _));

        actor = Sample("Classe vide");
        actor.ClassId = Guid.Empty;
        Assert.False(actor.Validate(out _));

        actor = Sample("Même objet");
        var same = Guid.NewGuid();
        actor.StartingWeaponItemId = same;
        actor.StartingArmorItemId = same;
        Assert.False(actor.Validate(out _));
    }

    [Fact]
    public async Task SaveDraft_Publish_RoundTrip_KeepsClassLookEquipAndStats()
    {
        var catalog = await CreateCatalogAsync();
        var actors = catalog.Actors;
        var classId = catalog.ClassId;
        var weaponId = catalog.WeaponId;
        var armorId = catalog.ArmorId;
        var session = new ActorWorkspaceSession(actors);
        var definition = Sample("Aude");
        definition.ClassId = classId;
        definition.FaceLogicalPath = "faces/heros/aude.png";
        definition.Body = 1;
        definition.Hair = 2;
        definition.Tunic = 1;
        definition.StartingWeaponItemId = weaponId;
        definition.StartingArmorItemId = armorId;
        definition.Description = "Héros de départ.";
        definition.Str = 14;
        session.AdoptNewDraft(definition);

        var saved = Assert.IsType<SaveActorResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        var published = Assert.IsType<SaveActorResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);

        session.Current!.Description = "Brouillon après publication.";
        session.Current.Str = 18;
        session.MarkDirty();
        Assert.IsType<SaveActorResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));

        var draft = (await actors.LoadByIdAsync(saved.ActorId))!.Definition;
        var snapshot = (await actors.LoadPublishedByIdAsync(saved.ActorId))!.Definition;
        Assert.Equal("Brouillon après publication.", draft.Description);
        Assert.Equal(18, draft.Str);
        Assert.Equal("Héros de départ.", snapshot.Description);
        Assert.Equal(14, snapshot.Str);
        Assert.Equal(classId, snapshot.ClassId);
        Assert.Equal("faces/heros/aude.png", snapshot.FaceLogicalPath);
        Assert.Equal(1, snapshot.Body);
        Assert.Equal(2, snapshot.Hair);
        Assert.Equal(1, snapshot.Tunic);
        Assert.Equal(weaponId, snapshot.StartingWeaponItemId);
        Assert.Equal(armorId, snapshot.StartingArmorItemId);
        Assert.Equal(new CharacterLook(1, 2, 1), snapshot.ToLook());

        Assert.IsType<SaveActorResult.Conflict>(await actors.SaveAsync(new SaveActorRequest
        {
            ActorId = saved.ActorId,
            Definition = Sample("Conflit"),
            ExpectedRevision = 1,
        }));

        session.SearchFilter = "Aude";
        session.StatusFilter = ContentPublishStatus.Draft;
        await session.RefreshCatalogAsync();
        Assert.Equal(saved.ActorId, Assert.Single(session.Catalog).ActorId);

        session.DuplicateCurrent();
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);
        Assert.Equal(classId, session.Current.ClassId);
        Assert.Equal(weaponId, session.Current.StartingWeaponItemId);

        Assert.True(await session.OpenAsync(saved.ActorId));
        Assert.IsType<DeleteActorResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await actors.LoadByIdAsync(saved.ActorId));
        Assert.Empty(await actors.ListPublishedAsync());
    }

    [Fact]
    public async Task Save_RejectsUnpublishedClassAndWrongItemKind()
    {
        var spells = new InMemorySpellRepository();
        var classes = new InMemoryClassRepository(spells);
        var items = new InMemoryItemRepository();
        var actors = new InMemoryActorRepository(classes, items);

        var missingClass = Sample("Sans classe");
        missingClass.ClassId = Guid.NewGuid();
        Assert.IsType<SaveActorResult.ValidationFailed>(await actors.SaveAsync(Draft(missingClass)));

        var draftClass = ClassWorkspaceSessionTests.CreateDefinition("Brouillon");
        var classSaved = Assert.IsType<SaveClassResult.Success>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = draftClass,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        missingClass.ClassId = classSaved.ClassId;
        Assert.IsType<SaveActorResult.ValidationFailed>(await actors.SaveAsync(Draft(missingClass)));

        var potion = Item("Potion", ItemType.Consumable);
        var potionSaved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = potion,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
        var hero = Sample("Mauvaise arme");
        hero.StartingWeaponItemId = potionSaved.ItemId;
        Assert.IsType<SaveActorResult.ValidationFailed>(await actors.SaveAsync(Draft(hero)));
    }

    [Fact]
    public async Task Delete_BlocksClassAndItemStillUsedAsStartingGear()
    {
        var catalog = await CreateCatalogAsync();
        var hero = Sample("Lié");
        hero.ClassId = catalog.ClassId;
        hero.StartingWeaponItemId = catalog.WeaponId;
        hero.StartingArmorItemId = catalog.ArmorId;
        Assert.IsType<SaveActorResult.Success>(await catalog.Actors.SaveAsync(new SaveActorRequest
        {
            Definition = hero,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        Assert.IsType<DeleteClassResult.Referenced>(await catalog.Classes.DeleteAsync(catalog.ClassId));
        Assert.IsType<DeleteItemResult.Referenced>(await catalog.Items.DeleteAsync(catalog.WeaponId));
        Assert.IsType<DeleteItemResult.Referenced>(await catalog.Items.DeleteAsync(catalog.ArmorId));
    }

    private static async Task<Catalog> CreateCatalogAsync()
    {
        var spells = new InMemorySpellRepository();
        var classes = new InMemoryClassRepository(spells);
        var items = new InMemoryItemRepository();
        var actors = new InMemoryActorRepository(classes, items);
        var classId = Assert.IsType<SaveClassResult.Success>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = ClassWorkspaceSessionTests.CreateDefinition("Chevalier"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        })).ClassId;
        var weaponId = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = Item("Épée"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        })).ItemId;
        var armorId = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = Item("Cotte", ItemType.Armor),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        })).ItemId;
        return new Catalog(actors, classes, items, classId, weaponId, armorId);
    }

    private sealed record Catalog(
        InMemoryActorRepository Actors,
        InMemoryClassRepository Classes,
        InMemoryItemRepository Items,
        Guid ClassId,
        Guid WeaponId,
        Guid ArmorId);

    private static SaveActorRequest Draft(ActorDefinition definition) => new()
    {
        Definition = definition,
        ExpectedRevision = 0,
        Intent = SaveContentIntent.SaveDraft,
    };

    private static ActorDefinition Sample(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BaseHp = 100,
        BaseMp = 40,
        Str = 10,
        Agi = 10,
        Vit = 10,
        Int = 10,
        Dex = 10,
        Luck = 10,
    };

    private static ItemDefinition Item(string name, ItemType kind = ItemType.Weapon) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
        MaxStack = 1,
        BuyPrice = 10,
        SellPrice = 5,
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
