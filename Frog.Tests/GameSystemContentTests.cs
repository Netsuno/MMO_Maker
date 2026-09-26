using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue Système (Données de jeu) : titre, monnaie, noms d’interrupteurs et de
/// variables, groupe de départ. Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class GameSystemContentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndSystemCategoryExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "SystemEditorPanel.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        var skills = form.IndexOf("\"Compétences\"", StringComparison.Ordinal);
        var system = form.IndexOf("\"Système\"", StringComparison.Ordinal);
        var shops = form.IndexOf("\"Boutiques\"", StringComparison.Ordinal);
        Assert.True(skills >= 0 && system > skills && shops > system);
        Assert.Contains("SystemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Titre du jeu", panel, StringComparison.Ordinal);
        Assert.Contains("Unité monétaire", panel, StringComparison.Ordinal);
        Assert.Contains("Interrupteurs", panel, StringComparison.Ordinal);
        Assert.Contains("Groupe de départ", panel, StringComparison.Ordinal);
        Assert.Contains("Enregistrer brouillon", panel, StringComparison.Ordinal);
        Assert.Contains("Brouillon", panel, StringComparison.Ordinal);
        Assert.Contains("Publié", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void FlagKeys_MatchEventSwitchAndVariableIds()
    {
        Assert.True(GameSystemDefinition.TryValidateFlagKey("gate_open", isSwitch: true, out _));
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"gate_open","value":true}""",
            out var switchId,
            out var switchValue,
            out _));
        Assert.Equal("gate_open", switchId);
        Assert.True(switchValue);

        Assert.True(GameSystemDefinition.TryValidateFlagKey("quest_step", isSwitch: false, out _));
        Assert.True(MapEventParameterSchemas.TryParseSetVariable(
            """{"variableId":"quest_step","value":2}""",
            out var variableId,
            out var variableValue,
            out _));
        Assert.Equal("quest_step", variableId);
        Assert.Equal(2, variableValue);

        Assert.False(GameSystemDefinition.TryValidateFlagKey("porte ouverte", isSwitch: true, out var error));
        Assert.Contains("[A-Za-z0-9_]", error, StringComparison.Ordinal);
        Assert.False(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"porte ouverte","value":true}""",
            out _,
            out _,
            out _));
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTripsNamesAndStartingParty()
    {
        var actors = CreateActors();
        var hero = ActorContentTestsSample("Aventurier");
        var publishedHero = Assert.IsType<SaveActorResult.Success>(await actors.SaveAsync(new SaveActorRequest
        {
            Definition = hero,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var repository = new InMemoryGameSystemRepository(actors);
        var session = new GameSystemWorkspaceSession(repository);
        var definition = Sample("Système");
        definition.Switches.Add(new NamedWorldFlag { Key = "door_open", Label = "Porte ouverte" });
        definition.Variables.Add(new NamedWorldFlag { Key = "quest_step", Label = "Étape" });
        definition.StartingPartyActorIds.Add(publishedHero.ActorId);
        session.AdoptNewDraft(definition);

        var draft = Assert.IsType<SaveGameSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.CurrencyUnit = "Écu";
        session.Current.Title = "Monde fumée";
        session.MarkDirty();
        var published = Assert.IsType<SaveGameSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.SystemId, published.SystemId);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var stored = await repository.LoadPublishedByIdAsync(published.SystemId);
        Assert.NotNull(stored);
        Assert.Equal("Monde fumée", stored!.Definition.Title);
        Assert.Equal("Écu", stored.Definition.CurrencyUnit);
        Assert.Equal("Porte ouverte", Assert.Single(stored.Definition.Switches).Label);
        Assert.Equal("quest_step", Assert.Single(stored.Definition.Variables).Key);
        Assert.Equal(publishedHero.ActorId, Assert.Single(stored.Definition.StartingPartyActorIds));
        Assert.True(stored.Definition.TryGetSwitchLabel("door_open", out var label));
        Assert.Equal("Porte ouverte", label);

        session.DuplicateCurrent();
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);
        Assert.Equal("Écu", session.Current.CurrencyUnit);
        Assert.IsType<SaveGameSystemResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        await session.RefreshCatalogAsync();
        Assert.Equal(2, session.Catalog.Count);

        session.SearchFilter = "door_open";
        await session.RefreshCatalogAsync();
        Assert.Equal(2, session.Catalog.Count);

        session.SearchFilter = null;
        session.StatusFilter = ContentPublishStatus.Published;
        await session.RefreshCatalogAsync();
        Assert.All(session.Catalog, entry => Assert.Equal(ContentPublishStatus.Published, entry.Status));

        Assert.True(await session.OpenAsync(published.SystemId));
        Assert.IsType<DeleteGameSystemResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(published.SystemId));
        Assert.Null(await repository.LoadPublishedByIdAsync(published.SystemId));
    }

    [Fact]
    public async Task UnknownHeroAndDuplicateSwitchKey_AreRejected()
    {
        var actors = CreateActors();
        var repository = new InMemoryGameSystemRepository(actors);
        var missingHero = Sample("Système");
        missingHero.StartingPartyActorIds.Add(Guid.NewGuid());
        var rejected = Assert.IsType<SaveGameSystemResult.ValidationFailed>(await repository.SaveAsync(new SaveGameSystemRequest
        {
            Definition = missingHero,
            ExpectedRevision = 0,
        }));
        Assert.Contains("catalogue publié", rejected.Error, StringComparison.Ordinal);

        var duplicate = Sample("Système");
        duplicate.Switches.Add(new NamedWorldFlag { Key = "door_open", Label = "Une" });
        duplicate.Switches.Add(new NamedWorldFlag { Key = "door_open", Label = "Deux" });
        Assert.False(duplicate.Validate(out var error));
        Assert.Contains("en double", error, StringComparison.Ordinal);

        duplicate.Switches.RemoveAt(1);
        duplicate.StartingPartyActorIds.Add(Guid.NewGuid());
        duplicate.StartingPartyActorIds.Add(duplicate.StartingPartyActorIds[0]);
        duplicate.StartingPartyActorIds.Add(Guid.NewGuid());
        duplicate.StartingPartyActorIds.Add(Guid.NewGuid());
        duplicate.StartingPartyActorIds.Add(Guid.NewGuid());
        Assert.False(duplicate.Validate(out error));
        Assert.Contains("4", error, StringComparison.Ordinal);
    }

    private static InMemoryActorRepository CreateActors()
    {
        var items = new InMemoryItemRepository();
        return new InMemoryActorRepository(
            new InMemoryClassRepository(new InMemorySpellRepository(), items: items),
            items);
    }

    private static GameSystemDefinition Sample(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Title = "Nouveau jeu",
        CurrencyUnit = GameSystemDefinition.DefaultCurrencyUnit,
        Description = "Notes",
    };

    private static ActorDefinition ActorContentTestsSample(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BaseHp = 100,
        BaseMp = 40,
        Str = 10,
        Agi = 10,
        Vit = 10,
        Int = 10,
        Dex = 10,
        Luck = 10,
    };

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
