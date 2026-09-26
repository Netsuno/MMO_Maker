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
/// Catalogue Système (interrupteurs et variables nommés). Même dépôt Phase 8, Hello 11, tuiles 48.
/// </summary>
public sealed class SystemCatalogTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndSystemCategoryExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(Phase8ContentKind.NamedSwitch, (Phase8ContentKind)8);
        Assert.Equal(Phase8ContentKind.NamedVariable, (Phase8ContentKind)9);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "SystemEditorPanel.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Système\"", form, StringComparison.Ordinal);
        Assert.Contains("SystemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Identifiant", panel, StringComparison.Ordinal);
        Assert.Contains("Libellé", panel, StringComparison.Ordinal);
        Assert.Contains("Interrupteurs", panel, StringComparison.Ordinal);
        Assert.Contains("Variables", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyRules_MatchEventCommandSwitchAndVariableIds()
    {
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"porte_ouverte","value":true}""",
            out var switchId,
            out var on,
            out var error),
            error);
        Assert.Equal("porte_ouverte", switchId);
        Assert.True(on);

        var named = Entry("porte_ouverte", "Porte ouverte", "Coffre du village.");
        Assert.True(named.Validate(SystemCatalogSlot.Switch, out error), error);

        Assert.False(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"mauvais id","value":false}""",
            out _,
            out _,
            out _));
        var invalid = Entry("mauvais id", "Mauvais", null);
        Assert.False(invalid.Validate(SystemCatalogSlot.Switch, out error));
        Assert.Equal("Identifiant : caractères autorisés [A-Za-z0-9_].", error);

        Assert.False(Entry("", "Sans clé", null).Validate(SystemCatalogSlot.Variable, out error));
        Assert.Equal("Identifiant requis.", error);
        Assert.False(Entry("or_possede", "", null).Validate(SystemCatalogSlot.Variable, out error));
        Assert.Contains("Libellé", error, StringComparison.Ordinal);

        var longKey = new string('a', 65);
        Assert.False(MapEventParameterSchemas.TryValidateVariableKey(longKey, out _));
        Assert.False(Entry(longKey, "Trop long", null).Validate(SystemCatalogSlot.Variable, out error));
        Assert.Equal("Identifiant trop long.", error);
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTrip_AndNoteSurvives()
    {
        var repository = new InMemoryPhase8ContentEditorRepository();
        var session = new SystemCatalogWorkspaceSession(repository, SystemCatalogSlot.Switch);
        session.AdoptNewDraft(Entry("porte_ouverte", "Porte ouverte", "Coffre du village."));

        var draft = Assert.IsType<Phase8SaveContentResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Null(session.PublishedRevision);

        session.Current!.Note = "Coffre du village, étage.";
        session.MarkDirty();
        var published = Assert.IsType<Phase8SaveContentResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.ContentId, published.ContentId);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);
        Assert.Equal(published.NewRevision, session.PublishedRevision);

        var stored = await repository.LoadDraftByIdAsync(published.ContentId);
        Assert.True(SystemCatalogCodec.TryRead(stored!.PayloadJson, out var entry, out var error), error);
        Assert.Equal("porte_ouverte", entry.Key);
        Assert.Equal("Porte ouverte", entry.Label);
        Assert.Equal("Coffre du village, étage.", entry.Note);
        Assert.Equal("Porte ouverte", stored.Name);

        session.DuplicateCurrent();
        Assert.Equal("porte_ouverte_copie", session.Current!.Key);
        Assert.Contains("(copie)", session.Current.Label, StringComparison.Ordinal);
        Assert.IsType<Phase8SaveContentResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        await session.RefreshCatalogAsync();
        Assert.Equal(2, session.Catalog.Count);

        Assert.True(await session.OpenAsync(published.ContentId));
        Assert.IsType<Phase8DeleteContentResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadDraftByIdAsync(published.ContentId));
        await session.RefreshCatalogAsync();
        Assert.Single(session.Catalog);
    }

    [Fact]
    public async Task DuplicateKey_Rejected_WithinSlot_ButAllowedAcrossSwitchAndVariable()
    {
        var repository = new InMemoryPhase8ContentEditorRepository();
        var switches = new SystemCatalogWorkspaceSession(repository, SystemCatalogSlot.Switch);
        var variables = new SystemCatalogWorkspaceSession(repository, SystemCatalogSlot.Variable);

        switches.AdoptNewDraft(Entry("coffre", "Coffre", null));
        Assert.IsType<Phase8SaveContentResult.Success>(
            await switches.SaveCurrentAsync(SaveContentIntent.Publish));

        switches.AdoptNewDraft(Entry("coffre", "Autre coffre", null));
        var duplicate = Assert.IsType<Phase8SaveContentResult.ValidationFailed>(
            await switches.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal("Cet identifiant est déjà utilisé.", duplicate.Error);

        variables.AdoptNewDraft(Entry("coffre", "Compteur coffre", "Même clé, autre catalogue."));
        Assert.IsType<Phase8SaveContentResult.Success>(
            await variables.SaveCurrentAsync(SaveContentIntent.Publish));

        switches.SearchFilter = "coffre";
        await switches.RefreshCatalogAsync();
        var onlySwitch = Assert.Single(switches.Catalog);
        Assert.Equal("Coffre", onlySwitch.Label);

        variables.StatusFilter = ContentPublishStatus.Published;
        await variables.RefreshCatalogAsync();
        var onlyVariable = Assert.Single(variables.Catalog);
        Assert.Equal("coffre", onlyVariable.Key);
        Assert.Equal("Même clé, autre catalogue.", onlyVariable.Note);
    }

    private static SystemCatalogEntry Entry(string key, string label, string? note) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Label = label,
        Note = note ?? string.Empty,
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
