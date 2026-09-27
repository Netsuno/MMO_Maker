using System;
using System.Collections.Generic;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterVitalCommandTests
{
    [Fact]
    public void Palette_RecoverAllAndChangeHpMp_ValidateWithoutBumpingHello()
    {
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.RecoverAllId, out var recover));
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeHpMpId, out var change));
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(recover, out var recoverErr), recoverErr);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(change, out var changeErr), changeErr);
        Assert.Equal("Récupération totale", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.RecoverAll));
        Assert.Equal("Changer PV/PM", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeHpMp));
        Assert.Equal(
            "Récupération totale",
            MapEventEditorLabels.CommandSummary(MapEventCommandDiscriminators.RecoverAll, "{}"));
        Assert.Equal(
            "Changer PV/PM : PM − 4",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ChangeHpMp,
                """{"vital":"MP","operation":"decrease","amount":4}"""));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.RecoverAll));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeHpMp));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.RecoverAll, """{"states":true}""")]
    [InlineData(MapEventCommandDiscriminators.RecoverAll, "[]")]
    [InlineData(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"MAXHP","operation":"increase","amount":1}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"HP","operation":"learn","amount":1}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"HP","operation":"increase","amount":0}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"MP","operation":"decrease","amount":1,"extra":1}""")]
    public void Validator_RejectsEmptyUnknownAndExtra(string discriminator, string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = discriminator,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Apply_RestoresCurrentVitalsAndClampsHpMp()
    {
        var hero = CharacterVitals.Default with
        {
            Level = 4,
            Experience = 12,
            Hp = 11,
            MaxHp = 80,
            Mp = 2,
            MaxMp = 30,
            Str = 9,
        };

        Assert.True(CharacterVitalCommands.TryApply(
            MapEventCommandDiscriminators.RecoverAll,
            "{}",
            hero,
            out var restored,
            out var restoredChanged,
            out var statsChanged,
            out var error), error);
        Assert.True(restoredChanged);
        Assert.False(statsChanged);
        Assert.Equal(80, restored.Hp);
        Assert.Equal(30, restored.Mp);
        Assert.Equal(80, restored.MaxHp);
        Assert.Equal(30, restored.MaxMp);
        Assert.Equal(4, restored.Level);
        Assert.Equal(12, restored.Experience);
        Assert.Equal(9, restored.Str);

        Assert.True(CharacterVitalCommands.TryApply(
            MapEventCommandDiscriminators.ChangeHpMp,
            """{"vital":"HP","operation":"decrease","amount":100}""",
            restored,
            out var hurt,
            out _,
            out statsChanged,
            out error), error);
        Assert.False(statsChanged);
        Assert.Equal(0, hurt.Hp);
        Assert.Equal(30, hurt.Mp);
        Assert.Equal(80, hurt.MaxHp);

        Assert.True(CharacterVitalCommands.TryApply(
            MapEventCommandDiscriminators.ChangeHpMp,
            """{"vital":"MP","operation":"increase","amount":5}""",
            hurt,
            out var regained,
            out _,
            out _,
            out error), error);
        Assert.Equal(0, regained.Hp);
        Assert.Equal(30, regained.Mp);
        Assert.Equal(9, regained.Str);
    }

    [Fact]
    public void Sandbox_CommitsVitalsAndRollsBackTheWholeUnit()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        sandbox.World.Vitals = CharacterVitals.Default with { Hp = 12, MaxHp = 50, Mp = 3, MaxMp = 18, Str = 7 };
        var unit = Unit(
        [
            Cmd(MapEventCommandDiscriminators.RecoverAll, "{}"),
            Cmd(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"HP","operation":"decrease","amount":5}"""),
            Cmd(MapEventCommandDiscriminators.SetSwitch, """{"switchId":"healed","value":true}"""),
        ]);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Equal(45, sandbox.World.Vitals.Hp);
        Assert.Equal(18, sandbox.World.Vitals.Mp);
        Assert.Equal(50, sandbox.World.Vitals.MaxHp);
        Assert.Equal(7, sandbox.World.Vitals.Str);
        Assert.True(sandbox.World.Switches["healed"]);

        var rollback = new MapEventTransactionalCommitSandbox();
        rollback.World.Vitals = CharacterVitals.Default with { Hp = 12, MaxHp = 50, Mp = 3, MaxMp = 18 };
        var broken = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeHpMp, """{"vital":"HP","operation":"decrease","amount":5}"""),
            Cmd(MapEventCommandDiscriminators.GiveItem, """{"itemId":"00000000-0000-0000-0000-000000000000","quantity":1}"""),
        ]);
        Assert.Equal(MapEventCommitDisposition.RolledBack, rollback.TryCommit(broken).Disposition);
        Assert.Equal(12, rollback.World.Vitals.Hp);
        Assert.Equal(3, rollback.World.Vitals.Mp);
    }

    private static MapEventExecutionIdentity Identity() =>
        MapEventExecutionIdentity.Create(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            placementId: 84,
            catalogAliasId: 84,
            requestId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private static MapEventTransactionalUnit Unit(IReadOnlyList<MapEventCommandDefinition> effects) =>
        MapEventTransactionalUnit.FromPlan(MapEventExecutionPlan.Ok(Identity(), effects));

    private static MapEventCommandDefinition Cmd(string discriminator, string json) =>
        new()
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = json,
        };
}
