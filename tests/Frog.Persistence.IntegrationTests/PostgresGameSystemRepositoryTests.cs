using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresGameSystemRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresGameSystemRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_Party_Conflict_AndDelete()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var actors = new PostgresActorRepository(gate);
        var heroId = Assert.IsType<SaveActorResult.Success>(await actors.SaveAsync(new SaveActorRequest
        {
            Definition = new ActorDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Héros système PG",
                BaseHp = 100,
                BaseMp = 40,
                Str = 10,
                Agi = 10,
                Vit = 10,
                Int = 10,
                Dex = 10,
                Luck = 10,
            },
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        })).ActorId;

        var repository = new PostgresGameSystemRepository(gate, actors);
        var definition = new GameSystemDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Système PG",
            Title = "Monde PG",
            CurrencyUnit = "Écu",
            Description = "Notes PG",
            Switches = { new NamedWorldFlag { Key = "door_open", Label = "Porte ouverte" } },
            Variables = { new NamedWorldFlag { Key = "quest_step", Label = "Étape" } },
            StartingPartyActorIds = { heroId },
        };

        var created = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(1, created.NewRevision);

        var missing = new GameSystemDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Système orphelin PG",
            Title = "Monde",
            CurrencyUnit = "Or",
            StartingPartyActorIds = { Guid.NewGuid() },
        };
        var rejected = Assert.IsType<SaveGameSystemResult.ValidationFailed>(await repository.SaveAsync(
            new SaveGameSystemRequest
            {
                Definition = missing,
                ExpectedRevision = 0,
            }));
        Assert.Contains("catalogue publié", rejected.Error, StringComparison.Ordinal);

        definition.Title = "Monde publié PG";
        var published = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            SystemId = created.SystemId,
            Definition = definition,
            ExpectedRevision = created.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        var conflict = Assert.IsType<SaveGameSystemResult.Conflict>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            SystemId = created.SystemId,
            Definition = definition,
            ExpectedRevision = 1,
        }));
        Assert.Equal(2, conflict.CurrentRevision);

        var stored = await repository.LoadPublishedByIdAsync(created.SystemId);
        Assert.NotNull(stored);
        Assert.Equal("Monde publié PG", stored!.Definition.Title);
        Assert.Equal("Écu", stored.Definition.CurrencyUnit);
        Assert.Equal("door_open", Assert.Single(stored.Definition.Switches).Key);
        Assert.Equal("Étape", Assert.Single(stored.Definition.Variables).Label);
        Assert.Equal(heroId, Assert.Single(stored.Definition.StartingPartyActorIds));

        var draftOnly = new GameSystemDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Brouillon filtrable PG",
            Title = "Filtre",
            CurrencyUnit = "Or",
        };
        var draft = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = draftOnly,
            ExpectedRevision = 0,
        }));
        var drafts = await repository.ListSummariesAsync(
            search: "filtrable PG",
            statusFilter: ContentPublishStatus.Draft);
        var entry = Assert.Single(drafts);
        Assert.Equal(draft.SystemId, entry.SystemId);
        Assert.Equal(0, entry.SwitchCount);

        var publishedList = await repository.ListPublishedAsync();
        Assert.Contains(publishedList, system => system.Id == created.SystemId && system.Title == "Monde publié PG");

        Assert.IsType<DeleteGameSystemResult.Success>(await repository.DeleteAsync(draft.SystemId));
        Assert.IsType<DeleteGameSystemResult.Success>(await repository.DeleteAsync(created.SystemId));
        Assert.Null(await repository.LoadPublishedByIdAsync(created.SystemId));
        Assert.IsType<DeleteGameSystemResult.NotFound>(await repository.DeleteAsync(created.SystemId));
    }
}
