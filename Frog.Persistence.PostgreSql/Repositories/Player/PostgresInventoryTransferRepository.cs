using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresInventoryTransferRepository : IInventoryTransferRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    /// <summary>Seam de test : lève une exception après mutations, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresInventoryTransferRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<InventoryTransferPickupResult> TryPickupAsync(
        Guid characterId,
        Guid groundItemId,
        int sessionMapId,
        int sessionPixelX,
        int sessionPixelY,
        int maxPickupDistancePixels,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return InventoryTransferPickupResult.Fail("Personnage introuvable.");
                }

                var ground = await db.PlayerGroundItems
                    .FromSqlInterpolated(
                        $"""
                        SELECT * FROM player.ground_items
                        WHERE id = {groundItemId} AND taken_at_utc IS NULL
                        FOR UPDATE
                        """)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
                if (ground is null)
                {
                    var existing = await db.PlayerGroundItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.Id == groundItemId, ct)
                        .ConfigureAwait(false);
                    if (existing is null)
                    {
                        return InventoryTransferPickupResult.Fail("Objet introuvable.");
                    }

                    return existing.TakenAtUtc is not null
                        ? InventoryTransferPickupResult.Fail("Deja ramasse.")
                        : InventoryTransferPickupResult.Fail("Objet indisponible.");
                }

                if (ground.MapId != sessionMapId)
                {
                    return InventoryTransferPickupResult.Fail("Objet sur une autre carte.");
                }

                if (ground.OwnerCharacterId is Guid owner && owner != characterId)
                {
                    return InventoryTransferPickupResult.Fail("Objet reserve.");
                }

                var rangeSq = (long)maxPickupDistancePixels * maxPickupDistancePixels;
                var distSq =
                    (long)(ground.PixelX - sessionPixelX) * (ground.PixelX - sessionPixelX)
                    + (long)(ground.PixelY - sessionPixelY) * (ground.PixelY - sessionPixelY);
                if (distSq > rangeSq)
                {
                    return InventoryTransferPickupResult.Fail("Hors portee.");
                }

                var published = await TryLoadPublishedItemAsync(db, ground.ItemId, ct).ConfigureAwait(false);
                if (published is null)
                {
                    return InventoryTransferPickupResult.Fail("Objet inconnu.");
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                if (!TryAddToInventory(invSlots, ground.ItemId, ground.Quantity, published.Value.MaxStack))
                {
                    return InventoryTransferPickupResult.Fail("Inventaire plein.");
                }

                ground.TakenAtUtc = _clock.GetUtcNow();
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return InventoryTransferPickupResult.Ok(new InventorySnapshot(characterId, invSlots));
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<InventoryTransferDropResult> TryDropAsync(
        Guid characterId,
        int inventorySlotIndex,
        int quantity,
        int sessionMapId,
        int sessionPixelX,
        int sessionPixelY,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (quantity <= 0
                || inventorySlotIndex < 0
                || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return InventoryTransferDropResult.Fail("Parametres invalides.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return InventoryTransferDropResult.Fail("Personnage introuvable.");
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                var slot = invSlots[inventorySlotIndex];
                if (slot.ItemId is not Guid itemId || slot.Quantity < quantity)
                {
                    return InventoryTransferDropResult.Fail("Objet insuffisant.");
                }

                if (!TryRemoveFromInventory(invSlots, inventorySlotIndex, quantity))
                {
                    return InventoryTransferDropResult.Fail("Retrait echoue.");
                }

                var onMap = await db.PlayerGroundItems
                    .CountAsync(i => i.MapId == sessionMapId && i.TakenAtUtc == null, ct)
                    .ConfigureAwait(false);
                if (onMap >= GameplayLimits.MaxGroundItemsPerMap)
                {
                    return InventoryTransferDropResult.Fail("Carte pleine.");
                }

                var groundEntity = new GroundItemEntity
                {
                    Id = Guid.NewGuid(),
                    MapId = sessionMapId,
                    PixelX = sessionPixelX,
                    PixelY = sessionPixelY,
                    ItemId = itemId,
                    Quantity = quantity,
                    OwnerCharacterId = characterId,
                    CreatedAtUtc = _clock.GetUtcNow(),
                };
                db.PlayerGroundItems.Add(groundEntity);
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return InventoryTransferDropResult.Ok(
                    new InventorySnapshot(characterId, invSlots),
                    PlayerEntityMapper.ToGroundItemRecord(groundEntity));
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<InventoryTransferEquipResult> TryEquipAsync(
        Guid characterId,
        int inventorySlotIndex,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (inventorySlotIndex < 0 || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return InventoryTransferEquipResult.Fail("Emplacement inventaire invalide.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return InventoryTransferEquipResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                var slot = invSlots[inventorySlotIndex];
                if (slot.ItemId is not Guid itemId)
                {
                    return InventoryTransferEquipResult.Fail("Emplacement vide.");
                }

                var published = await TryLoadPublishedItemAsync(db, itemId, ct).ConfigureAwait(false);
                if (published is null)
                {
                    return InventoryTransferEquipResult.Fail("Objet inconnu.");
                }

                var equipSlot = published.Value.Kind switch
                {
                    ItemType.Weapon => EquipmentSlotKind.Weapon,
                    ItemType.Armor => EquipmentSlotKind.Armor,
                    _ => EquipmentSlotKind.None,
                };
                if (equipSlot == EquipmentSlotKind.None)
                {
                    return InventoryTransferEquipResult.Fail("Type d'objet non equippable.");
                }

                if (!TryRemoveFromInventory(invSlots, inventorySlotIndex, 1))
                {
                    return InventoryTransferEquipResult.Fail("Retrait inventaire echoue.");
                }

                var previousItemId = equipSlot == EquipmentSlotKind.Weapon
                    ? character.EquippedWeaponItemId
                    : character.EquippedArmorItemId;
                if (previousItemId is Guid prevId)
                {
                    var prevPublished = await TryLoadPublishedItemAsync(db, prevId, ct).ConfigureAwait(false);
                    if (prevPublished is not null
                        && !TryAddToInventory(invSlots, prevId, 1, prevPublished.Value.MaxStack))
                    {
                        return InventoryTransferEquipResult.Fail("Inventaire plein pour l'objet precedemment equipe.");
                    }
                }

                if (equipSlot == EquipmentSlotKind.Weapon)
                {
                    character.EquippedWeaponItemId = itemId;
                }
                else
                {
                    character.EquippedArmorItemId = itemId;
                }

                character.UpdatedAtUtc = _clock.GetUtcNow();
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return InventoryTransferEquipResult.Ok(
                    new InventorySnapshot(characterId, invSlots),
                    new EquipmentRecord(
                        characterId,
                        character.EquippedWeaponItemId,
                        character.EquippedArmorItemId));
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    public Task<InventoryTransferUnequipResult> TryUnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor))
            {
                return InventoryTransferUnequipResult.Fail("Emplacement equipement invalide.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return InventoryTransferUnequipResult.Fail("Personnage introuvable.");
                }

                var character = await db.PlayerCharacters
                    .FirstAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var itemId = slot == EquipmentSlotKind.Weapon
                    ? character.EquippedWeaponItemId
                    : character.EquippedArmorItemId;
                if (itemId is not Guid equippedId)
                {
                    return InventoryTransferUnequipResult.Fail("Rien a desequiper.");
                }

                var published = await TryLoadPublishedItemAsync(db, equippedId, ct).ConfigureAwait(false);
                if (published is null)
                {
                    return InventoryTransferUnequipResult.Fail("Objet inconnu.");
                }

                var invRows = await db.PlayerInventorySlots
                    .Where(s => s.CharacterId == characterId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                var invSlots = InventorySlotsFromRows(invRows);
                if (!TryAddToInventory(invSlots, equippedId, 1, published.Value.MaxStack))
                {
                    return InventoryTransferUnequipResult.Fail("Inventaire plein.");
                }

                if (slot == EquipmentSlotKind.Weapon)
                {
                    character.EquippedWeaponItemId = null;
                }
                else
                {
                    character.EquippedArmorItemId = null;
                }

                character.UpdatedAtUtc = _clock.GetUtcNow();
                await PersistInventorySlotsAsync(db, characterId, invRows, invSlots, ct).ConfigureAwait(false);

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return InventoryTransferUnequipResult.Ok(
                    new InventorySnapshot(characterId, invSlots),
                    new EquipmentRecord(
                        characterId,
                        character.EquippedWeaponItemId,
                        character.EquippedArmorItemId));
            }
            catch (Exception)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Rollback best-effort: original exception (including OCE) is preserved below.
                }

                db.ChangeTracker.Clear();
                throw; // preserves original exception including OCE
            }
        }, cancellationToken);

    private static async Task<(ItemType Kind, int MaxStack)?> TryLoadPublishedItemAsync(
        FrogDbContext db,
        Guid itemId,
        CancellationToken ct)
    {
        var tip = await db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await db.ItemPublishedSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
            .ConfigureAwait(false);
        return snapshot is null ? null : (snapshot.Kind, snapshot.MaxStack);
    }

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
    {
        var exists = await db.PlayerCharacters
            .AnyAsync(c => c.Id == characterId, ct)
            .ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM player.characters WHERE id = {characterId} FOR UPDATE",
            ct).ConfigureAwait(false);
        return true;
    }

    private static InventorySlotRecord[] InventorySlotsFromRows(IReadOnlyList<InventorySlotEntity> rows)
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        foreach (var row in rows)
        {
            if (row.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                slots[row.SlotIndex] = new InventorySlotRecord(row.SlotIndex, row.ItemId, row.Quantity);
            }
        }

        return slots;
    }

    private static bool TryAddToInventory(InventorySlotRecord[] slots, Guid itemId, int quantity, int maxStack)
    {
        var remaining = quantity;
        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].ItemId == itemId && slots[i].Quantity < maxStack)
            {
                var can = Math.Min(maxStack - slots[i].Quantity, remaining);
                slots[i] = new InventorySlotRecord(i, itemId, slots[i].Quantity + can);
                remaining -= can;
            }
        }

        for (var i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].ItemId is null)
            {
                var can = Math.Min(maxStack, remaining);
                slots[i] = new InventorySlotRecord(i, itemId, can);
                remaining -= can;
            }
        }

        return remaining == 0;
    }

    private static bool TryRemoveFromInventory(InventorySlotRecord[] slots, int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            return false;
        }

        var slot = slots[slotIndex];
        if (slot.ItemId is null || slot.Quantity < quantity)
        {
            return false;
        }

        var left = slot.Quantity - quantity;
        slots[slotIndex] = left == 0
            ? new InventorySlotRecord(slotIndex, null, 0)
            : new InventorySlotRecord(slotIndex, slot.ItemId, left);
        return true;
    }

    private static Task PersistInventorySlotsAsync(
        FrogDbContext db,
        Guid characterId,
        List<InventorySlotEntity> rows,
        InventorySlotRecord[] slots,
        CancellationToken ct)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var row = rows.FirstOrDefault(r => r.SlotIndex == i);
            if (slot.ItemId is null || slot.Quantity <= 0)
            {
                if (row is not null)
                {
                    db.PlayerInventorySlots.Remove(row);
                    rows.Remove(row);
                }

                continue;
            }

            if (row is null)
            {
                db.PlayerInventorySlots.Add(new InventorySlotEntity
                {
                    CharacterId = characterId,
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity,
                });
            }
            else
            {
                row.ItemId = slot.ItemId;
                row.Quantity = slot.Quantity;
            }
        }

        return Task.CompletedTask;
    }
}
