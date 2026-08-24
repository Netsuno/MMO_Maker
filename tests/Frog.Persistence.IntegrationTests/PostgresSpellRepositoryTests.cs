using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresSpellRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresSpellRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_InvalidPublish_Rollback()
    {
        using var gate = CreateGate();
        var repository = new PostgresSpellRepository(gate);
        var definition = CreateDefinition(
            "Boule de feu PG",
            SpellKind.Spell,
            TargetType.SingleEnemy,
            manaCost: 25,
            cooldownMs: 1800);

        var created = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.Equal(1, created.NewRevision);

        definition.Name = "Boule de feu publiée PG";
        definition.Description = "Inflige des dégâts de feu.";
        var published = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                SpellId = created.SpellId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        using var gate2 = CreateGate();
        var repository2 = new PostgresSpellRepository(gate2);
        var draft = await repository2.LoadByIdAsync(created.SpellId);
        var snapshot = await repository2.LoadPublishedByIdAsync(created.SpellId);
        Assert.NotNull(draft);
        Assert.NotNull(snapshot);
        AssertDefinitionEqual(definition, draft!.Definition);
        AssertDefinitionEqual(definition, snapshot!.Definition);

        draft.Definition.Name = "Boule de feu brouillon PG";
        draft.Definition.ManaCost = 30;
        Assert.IsType<SaveSpellResult.Success>(await repository2.SaveAsync(new SaveSpellRequest
        {
            SpellId = created.SpellId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        using var gate3 = CreateGate();
        var repository3 = new PostgresSpellRepository(gate3);
        Assert.Equal(
            "Boule de feu brouillon PG",
            (await repository3.LoadByIdAsync(created.SpellId))!.Definition.Name);
        Assert.Equal(
            "Boule de feu publiée PG",
            (await repository3.LoadPublishedByIdAsync(created.SpellId))!.Definition.Name);
        Assert.IsType<SaveSpellResult.Conflict>(await repository3.SaveAsync(new SaveSpellRequest
        {
            SpellId = created.SpellId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var invalid = CreateDefinition(
            "Recharge invalide PG",
            SpellKind.Skill,
            TargetType.Self,
            manaCost: 0,
            cooldownMs: -1);
        Assert.IsType<SaveSpellResult.ValidationFailed>(await repository3.SaveAsync(
            new SaveSpellRequest
            {
                Definition = invalid,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        using var gate4 = CreateGate();
        var failing = new PostgresSpellRepository(gate4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failed = await failing.SaveAsync(new SaveSpellRequest
        {
            Definition = CreateDefinition(
                "Sort rollback PG",
                SpellKind.Spell,
                TargetType.AoE,
                manaCost: 40,
                cooldownMs: 3000),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveSpellResult.PersistenceFailed>(failed);

        using var gate5 = CreateGate();
        var afterRepository = new PostgresSpellRepository(gate5);
        Assert.Empty(await afterRepository.ListSummariesAsync(search: "Sort rollback PG"));
        Assert.Equal(before.Count, (await afterRepository.ListSummariesAsync()).Count);
        Assert.Contains(
            await afterRepository.ListPublishedAsync(),
            spell => spell.Id == created.SpellId && spell.Name == "Boule de feu publiée PG");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_StatusFilter_AndDelete()
    {
        using var gate = CreateGate();
        var repository = new PostgresSpellRepository(gate);
        var published = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = CreateDefinition(
                    "Soin filtrable PG",
                    SpellKind.Spell,
                    TargetType.SingleAlly,
                    manaCost: 12,
                    cooldownMs: 900),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        var draft = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = CreateDefinition(
                    "Esquive filtrable PG",
                    SpellKind.Skill,
                    TargetType.Self,
                    manaCost: 0,
                    cooldownMs: 1200),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));

        var byPath = await repository.ListSummariesAsync(search: "spells/");
        Assert.Contains(byPath, entry => entry.SpellId == published.SpellId);
        var publishedOnly = await repository.ListSummariesAsync(
            search: "filtrable PG",
            statusFilter: ContentPublishStatus.Published);
        var publishedEntry = Assert.Single(
            publishedOnly,
            entry => entry.SpellId == published.SpellId);
        Assert.Equal(TargetType.SingleAlly, publishedEntry.TargetType);
        Assert.DoesNotContain(publishedOnly, entry => entry.SpellId == draft.SpellId);

        Assert.IsType<DeleteSpellResult.Success>(await repository.DeleteAsync(draft.SpellId));
        Assert.Null(await repository.LoadByIdAsync(draft.SpellId));
        Assert.IsType<DeleteSpellResult.NotFound>(await repository.DeleteAsync(draft.SpellId));
    }

    private FrogDbContextGate CreateGate()
        => new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static SpellDefinition CreateDefinition(
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
        Description = "Description PG",
    };

    private static void AssertDefinitionEqual(SpellDefinition expected, SpellDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.ManaCost, actual.ManaCost);
        Assert.Equal(expected.CooldownMs, actual.CooldownMs);
        Assert.Equal(expected.TargetType, actual.TargetType);
        Assert.Equal(expected.IconLogicalPath, actual.IconLogicalPath);
        Assert.Equal(expected.Description, actual.Description);
    }
}
