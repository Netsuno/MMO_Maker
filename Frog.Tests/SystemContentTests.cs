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
/// Catalogue Système (interrupteurs et variables nommés). Hello reste 11. Tuiles TileAsset restent 48.
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
        Assert.Contains("\"Système\"", form, StringComparison.Ordinal);
        Assert.Contains("SystemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Libellé", panel, StringComparison.Ordinal);
        Assert.Contains("Interrupteurs", panel, StringComparison.Ordinal);
        Assert.Contains("Variables", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void KindLabels_RoundTrip_FrenchNames()
    {
        foreach (var kind in Enum.GetValues<SystemFlagKind>())
        {
            Assert.True(SystemFlagKindLabels.TryParse(SystemFlagKindLabels.French(kind), out var parsed));
            Assert.Equal(kind, parsed);
            Assert.True(SystemFlagKindLabels.TryParse(SystemFlagKindLabels.FrenchPlural(kind), out var plural));
            Assert.Equal(kind, plural);
        }

        Assert.Equal("Interrupteur", SystemFlagKindLabels.French(SystemFlagKind.Switch));
        Assert.Equal("Variable", SystemFlagKindLabels.French(SystemFlagKind.Variable));
        Assert.Equal("Interrupteurs", SystemFlagKindLabels.FrenchPlural(SystemFlagKind.Switch));
        Assert.Equal("Variables", SystemFlagKindLabels.FrenchPlural(SystemFlagKind.Variable));
        Assert.False(SystemFlagKindLabels.TryParse("Switch", out _));
    }

    [Fact]
    public void Validate_RejectsEmptyLabel_BadKey_AndLongNote()
    {
        var definition = Flag("Porte", "porte_ouverte", SystemFlagKind.Switch, "Note");
        Assert.True(definition.Validate(out _));

        definition.Label = " ";
        Assert.False(definition.Validate(out var labelError));
        Assert.Contains("Libellé", labelError, StringComparison.Ordinal);

        definition.Label = "Porte";
        definition.Key = "porte ouverte";
        Assert.False(definition.Validate(out var keyError));
        Assert.Contains("caractères autorisés", keyError, StringComparison.Ordinal);

        definition.Key = "porte_ouverte";
        definition.Note = new string('n', SystemFlagDefinition.MaxNoteLength + 1);
        Assert.False(definition.Validate(out var noteError));
        Assert.Contains("Note", noteError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KindFilter_ListsOnlySwitches_AndForcesKindOnSave()
    {
        var repository = new InMemorySystemFlagRepository();
        await repository.SaveAsync(new SaveSystemFlagRequest
        {
            Definition = Flag("Or du butin", "butin_or", SystemFlagKind.Variable, null),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        var session = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        session.AdoptNewDraft(Flag("Porte", "porte_ouverte", SystemFlagKind.Variable, "Ouverture."));
        Assert.Equal(SystemFlagKind.Switch, session.Current!.Kind);

        var published = Assert.IsType<SaveSystemFlagResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        await session.RefreshCatalogAsync();
        var entry = Assert.Single(session.Catalog);
        Assert.Equal("Porte", entry.Label);
        Assert.Equal("porte_ouverte", entry.Key);
        Assert.Equal(SystemFlagKind.Switch, entry.Kind);

        var stored = await repository.LoadPublishedByIdAsync(published.FlagId);
        Assert.Equal(SystemFlagKind.Switch, stored!.Definition.Kind);
        Assert.Equal("Ouverture.", stored.Definition.Note);

        var variables = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Variable };
        await variables.RefreshCatalogAsync();
        var variable = Assert.Single(variables.Catalog);
        Assert.Equal("butin_or", variable.Key);
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTrip_AndDuplicateKeyRejected()
    {
        var repository = new InMemorySystemFlagRepository();
        var session = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        session.AdoptNewDraft(Flag("Porte", "porte_ouverte", SystemFlagKind.Switch, null));
        var draft = Assert.IsType<SaveSystemFlagResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Null(await repository.LoadPublishedByIdAsync(draft.FlagId));

        session.Current!.Note = "La porte est ouverte.";
        session.MarkDirty();
        var published = Assert.IsType<SaveSystemFlagResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.FlagId, published.FlagId);
        Assert.Equal("La porte est ouverte.", (await repository.LoadPublishedByIdAsync(published.FlagId))!.Definition.Note);

        session.Current.Label = "Porte modifiée";
        session.MarkDirty();
        Assert.IsType<SaveSystemFlagResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Equal("Porte", (await repository.LoadPublishedByIdAsync(published.FlagId))!.Definition.Label);

        session.DuplicateCurrent();
        Assert.Equal(SystemFlagKind.Switch, session.Current!.Kind);
        Assert.Equal("porte_ouverte_copie", session.Current.Key);
        Assert.Contains("(copie)", session.Current.Label, StringComparison.Ordinal);
        Assert.IsType<SaveSystemFlagResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        var sameKey = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        sameKey.AdoptNewDraft(Flag("Autre", "porte_ouverte", SystemFlagKind.Switch, null));
        Assert.IsType<SaveSystemFlagResult.ValidationFailed>(await sameKey.SaveCurrentAsync(SaveContentIntent.Publish));

        var variable = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Variable };
        variable.AdoptNewDraft(Flag("Compteur", "porte_ouverte", SystemFlagKind.Variable, null));
        Assert.IsType<SaveSystemFlagResult.Success>(await variable.SaveCurrentAsync(SaveContentIntent.Publish));

        Assert.True(await session.OpenAsync(published.FlagId));
        Assert.IsType<DeleteSystemFlagResult.Success>(await session.DeleteCurrentAsync());
        Assert.Null(await repository.LoadByIdAsync(published.FlagId));
        Assert.Null(await repository.LoadPublishedByIdAsync(published.FlagId));
    }

    [Fact]
    public async Task StaleRevision_Conflicts()
    {
        var repository = new InMemorySystemFlagRepository();
        var first = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        var second = new SystemFlagWorkspaceSession(repository) { KindFilter = SystemFlagKind.Switch };
        first.AdoptNewDraft(Flag("Porte", "porte_ouverte", SystemFlagKind.Switch, null));
        var created = Assert.IsType<SaveSystemFlagResult.Success>(
            await first.SaveCurrentAsync(SaveContentIntent.Publish));

        Assert.True(await second.OpenAsync(created.FlagId));
        second.Current!.Label = "Autre libellé";
        second.MarkDirty();
        first.Current!.Label = "Libellé gagnant";
        first.MarkDirty();
        Assert.IsType<SaveSystemFlagResult.Success>(await first.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.IsType<SaveSystemFlagResult.Conflict>(await second.SaveCurrentAsync(SaveContentIntent.SaveDraft));
    }

    private static SystemFlagDefinition Flag(string label, string key, SystemFlagKind kind, string? note) => new()
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
