using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core;
using Frog.Core.Character;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Models;
using Frog.Server.Services;
using Microsoft.Extensions.Logging;

namespace Frog.Server.Gameplay;

/// <summary>Exécuteur de commandes typées pour l'interpréteur d'événements (P8-2+).</summary>
public sealed class MapEventCommandExecutor
{
    private readonly ICharacterWorldStateRepository _worldState;
    private readonly ICharacterRepository _characters;
    private readonly InventoryGameplayService _inventory;
    private readonly IPublishedItemCatalog _items;
    private readonly DialogGameplayService _dialogues;
    private readonly QuestGameplayService _quests;
    private readonly IPublishedCommonEventCatalog _commonEvents;
    private readonly IPublishedRegionCatalog _regions;
    private readonly ICharacterProfessionRepository _professions;
    private readonly ProfessionGameplayService _professionGameplay;
    private readonly ICharacterPayloadReader _payloadReader;
    private readonly ICharacterPayloadWriter _payloadWriter;
    private readonly MovementService _movement;
    private readonly ILogger<MapEventCommandExecutor> _logger;

    public MapEventCommandExecutor(
        ICharacterWorldStateRepository worldState,
        ICharacterRepository characters,
        InventoryGameplayService inventory,
        IPublishedItemCatalog items,
        DialogGameplayService dialogues,
        QuestGameplayService quests,
        IPublishedCommonEventCatalog commonEvents,
        IPublishedRegionCatalog regions,
        ICharacterProfessionRepository professions,
        ProfessionGameplayService professionGameplay,
        ICharacterPayloadReader payloadReader,
        ICharacterPayloadWriter payloadWriter,
        MovementService movement,
        ILogger<MapEventCommandExecutor> logger)
    {
        _worldState = worldState;
        _characters = characters;
        _inventory = inventory;
        _items = items;
        _dialogues = dialogues;
        _quests = quests;
        _commonEvents = commonEvents;
        _regions = regions;
        _professions = professions;
        _professionGameplay = professionGameplay;
        _payloadReader = payloadReader;
        _payloadWriter = payloadWriter;
        _movement = movement;
        _logger = logger;
    }

    public async Task<string?> ExecuteCommandsAsync(
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        var steps = state.TotalSteps;
        var startIndex = state.ResumeCommandIndex ?? 0;
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        for (var i = startIndex; i < commands.Count; i++)
        {
            if (++steps > MapEventRuntimeLimits.MaxExecutionSteps)
            {
                return "Limite d'exécution événement atteinte.";
            }

            state.TotalSteps = steps;
            state.ResumeCommandIndex = null;

            var err = await ExecuteOneAsync(session, characterId, commands[i], state, cancellationToken)
                .ConfigureAwait(false);
            if (err is not null)
            {
                return err;
            }

            if (state.Waiting)
            {
                var afterCurrent = commands.Skip(i + 1).ToList();
                if (state.PendingCommands is { Count: > 0 } nested)
                {
                    state.PendingCommands = nested.Concat(afterCurrent).ToList();
                }
                else
                {
                    state.PendingCommands = afterCurrent;
                }

                state.ResumeCommandIndex = 0;
                break;
            }

            if (state.StopExecution)
            {
                break;
            }
        }

        return null;
    }

