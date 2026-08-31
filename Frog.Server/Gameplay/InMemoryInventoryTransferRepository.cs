using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

/// <summary>Transferts inventaire / equipement / sol en memoire (tests / AllowInMemoryFallback).</summary>
public sealed class InMemoryInventoryTransferRepository : IInventoryTransferRepository
{
    private readonly IInventoryRepository _inventory;
    private readonly IEquipmentRepository _equipment;
    private readonly InMemoryGroundItemRepository _ground;
    private readonly IPublishedItemCatalog _items;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _characterLocks = new();
    private readonly SemaphoreSlim _groundGate = new(1, 1);

    /// <summary>Seam de test : lève une exception après mutations, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public InMemoryInventoryTransferRepository(
        IInventoryRepository inventory,
        IEquipmentRepository equipment,
        IGroundItemRepository ground,
        IPublishedItemCatalog items)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _ground = ground as InMemoryGroundItemRepository
            ?? throw new ArgumentException("Expected InMemoryGroundItemRepository.", nameof(ground));
    }

    public Task<InventoryTransferPickupResult> TryPickupAsync(
        Guid characterId,
        Guid groundItemId,
        int sessionMapId,
        int sessionPixelX,
        int sessionPixelY,
        int maxPickupDistancePixels,
        CancellationToken cancellationToken = default)
        => ExecuteCharacterAsync(characterId, async ct =>
        {
            await _groundGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_ground.TryGetUntaken(groundItemId, out var groundItem) || groundItem is null)
                {
                    return InventoryTransferPickupResult.Fail("Objet introuvable.");
                }

                if (groundItem.MapId != sessionMapId)
                {
                    return InventoryTransferPickupResult.Fail("Objet sur une autre carte.");
                }

                if (groundItem.OwnerCharacterId is Guid owner && owner != characterId)
                {
                    return InventoryTransferPickupResult.Fail("Objet reserve.");
                }

                var rangeSq = (long)maxPickupDistancePixels * maxPickupDistancePixels;
                var distSq = Frog.Core.Constants.WorldMetrics.DistanceSquaredPixels(
                    sessionPixelX,
                    sessionPixelY,
                    groundItem.PixelX,
                    groundItem.PixelY);
                if (distSq > rangeSq)
                {
                    return InventoryTransferPickupResult.Fail("Hors portee.");
                }

                var item = await _items.LoadPublishedByIdAsync(groundItem.ItemId, ct).ConfigureAwait(false);
                if (item is null)
                {
                    return InventoryTransferPickupResult.Fail("Objet inconnu.");
                }

                var invBefore = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
                var slots = CloneSlots(invBefore);
                if (!TryAddToInventory(slots, groundItem.ItemId, groundItem.Quantity, item.MaxStack))
                {
                    return InventoryTransferPickupResult.Fail("Inventaire plein.");
                }

                if (!_ground.TryRemoveUntaken(groundItemId, out _))
                {
                    return InventoryTransferPickupResult.Fail("Objet indisponible.");
                }

                try
                {
                    await _inventory.ReplaceAllAsync(characterId, slots, ct).ConfigureAwait(false);
                    if (TestBeforeCommitAsync is not null)
                    {
                        await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                    }
                }
                catch
                {
                    _ground.Restore(groundItem);
                    await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                    throw;
                }

                return InventoryTransferPickupResult.Ok(new InventorySnapshot(characterId, slots), groundItem.ItemId);
            }
            finally
            {
                _groundGate.Release();
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
        => ExecuteCharacterAsync(characterId, async ct =>
        {
            if (quantity <= 0
                || inventorySlotIndex < 0
                || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return InventoryTransferDropResult.Fail("Parametres invalides.");
            }

            var invBefore = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var slot = invBefore.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
            if (slot?.ItemId is not Guid itemId || slot.Quantity < quantity)
            {
                return InventoryTransferDropResult.Fail("Objet insuffisant.");
            }

            var slots = CloneSlots(invBefore);
            if (!TryRemoveFromInventory(slots, inventorySlotIndex, quantity))
            {
                return InventoryTransferDropResult.Fail("Retrait echoue.");
            }

            await _groundGate.WaitAsync(ct).ConfigureAwait(false);
            GroundItemRecord? dropped;
            try
            {
                var dropResult = await _ground.DropAsync(
                    sessionMapId,
                    sessionPixelX,
                    sessionPixelY,
                    itemId,
                    quantity,
                    characterId,
                    ct).ConfigureAwait(false);
                if (dropResult.Status != GroundItemMutationStatus.Ok || dropResult.Item is null)
                {
                    return InventoryTransferDropResult.Fail("Depot au sol echoue.");
                }

                dropped = dropResult.Item;
            }
            finally
            {
                _groundGate.Release();
            }

            try
            {
                await _inventory.ReplaceAllAsync(characterId, slots, ct).ConfigureAwait(false);
                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }
            }
            catch
            {
                if (dropped is not null)
                {
                    _ground.TryRemoveUntaken(dropped.Id, out _);
                }

                await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                throw;
            }

            return InventoryTransferDropResult.Ok(new InventorySnapshot(characterId, slots), dropped!);
        }, cancellationToken);

    public Task<InventoryTransferEquipResult> TryEquipAsync(
        Guid characterId,
        int inventorySlotIndex,
        CancellationToken cancellationToken = default)
        => ExecuteCharacterAsync(characterId, async ct =>
        {
            if (inventorySlotIndex < 0 || inventorySlotIndex >= GameplayLimits.InventorySlotCount)
            {
                return InventoryTransferEquipResult.Fail("Emplacement inventaire invalide.");
            }

            var invBefore = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var slot = invBefore.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
            if (slot?.ItemId is not Guid itemId)
            {
                return InventoryTransferEquipResult.Fail("Emplacement vide.");
            }

            var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
            if (item is null)
            {
                return InventoryTransferEquipResult.Fail("Objet inconnu.");
            }

            var equipSlot = item.Kind switch
            {
                ItemType.Weapon => EquipmentSlotKind.Weapon,
                ItemType.Armor => EquipmentSlotKind.Armor,
                _ => EquipmentSlotKind.None,
            };
            if (equipSlot == EquipmentSlotKind.None)
            {
                return InventoryTransferEquipResult.Fail("Type d'objet non equippable.");
            }

            var equipBefore = await _equipment.GetAsync(characterId, ct).ConfigureAwait(false);
            var previousItemId = equipSlot == EquipmentSlotKind.Weapon
                ? equipBefore.WeaponItemId
                : equipBefore.ArmorItemId;

            var slots = CloneSlots(invBefore);
            if (!TryRemoveFromInventory(slots, inventorySlotIndex, 1))
            {
                return InventoryTransferEquipResult.Fail("Retrait inventaire echoue.");
            }

            if (previousItemId is Guid prevId)
            {
                var prevItem = await _items.LoadPublishedByIdAsync(prevId, ct).ConfigureAwait(false);
                if (prevItem is not null
                    && !TryAddToInventory(slots, prevId, 1, prevItem.MaxStack))
                {
                    return InventoryTransferEquipResult.Fail("Inventaire plein pour l'objet precedemment equipe.");
                }
            }

            try
            {
                await _inventory.ReplaceAllAsync(characterId, slots, ct).ConfigureAwait(false);
                var equipResult = await _equipment.EquipAsync(characterId, equipSlot, itemId, ct).ConfigureAwait(false);
                if (equipResult.Status != EquipmentMutationStatus.Ok)
                {
                    await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                    return InventoryTransferEquipResult.Fail("Equipement echoue.");
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                return InventoryTransferEquipResult.Ok(
                    new InventorySnapshot(characterId, slots),
                    equipResult.Equipment!);
            }
            catch
            {
                await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                if (previousItemId is Guid prevId2)
                {
                    await _equipment.EquipAsync(characterId, equipSlot, prevId2, ct).ConfigureAwait(false);
                }
                else
                {
                    await _equipment.UnequipAsync(characterId, equipSlot, ct).ConfigureAwait(false);
                }

                throw;
            }
        }, cancellationToken);

    public Task<InventoryTransferUnequipResult> TryUnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default)
        => ExecuteCharacterAsync(characterId, async ct =>
        {
            if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor))
            {
                return InventoryTransferUnequipResult.Fail("Emplacement equipement invalide.");
            }

            var equipBefore = await _equipment.GetAsync(characterId, ct).ConfigureAwait(false);
            var itemId = slot == EquipmentSlotKind.Weapon ? equipBefore.WeaponItemId : equipBefore.ArmorItemId;
            if (itemId is not Guid equippedId)
            {
                return InventoryTransferUnequipResult.Fail("Rien a desequiper.");
            }

            var item = await _items.LoadPublishedByIdAsync(equippedId, ct).ConfigureAwait(false);
            if (item is null)
            {
                return InventoryTransferUnequipResult.Fail("Objet inconnu.");
            }

            var invBefore = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
            var slots = CloneSlots(invBefore);
            if (!TryAddToInventory(slots, equippedId, 1, item.MaxStack))
            {
                return InventoryTransferUnequipResult.Fail("Inventaire plein.");
            }

            try
            {
                await _inventory.ReplaceAllAsync(characterId, slots, ct).ConfigureAwait(false);
                var unequipResult = await _equipment.UnequipAsync(characterId, slot, ct).ConfigureAwait(false);
                if (unequipResult.Status != EquipmentMutationStatus.Ok)
                {
                    await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                    return InventoryTransferUnequipResult.Fail("Desequipement echoue.");
                }

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                return InventoryTransferUnequipResult.Ok(
                    new InventorySnapshot(characterId, slots),
                    unequipResult.Equipment!);
            }
            catch
            {
                await _inventory.ReplaceAllAsync(characterId, invBefore.Slots, ct).ConfigureAwait(false);
                await _equipment.EquipAsync(characterId, slot, equippedId, ct).ConfigureAwait(false);
                throw;
            }
        }, cancellationToken);

    private async Task<T> ExecuteCharacterAsync<T>(
        Guid characterId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var gate = _characterLocks.GetOrAdd(characterId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static InventorySlotRecord[] CloneSlots(InventorySnapshot snapshot)
    {
        var slots = new InventorySlotRecord[GameplayLimits.InventorySlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new InventorySlotRecord(i, null, 0);
        }

        foreach (var slot in snapshot.Slots)
        {
            if (slot.SlotIndex is >= 0 and < GameplayLimits.InventorySlotCount)
            {
                slots[slot.SlotIndex] = new InventorySlotRecord(slot.SlotIndex, slot.ItemId, slot.Quantity);
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
}
