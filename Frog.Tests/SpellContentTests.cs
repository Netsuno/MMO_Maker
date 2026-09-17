using System;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class SpellDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_AllKindsTargetsAndNonNegativeBoundaries()
    {
        foreach (var kind in Enum.GetValues<SpellKind>())
        {
            foreach (var targetType in Enum.GetValues<TargetType>())
            {
                var spell = SpellWorkspaceSessionTests.CreateDefinition(
                    "Capacité valide",
                    kind,
                    targetType,
                    manaCost: 0,
                    cooldownMs: int.MaxValue);
                Assert.True(spell.Validate(out var error));
                Assert.Null(error);
            }
        }
    }

    [Fact]
    public void Validate_Rejects_InvalidEnumsCostsPathAndDescription()
    {
        var spell = Valid();
        spell.Kind = (SpellKind)0;
        Assert.False(spell.Validate(out _));

        spell = Valid();
        spell.TargetType = (TargetType)0;
        Assert.False(spell.Validate(out _));

        spell = Valid();
        spell.ManaCost = -1;
        Assert.False(spell.Validate(out _));

        spell = Valid();
        spell.CooldownMs = -1;
        Assert.False(spell.Validate(out _));

        spell = Valid();
        spell.IconLogicalPath = "../outside.png";
        Assert.False(spell.Validate(out _));

        spell = Valid();
        spell.Description = new string('x', SpellDefinition.MaxDescriptionLength + 1);
        Assert.False(spell.Validate(out _));
    }

    private static SpellDefinition Valid() => SpellWorkspaceSessionTests.CreateDefinition(
        "Boule de feu",
        SpellKind.Spell,
        TargetType.SingleEnemy,
        manaCost: 20,
        cooldownMs: 1500);
}

public sealed class SpellWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_Duplicate_SearchAndFilter_RoundTrip()
    {
        var repository = new InMemorySpellRepository();
        var session = new SpellWorkspaceSession(repository);
        session.AdoptNewDraft(CreateDefinition(
            "Boule de feu majeure",
            SpellKind.Spell,
            TargetType.SingleEnemy,
            manaCost: 25,
            cooldownMs: 1800));

        var saved = Assert.IsType<SaveSpellResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Description = "Inflige des dégâts de feu.";
        session.MarkDirty();
        var published = Assert.IsType<SaveSpellResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var snapshot = await repository.LoadPublishedByIdAsync(saved.SpellId);
        Assert.Equal("Inflige des dégâts de feu.", snapshot!.Definition.Description);
        Assert.Equal(25, snapshot.Definition.ManaCost);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);

        session.SearchFilter = "Boule";
        session.StatusFilter = ContentPublishStatus.Published;
        await session.RefreshCatalogAsync();
        var entry = Assert.Single(session.Catalog);
        Assert.Equal(saved.SpellId, entry.SpellId);
    }

    [Fact]
    public async Task DraftDistinctFromPublished_StaleRevisionConflicts_AndDeleteWorks()
    {
        var repository = new InMemorySpellRepository();
        var definition = CreateDefinition(
            "Frappe v1",
            SpellKind.Skill,
            TargetType.SingleEnemy,
            manaCost: 0,
            cooldownMs: 500);
        var published = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        definition.Name = "Frappe v2 brouillon";
        Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(new SaveSpellRequest
        {
            SpellId = published.SpellId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Equal(
            "Frappe v2 brouillon",
            (await repository.LoadByIdAsync(published.SpellId))!.Definition.Name);
        Assert.Equal(
            "Frappe v1",
            (await repository.LoadPublishedByIdAsync(published.SpellId))!.Definition.Name);
        Assert.IsType<SaveSpellResult.Conflict>(await repository.SaveAsync(new SaveSpellRequest
        {
            SpellId = published.SpellId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.IsType<DeleteSpellResult.Success>(await repository.DeleteAsync(published.SpellId));
        Assert.Null(await repository.LoadByIdAsync(published.SpellId));
    }

    [Fact]
    public async Task InvalidSpell_CannotPublish()
    {
        var repository = new InMemorySpellRepository();
        var session = new SpellWorkspaceSession(repository);
        var invalid = CreateDefinition(
            "Recharge invalide",
            SpellKind.Skill,
            TargetType.Self,
            manaCost: 0,
            cooldownMs: -1);
        session.AdoptNewDraft(invalid);

        Assert.IsType<SaveSpellResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Empty(await repository.ListPublishedAsync());
    }

    internal static SpellDefinition CreateDefinition(
        string name,
        SpellKind kind,
        TargetType targetType,
        int manaCost,
        int cooldownMs) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        ManaCost = manaCost,
        CooldownMs = cooldownMs,
        TargetType = targetType,
        IconLogicalPath = $"icons/spells/{Guid.NewGuid():N}.png",
        Description = "Description de test",
    };
}

public sealed class PublishedSpellConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublishedDefinitions()
    {
        var repository = new InMemorySpellRepository();
        await repository.SaveAsync(new SaveSpellRequest
        {
            Definition = SpellWorkspaceSessionTests.CreateDefinition(
                "Brouillon",
                SpellKind.Skill,
                TargetType.Self,
                0,
                1000),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await repository.SaveAsync(new SaveSpellRequest
        {
            Definition = SpellWorkspaceSessionTests.CreateDefinition(
                "Soin publié",
                SpellKind.Spell,
                TargetType.SingleAlly,
                10,
                800),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedSpellConsumer(
            repository,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedSpellConsumer>());
        var loaded = await consumer.LoadPublishedAsync();

        var definition = Assert.Single(loaded);
        Assert.Equal("Soin publié", definition.Name);
        Assert.Equal(TargetType.SingleAlly, definition.TargetType);
    }
}