    public async Task<bool> EvaluateConditionAsync(
        Session session,
        Guid characterId,
        MapEventConditionDefinition condition,
        CancellationToken cancellationToken)
    {
        switch (condition.Kind)
        {
            case MapEventConditionKinds.CharacterSwitch:
                if (!MapEventParameterSchemas.TryParseCharacterSwitchCondition(
                        condition.ParameterJson,
                        out var switchId,
                        out var expected,
                        out _))
                {
                    return false;
                }

                var actualSwitch = await _worldState.GetSwitchAsync(characterId, switchId, cancellationToken)
                    .ConfigureAwait(false);
                return (actualSwitch ?? false) == expected;

            case MapEventConditionKinds.CharacterVariableCompare:
                if (!MapEventParameterSchemas.TryParseCharacterVariableCompare(
                        condition.ParameterJson,
                        out var variableId,
                        out var op,
                        out var compareValue,
                        out _))
                {
                    return false;
                }

                var actualVar = await _worldState.GetVariableAsync(characterId, variableId, cancellationToken)
                    .ConfigureAwait(false);
                return MapEventParameterSchemas.EvaluateVariableCompare(actualVar ?? 0, op, compareValue);

            case MapEventConditionKinds.QuestStatus:
                if (!MapEventParameterSchemas.TryParseQuestStatusCondition(
                        condition.ParameterJson,
                        out var questId,
                        out var status,
                        out _))
                {
                    return false;
                }

                return await _quests.MatchesStatusAsync(characterId, questId, status, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventConditionKinds.ItemQuantity:
                if (!MapEventParameterSchemas.TryParseItemQuantity(
                        condition.ParameterJson,
                        out var itemId,
                        out var quantity,
                        out _))
                {
                    return false;
                }

                return await CountItemAsync(characterId, itemId, cancellationToken).ConfigureAwait(false) >= quantity;

            case MapEventConditionKinds.CharacterLevel:
                if (!MapEventParameterSchemas.TryParseCharacterLevel(condition.ParameterJson, out var minLevel, out _))
                {
                    return false;
                }

                var character = await _characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
                return character is not null && character.Level >= minLevel;

            case MapEventConditionKinds.ProfessionLevel:
                if (!MapEventParameterSchemas.TryParseProfessionLevel(
                        condition.ParameterJson,
                        out var professionId,
                        out var minProfLevel,
                        out _))
                {
                    return false;
                }

                var prof = await _professions.TryGetAsync(characterId, professionId, cancellationToken)
                    .ConfigureAwait(false);
                return prof is not null && prof.Level >= minProfLevel;

            case MapEventConditionKinds.MapOrRegion:
                if (!MapEventParameterSchemas.TryParseMapOrRegion(
                        condition.ParameterJson,
                        out var mapId,
                        out var regionId,
                        out _))
                {
                    return false;
                }

                if (mapId is not null)
                {
                    return session.CurrentMapId == mapId.Value;
                }

                if (regionId is not null)
                {
                    var region = await _regions.TryGetRegionForTileAsync(
                            session.CurrentMapId,
                            session.PositionX,
                            session.PositionY,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return region?.Id == regionId.Value;
                }

                return false;

            default:
                _logger.LogWarning("Condition événement non implémentée: {Kind}", condition.Kind);
                return false;
        }
    }

    private async Task<string?> ExecuteOneAsync(
        Session session,
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.ShowText:
                if (!MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out var text, out var showErr))
                {
                    return showErr ?? "show_text invalide.";
                }

                state.ShowText = text;
                return null;

            case MapEventCommandDiscriminators.SetSwitch:
                return await ExecuteSetSwitchAsync(session, characterId, command.ParameterJson, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.SetVariable:
            case MapEventCommandDiscriminators.AddVariable:
            case MapEventCommandDiscriminators.SubVariable:
                return await ExecuteVariableAsync(characterId, command, state, cancellationToken).ConfigureAwait(false);

            case MapEventCommandDiscriminators.GiveItem:
            case MapEventCommandDiscriminators.TakeItem:
                return await ExecuteItemMutationAsync(characterId, command, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.GiveGold:
            case MapEventCommandDiscriminators.TakeGold:
                return await ExecuteGoldMutationAsync(session, characterId, command, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.Teleport:
                return ExecuteTeleport(session, command.ParameterJson, state);

            case MapEventCommandDiscriminators.Wait:
                if (!MapEventParameterSchemas.TryParseWait(command.ParameterJson, out var waitMs, out var waitErr))
                {
                    return waitErr;
                }

                state.Waiting = true;
                state.WaitUntilUtc = DateTimeOffset.UtcNow.AddMilliseconds(waitMs);
                state.StopExecution = false;
                return null;

            case MapEventCommandDiscriminators.StartDialogue:
                return await ExecuteStartDialogueAsync(characterId, command.ParameterJson, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.StartQuest:
            case MapEventCommandDiscriminators.AdvanceQuest:
            case MapEventCommandDiscriminators.TurnInQuest:
                return await ExecuteQuestCommandAsync(characterId, command, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.Branch:
                return await ExecuteBranchAsync(session, characterId, command.ParameterJson, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.CallCommonEvent:
                return await ExecuteCallCommonEventAsync(session, characterId, command.ParameterJson, state, cancellationToken)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.LearnProfession:
                return await ExecuteLearnProfessionAsync(characterId, command.ParameterJson, state, cancellationToken)
                    .ConfigureAwait(false);

            default:
                _logger.LogWarning("Commande événement non implémentée: {Discriminator}", command.Discriminator);
                return $"Commande non supportée: {command.Discriminator}.";
        }
    }

    private async Task<string?> ExecuteSetSwitchAsync(
        Session session,
        Guid characterId,
        string parameterJson,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseSetSwitch(parameterJson, out var switchId, out var switchValue, out var switchErr))
        {
            return switchErr ?? "set_switch invalide.";
        }

        await _worldState.SetSwitchAsync(characterId, switchId, switchValue, cancellationToken).ConfigureAwait(false);
        if (await SyncSwitchToPayloadAsync(session, switchId, switchValue, cancellationToken).ConfigureAwait(false))
        {
            state.SwitchesChanged = true;
        }

        return null;
    }

    private async Task<string?> ExecuteVariableAsync(
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.SetVariable:
                if (!MapEventParameterSchemas.TryParseSetVariable(
                        command.ParameterJson,
                        out var varId,
                        out var value,
                        out var setErr))
                {
                    return setErr;
                }

                await _worldState.SetVariableAsync(characterId, varId, value, cancellationToken).ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.AddVariable:
                if (!MapEventParameterSchemas.TryParseAddVariable(
                        command.ParameterJson,
                        out var addId,
                        out var addDelta,
                        out var addErr))
                {
                    return addErr;
                }

                await _worldState.AddVariableAsync(characterId, addId, addDelta, cancellationToken).ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.SubVariable:
                if (!MapEventParameterSchemas.TryParseSubVariable(
                        command.ParameterJson,
                        out var subId,
                        out var subDelta,
                        out var subErr))
                {
                    return subErr;
                }

                await _worldState.AddVariableAsync(characterId, subId, -subDelta, cancellationToken).ConfigureAwait(false);
                break;
        }

        state.VariablesChanged = true;
        return null;
    }

    private async Task<string?> ExecuteItemMutationAsync(
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseItemMutation(command.ParameterJson, out var itemId, out var quantity, out var err))
        {
            return err;
        }

        if (command.Discriminator == MapEventCommandDiscriminators.GiveItem)
        {
            var add = await _inventory.TryAddItemAsync(characterId, itemId, quantity, cancellationToken)
                .ConfigureAwait(false);
            if (add.Status != InventoryMutationStatus.Ok)
            {
                return add.ErrorMessage ?? "give_item échoué.";
            }

            state.InventoryChanged = true;
            return null;
        }

        var remaining = quantity;
        var snapshot = await _inventory.GetInventoryAsync(characterId, cancellationToken).ConfigureAwait(false);
        var totalHave = snapshot.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);
        if (totalHave < quantity)
        {
            return "take_item: quantité insuffisante.";
        }

        foreach (var slot in snapshot.Slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.ItemId != itemId || slot.Quantity <= 0)
            {
                continue;
            }

            var take = Math.Min(remaining, slot.Quantity);
            var remove = await _inventory.TryRemoveFromSlotAsync(characterId, slot.SlotIndex, take, cancellationToken)
                .ConfigureAwait(false);
            if (remove.Status != InventoryMutationStatus.Ok)
            {
                return remove.ErrorMessage ?? "take_item échoué.";
            }

            remaining -= take;
        }

        if (remaining > 0)
        {
            return "take_item: quantité insuffisante.";
        }

        state.InventoryChanged = true;
        return null;
    }

    private async Task<string?> ExecuteGoldMutationAsync(
        Session session,
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseGoldMutation(command.ParameterJson, out var amount, out var err))
        {
            return err;
        }

        var character = await _characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
        if (character is null)
        {
            return "Personnage introuvable.";
        }

        var delta = command.Discriminator == MapEventCommandDiscriminators.GiveGold ? amount : -amount;
        var newGold = character.Gold + delta;
        if (newGold < 0)
        {
            return "take_gold: or insuffisant.";
        }

        var updated = character with { Gold = newGold };
        await _characters.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        session.Gold = newGold;
        state.GoldChanged = true;
        return null;
    }

