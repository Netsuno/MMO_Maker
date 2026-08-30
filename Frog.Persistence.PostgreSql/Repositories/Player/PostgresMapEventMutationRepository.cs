using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Application.Gameplay;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresMapEventMutationRepository(
    FrogDbContextGate gate,
    IPublishedItemCatalog items,
    TimeProvider? clock = null) : IMapEventMutationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IPublishedItemCatalog _items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    internal Func<CancellationToken, Task>? TestAfterClaimAsync { get; set; }

    internal Func<CancellationToken, Task>? TestAfterInventoryMutationAsync { get; set; }

    public Task<MapEventMutationResult> TryExecutePageAsync(
        Guid characterId,
        Guid requestId,
        long placementId,
        int catalogAliasId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || requestId == Guid.Empty)
            {
                return new MapEventMutationResult(MapEventMutationStatus.Failed, "Paramètres invalides.");
            }

            foreach (var cmd in commands)
            {
                if (!MapEventCommandParameterValidator.ValidateParameters(cmd, out var paramErr))
                {
                    return new MapEventMutationResult(MapEventMutationStatus.Failed, paramErr);
                }
            }

            var existing = await db.PlayerMapEventExecutionRequests.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId && r.RequestId == requestId, ct)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.PlacementId != placementId || existing.CatalogAliasId != catalogAliasId)
                {
                    return new MapEventMutationResult(
                        MapEventMutationStatus.Failed,
                        "RequestId réutilisé avec événement différent.");
                }

                var replay = DeserializeSnapshot(existing.ResultJson);
                return new MapEventMutationResult(MapEventMutationStatus.IdempotentReplay, null, replay);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshot = new MapEventExecutionSnapshot();
                var pendingAfterWait = new List<MapEventCommandDefinition>();
                var waiting = false;

                var character = await db.PlayerCharacters
                    .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);
                if (character is null)
                {
                    return new MapEventMutationResult(MapEventMutationStatus.Failed, "Personnage introuvable.");
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var slots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(invRows);

                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i];
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

                    if (command.Discriminator == MapEventCommandDiscriminators.Wait)
                    {
                        if (MapEventParameterSchemas.TryParseWait(command.ParameterJson, out var waitMs, out _))
                        {
                            snapshot.Waiting = true;
                            snapshot.WaitUntilUtc = _clock.GetUtcNow().AddMilliseconds(waitMs);
                            pendingAfterWait = commands.Skip(i + 1).ToList();
                            waiting = true;
                        }

                        break;
                    }
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
                    PlacementId = placementId,
                    CatalogAliasId = catalogAliasId,
                    ResultJson = SerializeSnapshot(snapshot, pendingAfterWait, waiting),
                    CompletedAtUtc = _clock.GetUtcNow(),
                });

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();

                snapshot.PendingCommands = pendingAfterWait.Count > 0 ? pendingAfterWait : null;
                snapshot.ResultGold = character.Gold;
                return new MapEventMutationResult(MapEventMutationStatus.Executed, null, snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new MapEventMutationResult(MapEventMutationStatus.Failed, ex.Message);
            }
        }, cancellationToken);

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

            case MapEventCommandDiscriminators.Wait:
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

        var row = await db.PlayerCharacterWorldSwitches
            .FirstOrDefaultAsync(s => s.CharacterId == characterId && s.SwitchKey == switchId, ct)
            .ConfigureAwait(false);
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

        snapshot.SwitchesChanged = true;
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
        var row = await db.PlayerCharacterWorldVariables.AsNoTracking()
            .FirstOrDefaultAsync(v => v.CharacterId == characterId && v.VariableKey == variableId, ct)
            .ConfigureAwait(false);
        return row?.Value ?? 0;
    }

    private static async Task UpsertVariableAsync(
        FrogDbContext db,
        Guid characterId,
        string variableId,
        int value,
        CancellationToken ct)
    {
        var row = await db.PlayerCharacterWorldVariables
            .FirstOrDefaultAsync(v => v.CharacterId == characterId && v.VariableKey == variableId, ct)
            .ConfigureAwait(false);
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
            string? switchKey = null;
            if (!string.IsNullOrEmpty(onceKey))
            {
                switchKey = MapEventOnceGrantKeys.SwitchKeyFor(onceKey);
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

    private static async Task<bool> TryClaimSwitchInTransactionAsync(
        FrogDbContext db,
        Guid characterId,
        string switchKey,
        CancellationToken ct)
    {
        var existing = await db.PlayerCharacterWorldSwitches
            .FirstOrDefaultAsync(s => s.CharacterId == characterId && s.SwitchKey == switchKey, ct)
            .ConfigureAwait(false);
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
            ResultGold = snapshot.ResultGold,
            Waiting = waiting,
            WaitUntilUtc = snapshot.WaitUntilUtc,
            PendingCommands = pending.Count > 0 ? pending.ToList() : null,
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
                ResultGold = stored.ResultGold,
                Waiting = stored.Waiting,
                WaitUntilUtc = stored.WaitUntilUtc,
                PendingCommands = stored.PendingCommands,
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

        public int? ResultGold { get; set; }

        public bool Waiting { get; set; }

        public DateTimeOffset? WaitUntilUtc { get; set; }

        public List<MapEventCommandDefinition>? PendingCommands { get; set; }
    }
}
