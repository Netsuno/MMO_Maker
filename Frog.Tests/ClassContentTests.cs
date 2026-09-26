using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class ClassDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_ResourceAndStatBoundaries()
    {
        var minimum = ClassWorkspaceSessionTests.CreateDefinition("Minimum");
        minimum.BaseHp = 1;
        minimum.BaseMp = 1;
        minimum.Str = minimum.Agi = minimum.Vit = minimum.Int = minimum.Dex = minimum.Luck = 1;
        Assert.True(minimum.Validate(out var minimumError));
        Assert.Null(minimumError);

        var maximum = ClassWorkspaceSessionTests.CreateDefinition("Maximum");
        maximum.Str = maximum.Agi = maximum.Vit = maximum.Int = maximum.Dex = maximum.Luck = 99;
        Assert.True(maximum.Validate(out var maximumError));
        Assert.Null(maximumError);
    }

    [Fact]
    public void Validate_Rejects_InvalidResourcesStatsDescriptionAndEmptySpellId()
    {
        var definition = ClassWorkspaceSessionTests.CreateDefinition("PV invalides");
        definition.BaseHp = 0;
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("PM invalides");
        definition.BaseMp = -1;
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("Stat invalide");
        definition.Luck = 100;
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("Description invalide");
        definition.Description = new string('x', ClassDefinition.MaxDescriptionLength + 1);
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("Sort invalide");
        definition.StartingSpellId = Guid.Empty;
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("Arme vide");
        definition.DefaultWeaponItemId = Guid.Empty;
        Assert.False(definition.Validate(out _));

        definition = ClassWorkspaceSessionTests.CreateDefinition("Même objet");
        var same = Guid.NewGuid();
        definition.DefaultWeaponItemId = same;
        definition.DefaultArmorItemId = same;
        Assert.False(definition.Validate(out _));
    }
}

