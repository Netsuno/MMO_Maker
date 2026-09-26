using System;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;
using ServerPlanner = Frog.Server.Gameplay.MapEventExecutionPlanner;

namespace Frog.Tests;

public sealed class ChangeEconomyCommandTests
{
    private static readonly Guid ItemId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");

    [Fact]
    public void Palette_ChangerOrEtObjets_DefaultsAndKeepsHello11()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ChangeGoldId && entry.Label == "Changer or");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ChangeItemsId && entry.Label == "Changer objets");
        Assert.Equal(Guid.Empty, MapEventCommandPalette.ChangeItemsPlaceholderId);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeGoldId, out var gold));
        Assert.Equal(MapEventCommandDiscriminators.ChangeGold, gold.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(gold, out var goldError), goldError);
        Assert.True(
            MapEventParameterSchemas.TryParseChangeGold(gold.ParameterJson, out var operation, out var amount, out var parseErr),
            parseErr);
        Assert.Equal(MapEventChangeOperation.Increase, operation);
        Assert.Equal(1, amount);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeItemsId, out var items));
        Assert.Equal(MapEventCommandDiscriminators.ChangeItems, items.Discriminator);
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(items, out var itemError));
        Assert.Contains("itemId", itemError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rewrite_IncreaseAndDecrease_BecomeGiveAndTake()
    {
        var giveGold = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeGold,
            ParameterJson = """{"operation":"INCREASE","amount":25}""",
        };
        Assert.True(MapEventParameterSchemas.TryRewriteChangeGold(giveGold, out var goldUp, out var goldUpErr), goldUpErr);
        Assert.Equal(MapEventCommandDiscriminators.GiveGold, goldUp.Discriminator);
        Assert.Contains("\"amount\":25", goldUp.ParameterJson, StringComparison.Ordinal);

        var takeGold = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeGold,
            ParameterJson = """{"operation":"decrease","amount":4}""",
        };
        Assert.True(MapEventParameterSchemas.TryRewriteChangeGold(takeGold, out var goldDown, out var goldDownErr), goldDownErr);
        Assert.Equal(MapEventCommandDiscriminators.TakeGold, goldDown.Discriminator);

        var giveItem = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeItems,
            ParameterJson = $$"""{"itemId":"{{ItemId:D}}","operation":"increase","quantity":2}""",
        };
        Assert.True(MapEventParameterSchemas.TryRewriteChangeItems(giveItem, out var itemUp, out var itemUpErr), itemUpErr);
        Assert.Equal(MapEventCommandDiscriminators.GiveItem, itemUp.Discriminator);
        Assert.Contains(ItemId.ToString("D"), itemUp.ParameterJson, StringComparison.Ordinal);

        var takeItem = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeItems,
            ParameterJson = $$"""{"itemId":"{{ItemId:D}}","operation":"decrease","quantity":1}""",
        };
        Assert.True(MapEventParameterSchemas.TryRewriteChangeItems(takeItem, out var itemDown, out var itemDownErr), itemDownErr);
        Assert.Equal(MapEventCommandDiscriminators.TakeItem, itemDown.Discriminator);
    }

    [Theory]
    [InlineData("""{"operation":"set","amount":1}""")]
    [InlineData("""{"operation":"increase","amount":0}""")]
    [InlineData("""{"amount":1}""")]
    [InlineData("""{"operation":"increase","amount":1,"onceKey":"chest"}""")]
    public void Validator_RejectsBadChangeGold(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeGold,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_ShowSignedGoldAndItems()
    {
        Assert.Equal("Changer or", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeGold));
        Assert.Equal("Changer objets", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeItems));
        Assert.Equal("Opération", MapEventEditorLabels.Field("operation"));
        Assert.Equal("Augmenter", MapEventEditorLabels.ChangeOperation(MapEventChangeOperation.Increase));
        Assert.Equal("Diminuer", MapEventEditorLabels.ChangeOperation(MapEventChangeOperation.Decrease));
        Assert.Equal(
            "Changer or : + 10",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ChangeGold,
                """{"operation":"increase","amount":10}"""));
        Assert.Equal(
            "Changer or : − 3",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ChangeGold,
                """{"operation":"decrease","amount":3}"""));
        Assert.Contains(
            "Changer objets : +",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ChangeItems,
                $$"""{"itemId":"{{ItemId:D}}","operation":"increase","quantity":2}"""),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Planner_TreatsChangeCommandsAsPersistent()
    {
        Assert.Equal(
            MapEventEffectCommitKind.Persistent,
            MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeGold));
        Assert.Equal(
            MapEventEffectCommitKind.Persistent,
            MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeItems));
        Assert.True(ServerPlanner.CanExecuteTransactionally(
        [
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.ChangeGold,
                ParameterJson = """{"operation":"increase","amount":10}""",
            },
            new MapEventCommandDefinition
            {
                Discriminator = MapEventCommandDiscriminators.ChangeItems,
                ParameterJson = $$"""{"itemId":"{{ItemId:D}}","operation":"decrease","quantity":1}""",
            },
        ]));
    }

    [Fact]
    public void Sandbox_AppliesIncreaseAndDecreaseOnCommit()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        sandbox.World.Gold = 40;
        sandbox.World.Items[ItemId] = 3;
        var identity = MapEventExecutionIdentity.BeginActivation(Guid.NewGuid(), placementId: 4, catalogAliasId: 8);
        var unit = MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(
            identity,
            [
                Cmd(MapEventCommandDiscriminators.ChangeGold, """{"operation":"increase","amount":10}"""),
                Cmd(
                    MapEventCommandDiscriminators.ChangeItems,
                    $$"""{"itemId":"{{ItemId:D}}","operation":"decrease","quantity":2}"""),
            ]));

        var outcome = sandbox.TryCommit(unit);

        Assert.Equal(MapEventCommitDisposition.Committed, outcome.Disposition);
        Assert.Equal(50, sandbox.World.Gold);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
    }

    [Fact]
    public void Sandbox_InsufficientGold_RollsBackThePage()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        sandbox.World.Gold = 5;
        sandbox.World.Items[ItemId] = 1;
        var identity = MapEventExecutionIdentity.BeginActivation(Guid.NewGuid(), placementId: 4, catalogAliasId: 9);
        var unit = MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(
            identity,
            [
                Cmd(
                    MapEventCommandDiscriminators.ChangeItems,
                    $$"""{"itemId":"{{ItemId:D}}","operation":"increase","quantity":4}"""),
                Cmd(MapEventCommandDiscriminators.ChangeGold, """{"operation":"decrease","amount":10}"""),
            ]));

        var outcome = sandbox.TryCommit(unit);

        Assert.Equal(MapEventCommitDisposition.RolledBack, outcome.Disposition);
        Assert.Contains("or insuffisant", outcome.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, sandbox.World.Gold);
        Assert.Equal(1, sandbox.World.Items[ItemId]);
    }

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };
}
