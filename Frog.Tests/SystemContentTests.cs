using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue Système (Données de jeu) : interrupteurs, variables, options du projet.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class SystemContentTests
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
        Assert.Contains("Nom du jeu", panel, StringComparison.Ordinal);
        Assert.Contains("Musique de départ", panel, StringComparison.Ordinal);
        Assert.Contains("Interrupteurs", panel, StringComparison.Ordinal);
        Assert.Contains("Variables", panel, StringComparison.Ordinal);
        Assert.Contains("Libellé", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void EventKeys_RoundTrip_WithSwitchAndVariableCommands()
    {
        const string switchKey = "porte_ouverte";
        const string variableKey = "score_quetes";
        Assert.True(GameSystemEntryDefinition.TryValidateEventKey(switchKey, out _));
        Assert.True(GameSystemEntryDefinition.TryValidateEventKey(variableKey, out _));
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(
            "{\"switchId\":\"porte_ouverte\",\"value\":true}",
            out var parsedSwitch,
            out var switchValue,
            out _));
        Assert.Equal(switchKey, parsedSwitch);
        Assert.True(switchValue);
        Assert.True(MapEventParameterSchemas.TryParseSetVariable(
            "{\"variableId\":\"score_quetes\",\"value\":4}",
            out var parsedVariable,
            out var variableValue,
            out _));
        Assert.Equal(variableKey, parsedVariable);
        Assert.Equal(4, variableValue);

        Assert.False(GameSystemEntryDefinition.TryValidateEventKey("porte-ouverte", out _));
        Assert.False(MapEventParameterSchemas.TryParseSetSwitch(
            "{\"switchId\":\"porte-ouverte\",\"value\":true}",
            out _,
            out _,
            out _));
        Assert.False(GameSystemEntryDefinition.TryValidateEventKey("", out _));
    }

    [Fact]
    public async Task SwitchesAndVariables_DraftPublish_DuplicateKeyRejected_SameKeyAcrossKindsAllowed()
    {
        var repository = new InMemoryGameSystemRepository();
        var switches = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Switch };
        switches.AdoptNewDraft(Flag(GameSystemEntryKind.Switch, "porte_ouverte", "Porte ouverte", "Le pont-levis."));
        var draft = Assert.IsType<SaveGameSystemResult.Success>(
            await switches.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, switches.CurrentStatus);

        switches.Current!.Note = "Ouverte après la quête.";
        switches.MarkDirty();
        var published = Assert.IsType<SaveGameSystemResult.Success>(
            await switches.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.EntryId, published.EntryId);
        var stored = await repository.LoadPublishedByIdAsync(published.EntryId);
        Assert.Equal("Ouverte après la quête.", stored!.Definition.Note);
        Assert.Equal(GameSystemEntryKind.Switch, stored.Definition.Kind);

        var clash = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Switch };
        clash.AdoptNewDraft(Flag(GameSystemEntryKind.Switch, "porte_ouverte", "Autre", null));
        Assert.IsType<SaveGameSystemResult.ValidationFailed>(
            await clash.SaveCurrentAsync(SaveContentIntent.Publish));

        var variables = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Variable };
        variables.AdoptNewDraft(Flag(GameSystemEntryKind.Variable, "porte_ouverte", "Compteur", null));
        var variable = Assert.IsType<SaveGameSystemResult.Success>(
            await variables.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal("porte_ouverte", (await repository.LoadByIdAsync(variable.EntryId))!.Definition.Key);

        await switches.RefreshCatalogAsync();
        Assert.Single(switches.Catalog);
        Assert.Equal("Porte ouverte", switches.Catalog[0].Label);
        await variables.RefreshCatalogAsync();
        Assert.Single(variables.Catalog);

        variables.DuplicateCurrent(await variables.ListKeysAsync());
        Assert.Contains("(copie)", variables.Current!.Label, StringComparison.Ordinal);
        Assert.NotEqual("porte_ouverte", variables.Current.Key);
        Assert.IsType<SaveGameSystemResult.Success>(await variables.SaveCurrentAsync(SaveContentIntent.Publish));

        Assert.True(await switches.OpenAsync(published.EntryId));
        Assert.IsType<DeleteGameSystemResult.Success>(await switches.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(published.EntryId));
        Assert.Null(await repository.LoadPublishedByIdAsync(published.EntryId));
    }

    [Fact]
    public async Task ProjectOptions_SaveStartingBgm_AndRejectInvalidPath()
    {
        var repository = new InMemoryGameSystemRepository();
        var session = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Options };
        session.AdoptNewDraft(new GameSystemEntryDefinition
        {
            Id = Guid.NewGuid(),
            Kind = GameSystemEntryKind.Options,
            Label = "Frog",
            StartingBgmAsset = "Assets/Audio/theme.wav",
            StartingBgmVolume = 80,
            StartingBgmFadeMs = 400,
        });

        var published = Assert.IsType<SaveGameSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        var stored = await repository.LoadPublishedByIdAsync(published.EntryId);
        Assert.Equal("Frog", stored!.Definition.Label);
        Assert.Equal("Assets/Audio/theme.wav", stored.Definition.StartingBgmAsset);
        Assert.Equal(80, stored.Definition.StartingBgmVolume);
        Assert.Equal(400, stored.Definition.StartingBgmFadeMs);
        Assert.Equal(string.Empty, stored.Definition.Key);

        var catalog = await ((IPublishedGameSystemCatalog)repository)
            .ListPublishedAsync(GameSystemEntryKind.Options);
        Assert.Equal("Frog", Assert.Single(catalog).Label);

        session.Current!.StartingBgmAsset = "../secret.wav";
        session.MarkDirty();
        var rejected = Assert.IsType<SaveGameSystemResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Contains("Musique de départ", rejected.Error, StringComparison.Ordinal);
        Assert.Equal("Assets/Audio/theme.wav", (await repository.LoadPublishedByIdAsync(published.EntryId))!.Definition.StartingBgmAsset);

        var second = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Options };
        second.AdoptNewDraft(new GameSystemEntryDefinition
        {
            Id = Guid.NewGuid(),
            Kind = GameSystemEntryKind.Options,
            Label = "Autre",
        });
        Assert.IsType<SaveGameSystemResult.ValidationFailed>(await second.SaveCurrentAsync(SaveContentIntent.SaveDraft));
    }

    [Fact]
    public async Task SearchAndStatusFilter_ApplyToTheActiveKind()
    {
        var repository = new InMemoryGameSystemRepository();
        var session = new GameSystemWorkspaceSession(repository) { KindFilter = GameSystemEntryKind.Switch };
        session.AdoptNewDraft(Flag(GameSystemEntryKind.Switch, "interrupteur_1", "Pont", null));
        await session.SaveCurrentAsync(SaveContentIntent.Publish);
        session.AdoptNewDraft(Flag(GameSystemEntryKind.Switch, "interrupteur_2", "Cave", "brouillon"));
        await session.SaveCurrentAsync(SaveContentIntent.SaveDraft);

        session.SearchFilter = "Cave";
        await session.RefreshCatalogAsync();
        var match = Assert.Single(session.Catalog);
        Assert.Equal("interrupteur_2", match.Key);

        session.SearchFilter = null;
        session.StatusFilter = ContentPublishStatus.Published;
        await session.RefreshCatalogAsync();
        Assert.Equal("Pont", Assert.Single(session.Catalog).Label);
    }

    private static GameSystemEntryDefinition Flag(GameSystemEntryKind kind, string key, string label, string? note) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Key = key,
        Label = label,
        Note = note,
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
