using System;
using System.Collections.Generic;
using System.Text.Json;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterProgressionCommandTests
{
    [Fact]
    public void Palette_ChangeLevelExpAndParam_ValidateWithoutBumpingHello()
    {
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeLevelId, out var level));
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeExpId, out var exp));
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeParamId, out var param));
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(level, out var levelErr), levelErr);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(exp, out var expErr), expErr);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(param, out var paramErr), paramErr);
        Assert.Equal("Changer niveau", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeLevel));
        Assert.Equal("Changer EXP", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeExp));
        Assert.Equal("Changer paramètre", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeParam));
        Assert.Equal(
            "Changer niveau : +2",
            MapEventEditorLabels.CommandSummary(MapEventCommandDiscriminators.ChangeLevel, """{"delta":2}"""));
        Assert.Equal(
            "Changer EXP : −15",
            MapEventEditorLabels.CommandSummary(MapEventCommandDiscriminators.ChangeExp, """{"delta":-15}"""));
        Assert.Equal(
            "Changer paramètre : STR +1",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ChangeParam,
                """{"stat":"STR","delta":1}"""));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeExp));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeParam));
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.ChangeLevel, """{"delta":0}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeLevel, """{"delta":99}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeExp, """{"amount":10}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeParam, """{"stat":"ATK","delta":1}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeParam, """{"stat":"STR","delta":1,"extra":1}""")]
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
    public void Adjust_KeepsLevelExpAndParamsIndependent()
    {
        var hero = new CharacterVitals
        {
            Level = 1,
            Experience = 0,
            Hp = 100,
            MaxHp = 100,
            Mp = 40,
            MaxMp = 40,
            Str = 10,
            Agi = 10,
            Vit = 10,
            Int = 10,
            Dex = 10,
            Luck = 10,
        };

        var leveled = CharacterProgressionAdjust.AdjustLevel(hero, 2);
        Assert.Equal(3, leveled.Level);
        Assert.Equal(0, leveled.Experience);
        Assert.Equal(10, leveled.Str);
        Assert.Equal(100, leveled.Hp);

        var experienced = CharacterProgressionAdjust.AdjustExperience(leveled, 5_000);
        Assert.Equal(3, experienced.Level);
        Assert.Equal(ProgressionCurve.ExperienceToNextLevel(3), experienced.Experience);
        Assert.Equal(10, experienced.Str);

        var stronger = CharacterProgressionAdjust.AdjustParam(experienced, CharacterProgressionAdjust.StatStr, 3);
        Assert.Equal(13, stronger.Str);
        Assert.Equal(10, stronger.Agi);
        Assert.Equal(experienced.Level, stronger.Level);
        Assert.Equal(experienced.Experience, stronger.Experience);

        var hurt = CharacterProgressionAdjust.AdjustParam(stronger, CharacterProgressionAdjust.StatHp, -10);
        Assert.Equal(90, hurt.Hp);
        Assert.Equal(100, hurt.MaxHp);

        var capped = CharacterProgressionAdjust.AdjustLevel(hero, 500);
        Assert.Equal(ProgressionCurve.MaxLevel, capped.Level);
        Assert.Equal(0, capped.Experience);

        var floored = CharacterProgressionAdjust.AdjustExperience(hero, -20);
        Assert.Equal(0, floored.Experience);
        Assert.Equal(1, floored.Level);

        var statCap = CharacterProgressionAdjust.AdjustParam(hero, CharacterProgressionAdjust.StatLuck, 500);
        Assert.Equal(99, statCap.Luck);
    }

    [Fact]
    public void Sandbox_CommitsProgressionAndRollsBackTheWholeUnit()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var unit = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeLevel, """{"delta":2}"""),
            Cmd(MapEventCommandDiscriminators.ChangeExp, """{"delta":50}"""),
            Cmd(MapEventCommandDiscriminators.ChangeParam, """{"stat":"AGI","delta":4}"""),
            Cmd(MapEventCommandDiscriminators.SetSwitch, """{"switchId":"trained","value":true}"""),
        ]);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Equal(3, sandbox.World.Vitals.Level);
        Assert.Equal(50, sandbox.World.Vitals.Experience);
        Assert.Equal(5, sandbox.World.Vitals.Agi);
        Assert.True(sandbox.World.Switches["trained"]);

        var rollback = new MapEventTransactionalCommitSandbox();
        var broken = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeLevel, """{"delta":4}"""),
            Cmd(MapEventCommandDiscriminators.GiveItem, """{"itemId":"00000000-0000-0000-0000-000000000000","quantity":1}"""),
        ]);
        Assert.Equal(MapEventCommitDisposition.RolledBack, rollback.TryCommit(broken).Disposition);
        Assert.Equal(CharacterVitals.Default.Level, rollback.World.Vitals.Level);
        Assert.Equal(CharacterVitals.Default.Experience, rollback.World.Vitals.Experience);
    }

    [Fact]
    public void StatsPayload_ReusesCharacterPayloadShape()
    {
        var json = CharacterProgressionAdjust.StatsPayloadJson(11, 12, 13, 14, 15, 16);
        using var doc = JsonDocument.Parse(json);
        var stats = doc.RootElement.GetProperty("stats");
        Assert.Equal(11, stats.GetProperty("STR").GetInt32());
        Assert.Equal(12, stats.GetProperty("AGI").GetInt32());
        Assert.Equal(13, stats.GetProperty("DEX").GetInt32());
        Assert.Equal(14, stats.GetProperty("INT").GetInt32());
        Assert.Equal(15, stats.GetProperty("VIT").GetInt32());
        Assert.Equal(16, stats.GetProperty("LUCK").GetInt32());
        Assert.Equal("STR 11 · AGI 12 · DEX 13 · INT 14 · VIT 15 · LUCK 16", CharacterProgressionAdjust.FormatPrimaryStats(11, 12, 13, 14, 15, 16));
    }

    private static MapEventExecutionIdentity Identity() =>
        MapEventExecutionIdentity.Create(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            placementId: 81,
            catalogAliasId: 81,
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
