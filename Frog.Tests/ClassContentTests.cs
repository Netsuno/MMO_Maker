using System;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Enums;
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
