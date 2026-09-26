using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresSystemRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresSystemRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_DraftDiverges_AndConflict()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repository = new PostgresSystemRepository(gate);
        var definition = new SystemDefinition
        {
            Id = SystemDefinition.SingletonId,
            Switches =
            [
                new SystemSwitchEntry
                {
                    Id = "gate_open",
                    Label = "Porte ouverte",
                    Note = "Coffre du hall",
                },
            ],
            Variables =
            [
                new SystemVariableEntry { Id = "nuits", Label = "Nuits passées" },
            ],
        };

        var draft = Assert.IsType<SaveSystemResult.Success>(await repository.SaveAsync(new SaveSystemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(1, draft.NewRevision);
        Assert.Null(await repository.LoadPublishedAsync());

        definition.Switches[0].Label = "Porte publiée";
        var published = Assert.IsType<SaveSystemResult.Success>(await repository.SaveAsync(new SaveSystemRequest
        {
            Definition = definition,
            ExpectedRevision = draft.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(published.NewRevision, published.PublishedRevision);

        definition.Switches[0].Label = "Porte brouillon";
        Assert.IsType<SaveSystemResult.Success>(await repository.SaveAsync(new SaveSystemRequest
        {
            Definition = definition,
            ExpectedRevision = published.NewRevision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var stored = await repository.LoadAsync();
        Assert.Equal("Porte brouillon", stored!.Definition.Switches[0].Label);
        Assert.Equal("Coffre du hall", stored.Definition.Switches[0].Note);
        Assert.Equal(ContentPublishStatus.Draft, stored.Status);
        Assert.Equal(published.PublishedRevision, stored.PublishedRevision);

        var publishedStored = await repository.LoadPublishedAsync();
        Assert.Equal("Porte publiée", publishedStored!.Definition.Switches[0].Label);

        var conflict = Assert.IsType<SaveSystemResult.Conflict>(await repository.SaveAsync(new SaveSystemRequest
        {
            Definition = definition,
            ExpectedRevision = published.NewRevision,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(stored.Revision, conflict.CurrentRevision);

        definition.Switches[0].Id = "bad id";
        Assert.IsType<SaveSystemResult.ValidationFailed>(await repository.SaveAsync(new SaveSystemRequest
        {
            Definition = definition,
            ExpectedRevision = conflict.CurrentRevision,
            Intent = SaveContentIntent.Publish,
        }));
    }
}
