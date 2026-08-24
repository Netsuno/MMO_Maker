using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresClassRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresClassRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_InvalidReference_Rollback()
    {
        using var gate = CreateGate();
        var spells = new PostgresSpellRepository(gate);
        var spellId = await PublishSpellAsync(spells, "Compétence de classe PG");
        var repository = new PostgresClassRepository(gate, spells);
        var definition = CreateDefinition("Guerrier PG", spellId);

        var created = Assert.IsType<SaveClassResult.Success>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = definition,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));
        Assert.Equal(1, created.NewRevision);

        definition.Name = "Guerrier publié PG";
        definition.Description = "Classe publiée PG";
        var published = Assert.IsType<SaveClassResult.Success>(await repository.SaveAsync(
            new SaveClassRequest
            {
                ClassId = created.ClassId,
                Definition = definition,
                ExpectedRevision = 1,
                Intent = SaveContentIntent.Publish,
            }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        using var gate2 = CreateGate();
        var repository2 = new PostgresClassRepository(gate2);
        var draft = await repository2.LoadByIdAsync(created.ClassId);
        var snapshot = await repository2.LoadPublishedByIdAsync(created.ClassId);
        Assert.NotNull(draft);
        Assert.NotNull(snapshot);
        AssertDefinitionEqual(definition, draft!.Definition);
        AssertDefinitionEqual(definition, snapshot!.Definition);

        draft.Definition.Name = "Guerrier brouillon PG";
        draft.Definition.Str = 20;
        Assert.IsType<SaveClassResult.Success>(await repository2.SaveAsync(new SaveClassRequest
        {
            ClassId = created.ClassId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        using var gate3 = CreateGate();
        var repository3 = new PostgresClassRepository(gate3);
        Assert.Equal(
            "Guerrier brouillon PG",
            (await repository3.LoadByIdAsync(created.ClassId))!.Definition.Name);
        Assert.Equal(
            "Guerrier publié PG",
            (await repository3.LoadPublishedByIdAsync(created.ClassId))!.Definition.Name);
        Assert.IsType<SaveClassResult.Conflict>(await repository3.SaveAsync(new SaveClassRequest
        {
            ClassId = created.ClassId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var invalidReference = CreateDefinition("Classe référence invalide PG", Guid.NewGuid());
        Assert.IsType<SaveClassResult.ValidationFailed>(await repository3.SaveAsync(
            new SaveClassRequest
            {
                Definition = invalidReference,
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));

        using var gate4 = CreateGate();
        var failing = new PostgresClassRepository(gate4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failed = await failing.SaveAsync(new SaveClassRequest
        {
            Definition = CreateDefinition("Classe rollback PG"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveClassResult.PersistenceFailed>(failed);

        using var gate5 = CreateGate();
        var afterRepository = new PostgresClassRepository(gate5);
        Assert.Empty(await afterRepository.ListSummariesAsync(search: "Classe rollback PG"));
        Assert.Equal(before.Count, (await afterRepository.ListSummariesAsync()).Count);
        Assert.Contains(
            await afterRepository.ListPublishedAsync(),
            characterClass =>
                characterClass.Id == created.ClassId
                && characterClass.Name == "Guerrier publié PG");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_StatusFilter_Delete_AndPreventReferencedSpellDelete()
    {
        using var gate = CreateGate();
        var spells = new PostgresSpellRepository(gate);
        var spellId = await PublishSpellAsync(spells, "Sort protégé par classe PG");
        var repository = new PostgresClassRepository(gate, spells);
        var published = Assert.IsType<SaveClassResult.Success>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = CreateDefinition("Mage filtrable PG", spellId),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        var draft = Assert.IsType<SaveClassResult.Success>(await repository.SaveAsync(
            new SaveClassRequest
            {
                Definition = CreateDefinition("Voleur filtrable PG"),
                ExpectedRevision = 0,
                Intent = SaveContentIntent.SaveDraft,
            }));

        var publishedOnly = await repository.ListSummariesAsync(
            search: "filtrable PG",
            statusFilter: ContentPublishStatus.Published);
        var publishedEntry = Assert.Single(
            publishedOnly,
            entry => entry.ClassId == published.ClassId);
        Assert.Equal(spellId, publishedEntry.StartingSpellId);
        Assert.DoesNotContain(publishedOnly, entry => entry.ClassId == draft.ClassId);

        Assert.IsType<DeleteSpellResult.Referenced>(await spells.DeleteAsync(spellId));
        Assert.IsType<DeleteClassResult.Success>(await repository.DeleteAsync(published.ClassId));
        Assert.IsType<DeleteSpellResult.Success>(await spells.DeleteAsync(spellId));
        Assert.Null(await repository.LoadByIdAsync(published.ClassId));

        Assert.IsType<DeleteClassResult.Success>(await repository.DeleteAsync(draft.ClassId));
        Assert.IsType<DeleteClassResult.NotFound>(await repository.DeleteAsync(draft.ClassId));
    }

    private FrogDbContextGate CreateGate()
        => new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static async Task<Guid> PublishSpellAsync(PostgresSpellRepository repository, string name)
    {
        var published = Assert.IsType<SaveSpellResult.Success>(await repository.SaveAsync(
            new SaveSpellRequest
            {
                Definition = new SpellDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Kind = SpellKind.Skill,
                    ManaCost = 0,
                    CooldownMs = 500,
                    TargetType = TargetType.Self,
                    IconLogicalPath = $"icons/spells/{Guid.NewGuid():N}.png",
                },
                ExpectedRevision = 0,
                Intent = SaveContentIntent.Publish,
            }));
        return published.SpellId;
    }

    private static ClassDefinition CreateDefinition(
        string name,
        Guid? startingSpellId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Description classe PG",
        BaseHp = 110,
        BaseMp = 45,
        Str = 13,
        Agi = 9,
        Vit = 12,
        Int = 8,
        Dex = 10,
        Luck = 6,
        StartingSpellId = startingSpellId,
    };

    private static void AssertDefinitionEqual(ClassDefinition expected, ClassDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.BaseHp, actual.BaseHp);
        Assert.Equal(expected.BaseMp, actual.BaseMp);
        Assert.Equal(expected.Str, actual.Str);
        Assert.Equal(expected.Agi, actual.Agi);
        Assert.Equal(expected.Vit, actual.Vit);
        Assert.Equal(expected.Int, actual.Int);
        Assert.Equal(expected.Dex, actual.Dex);
        Assert.Equal(expected.Luck, actual.Luck);
        Assert.Equal(expected.StartingSpellId, actual.StartingSpellId);
    }
}
