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

    [PostgresTheory]
    [Trait("Category", "PostgreSql")]
    [InlineData(Phase8ContentKind.Dialogue)]
    [InlineData(Phase8ContentKind.Quest)]
    [InlineData(Phase8ContentKind.CommonEvent)]
    [InlineData(Phase8ContentKind.Profession)]
    [InlineData(Phase8ContentKind.Recipe)]
    [InlineData(Phase8ContentKind.Region)]
    [InlineData(Phase8ContentKind.WeatherProfile)]
    public async Task SaveAndPublish_DraftInvisible_ThenVisible_AllKinds(Phase8ContentKind kind)
    {
        await AssertDraftInvisibleThenPublishVisibleAsync(kind).ConfigureAwait(false);
    }

    private async Task AssertDraftInvisibleThenPublishVisibleAsync(Phase8ContentKind kind)
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresPhase8PublishedCatalogs(gate);
        var contentId = Guid.NewGuid();
        var name = $"DraftGate-{kind}";
        var payload = CreatePayload(kind, contentId, name);

        var draft = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = contentId,
            Kind = kind,
            Name = name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }).ConfigureAwait(false);
        Assert.IsType<Phase8SaveContentResult.Success>(draft);

        Assert.False(await IsPublishedVisibleAsync(repo, kind, contentId).ConfigureAwait(false));

        var published = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            ContentId = contentId,
            Kind = kind,
            Name = name,
            PayloadJson = payload,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
        Assert.IsType<Phase8SaveContentResult.Success>(published);

        Assert.True(await IsPublishedVisibleAsync(repo, kind, contentId).ConfigureAwait(false));
    }

    private static string CreatePayload(Phase8ContentKind kind, Guid id, string name) => kind switch
    {
        Phase8ContentKind.Dialogue => Phase8ContentCodec.SerializeDialogue(new DialogueDefinition
        {
            Id = id,
            Name = name,
            Lines = [new DialogueLineDefinition { Speaker = "A", Text = "Hi" }],
        }),
        Phase8ContentKind.Quest => Phase8ContentCodec.SerializeQuest(new QuestDefinition
        {
            Id = id,
            Name = name,
            Stages = [new QuestStageDefinition { Description = "Step 1" }],
        }),
        Phase8ContentKind.CommonEvent => Phase8ContentCodec.SerializeCommonEvent(new CommonEventDefinition
        {
            Id = id,
            Name = name,
        }),
        Phase8ContentKind.Profession => Phase8ContentCodec.SerializeProfession(new ProfessionDefinition
        {
            Id = id,
            Name = name,
            MaxLevel = 50,
        }),
        Phase8ContentKind.Recipe => Phase8ContentCodec.SerializeRecipe(new RecipeDefinition
        {
            Id = id,
            Name = name,
            ProfessionId = Guid.NewGuid(),
            OutputItemId = Guid.NewGuid(),
            Ingredients = [new RecipeIngredientDefinition { ItemId = Guid.NewGuid(), Quantity = 1 }],
        }),
        Phase8ContentKind.Region => Phase8ContentCodec.SerializeRegion(new RegionDefinition
        {
            Id = id,
            Name = name,
            MapId = 1,
            TileXMax = 4,
            TileYMax = 4,
            WeatherProfileId = Guid.NewGuid(),
        }),
        Phase8ContentKind.WeatherProfile => Phase8ContentCodec.SerializeWeather(new WeatherProfileDefinition
        {
            Id = id,
            Name = name,
            WeatherKind = "clear",
            LightingFactor = 0.8f,
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task<bool> IsPublishedVisibleAsync(
        PostgresPhase8PublishedCatalogs repo,
        Phase8ContentKind kind,
        Guid id)
    {
        return kind switch
        {
            Phase8ContentKind.Dialogue => await ((IPublishedDialogueCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            Phase8ContentKind.Quest => await ((IPublishedQuestCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            Phase8ContentKind.CommonEvent => await ((IPublishedCommonEventCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            Phase8ContentKind.Profession => await ((IPublishedProfessionCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            Phase8ContentKind.Recipe => await ((IPublishedRecipeCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            Phase8ContentKind.Region => (await ((IPublishedRegionCatalog)repo).ListPublishedAsync().ConfigureAwait(false))
                .Any(r => r.Id == id),
            Phase8ContentKind.WeatherProfile => await ((IPublishedWeatherCatalog)repo).TryGetPublishedByIdAsync(id).ConfigureAwait(false) is not null,
            _ => false,
        };
    }
}