public sealed class ClassWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_DraftDistinct_Conflict_SearchDuplicateAndDelete()
    {
        var spells = new InMemorySpellRepository();
        var spellId = await PublishSpellAsync(spells, "Frappe héroïque");
        var repository = new InMemoryClassRepository(spells);
        var session = new ClassWorkspaceSession(repository);
        session.AdoptNewDraft(CreateDefinition("Guerrier", spellId));

        var saved = Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Description = "Classe robuste publiée.";
        session.MarkDirty();
        var published = Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        session.Current.Description = "Modification brouillon.";
        session.MarkDirty();
        Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(
            "Modification brouillon.",
            (await repository.LoadByIdAsync(saved.ClassId))!.Definition.Description);
        Assert.Equal(
            "Classe robuste publiée.",
            (await repository.LoadPublishedByIdAsync(saved.ClassId))!.Definition.Description);

        Assert.IsType<SaveClassResult.Conflict>(await repository.SaveAsync(new SaveClassRequest
        {
            ClassId = saved.ClassId,
            Definition = CreateDefinition("Conflit", spellId),
            ExpectedRevision = 1,
        }));

        session.SearchFilter = "Guerr";
        session.StatusFilter = ContentPublishStatus.Draft;
        await session.RefreshCatalogAsync();
        Assert.Equal(saved.ClassId, Assert.Single(session.Catalog).ClassId);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);

        Assert.True(await session.OpenAsync(saved.ClassId));
        Assert.IsType<DeleteClassResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(saved.ClassId));
    }

    [Fact]
    public async Task StartingSpell_MustExistInPublishedCatalog_OnDraftAndPublish()
    {
        var spells = new InMemorySpellRepository();
        var repository = new InMemoryClassRepository(spells);
        var missingId = Guid.NewGuid();
        var definition = CreateDefinition("Mage invalide", missingId);

        Assert.IsType<SaveClassResult.ValidationFailed>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.IsType<SaveClassResult.ValidationFailed>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        var draftSpell = CreateSpell("Éclair brouillon");
        var spellSaved = Assert.IsType<SaveSpellResult.Success>(await spells.SaveAsync(
            new SaveSpellRequest
            {
                Definition = draftSpell,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        definition.StartingSpellId = spellSaved.SpellId;
        Assert.IsType<SaveClassResult.ValidationFailed>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        Assert.IsType<SaveSpellResult.Success>(await spells.SaveAsync(new SaveSpellRequest
        {
            SpellId = spellSaved.SpellId,
            Definition = draftSpell,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.IsType<SaveClassResult.Success>(await repository.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
    }

    internal static ClassDefinition CreateDefinition(string name, Guid? startingSpellId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description de classe",
        BaseHp = 100,
        BaseMp = 50,
        Str = 12,
        Agi = 10,
        Vit = 13,
        Int = 8,
        Dex = 11,
        Luck = 7,
        StartingSpellId = startingSpellId,
    };

    internal static async Task<Guid> PublishSpellAsync(InMemorySpellRepository repository, string name)
    {
        var result = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = CreateSpell(name),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        return result.SpellId;
    }

    private static SpellDefinition CreateSpell(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = SpellKind.Skill,
        ManaCost = 0,
        CooldownMs = 500,
        TargetType = TargetType.Self,
        IconLogicalPath = $"icons/spells/{Guid.NewGuid():N}.png",
    };
}

public sealed class PublishedClassConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublishedDefinitions()
    {
        var spells = new InMemorySpellRepository();
        var repository = new InMemoryClassRepository(spells);
        await repository.SaveAsync(new SaveClassRequest
        {
            Definition = ClassWorkspaceSessionTests.CreateDefinition("Brouillon"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await repository.SaveAsync(new SaveClassRequest
        {
            Definition = ClassWorkspaceSessionTests.CreateDefinition("Paladin publié"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedClassConsumer(
            repository,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedClassConsumer>());
        var loaded = await consumer.LoadPublishedAsync();

        Assert.Equal("Paladin publié", Assert.Single(loaded).Name);
    }
}

/// <summary>
/// Équipement par défaut des classes. Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class ClassDefaultEquipmentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndClassSheetLabelsExist()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(EquipmentSlotKind.Weapon, (EquipmentSlotKind)1);
        Assert.Equal(EquipmentSlotKind.Armor, (EquipmentSlotKind)2);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Classes\"", form, StringComparison.Ordinal);
        Assert.Contains("Notes", form, StringComparison.Ordinal);
        Assert.Contains("Arme par défaut", form, StringComparison.Ordinal);
        Assert.Contains("Armure par défaut", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDraft_Publish_RoundTrip_KeepsDefaultEquipment_AndBlocksItemDelete()
    {
        var spells = new InMemorySpellRepository();
        var items = new InMemoryItemRepository();
        var classes = new InMemoryClassRepository(spells, items: items);
        var weaponId = await PublishItemAsync(items, "Épée de classe", ItemType.Weapon);
        var armorId = await PublishItemAsync(items, "Cotte de classe", ItemType.Armor);
        var session = new ClassWorkspaceSession(classes);
        var definition = ClassWorkspaceSessionTests.CreateDefinition("Guerrier équipé");
        definition.Description = "Notes de la classe.";
        definition.DefaultWeaponItemId = weaponId;
        definition.DefaultArmorItemId = armorId;
        session.AdoptNewDraft(definition);

        var saved = Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        var published = Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);

        session.Current!.Description = "Notes brouillon.";
        session.Current.DefaultArmorItemId = null;
        session.MarkDirty();
        Assert.IsType<SaveClassResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));

        var draft = (await classes.LoadByIdAsync(saved.ClassId))!.Definition;
        var snapshot = (await classes.LoadPublishedByIdAsync(saved.ClassId))!.Definition;
        Assert.Equal("Notes brouillon.", draft.Description);
        Assert.Equal(weaponId, draft.DefaultWeaponItemId);
        Assert.Null(draft.DefaultArmorItemId);
        Assert.Equal("Notes de la classe.", snapshot.Description);
        Assert.Equal(weaponId, snapshot.DefaultWeaponItemId);
        Assert.Equal(armorId, snapshot.DefaultArmorItemId);

        session.DuplicateCurrent();
        Assert.Equal(weaponId, session.Current!.DefaultWeaponItemId);
        Assert.Null(session.Current.DefaultArmorItemId);

        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(weaponId));
        Assert.IsType<DeleteItemResult.Referenced>(await items.DeleteAsync(armorId));

        Assert.True(await session.OpenAsync(saved.ClassId));
        Assert.IsType<DeleteClassResult.Success>(await session.DeleteCurrentAsync());
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(weaponId));
        Assert.IsType<DeleteItemResult.Success>(await items.DeleteAsync(armorId));
    }

    [Fact]
    public async Task Save_RejectsWrongItemKind_UnpublishedGear_AndMissingCatalog()
    {
        var spells = new InMemorySpellRepository();
        var items = new InMemoryItemRepository();
        var classes = new InMemoryClassRepository(spells, items: items);
        var potionId = await PublishItemAsync(items, "Potion", ItemType.Consumable);
        var definition = ClassWorkspaceSessionTests.CreateDefinition("Mauvaise arme");
        definition.DefaultWeaponItemId = potionId;
        Assert.IsType<SaveClassResult.ValidationFailed>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        definition.DefaultWeaponItemId = Guid.NewGuid();
        Assert.IsType<SaveClassResult.ValidationFailed>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var bare = new InMemoryClassRepository(spells);
        definition.DefaultWeaponItemId = potionId;
        Assert.IsType<SaveClassResult.ValidationFailed>(await bare.SaveAsync(new SaveClassRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
    }

    private static async Task<Guid> PublishItemAsync(
        InMemoryItemRepository items,
        string name,
        ItemType kind)
    {
        var saved = Assert.IsType<SaveItemResult.Success>(await items.SaveAsync(new SaveItemRequest
        {
            Definition = new ItemDefinition
            {
                Id = Guid.NewGuid(),
                Name = name,
                Kind = kind,
                IconLogicalPath = $"icons/items/{Guid.NewGuid():N}.png",
                MaxStack = 1,
                BuyPrice = 10,
                SellPrice = 4,
            },
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
        return saved.ItemId;
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

        throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }
}
