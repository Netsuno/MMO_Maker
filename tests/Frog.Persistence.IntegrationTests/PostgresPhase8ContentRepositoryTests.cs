using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresPhase8ContentRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresPhase8ContentRepositoryTests(IsolatedPostgresFixture fixture) => _fixture = fixture;

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SaveAndPublish_Quest_DraftInvisibleToPublishedCatalog()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresPhase8PublishedCatalogs(gate);
        var questId = Guid.NewGuid();
        var payload = Phase8ContentCodec.SerializeQuest(new QuestDefinition
        {
            Id = questId,
            Name = "Test Quest",
            Stages = [new QuestStageDefinition { Description = "Step 1" }],
        });

        var draft = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = questId,
            Kind = Phase8ContentKind.Quest,
            Name = "Test Quest",
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        Assert.IsType<Phase8SaveContentResult.Success>(draft);

        IPublishedQuestCatalog catalog = repo;
        Assert.Null(await catalog.TryGetPublishedByIdAsync(questId));

        var published = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            ContentId = questId,
            Kind = Phase8ContentKind.Quest,
            Name = "Test Quest",
            PayloadJson = payload,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<Phase8SaveContentResult.Success>(published);

        var loaded = await catalog.TryGetPublishedByIdAsync(questId);
        Assert.NotNull(loaded);
        Assert.Equal("Test Quest", loaded!.Name);
    }
}
