using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue Système (Données de jeu) : interrupteurs et variables nommés.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class SystemFlagContentTests
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
        var labels = File.ReadAllText(Path.Combine(root, "Frog.Core", "Enums", "SystemFlagKind.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Système\"", form, StringComparison.Ordinal);
        Assert.Contains("SystemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Identifiant", panel, StringComparison.Ordinal);
        Assert.Contains("Libellé", panel, StringComparison.Ordinal);
        Assert.Contains("\"Interrupteurs\"", labels, StringComparison.Ordinal);
        Assert.Contains("\"Variables\"", labels, StringComparison.Ordinal);
    }

    [Fact]
    public void KindLabels_RoundTrip_FrenchCatalogueNames()
    {
        Assert.Equal("Interrupteur", SystemFlagKindLabels.French(SystemFlagKind.Switch));
        Assert.Equal("Variable", SystemFlagKindLabels.French(SystemFlagKind.Variable));
        Assert.Equal("Interrupteurs", SystemFlagKindLabels.FrenchList(SystemFlagKind.Switch));
        Assert.Equal("Variables", SystemFlagKindLabels.FrenchList(SystemFlagKind.Variable));
        Assert.True(SystemFlagKindLabels.TryParseList("Variables", out var variable));
        Assert.Equal(SystemFlagKind.Variable, variable);
        Assert.False(SystemFlagKindLabels.TryParseList("Switch", out _));
    }

    [Fact]
    public void Validate_RejectsBlankLabel_IllegalKey_AndLongNote()
    {
        var definition = Flag("porte_nord", "Porte nord", SystemFlagKind.Switch, "note");
        Assert.True(definition.Validate(out _));

        definition.Label = " ";
        Assert.False(definition.Validate(out var error));
        Assert.Contains("Libellé", error, StringComparison.Ordinal);

        definition = Flag("porte nord", "Porte nord", SystemFlagKind.Switch, null);
        Assert.False(definition.Validate(out error));
        Assert.Contains("[A-Za-z0-9_]", error, StringComparison.Ordinal);

        definition = Flag("porte_nord", "Porte nord", SystemFlagKind.Switch, new string('n', 501));
        Assert.False(definition.Validate(out error));
        Assert.Contains("Note", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KindFilter_ListsOnlyTheSelectedCatalogue_AndSameKeyCanExistOnBoth()
    {
        var repository = new InMemorySystemFlagRepository();
        var switches = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        switches.AdoptNewDraft(Flag("porte_nord", "Porte nord", SystemFlagKind.Variable, "Ouverture."));
        Assert.Equal(SystemFlagKind.Switch, switches.Current!.Kind);
        var publishedSwitch = Assert.IsType<SaveSystemFlagResult.Success>(
            await switches.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(ContentPublishStatus.Published, switches.CurrentStatus);

        var variables = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Variable };
        variables.AdoptNewDraft(Flag("porte_nord", "Compteur porte", SystemFlagKind.Switch, null));
        Assert.Equal(SystemFlagKind.Variable, variables.Current!.Kind);
        Assert.IsType<SaveSystemFlagResult.Success>(
            await variables.SaveCurrentAsync(SaveContentIntent.Publish));

        await switches.RefreshCatalogAsync();
        var switchEntry = Assert.Single(switches.Catalog);
        Assert.Equal("Porte nord", switchEntry.Label);
        Assert.Equal("porte_nord", switchEntry.Key);
        Assert.Equal(SystemFlagKind.Switch, switchEntry.Kind);

        await variables.RefreshCatalogAsync();
        var variableEntry = Assert.Single(variables.Catalog);
        Assert.Equal(SystemFlagKind.Variable, variableEntry.Kind);
        Assert.Equal("porte_nord", variableEntry.Key);

        var stored = await repository.LoadPublishedByIdAsync(publishedSwitch.FlagId);
        Assert.Equal("Ouverture.", stored!.Definition.Note);
        Assert.Equal(SystemFlagKind.Switch, stored.Definition.Kind);
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTrip_AndDuplicateKeyIsRejected()
    {
        var repository = new InMemorySystemFlagRepository();
        var session = new SystemFlagWorkspaceSession(repository);
        session.AdoptNewDraft(Flag("coffre_ouvert", "Coffre ouvert", SystemFlagKind.Switch, null));
        var draft = Assert.IsType<SaveSystemFlagResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Note = "Déjà fouillé.";
        session.MarkDirty();
        var published = Assert.IsType<SaveSystemFlagResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.FlagId, published.FlagId);
        Assert.Equal("Déjà fouillé.", (await repository.LoadPublishedByIdAsync(published.FlagId))!.Definition.Note);

        session.DuplicateCurrent();
        Assert.Equal(SystemFlagKind.Switch, session.Current!.Kind);
        Assert.Equal("coffre_ouvert_copie", session.Current.Key);
        Assert.Contains("(copie)", session.Current.Label, StringComparison.Ordinal);
        Assert.IsType<SaveSystemFlagResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        var other = new SystemFlagWorkspaceSession(repository);
        other.AdoptNewDraft(Flag("coffre_ouvert", "Doublon", SystemFlagKind.Switch, null));
        var duplicate = Assert.IsType<SaveSystemFlagResult.ValidationFailed>(
            await other.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Contains("existe déjà", duplicate.Error, StringComparison.Ordinal);

        Assert.True(await session.OpenAsync(published.FlagId));
        Assert.IsType<DeleteSystemFlagResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(published.FlagId));

        other.AdoptNewDraft(Flag("coffre_ouvert", "Coffre rouvert", SystemFlagKind.Switch, null));
        Assert.IsType<SaveSystemFlagResult.Success>(await other.SaveCurrentAsync(SaveContentIntent.Publish));
    }

    private static SystemFlagDefinition Flag(string key, string label, SystemFlagKind kind, string? note) => new()
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