    private string? ExecuteTeleport(Session session, string parameterJson, MapEventExecutionState state)
    {
        if (!MapEventParameterSchemas.TryParseTeleport(parameterJson, out var mapId, out var tileX, out var tileY, out var err))
        {
            return err;
        }

        if (!_movement.TryTeleportToTile(session, mapId, tileX, tileY, out var teleportErr))
        {
            return teleportErr ?? "teleport échoué.";
        }

        state.TeleportApplied = true;
        return null;
    }

    private async Task<string?> ExecuteLearnProfessionAsync(
        Guid characterId,
        string parameterJson,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseLearnProfession(parameterJson, out var professionId, out var err))
        {
            return err;
        }

        var (success, message) = await _professionGameplay.TryAcquireProfessionAsync(
                characterId,
                professionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!success)
        {
            return message;
        }

        state.ShowText ??= message;
        return null;
    }

    private async Task<string?> ExecuteStartDialogueAsync(
        Guid characterId,
        string parameterJson,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseStartDialogue(parameterJson, out var dialogueId, out var err))
        {
            return err;
        }

        var started = await _dialogues.TryStartDialogueSessionAsync(characterId, dialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (started is null)
        {
            return "Dialogue introuvable.";
        }

        var speaker = string.IsNullOrWhiteSpace(started.Speaker) ? string.Empty : $"{started.Speaker}: ";
        var summary = speaker + started.Text;
        state.DialogueState = new DialogueStatePushWire(
            dialogueId,
            started.PublishedRevision,
            started.SessionToken,
            started.Speaker,
            started.Text,
            started.Choices);
        state.DialogueSummary = summary;
        state.ShowText ??= summary;
        return null;
    }

    private async Task<string?> ExecuteQuestCommandAsync(
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        string? summary;
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.StartQuest:
                if (!MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out var startId, out var startErr))
                {
                    return startErr;
                }

                summary = await _quests.TryStartQuestAsync(characterId, startId, cancellationToken).ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.AdvanceQuest:
                if (!MapEventParameterSchemas.TryParseAdvanceQuest(
                        command.ParameterJson,
                        out var advanceId,
                        out var stageIndex,
                        out var advErr))
                {
                    return advErr;
                }

                summary = await _quests.TryAdvanceQuestAsync(characterId, advanceId, stageIndex, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.TurnInQuest:
                if (!MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out var turnInId, out var turnErr))
                {
                    return turnErr;
                }

                var turnInResult = await _quests.TryTurnInQuestAsync(
                        characterId,
                        turnInId,
                        Guid.NewGuid(),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (turnInResult is null || turnInResult.Status is QuestTurnInStatus.Failed or QuestTurnInStatus.NotReady)
                {
                    return turnInResult?.Message ?? "Turn-in quête invalide.";
                }

                summary = turnInResult.Message;
                if (turnInResult.Status == QuestTurnInStatus.TurnedIn)
                {
                    state.InventoryChanged = true;
                    state.GoldChanged = true;
                }

                break;

            default:
                return "Commande quête inconnue.";
        }

        if (summary is null)
        {
            return "Quête introuvable ou transition invalide.";
        }

        state.QuestSummary = summary;
        state.ShowText ??= summary;
        return null;
    }

    private async Task<string?> ExecuteBranchAsync(
        Session session,
        Guid characterId,
        string parameterJson,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseBranch(
                parameterJson,
                out var condition,
                out var thenCommands,
                out var elseCommands,
                out var err))
        {
            return err;
        }

        var pass = await EvaluateConditionAsync(session, characterId, condition, cancellationToken).ConfigureAwait(false);
        var branch = pass ? thenCommands : elseCommands;
        return await ExecuteCommandsAsync(session, characterId, branch, state, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ExecuteCallCommonEventAsync(
        Session session,
        Guid characterId,
        string parameterJson,
        MapEventExecutionState state,
        CancellationToken cancellationToken)
    {
        if (!MapEventParameterSchemas.TryParseCallCommonEvent(
                parameterJson,
                out var eventId,
                out var aliasId,
                out var err))
        {
            return err;
        }

        if (++state.CommonEventDepth > MapEventRuntimeLimits.MaxCommonEventRecursionDepth)
        {
            return "Profondeur call_common_event dépassée.";
        }

        try
        {
            CommonEventDefinition? definition = null;
            if (eventId != Guid.Empty)
            {
                definition = await _commonEvents.TryGetPublishedByIdAsync(eventId, cancellationToken).ConfigureAwait(false);
            }
            else if (aliasId is not null)
            {
                definition = await _commonEvents.TryGetPublishedByAliasAsync(aliasId.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (definition is null || definition.Pages.Count == 0)
            {
                return "Événement commun introuvable.";
            }

            var page = definition.Pages.OrderBy(p => p.PageOrder).First();
            return await ExecuteCommandsAsync(session, characterId, page.Commands, state, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            state.CommonEventDepth--;
        }
    }

    private async Task<int> CountItemAsync(Guid characterId, Guid itemId, CancellationToken cancellationToken)
    {
        var snapshot = await _inventory.GetInventoryAsync(characterId, cancellationToken).ConfigureAwait(false);
        return snapshot.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);
    }

    private Task<bool> SyncSwitchToPayloadAsync(
        Session session,
        string switchId,
        bool value,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(session.CharacterId))
        {
            return Task.FromResult(false);
        }

        _payloadReader.TryGetPayloadJson(session.CharacterId, out var currentJson);
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            currentJson = CharacterPayloadDefaults.NewHeroJson;
        }

        var patchJson = JsonSerializer.Serialize(new Dictionary<string, bool> { [switchId] = value });
        if (!CharacterPayloadWorldFlags.TryMergeWorldFlags(currentJson, patchJson, out var merged, out _))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_payloadWriter.TryUpdatePayloadJson(session.CharacterId, merged));
    }
}
