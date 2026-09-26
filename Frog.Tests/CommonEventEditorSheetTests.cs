using System;
using System.Collections.Generic;
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
/// Fiche Événements communs : déclencheur et interrupteur sur les pages existantes.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class CommonEventEditorSheetTests
{
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndCommonEventsEntryExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var window = File.ReadAllText(Path.Combine(root, "Frog.Editor", "MainWindow.xaml"));
        var commands = File.ReadAllText(Path.Combine(root, "Frog.Editor", "MainWindow.xaml.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "MainForm.cs"));
        var service = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Services", "Phase8ContentPostgreSqlService.cs"));

        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("Événements communs…", window, StringComparison.Ordinal);
        Assert.Contains("CmdBrowseCommonEvents", commands, StringComparison.Ordinal);
        Assert.Contains("BrowseCommonEvents", form, StringComparison.Ordinal);
        Assert.Contains("CreateDefaultPages", service, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatListLine_AndNextAlias_SkipUsedNumbers()
    {
        Assert.Equal("001  Ouverture  ·  Brouillon", CommonEventEditorSheet.FormatListLine(1, "Ouverture", "Brouillon"));
        Assert.Equal("—  (sans nom)  ·  Publié", CommonEventEditorSheet.FormatListLine(null, "  ", "Publié"));
        Assert.Equal(1, CommonEventEditorSheet.NextAlias(Array.Empty<int?>()));
        Assert.Equal(3, CommonEventEditorSheet.NextAlias(new int?[] { 1, null, 2, 0 }));

        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var rows = new (Guid Id, int? Alias)[] { (id, 4), (other, 5) };
        Assert.False(CommonEventEditorSheet.IsAliasTaken(rows, id, 4));
        Assert.True(CommonEventEditorSheet.IsAliasTaken(rows, id, 5));
        Assert.False(CommonEventEditorSheet.IsAliasTaken(rows, id, 0));
    }

    [Fact]
    public void Apply_SetsTriggerAndSwitch_KeepsCommandsAndOtherConditions()
    {
        var kept = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            ParameterJson = """{"text":"Bonjour."}""",
        };
        var level = new MapEventConditionDefinition
        {
            Kind = MapEventConditionKinds.CharacterLevel,
            ParameterJson = """{"minLevel":2}""",
        };
        var second = new MapEventPageDefinition
        {
            PageOrder = 1,
            TriggerKind = Phase8MapEventTriggerKinds.Parallel,
            Commands = [kept],
        };

        Assert.True(CommonEventEditorSheet.TryApplyToPage(
            [Page(Phase8MapEventTriggerKinds.Action, [level], [kept]), second],
            pageIndex: 0,
            Phase8MapEventTriggerKinds.Autorun,
            "porte_ouverte",
            switchValue: false,
            out var updated,
            out var error), error);

        Assert.Equal(2, updated.Count);
        Assert.Equal(Phase8MapEventTriggerKinds.Autorun, updated[0].TriggerKind);
        Assert.Equal(Phase8MapEventTriggerKinds.Parallel, updated[1].TriggerKind);
        Assert.Equal(MapEventConditionKinds.CharacterSwitch, updated[0].Conditions[0].Kind);
        Assert.Equal(MapEventConditionKinds.CharacterLevel, updated[0].Conditions[1].Kind);
        Assert.Equal(kept.ParameterJson, updated[0].Commands[0].ParameterJson);
        Assert.Single(updated[1].Commands);

        Assert.True(CommonEventEditorSheet.TryReadPage(updated, 0, out var trigger, out var switchId, out var value));
        Assert.Equal(Phase8MapEventTriggerKinds.Autorun, trigger);
        Assert.Equal("porte_ouverte", switchId);
        Assert.False(value);

        Assert.True(CommonEventEditorSheet.TryApplyToPage(
            updated,
            0,
            Phase8MapEventTriggerKinds.Autorun,
            switchId: null,
            switchValue: true,
            out var cleared,
            out error), error);
        Assert.Single(cleared[0].Conditions);
        Assert.Equal(MapEventConditionKinds.CharacterLevel, cleared[0].Conditions[0].Kind);
        Assert.True(CommonEventEditorSheet.TryReadPage(cleared, 0, out _, out var gone, out _));
        Assert.Null(gone);
    }

    [Fact]
    public void Apply_RejectsUnknownTriggerAndSwitch_AndCreatesAPageWhenEmpty()
    {
        Assert.False(CommonEventEditorSheet.TryApplyToPage(
            CommonEventEditorSheet.CreateDefaultPages(),
            0,
            "none",
            null,
            true,
            out _,
            out var triggerError));
        Assert.Contains("Déclencheur", triggerError, StringComparison.Ordinal);

        Assert.False(CommonEventEditorSheet.TryApplyToPage(
            CommonEventEditorSheet.CreateDefaultPages(),
            0,
            Phase8MapEventTriggerKinds.Parallel,
            "mauvais-id",
            true,
            out _,
            out var switchError));
        Assert.False(string.IsNullOrWhiteSpace(switchError));

        Assert.True(CommonEventEditorSheet.TryApplyToPage(
            Array.Empty<MapEventPageDefinition>(),
            -1,
            Phase8MapEventTriggerKinds.Parallel,
            "intro_vue",
            true,
            out var created,
            out var createdError), createdError);
        Assert.Single(created);
        Assert.Equal(Phase8MapEventTriggerKinds.Parallel, created[0].TriggerKind);
        Assert.True(created[0].Validate(out var pageError), pageError);
        Assert.Equal(Phase8MapEventTriggerKinds.Action, CommonEventEditorSheet.CreateDefaultPages()[0].TriggerKind);
    }

    [Fact]
    public async Task SaveDraft_Publish_RoundTrip_KeepsTriggerSwitchAndCommand()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            ParameterJson = """{"text":"Bienvenue."}""",
        };
        Assert.True(CommonEventEditorSheet.TryCompose(
            id,
            "Ouverture",
            editorAliasId: 4,
            pages: [Page(Phase8MapEventTriggerKinds.Action, [], [command])],
            pageIndex: 0,
            Phase8MapEventTriggerKinds.Parallel,
            "intro_vue",
            switchValue: true,
            out var definition,
            out var composeError), composeError);
        Assert.NotNull(definition);

        var repo = new InMemoryPhase8ContentEditorRepository();
        var json = JsonSerializer.Serialize(definition, Camel);
        var drafted = Assert.IsType<Phase8SaveContentResult.Success>(await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = id,
            Kind = Phase8ContentKind.CommonEvent,
            Name = definition.Name,
            EditorAliasId = definition.EditorAliasId,
            PayloadJson = json,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(ContentPublishStatus.Draft, (await repo.LoadDraftByIdAsync(id))!.Status);

        var published = Assert.IsType<Phase8SaveContentResult.Success>(await repo.SaveAsync(new Phase8SaveContentRequest
        {
            ContentId = id,
            Kind = Phase8ContentKind.CommonEvent,
            Name = "Ouverture",
            EditorAliasId = 4,
            PayloadJson = json,
            ExpectedRevision = drafted.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(published.NewRevision, published.PublishedRevision);

        var stored = await repo.LoadDraftByIdAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(ContentPublishStatus.Published, stored.Status);
        Assert.Equal(4, stored.EditorAliasId);
        var restored = JsonSerializer.Deserialize<CommonEventDefinition>(stored.PayloadJson, Camel);
        Assert.NotNull(restored);
        Assert.Equal("Ouverture", restored.Name);
        Assert.True(CommonEventEditorSheet.TryReadPage(restored.Pages, 0, out var trigger, out var switchId, out var value));
        Assert.Equal(Phase8MapEventTriggerKinds.Parallel, trigger);
        Assert.Equal("intro_vue", switchId);
        Assert.True(value);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, restored.Pages[0].Commands[0].Discriminator);
        Assert.Contains("Bienvenue.", restored.Pages[0].Commands[0].ParameterJson, StringComparison.Ordinal);
    }

    private static MapEventPageDefinition Page(
        string trigger,
        IReadOnlyList<MapEventConditionDefinition> conditions,
        IReadOnlyList<MapEventCommandDefinition> commands) => new()
    {
        PageOrder = 0,
        TriggerKind = trigger,
        MovementKind = MapEventMovementKinds.Fixed,
        Conditions = conditions,
        Commands = commands,
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
