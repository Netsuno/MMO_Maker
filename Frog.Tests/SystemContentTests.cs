using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue Système (Données de jeu) : interrupteurs et variables nommés.
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
        Assert.Contains("\"Système\"", form, StringComparison.Ordinal);
        Assert.Contains("SystemEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Interrupteurs", panel, StringComparison.Ordinal);
        Assert.Contains("Libellé", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("bateau", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aéronef", panel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsEventKeys_AndRejectsSpacesDuplicatesAndBlankLabels()
    {
        var definition = Sample();
        Assert.True(definition.Validate(out var error), error);

        var json = JsonSerializer.Serialize(new { switchId = "gate_open", value = true });
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(json, out var switchId, out var on, out error), error);
        Assert.Equal("gate_open", switchId);
        Assert.True(on);
        Assert.Equal("Porte ouverte", definition.Switches[0].Label);
        Assert.Equal("Coffre du hall", definition.Switches[0].Note);

        definition.Switches[0].Id = "porte ouverte";
        Assert.False(definition.Validate(out error));
        Assert.Contains("[A-Za-z0-9_]", error, StringComparison.Ordinal);

        definition.Switches[0].Id = "gate_open";
        definition.Switches.Add(new SystemSwitchEntry { Id = "gate_open", Label = "Doublon" });
        Assert.False(definition.Validate(out error));
        Assert.Contains("en double", error, StringComparison.Ordinal);

        definition.Switches.RemoveAt(1);
        definition.Variables[0].Label = " ";
        Assert.False(definition.Validate(out error));
        Assert.Contains("Libellé de variable", error, StringComparison.Ordinal);

        definition.Id = Guid.NewGuid();
        definition.Variables[0].Label = "Nuits passées";
        Assert.False(definition.Validate(out _));
    }

    [Fact]
    public async Task DraftPublish_RoundTrip_PublishedStaysWhileDraftMoves()
    {
        var repository = new InMemorySystemRepository();
        var session = new SystemWorkspaceSession(repository);
        await session.LoadAsync();
        Assert.NotNull(session.Current);
        Assert.Empty(session.Current.Switches);
        Assert.False(session.IsDirty);

        session.Current.Switches.Add(new SystemSwitchEntry
        {
            Id = "gate_open",
            Label = "Porte ouverte",
            Note = "Coffre du hall",
        });
        session.Current.Variables.Add(new SystemVariableEntry
        {
            Id = "nuits",
            Label = "Nuits passées",
        });
        session.MarkDirty();

        var draft = Assert.IsType<SaveSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, draft.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Null(await repository.LoadPublishedAsync());

        var published = Assert.IsType<SaveSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.NewRevision + 1, published.NewRevision);
        Assert.Equal(published.NewRevision, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        session.Current.Switches[0].Label = "Portail ouvert";
        session.MarkDirty();
        Assert.IsType<SaveSystemResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Equal(published.PublishedRevision, session.PublishedRevision);

        var stored = await repository.LoadAsync();
        Assert.Equal("Portail ouvert", stored!.Definition.Switches[0].Label);
        Assert.Equal("Coffre du hall", stored.Definition.Switches[0].Note);
        Assert.Equal("Nuits passées", stored.Definition.Variables[0].Label);

        var publishedStored = await repository.LoadPublishedAsync();
        Assert.Equal("Porte ouverte", publishedStored!.Definition.Switches[0].Label);
        Assert.Equal(published.PublishedRevision, publishedStored.PublishedRevision);

        var stale = new SystemWorkspaceSession(repository);
        var winner = new SystemWorkspaceSession(repository);
        await stale.LoadAsync();
        await winner.LoadAsync();
        winner.Current!.Variables[0].Label = "Autre";
        Assert.IsType<SaveSystemResult.Success>(await winner.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        stale.Current!.Switches[0].Label = "Perdant";
        var blocked = Assert.IsType<SaveSystemResult.Conflict>(
            await stale.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(winner.CurrentRevision, blocked.CurrentRevision);
    }

    [Fact]
    public async Task DemoRepository_RefusesSave()
    {
        var repository = new InMemorySystemRepository(ContentRepositoryCapabilities.InMemoryDemo);
        var session = new SystemWorkspaceSession(repository);
        await session.LoadAsync();
        session.Current!.Switches.Add(new SystemSwitchEntry { Id = "a", Label = "A" });
        Assert.IsType<SaveSystemResult.NotDurable>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Null(await repository.LoadAsync());
    }

    private static SystemDefinition Sample() => new()
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
