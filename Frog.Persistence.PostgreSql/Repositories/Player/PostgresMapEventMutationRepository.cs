using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresMapEventMutationRepository(
    FrogDbContextGate gate,
    IPublishedItemCatalog items,
    IPublishedQuestCatalog? quests = null,
    IPublishedProfessionCatalog? professions = null,
    IPublishedRecipeCatalog? recipes = null,
    TimeProvider? clock = null) : IMapEventMutationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IPublishedItemCatalog _items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly IPublishedQuestCatalog? _quests = quests;
    private readonly IPublishedProfessionCatalog? _professions = professions;
    private readonly IPublishedRecipeCatalog? _recipes = recipes;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    internal Func<CancellationToken, Task>? TestAfterClaimAsync { get; set; }

    internal Func<CancellationToken, Task>? TestAfterInventoryMutationAsync { get; set; }

    internal int TransactionsBegun { get; private set; }

    public Task<MapEventMutationResult> TryExecutePageAsync(
        Guid characterId,
        Guid requestId,
        long placementId,
        int catalogAliasId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken = default)
    {
        var identity = new MapEventExecutionIdentity(requestId, characterId, placementId, catalogAliasId);
        return TryExecutePlanAsync(MapEventExecutionPlan.Ok(identity, commands), cancellationToken);
    }

    public Task<MapEventMutationResult> TryExecutePlanAsync(
        MapEventExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return _gate.ExecuteAsync((db, ct) => ExecutePlanCoreAsync(db, plan, ct), cancellationToken);
    }

    private async Task<MapEventMutationResult> ExecutePlanCoreAsync(
        FrogDbContext db,
        MapEventExecutionPlan plan,
        CancellationToken ct)
    {
        if (!plan.IsSuccess)
        {
            return new MapEventMutationResult(MapEventMutationStatus.Failed, plan.Error);
        }

        var identity = plan.Identity;
        var (characterId, requestId) = identity.LedgerKey;
        if (!identity.IsValid || characterId == Guid.Empty || requestId == Guid.Empty)
        {
            return new MapEventMutationResult(MapEventMutationStatus.Failed, "Paramètres invalides.");
        }

        foreach (var cmd in plan.Effects)
        {
            if (cmd.Discriminator is MapEventCommandDiscriminators.Branch
                or MapEventCommandDiscriminators.CallCommonEvent
                or MapEventCommandDiscriminators.ShowChoices)
            {
                return new MapEventMutationResult(
                    MapEventMutationStatus.Failed,
                    $"Commande déjà résolue attendue (pas de re-résolution SQL): {cmd.Discriminator}.");
            }

            if (!MapEventCommandParameterValidator.ValidateParameters(cmd, out var paramErr))
            {
                return new MapEventMutationResult(MapEventMutationStatus.Failed, paramErr);
            }
        }

        var unit = MapEventTransactionalUnit.FromPlan(plan);
        if (!unit.IsSuccess)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                unit.Error ?? "Unité transactionnelle invalide.");
        }

        var existing = await db.PlayerMapEventExecutionRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return ReplayOrReject(existing, identity);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        TransactionsBegun++;
        try
        {
            if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new MapEventMutationResult(MapEventMutationStatus.Failed, "Personnage introuvable.");
            }

            var claimed = await db.PlayerMapEventExecutionRequests
                .FirstOrDefaultAsync(r => r.RequestId == requestId, ct)
                .ConfigureAwait(false);
            if (claimed is not null)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return ReplayOrReject(claimed, identity);
            }

            var snapshot = new MapEventExecutionSnapshot
            {
                ActivationId = identity.EffectiveActivationId,
                WaitOrdinal = identity.WaitOrdinal,
            };
            IReadOnlyList<MapEventCommandDefinition> pendingAfterWait = Array.Empty<MapEventCommandDefinition>();
            var waiting = false;

            var character = await db.PlayerCharacters
                .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                .ConfigureAwait(false);
            if (character is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new MapEventMutationResult(MapEventMutationStatus.Failed, "Personnage introuvable.");
            }

            var invRows = await db.PlayerInventorySlots
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var slots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(invRows);

            foreach (var command in unit.CommitEffects)
            {
                var err = await ApplyCommandAsync(
                        db,
                        character,
                        slots,
                        snapshot,
                        command,
                        ct)
                    .ConfigureAwait(false);
                if (err is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    db.ChangeTracker.Clear();
                    return new MapEventMutationResult(MapEventMutationStatus.Failed, err);
                }
            }

            if (unit.Wait is { } wait)
            {
                snapshot.Waiting = true;
                snapshot.WaitUntilUtc = _clock.GetUtcNow().AddMilliseconds(wait.WaitMilliseconds);
                pendingAfterWait = wait.RemainingEffects;
                waiting = true;
            }

            if (snapshot.InventoryChanged)
            {
                await PostgresEconomyTransactionRepository.PersistInventorySlotsAsync(
                        db, characterId, invRows, slots, ct)
                    .ConfigureAwait(false);
            }

            snapshot.ResultGold = character.Gold;

            db.PlayerMapEventExecutionRequests.Add(new MapEventExecutionRequestEntity
            {
                CharacterId = characterId,
                RequestId = requestId,
                PlacementId = identity.PlacementId,
                CatalogAliasId = identity.CatalogAliasId,
                ActivationId = identity.EffectiveActivationId,
                WaitOrdinal = identity.WaitOrdinal,
                ResultJson = SerializeSnapshot(snapshot, pendingAfterWait, waiting),
                CompletedAtUtc = _clock.GetUtcNow(),
            });

            if (TestBeforeCommitAsync is not null)
            {
                await TestBeforeCommitAsync(ct).ConfigureAwait(false);
            }

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (IsLedgerDuplicate(ex))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return await LoadReplayAfterConflictAsync(db, identity, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();

            snapshot.PendingCommands = pendingAfterWait.Count > 0 ? pendingAfterWait : null;
            snapshot.ResultGold = character.Gold;
            return new MapEventMutationResult(MapEventMutationStatus.Executed, null, snapshot);
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "Commit annulé avant écriture ledger.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new MapEventMutationResult(MapEventMutationStatus.Failed, ex.Message);
        }
    }

    private static MapEventMutationResult ReplayOrReject(
        MapEventExecutionRequestEntity existing,
        MapEventExecutionIdentity identity)
    {
        if (existing.CharacterId != identity.CharacterId)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "RequestId réutilisé avec personnage différent.");
        }

        return ReplayOrMismatch(existing, identity);
    }

    private static MapEventMutationResult ReplayOrMismatch(
        MapEventExecutionRequestEntity existing,
        MapEventExecutionIdentity identity)
    {
        if (existing.PlacementId != identity.PlacementId || existing.CatalogAliasId != identity.CatalogAliasId)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "RequestId réutilisé avec événement différent.");
        }

        if (existing.ActivationId != Guid.Empty
            && existing.ActivationId != identity.EffectiveActivationId)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "RequestId réutilisé avec événement différent.");
        }

        if (existing.WaitOrdinal != identity.WaitOrdinal)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "RequestId réutilisé avec événement différent.");
        }

        var replay = DeserializeSnapshot(existing.ResultJson) ?? new MapEventExecutionSnapshot();
        if (replay.ActivationId == Guid.Empty)
        {
            replay.ActivationId = existing.ActivationId != Guid.Empty
                ? existing.ActivationId
                : identity.EffectiveActivationId;
        }

        replay.WaitOrdinal = existing.WaitOrdinal;
        return new MapEventMutationResult(MapEventMutationStatus.IdempotentReplay, null, replay);
    }

    private static async Task<MapEventMutationResult> LoadReplayAfterConflictAsync(
        FrogDbContext db,
        MapEventExecutionIdentity identity,
        CancellationToken ct)
    {
        var existing = await db.PlayerMapEventExecutionRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == identity.RequestId, ct)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return new MapEventMutationResult(
                MapEventMutationStatus.Failed,
                "Conflit ledger sans ligne commise.");
        }

        return ReplayOrReject(existing, identity);
    }

    private async Task<string?> ApplyCommandAsync(
        FrogDbContext db,
        CharacterEntity character,
        InventorySlotRecord[] slots,
        MapEventExecutionSnapshot snapshot,
        MapEventCommandDefinition command,
        CancellationToken ct)
    {
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.ShowText:
                if (!MapEventParameterSchemas.TryParseShowText(command.ParameterJson, out var text, out var showErr))
                {
                    return showErr;
                }

                snapshot.ShowText = text;
                return null;

            case MapEventCommandDiscriminators.SetSwitch:
                return await ApplySetSwitchAsync(db, character.Id, command.ParameterJson, snapshot, ct)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.SetVariable:
            case MapEventCommandDiscriminators.AddVariable:
            case MapEventCommandDiscriminators.SubVariable:
                return await ApplyVariableAsync(db, character.Id, command, snapshot, ct).ConfigureAwait(false);

            case MapEventCommandDiscriminators.GiveItem:
            case MapEventCommandDiscriminators.TakeItem:
                var itemErr = await ApplyItemMutationAsync(db, character.Id, slots, command, snapshot, ct)
                    .ConfigureAwait(false);
                if (itemErr is null && TestAfterInventoryMutationAsync is not null)
                {
                    await TestAfterInventoryMutationAsync(ct).ConfigureAwait(false);
                }

                return itemErr;

            case MapEventCommandDiscriminators.GiveGold:
            case MapEventCommandDiscriminators.TakeGold:
                return await ApplyGoldMutationAsync(db, character, command, snapshot, ct).ConfigureAwait(false);

            case MapEventCommandDiscriminators.StartQuest:
            case MapEventCommandDiscriminators.AdvanceQuest:
            case MapEventCommandDiscriminators.TurnInQuest:
                return await ApplyQuestCommandAsync(db, character, slots, command, snapshot, ct)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.LearnProfession:
                return await ApplyLearnProfessionAsync(db, character.Id, command.ParameterJson, snapshot, ct)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.Wait:
                return null;

            case MapEventCommandDiscriminators.StartDialogue:
                if (!MapEventParameterSchemas.TryParseStartDialogue(
                        command.ParameterJson,
                        out var dialogueId,
                        out var dialogueErr))
                {
                    return dialogueErr;
                }

                snapshot.RecordDialogue(dialogueId);
                return null;

            case MapEventCommandDiscriminators.Teleport:
                if (!MapEventParameterSchemas.TryParseTeleport(
                        command.ParameterJson,
                        out var mapId,
                        out var tileX,
                        out var tileY,
                        out var teleportErr))
                {
                    return teleportErr;
                }

                snapshot.RecordTeleport(mapId, tileX, tileY);
                return null;

            case MapEventCommandDiscriminators.OpenShop:
                if (!MapEventParameterSchemas.TryParseOpenShop(
                        command.ParameterJson,
                        out var openShopId,
                        out _,
                        out var openShopErr))
                {
                    return openShopErr;
                }

                snapshot.RecordShop(openShopId);
                return null;

            case MapEventCommandDiscriminators.PlayBgm:
            case MapEventCommandDiscriminators.PlaySe:
                // No-op ledger : piste validée, pas d'opcode audio (Hello 11).
                return MapEventDeferredPresentation.TryAcceptPlayAudio(
                    command.Discriminator,
                    command.ParameterJson,
                    out var audioErr)
                    ? null
                    : audioErr;

            case MapEventCommandDiscriminators.SetWeather:
                if (!MapEventParameterSchemas.TryParseSetWeather(
                        command.ParameterJson,
                        out var weatherKind,
                        out var weatherErr))
                {
                    return MapEventParameterSchemas.IsUnknownWeatherKind(weatherErr) ? null : weatherErr;
                }

                snapshot.RecordWeather(weatherKind);
                return null;

            case MapEventCommandDiscriminators.ShowPicture:
                if (!MapEventParameterSchemas.TryParseShowPicture(
                        command.ParameterJson,
                        out var shown,
                        out var showPictureErr))
                {
                    return showPictureErr;
                }

                snapshot.RecordPicture(MapEventPictureOp.ForShow(
                    shown.PictureId,
                    shown.Asset,
                    shown.X,
                    shown.Y,
                    shown.Opacity,
                    shown.Blend));
                return null;

            case MapEventCommandDiscriminators.ErasePicture:
                if (!MapEventParameterSchemas.TryParseErasePicture(
                        command.ParameterJson,
                        out var erasePictureId,
                        out var erasePictureErr))
                {
                    return erasePictureErr;
                }

                snapshot.RecordPicture(MapEventPictureOp.ForErase(erasePictureId));
                return null;

            default:
                return $"Commande non supportée en transaction atomique: {command.Discriminator}.";
        }
    }

    private async Task<string?> ApplySetSwitchAsync(
        FrogDbContext db,
        Guid characterId,
        string parameterJson,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (!MapEventParameterSchemas.TryParseSetSwitch(parameterJson, out var switchId, out var value, out var err))
        {
            return err;
        }

        var row = await FindSwitchAsync(db, characterId, switchId, ct).ConfigureAwait(false);
        if (row is null)
        {
            db.PlayerCharacterWorldSwitches.Add(new CharacterWorldSwitchEntity
            {
                CharacterId = characterId,
                SwitchKey = switchId,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }

        snapshot.RecordSwitch(switchId, value);
        return null;
    }

    private async Task<string?> ApplyVariableAsync(
        FrogDbContext db,
        Guid characterId,
        MapEventCommandDefinition command,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.SetVariable:
                if (!MapEventParameterSchemas.TryParseSetVariable(
                        command.ParameterJson, out var varId, out var value, out var setErr))
                {
                    return setErr;
                }

                await UpsertVariableAsync(db, characterId, varId, value, ct).ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.AddVariable:
                if (!MapEventParameterSchemas.TryParseAddVariable(
                        command.ParameterJson, out var addId, out var addDelta, out var addErr))
                {
                    return addErr;
                }

                var addCurrent = await GetVariableValueAsync(db, characterId, addId, ct).ConfigureAwait(false);
                await UpsertVariableAsync(db, characterId, addId, checked(addCurrent + addDelta), ct)
                    .ConfigureAwait(false);
                break;

            case MapEventCommandDiscriminators.SubVariable:
                if (!MapEventParameterSchemas.TryParseSubVariable(
                        command.ParameterJson, out var subId, out var subDelta, out var subErr))
                {
                    return subErr;
                }

                var subCurrent = await GetVariableValueAsync(db, characterId, subId, ct).ConfigureAwait(false);
                await UpsertVariableAsync(db, characterId, subId, checked(subCurrent - subDelta), ct)
                    .ConfigureAwait(false);
                break;
        }

        snapshot.VariablesChanged = true;
        return null;
    }

    private static async Task<int> GetVariableValueAsync(
        FrogDbContext db,
        Guid characterId,
        string variableId,
        CancellationToken ct)
    {
        var row = await FindVariableAsync(db, characterId, variableId, ct).ConfigureAwait(false);
        return row?.Value ?? 0;
    }

    private static async Task UpsertVariableAsync(
        FrogDbContext db,
        Guid characterId,
        string variableId,
        int value,
        CancellationToken ct)
    {
        var row = await FindVariableAsync(db, characterId, variableId, ct).ConfigureAwait(false);
        if (row is null)
        {
            db.PlayerCharacterWorldVariables.Add(new CharacterWorldVariableEntity
            {
                CharacterId = characterId,
                VariableKey = variableId,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
    }

    private async Task<string?> ApplyItemMutationAsync(
        FrogDbContext db,
        Guid characterId,
        InventorySlotRecord[] slots,
        MapEventCommandDefinition command,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (!MapEventParameterSchemas.TryParseItemMutation(
                command.ParameterJson,
                out var itemId,
                out var quantity,
                out var onceKey,
                out var err))
        {
            return err;
        }

        if (command.Discriminator == MapEventCommandDiscriminators.GiveItem)
        {
            if (!string.IsNullOrEmpty(onceKey))
            {
                var switchKey = MapEventOnceGrantKeys.SwitchKeyFor(onceKey);
                if (!await TryClaimSwitchInTransactionAsync(db, characterId, switchKey, ct).ConfigureAwait(false))
                {
                    snapshot.SwitchesChanged = true;
                    return null;
                }

                snapshot.SwitchesChanged = true;
                if (TestAfterClaimAsync is not null)
                {
                    await TestAfterClaimAsync(ct).ConfigureAwait(false);
                }
            }

            var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
            if (item is null)
            {
                return "Objet inconnu.";
            }

            if (!PostgresEconomyTransactionRepository.TryAddToInventory(
                    slots, itemId, quantity, item.MaxStack))
            {
                return "Inventaire plein.";
            }

            snapshot.InventoryChanged = true;
            return null;
        }

        var remaining = quantity;
        var totalHave = slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);
        if (totalHave < quantity)
        {
            return "take_item: quantité insuffisante.";
        }

        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            var slot = slots[i];
            if (slot.ItemId != itemId || slot.Quantity <= 0)
            {
                continue;
            }

            var take = Math.Min(remaining, slot.Quantity);
            slots[i] = slot with { Quantity = slot.Quantity - take };
            if (slots[i].Quantity == 0)
            {
                slots[i] = slot with { ItemId = null, Quantity = 0 };
            }

            remaining -= take;
        }

        snapshot.InventoryChanged = true;
        return null;
    }

    private async Task<string?> ApplyGoldMutationAsync(
        FrogDbContext db,
        CharacterEntity character,
        MapEventCommandDefinition command,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (!MapEventParameterSchemas.TryParseGoldMutation(
                command.ParameterJson,
                out var amount,
                out var onceKey,
                out var err))
        {
            return err;
        }

        if (command.Discriminator == MapEventCommandDiscriminators.GiveGold && !string.IsNullOrEmpty(onceKey))
        {
            var switchKey = MapEventOnceGrantKeys.SwitchKeyFor(onceKey);
            if (!await TryClaimSwitchInTransactionAsync(db, character.Id, switchKey, ct).ConfigureAwait(false))
            {
                snapshot.SwitchesChanged = true;
                return null;
            }

            snapshot.SwitchesChanged = true;
            if (TestAfterClaimAsync is not null)
            {
                await TestAfterClaimAsync(ct).ConfigureAwait(false);
            }
        }

        if (command.Discriminator == MapEventCommandDiscriminators.GiveGold)
        {
            character.Gold = checked(character.Gold + amount);
            snapshot.GoldChanged = true;
            return null;
        }

        if (character.Gold < amount)
        {
            return "take_gold: or insuffisant.";
        }

        character.Gold = checked(character.Gold - amount);
        snapshot.GoldChanged = true;
        return null;
    }

    private async Task<string?> ApplyQuestCommandAsync(
        FrogDbContext db,
        CharacterEntity character,
        InventorySlotRecord[] slots,
        MapEventCommandDefinition command,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (_quests is null)
        {
            return "Catalogue de quêtes indisponible.";
        }

        switch (command.Discriminator)
        {
            case MapEventCommandDiscriminators.StartQuest:
                if (!MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out var startId, out var startErr))
                {
                    return startErr;
                }

                return await ApplyStartQuestAsync(db, character.Id, startId, snapshot, ct).ConfigureAwait(false);

            case MapEventCommandDiscriminators.AdvanceQuest:
                if (!MapEventParameterSchemas.TryParseAdvanceQuest(
                        command.ParameterJson, out var advanceId, out var stageIndex, out var advErr))
                {
                    return advErr;
                }

                return await ApplyAdvanceQuestAsync(db, character.Id, advanceId, stageIndex, snapshot, ct)
                    .ConfigureAwait(false);

            case MapEventCommandDiscriminators.TurnInQuest:
                if (!MapEventParameterSchemas.TryParseQuestId(command.ParameterJson, out var turnInId, out var turnErr))
                {
                    return turnErr;
                }

                return await ApplyTurnInQuestAsync(db, character, slots, turnInId, snapshot, ct)
                    .ConfigureAwait(false);

            default:
                return "Commande quête inconnue.";
        }
    }

    private async Task<string?> ApplyStartQuestAsync(
        FrogDbContext db,
        Guid characterId,
        Guid questId,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        var definition = await _quests!.TryGetPublishedByIdAsync(questId, ct).ConfigureAwait(false);
        if (definition is null)
        {
            return "Quête introuvable ou transition invalide.";
        }

        var row = await FindQuestProgressAsync(db, characterId, questId, ct).ConfigureAwait(false);
        if (row is not null
            && row.Status != CharacterQuestStatus.NotStarted
            && !definition.Repeatable)
        {
            snapshot.QuestSummary = $"Quête déjà démarrée: {definition.Name}";
            snapshot.ShowText ??= snapshot.QuestSummary;
            MarkRecipeObjective(snapshot, definition);
            return null;
        }

        foreach (var prereqId in definition.PrerequisiteQuestIds)
        {
            var prereq = await FindQuestProgressAsync(db, characterId, prereqId, ct).ConfigureAwait(false);
            if (prereq is null || prereq.Status != CharacterQuestStatus.Completed)
            {
                return "Prérequis de quête non satisfaits.";
            }
        }

        if (row is null)
        {
            db.PlayerCharacterQuestProgress.Add(new CharacterQuestProgressEntity
            {
                CharacterId = characterId,
                QuestId = questId,
                Status = CharacterQuestStatus.Active,
                StageIndex = 0,
                RewardClaimed = false,
                ObjectiveCountersJson = "{}",
            });
        }
        else
        {
            row.Status = CharacterQuestStatus.Active;
            row.StageIndex = 0;
            row.RewardClaimed = false;
            row.ObjectiveCountersJson = "{}";
        }

        snapshot.QuestsChanged = true;
        snapshot.QuestSummary = $"Quête démarrée: {definition.Name}";
        snapshot.ShowText ??= snapshot.QuestSummary;
        MarkRecipeObjective(snapshot, definition);
        return null;
    }

    private async Task<string?> ApplyAdvanceQuestAsync(
        FrogDbContext db,
        Guid characterId,
        Guid questId,
        int stageIndex,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        var definition = await _quests!.TryGetPublishedByIdAsync(questId, ct).ConfigureAwait(false);
        if (definition is null)
        {
            return "Quête introuvable ou transition invalide.";
        }

        var row = await FindQuestProgressAsync(db, characterId, questId, ct).ConfigureAwait(false);
        if (row is null || row.Status is CharacterQuestStatus.NotStarted or CharacterQuestStatus.Completed)
        {
            return "Quête introuvable ou transition invalide.";
        }

        if (stageIndex >= definition.Stages.Count)
        {
            row.Status = CharacterQuestStatus.ReadyToTurnIn;
            row.StageIndex = definition.Stages.Count - 1;
        }
        else
        {
            row.StageIndex = stageIndex;
            row.Status = CharacterQuestStatus.Active;
        }

        var stageDesc = definition.Stages[Math.Min(row.StageIndex, definition.Stages.Count - 1)].Description;
        snapshot.QuestsChanged = true;
        snapshot.QuestSummary = $"Objectif: {stageDesc}";
        snapshot.ShowText ??= snapshot.QuestSummary;
        MarkRecipeObjective(snapshot, definition);
        return null;
    }

    private async Task<string?> ApplyTurnInQuestAsync(
        FrogDbContext db,
        CharacterEntity character,
        InventorySlotRecord[] slots,
        Guid questId,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        var definition = await _quests!.TryGetPublishedByIdAsync(questId, ct).ConfigureAwait(false);
        if (definition is null)
        {
            return "Quête introuvable ou transition invalide.";
        }

        var progressRow = await FindQuestProgressAsync(db, character.Id, questId, ct).ConfigureAwait(false);
        if (progressRow is null
            || progressRow.Status is not (CharacterQuestStatus.ReadyToTurnIn or CharacterQuestStatus.Active)
            || progressRow.RewardClaimed)
        {
            return "Quête non prête pour turn-in.";
        }

        if (!AreObjectivesComplete(definition, progressRow))
        {
            return "Objectifs incomplets.";
        }

        if (definition.CompletionReward is not null)
        {
            if (definition.CompletionReward.Gold > 0)
            {
                character.Gold = checked(character.Gold + definition.CompletionReward.Gold);
                snapshot.GoldChanged = true;
            }

            if (definition.CompletionReward.ItemId is Guid itemId && definition.CompletionReward.ItemQuantity > 0)
            {
                var itemDef = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
                if (itemDef is null)
                {
                    return "Objet récompense inconnu.";
                }

                if (!PostgresEconomyTransactionRepository.TryAddToInventory(
                        slots,
                        itemId,
                        definition.CompletionReward.ItemQuantity,
                        itemDef.MaxStack))
                {
                    return "Inventaire plein.";
                }

                snapshot.InventoryChanged = true;
            }
        }

        progressRow.Status = CharacterQuestStatus.Completed;
        progressRow.RewardClaimed = true;
        snapshot.QuestsChanged = true;
        snapshot.QuestSummary = $"Quête terminée — récompense reçue: {definition.Name}";
        snapshot.ShowText ??= snapshot.QuestSummary;
        MarkRecipeObjective(snapshot, definition);
        return null;
    }

    private async Task<string?> ApplyLearnProfessionAsync(
        FrogDbContext db,
        Guid characterId,
        string parameterJson,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (_professions is null)
        {
            return "Catalogue de métiers indisponible.";
        }

        if (!MapEventParameterSchemas.TryParseLearnProfession(parameterJson, out var professionId, out var err))
        {
            return err;
        }

        var profession = await _professions.TryGetPublishedByIdAsync(professionId, ct).ConfigureAwait(false);
        if (profession is null)
        {
            return "Métier inconnu.";
        }

        var existing = await FindProfessionAsync(db, characterId, professionId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            snapshot.ShowText ??= $"Métier {profession.Name} déjà acquis (niv. {existing.Level}).";
            await MarkRecipesForProfessionAsync(professionId, snapshot, ct).ConfigureAwait(false);
            return null;
        }

        db.PlayerCharacterProfessionProgress.Add(new CharacterProfessionProgressEntity
        {
            CharacterId = characterId,
            ProfessionId = professionId,
            Level = 1,
            Experience = 0,
        });
        snapshot.ProfessionsChanged = true;
        snapshot.ShowText ??= $"Métier {profession.Name} acquis.";
        await MarkRecipesForProfessionAsync(professionId, snapshot, ct).ConfigureAwait(false);
        return null;
    }

    private async Task MarkRecipesForProfessionAsync(
        Guid professionId,
        MapEventExecutionSnapshot snapshot,
        CancellationToken ct)
    {
        if (_recipes is null)
        {
            return;
        }

        var published = await _recipes.ListPublishedAsync(ct).ConfigureAwait(false);
        if (published.Any(r => r.ProfessionId == professionId))
        {
            snapshot.RecipesChanged = true;
        }
    }

    private static void MarkRecipeObjective(MapEventExecutionSnapshot snapshot, QuestDefinition definition)
    {
        if (definition.Stages.Any(s => s.Objectives.Any(o => o.TargetRecipeId is not null)))
        {
            snapshot.RecipesChanged = true;
        }
    }

    private static bool AreObjectivesComplete(QuestDefinition definition, CharacterQuestProgressEntity progress)
    {
        if (definition.Stages.Count == 0)
        {
            return false;
        }

        var counters = PostgresQuestMutationRepository.DeserializeCounters(progress.ObjectiveCountersJson);
        var stageIndex = Math.Min(progress.StageIndex, definition.Stages.Count - 1);
        var stage = definition.Stages[stageIndex];
        if (stage.Objectives.Count == 0)
        {
            return progress.Status == CharacterQuestStatus.ReadyToTurnIn
                   || stageIndex >= definition.Stages.Count - 1;
        }

        for (var i = 0; i < stage.Objectives.Count; i++)
        {
            var key = QuestObjectiveKeys.For(stageIndex, i);
            counters.TryGetValue(key, out var current);
            if (current < stage.Objectives[i].RequiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> TryClaimSwitchInTransactionAsync(
        FrogDbContext db,
        Guid characterId,
        string switchKey,
        CancellationToken ct)
    {
        var existing = await FindSwitchAsync(db, characterId, switchKey, ct).ConfigureAwait(false);
        if (existing is { Value: true })
        {
            return false;
        }

        if (existing is null)
        {
            db.PlayerCharacterWorldSwitches.Add(new CharacterWorldSwitchEntity
            {
                CharacterId = characterId,
                SwitchKey = switchKey,
                Value = true,
            });
        }
        else
        {
            existing.Value = true;
        }

        return true;
    }

    private static async Task<CharacterWorldSwitchEntity?> FindSwitchAsync(
        FrogDbContext db,
        Guid characterId,
        string switchKey,
        CancellationToken ct)
    {
        var local = db.PlayerCharacterWorldSwitches.Local
            .FirstOrDefault(s => s.CharacterId == characterId && s.SwitchKey == switchKey);
        if (local is not null)
        {
            return local;
        }

        return await db.PlayerCharacterWorldSwitches
            .FirstOrDefaultAsync(s => s.CharacterId == characterId && s.SwitchKey == switchKey, ct)
            .ConfigureAwait(false);
    }

    private static async Task<CharacterWorldVariableEntity?> FindVariableAsync(
        FrogDbContext db,
        Guid characterId,
        string variableId,
        CancellationToken ct)
    {
        var local = db.PlayerCharacterWorldVariables.Local
            .FirstOrDefault(v => v.CharacterId == characterId && v.VariableKey == variableId);
        if (local is not null)
        {
            return local;
        }

        return await db.PlayerCharacterWorldVariables
            .FirstOrDefaultAsync(v => v.CharacterId == characterId && v.VariableKey == variableId, ct)
            .ConfigureAwait(false);
    }

    private static async Task<CharacterQuestProgressEntity?> FindQuestProgressAsync(
        FrogDbContext db,
        Guid characterId,
        Guid questId,
        CancellationToken ct)
    {
        var local = db.PlayerCharacterQuestProgress.Local
            .FirstOrDefault(q => q.CharacterId == characterId && q.QuestId == questId);
        if (local is not null)
        {
            return local;
        }

        return await db.PlayerCharacterQuestProgress
            .FirstOrDefaultAsync(q => q.CharacterId == characterId && q.QuestId == questId, ct)
            .ConfigureAwait(false);
    }

    private static async Task<CharacterProfessionProgressEntity?> FindProfessionAsync(
        FrogDbContext db,
        Guid characterId,
        Guid professionId,
        CancellationToken ct)
    {
        var local = db.PlayerCharacterProfessionProgress.Local
            .FirstOrDefault(p => p.CharacterId == characterId && p.ProfessionId == professionId);
        if (local is not null)
        {
            return local;
        }

        return await db.PlayerCharacterProfessionProgress
            .FirstOrDefaultAsync(p => p.CharacterId == characterId && p.ProfessionId == professionId, ct)
            .ConfigureAwait(false);
    }

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
    {
        var exists = await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM player.characters WHERE id = {characterId} FOR UPDATE",
            ct).ConfigureAwait(false);
        return true;
    }

    private static bool IsLedgerDuplicate(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string SerializeSnapshot(
        MapEventExecutionSnapshot snapshot,
        IReadOnlyList<MapEventCommandDefinition> pending,
        bool waiting) =>
        JsonSerializer.Serialize(new StoredSnapshot
        {
            ShowText = snapshot.ShowText,
            SwitchesChanged = snapshot.SwitchesChanged,
            VariablesChanged = snapshot.VariablesChanged,
            InventoryChanged = snapshot.InventoryChanged,
            GoldChanged = snapshot.GoldChanged,
            QuestsChanged = snapshot.QuestsChanged,
            ProfessionsChanged = snapshot.ProfessionsChanged,
            RecipesChanged = snapshot.RecipesChanged,
            QuestSummary = snapshot.QuestSummary,
            ResultGold = snapshot.ResultGold,
            SwitchChanges = snapshot.SwitchChanges.Count > 0 ? snapshot.SwitchChanges : null,
            Waiting = waiting,
            WaitUntilUtc = snapshot.WaitUntilUtc,
            PendingCommands = pending.Count > 0 ? pending.ToList() : null,
            ActivationId = snapshot.ActivationId,
            WaitOrdinal = snapshot.WaitOrdinal,
            DialogueId = snapshot.DialogueId,
            TeleportMapId = snapshot.TeleportMapId,
            TeleportTileX = snapshot.TeleportTileX,
            TeleportTileY = snapshot.TeleportTileY,
            ShopId = snapshot.ShopId,
            WeatherKind = snapshot.WeatherKind,
            PictureOps = snapshot.PictureOps is { Count: > 0 } ops ? ops : null,
        }, JsonOptions);

    private static MapEventExecutionSnapshot? DeserializeSnapshot(string json)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredSnapshot>(json, JsonOptions);
            if (stored is null)
            {
                return null;
            }

            return new MapEventExecutionSnapshot
            {
                ShowText = stored.ShowText,
                SwitchesChanged = stored.SwitchesChanged,
                VariablesChanged = stored.VariablesChanged,
                InventoryChanged = stored.InventoryChanged,
                GoldChanged = stored.GoldChanged,
                QuestsChanged = stored.QuestsChanged,
                ProfessionsChanged = stored.ProfessionsChanged,
                RecipesChanged = stored.RecipesChanged,
                QuestSummary = stored.QuestSummary,
                ResultGold = stored.ResultGold,
                SwitchChanges = stored.SwitchChanges ?? [],
                Waiting = stored.Waiting,
                WaitUntilUtc = stored.WaitUntilUtc,
                PendingCommands = stored.PendingCommands,
                ActivationId = stored.ActivationId,
                WaitOrdinal = stored.WaitOrdinal,
                DialogueId = stored.DialogueId,
                TeleportMapId = stored.TeleportMapId,
                TeleportTileX = stored.TeleportTileX,
                TeleportTileY = stored.TeleportTileY,
                ShopId = stored.ShopId,
                WeatherKind = stored.WeatherKind,
                PictureOps = stored.PictureOps ?? [],
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class StoredSnapshot
    {
        public string? ShowText { get; set; }

        public bool SwitchesChanged { get; set; }

        public bool VariablesChanged { get; set; }

        public bool InventoryChanged { get; set; }

        public bool GoldChanged { get; set; }

        public bool QuestsChanged { get; set; }

        public bool ProfessionsChanged { get; set; }

        public bool RecipesChanged { get; set; }

        public string? QuestSummary { get; set; }

        public int? ResultGold { get; set; }

        public List<WorldSwitchWire>? SwitchChanges { get; set; }

        public bool Waiting { get; set; }

        public DateTimeOffset? WaitUntilUtc { get; set; }

        public List<MapEventCommandDefinition>? PendingCommands { get; set; }

        public Guid ActivationId { get; set; }

        public int WaitOrdinal { get; set; }

        public Guid? DialogueId { get; set; }

        public int? TeleportMapId { get; set; }

        public int? TeleportTileX { get; set; }

        public int? TeleportTileY { get; set; }

        public Guid? ShopId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WeatherKind { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MapEventPictureOp>? PictureOps { get; set; }
    }
}
