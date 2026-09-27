using System;
using System.Collections.Generic;
using Frog.Core.Constants;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterLoadoutCommandTests
{
    private static readonly Guid SkillId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid WeaponId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Palette_ChangeSkillsAndEquipment_ValidateWithoutBumpingHello()
    {
        var skillJson = $$"""{"operation":"increase","skillId":"{{SkillId:D}}"}""";
        var gearJson = $$"""{"operation":"equip","slot":"weapon","itemId":"{{WeaponId:D}}"}""";
        var skill = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeSkills,
            ParameterJson = skillJson,
        };
        var gear = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ChangeEquipment,
            ParameterJson = gearJson,
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(skill, out var skillErr), skillErr);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(gear, out var gearErr), gearErr);
        Assert.Equal("Changer compétences", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeSkills));
        Assert.Equal("Changer équipement", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ChangeEquipment));
        Assert.Contains("Changer compétences", MapEventEditorLabels.CommandSummary(skill.Discriminator, skillJson), StringComparison.Ordinal);
        Assert.Contains("Arme", MapEventEditorLabels.CommandSummary(gear.Discriminator, gearJson), StringComparison.Ordinal);
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeSkills));
        Assert.Equal(MapEventEffectCommitKind.Persistent, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.ChangeEquipment));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeSkillsId, out _));
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ChangeEquipmentId, out _));
    }

    [Theory]
    [InlineData(MapEventCommandDiscriminators.ChangeSkills, """{"operation":"learn","skillId":"11111111-2222-3333-4444-555555555555"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeSkills, """{"operation":"increase","skillId":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeSkills, """{"operation":"increase","skillId":"11111111-2222-3333-4444-555555555555","extra":1}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeEquipment, """{"operation":"equip","slot":"head","itemId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}""")]
    [InlineData(MapEventCommandDiscriminators.ChangeEquipment, """{"operation":"equip","slot":"weapon","itemId":"00000000-0000-0000-0000-000000000000"}""")]
    public void Validator_RejectsUnknownSlotOperationAndEmptyIds(string discriminator, string parameterJson)
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
    public void Unequip_AllowsMissingItemId()
    {
        Assert.True(MapEventParameterSchemas.TryParseChangeEquipment(
            """{"operation":"unequip","slot":"armor"}""",
            out var operation,
            out var slot,
            out var itemId,
            out var error), error);
        Assert.Equal(CharacterGearChange.Unequip, operation);
        Assert.Equal(CharacterGearChange.SlotArmor, slot);
        Assert.Equal(Guid.Empty, itemId);
    }

    [Fact]
    public void Skills_LearnIsIdempotent_ForgetFailsWhenAbsent()
    {
        var known = new HashSet<Guid>();
        Assert.True(CharacterSkillSet.TryChange(known, MapEventChangeOperation.Increase, SkillId, out var learned, out var changed, out var error), error);
        Assert.True(changed);
        Assert.Contains(SkillId, learned);

        Assert.True(CharacterSkillSet.TryChange(learned, MapEventChangeOperation.Increase, SkillId, out var again, out changed, out error), error);
        Assert.False(changed);
        Assert.Equal(learned, again);

        Assert.False(CharacterSkillSet.TryChange(known, MapEventChangeOperation.Decrease, SkillId, out _, out _, out error));
        Assert.Equal("change_skills: compétence absente.", error);

        Assert.True(CharacterSkillSet.TryChange(learned, MapEventChangeOperation.Decrease, SkillId, out var forgotten, out changed, out error), error);
        Assert.True(changed);
        Assert.DoesNotContain(SkillId, forgotten);
        Assert.Equal("[]", CharacterSkillSet.Format(forgotten));
    }

    [Fact]
    public void Gear_EquipReplaces_UnequipFailsWhenEmpty()
    {
        Assert.True(CharacterGearChange.TryApply(
            null,
            null,
            CharacterGearChange.Equip,
            CharacterGearChange.SlotWeapon,
            WeaponId,
            out var weapon,
            out var armor,
            out var changed,
            out var error), error);
        Assert.True(changed);
        Assert.Equal(WeaponId, weapon);
        Assert.Null(armor);

        Assert.False(CharacterGearChange.TryApply(
            null,
            null,
            CharacterGearChange.Unequip,
            CharacterGearChange.SlotArmor,
            Guid.Empty,
            out _,
            out _,
            out _,
            out error));
        Assert.Equal("change_equipment: emplacement vide.", error);

        Assert.True(CharacterGearChange.TryApply(
            weapon,
            armor,
            CharacterGearChange.Unequip,
            CharacterGearChange.SlotWeapon,
            Guid.Empty,
            out var cleared,
            out _,
            out changed,
            out error), error);
        Assert.True(changed);
        Assert.Null(cleared);
    }

    [Fact]
    public void Sandbox_CommitsLoadoutAndRollsBackTheWholeUnit()
    {
        var sandbox = new MapEventTransactionalCommitSandbox();
        var unit = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeSkills, $$"""{"operation":"increase","skillId":"{{SkillId:D}}"}"""),
            Cmd(MapEventCommandDiscriminators.ChangeEquipment, $$"""{"operation":"equip","slot":"weapon","itemId":"{{WeaponId:D}}"}"""),
            Cmd(MapEventCommandDiscriminators.SetSwitch, """{"switchId":"armed","value":true}"""),
        ]);
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Contains(SkillId, sandbox.World.LearnedSkillIds);
        Assert.Equal(WeaponId, sandbox.World.WeaponItemId);
        Assert.True(sandbox.World.Switches["armed"]);

        var rollback = new MapEventTransactionalCommitSandbox();
        var broken = Unit(
        [
            Cmd(MapEventCommandDiscriminators.ChangeEquipment, $$"""{"operation":"equip","slot":"armor","itemId":"{{WeaponId:D}}"}"""),
            Cmd(MapEventCommandDiscriminators.ChangeSkills, $$"""{"operation":"decrease","skillId":"{{SkillId:D}}"}"""),
        ]);
        Assert.Equal(MapEventCommitDisposition.RolledBack, rollback.TryCommit(broken).Disposition);
        Assert.Empty(rollback.World.LearnedSkillIds);
        Assert.Null(rollback.World.ArmorItemId);
        Assert.Null(rollback.World.WeaponItemId);
    }

    private static MapEventExecutionIdentity Identity() =>
        MapEventExecutionIdentity.Create(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            placementId: 82,
            catalogAliasId: 82,
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
