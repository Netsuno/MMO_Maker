using System;
using System.Collections.Generic;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterIdentityCommandTests
{
    private static readonly Guid ClassId = Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");

    [Fact]
    public void Palette_ChangeNameAndClass_ValidateWithoutBumpingHello()
    {
        var nameJson = """{"name":"Grenouille"}""";
        var classJson = $$"""{"classId":"{{ClassId:D}}"}""";
        var rename = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeName,
            ParameterJson = nameJson,
        };
        var reclass = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeClass,
            ParameterJson = classJson,
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(rename, out var nameErr), nameErr);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(reclass, out var classErr), classErr);
        Assert.Equal("Changer nom", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeName));
        Assert.Equal("Changer classe", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeClass));
        Assert.Contains("Grenouille", MapEventEditorLabels.CommandSummary(rename.Discriminator, nameJson), StringComparison.Ordinal);
        Assert.Contains("bbbbbbbb", MapEventEditorLabels.CommandSummary(reclass.Discriminator, classJson), StringComparison.Ordinal);
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeName));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeClass));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeNameId, out var createdName));
        Assert.True(MapEventParameterSchemas.TryParseChangeName(createdName.ParameterJson, out var defaultName, out var defaultErr), defaultErr);
        Assert.Equal(MapEventCommandPalette.ChangeNameDefault, defaultName);
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeClassId, out _));
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.ChangeName, """{"name":""}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeName, """{"name":"Bad/Name"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeName, """{"name":"Grenouille","extra":1}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeClass, """{"classId":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeClass, """{"classId":"not-a-guid"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeClass, """{"classId":"bbbbbbbb-cccc-4ddd-8eee-ffffffffffff","keepLevel":true}""")]
    public void Validator_RejectsBlankNameUnknownClassAndExtraFields(string discriminator, string parameterJson)
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
    public void Rename_IsIdempotent_ReclassKeepsCallerLevel()
    {
        Assert.True(CharacterIdentityChange.TryRename("Hero", "Grenouille", out var renamed, out var changed, out var error), error);
        Assert.True(changed);
        Assert.Equal("Grenouille", renamed);

        Assert.True(CharacterIdentityChange.TryRename(renamed, "Grenouille", out var again, out changed, out error), error);
        Assert.False(changed);
        Assert.Equal("Grenouille", again);

        Assert.True(CharacterIdentityChange.NameTaken(
            "Pris",
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            [(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "pris")]));
        Assert.False(CharacterIdentityChange.NameTaken(
            "Grenouille",
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            [(Guid.Parse("11111111-2222-3333-4444-555555555555"), "Grenouille")]));

        var currentClass = Guid.Parse("aaaaaaaa-0001-4000-8000-000000000001");
        Assert.True(CharacterIdentityChange.TryReclass(currentClass, ClassId, out var next, out changed, out error), error);
        Assert.True(changed);
        Assert.Equal(ClassId, next);
        Assert.True(CharacterIdentityChange.TryReclass(ClassId, ClassId, out _, out changed, out error), error);
        Assert.False(changed);
    }

    [Fact]
    public void Sandbox_CommitsIdentityAndRollsBackTheWholeUnit()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var unit = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeName, """{"name":"Grenouille"}"""),
            Cmd(MapEventCommandDiscriminators.ChangeClass, $$"""{"classId":"{{ClassId:D}}"}"""),
            Cmd(MapEventCommandDiscriminators.SetSwitch, """{"switchId":"named","value":true}"""),
        ]);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Equal("Grenouille", sandbox.World.DisplayName);
        Assert.Equal(ClassId, sandbox.World.ClassId);
        Assert.True(sandbox.World.Switches["named"]);

        var rollback = new MapEventTransactionalCommitSandbox();
        var broken = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeName, """{"name":"Grenouille"}"""),
            Cmd(MapEventCommandDiscriminators.ChangeClass, """{"classId":"00000000-0000-0000-0000-000000000000"}"""),
        ]);
        Assert.Equal(MapEventCommitDisposition.RolledBack, rollback.TryCommit(broken).Disposition);
        Assert.Equal(string.Empty, rollback.World.DisplayName);
        Assert.Equal(Guid.Empty, rollback.World.ClassId);
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
