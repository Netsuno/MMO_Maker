using Frog.Application.Content;
using Frog.Core.Enums;
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
    public async Task Save_Publish_Reload_DuplicateKey_AndOptions()
    {
        using var gate = CreateGate();
        var repository = new PostgresGameSystemRepository(gate);
        var definition = Flag(GameSystemEntryKind.Switch, "porte_ouverte", "Porte ouverte");

        var created = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(1, created.NewRevision);

        definition.Note = "Quête du pont.";
        var published = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            EntryId = created.EntryId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        var stored = await repository.LoadPublishedByIdAsync(published.EntryId);
        Assert.Equal("Quête du pont.", stored!.Definition.Note);
        Assert.Equal(ContentPublishStatus.Published, (await repository.LoadByIdAsync(published.EntryId))!.Status);

        var clash = Assert.IsType<SaveGameSystemResult.ValidationFailed>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = Flag(GameSystemEntryKind.Switch, "porte_ouverte", "Doublon"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Contains("porte_ouverte", clash.Error, StringComparison.Ordinal);

        var variable = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = Flag(GameSystemEntryKind.Variable, "porte_ouverte", "Compteur"),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(GameSystemEntryKind.Variable, (await repository.LoadByIdAsync(variable.EntryId))!.Definition.Kind);

        var options = new GameSystemEntryDefinition
        {
            Id = Guid.NewGuid(),
            Kind = GameSystemEntryKind.Options,
            Label = "Frog PG",
            StartingBgmAsset = "Assets/Audio/theme.wav",
            StartingBgmVolume = 70,
            StartingBgmFadeMs = 250,
        };
        var savedOptions = Assert.IsType<SaveGameSystemResult.Success>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = options,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));
        var loadedOptions = await repository.LoadPublishedByIdAsync(savedOptions.EntryId);
        Assert.Equal("Frog PG", loadedOptions!.Definition.Label);
        Assert.Equal(70, loadedOptions.Definition.StartingBgmVolume);
        Assert.Equal("Assets/Audio/theme.wav", loadedOptions.Definition.StartingBgmAsset);

        var publishedSwitches = await repository.ListPublishedAsync(GameSystemEntryKind.Switch);
        Assert.Contains(publishedSwitches, entry => entry.Key == "porte_ouverte");
        Assert.DoesNotContain(publishedSwitches, entry => entry.Kind == GameSystemEntryKind.Variable);

        Assert.IsType<DeleteGameSystemResult.Success>(await repository.DeleteAsync(variable.EntryId));
        Assert.Null(await repository.LoadByIdAsync(variable.EntryId));
    }

    private FrogDbContextGate CreateGate()
        => new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static GameSystemEntryDefinition Flag(GameSystemEntryKind kind, string key, string label) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Key = key,
        Label = label,
    };
}
