using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Gameplay;

public sealed class InventoryGameplayService(
    IInventoryRepository inventory,
    IEquipmentRepository equipment,
    IGroundItemRepository groundItems,
    ICharacterRepository characters,
    IPublishedItemCatalog items)
{
    private readonly IInventoryRepository _inventory = inventory;
    private readonly IEquipmentRepository _equipment = equipment;
    private readonly IGroundItemRepository _groundItems = groundItems;
    private readonly ICharacterRepository _characters = characters;
    private readonly IPublishedItemCatalog _items = items;

    public Task<InventorySnapshot> GetInventoryAsync(Guid characterId, CancellationToken ct = default)
        => _inventory.GetAsync(characterId, ct);

    public async Task<InventoryMutationResult> TryAddItemAsync(
        Guid characterId,
        Guid itemId,
        int quantity,
        CancellationToken ct = default)
    {
        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return new InventoryMutationResult(InventoryMutationStatus.ItemNotFound, ErrorMessage: "Objet inconnu.");
        }

        return await _inventory.TryAddAsync(characterId, itemId, quantity, item.MaxStack, ct).ConfigureAwait(false);
    }

    public Task<InventoryMutationResult> TryRemoveFromSlotAsync(
        Guid characterId,
        int slotIndex,
        int quantity,
        CancellationToken ct = default)
        => _inventory.TryRemoveAsync(characterId, slotIndex, quantity, ct);

    public async Task<EquipResult> TryEquipAsync(Session session, int inventorySlotIndex, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return EquipResult.Fail("Aucun personnage actif.");
        }

        if (session.IsDead)
        {
            return EquipResult.Fail("Personnage mort.");
        }

        if (inventorySlotIndex is < 0 or >= GameplayLimits.InventorySlotCount)
        {
            return EquipResult.Fail("Emplacement inventaire invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
        var slot = inv.Slots.FirstOrDefault(s => s.SlotIndex == inventorySlotIndex);
        if (slot?.ItemId is not Guid itemId)
        {
            return EquipResult.Fail("Emplacement vide.");
        }

        var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return EquipResult.Fail("Objet inconnu.");
        }

        var equipSlot = item.Kind switch
        {
            ItemType.Weapon => EquipmentSlotKind.Weapon,
            ItemType.Armor => EquipmentSlotKind.Armor,
            _ => EquipmentSlotKind.None,
        };
        if (equipSlot == EquipmentSlotKind.None)
        {
            return EquipResult.Fail("Type d'objet non equippable.");
        }

        var removed = await _inventory.TryRemoveAsync(characterId, inventorySlotIndex, 1, ct).ConfigureAwait(false);
        if (removed.Status != InventoryMutationStatus.Ok)
        {
            return EquipResult.Fail(removed.ErrorMessage ?? "Retrait inventaire echoue.");
        }

        var currentEquip = await _equipment.GetAsync(characterId, ct).ConfigureAwait(false);
        var previousItemId = equipSlot == EquipmentSlotKind.Weapon
            ? currentEquip.WeaponItemId
            : currentEquip.ArmorItemId;

        if (previousItemId is Guid prevId)
        {
            var prevItem = await _items.LoadPublishedByIdAsync(prevId, ct).ConfigureAwait(false);
            if (prevItem is not null)
            {
                var readd = await _inventory.TryAddAsync(characterId, prevId, 1, prevItem.MaxStack, ct).ConfigureAwait(false);
                if (readd.Status != InventoryMutationStatus.Ok)
                {
                    await _inventory.TryAddAsync(characterId, itemId, 1, item.MaxStack, ct).ConfigureAwait(false);
                    return EquipResult.Fail("Inventaire plein pour l'objet precedemment equipe.");
                }
            }
        }

        var equip = await _equipment.EquipAsync(characterId, equipSlot, itemId, ct).ConfigureAwait(false);
        if (equip.Status != EquipmentMutationStatus.Ok)
        {
            await _inventory.TryAddAsync(characterId, itemId, 1, item.MaxStack, ct).ConfigureAwait(false);
            return EquipResult.Fail("Equipement echoue.");
        }

        if (equipSlot == EquipmentSlotKind.Weapon)
        {
            session.EquippedWeaponItemId = itemId;
        }
        else
        {
            session.EquippedArmorItemId = itemId;
        }

        await PersistEquipmentAsync(session, ct).ConfigureAwait(false);
        return EquipResult.Ok(await _inventory.GetAsync(characterId, ct).ConfigureAwait(false), equip.Equipment!);
    }

    public async Task<UnequipResult> TryUnequipAsync(Session session, EquipmentSlotKind slot, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return UnequipResult.Fail("Aucun personnage actif.");
        }

        if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor))
        {
            return UnequipResult.Fail("Emplacement equipement invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var current = await _equipment.GetAsync(characterId, ct).ConfigureAwait(false);
        var itemId = slot == EquipmentSlotKind.Weapon ? current.WeaponItemId : current.ArmorItemId;
        if (itemId is not Guid equippedId)
        {
            return UnequipResult.Fail("Rien a desequiper.");
        }

        var item = await _items.LoadPublishedByIdAsync(equippedId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return UnequipResult.Fail("Objet inconnu.");
        }

        var added = await _inventory.TryAddAsync(characterId, equippedId, 1, item.MaxStack, ct).ConfigureAwait(false);
        if (added.Status != InventoryMutationStatus.Ok)
        {
            return UnequipResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
        }

        var unequip = await _equipment.UnequipAsync(characterId, slot, ct).ConfigureAwait(false);
        if (unequip.Status != EquipmentMutationStatus.Ok)
        {
            await _inventory.TryRemoveAsync(characterId, FindSlotWithItem(added.Snapshot!, equippedId), 1, ct).ConfigureAwait(false);
            return UnequipResult.Fail("Desequipement echoue.");
        }

        if (slot == EquipmentSlotKind.Weapon)
        {
            session.EquippedWeaponItemId = null;
        }
        else
        {
            session.EquippedArmorItemId = null;
        }

        await PersistEquipmentAsync(session, ct).ConfigureAwait(false);
        return UnequipResult.Ok(added.Snapshot!, unequip.Equipment!);
    }

    public async Task<DropResult> TryDropAsync(Session session, int slotIndex, int quantity, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return DropResult.Fail("Aucun personnage actif.");
        }

        if (quantity <= 0)
        {
            return DropResult.Fail("Quantite invalide.");
        }

        var characterId = session.RequireCharacterGuid();
        var inv = await _inventory.GetAsync(characterId, ct).ConfigureAwait(false);
        var slot = inv.Slots.FirstOrDefault(s => s.SlotIndex == slotIndex);
        if (slot?.ItemId is not Guid itemId || slot.Quantity < quantity)
        {
            return DropResult.Fail("Objet insuffisant.");
        }

        var removed = await _inventory.TryRemoveAsync(characterId, slotIndex, quantity, ct).ConfigureAwait(false);
        if (removed.Status != InventoryMutationStatus.Ok)
        {
            return DropResult.Fail(removed.ErrorMessage ?? "Retrait echoue.");
        }

        var dropped = await _groundItems.DropAsync(
            session.CurrentMapId,
            session.PixelX,
            session.PixelY,
            itemId,
            quantity,
            characterId,
            ct).ConfigureAwait(false);
        if (dropped.Status != GroundItemMutationStatus.Ok)
        {
            var item = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
            await _inventory.TryAddAsync(characterId, itemId, quantity, item?.MaxStack ?? 1, ct)
                .ConfigureAwait(false);
            return DropResult.Fail("Depot au sol echoue.");
        }

        return DropResult.Ok(removed.Snapshot!, dropped.Item!);
    }

    public async Task<PickupResult> TryPickupAsync(Session session, Guid groundItemId, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return PickupResult.Fail("Aucun personnage actif.");
        }

        if (session.IsDead)
        {
            return PickupResult.Fail("Personnage mort.");
        }

        var characterId = session.RequireCharacterGuid();
        var taken = await _groundItems.TryPickupAsync(
            groundItemId,
            characterId,
            session.PixelX,
            session.PixelY,
            GameplayLimits.GroundPickupRangePixels,
            ct).ConfigureAwait(false);
        if (taken.Status != GroundItemMutationStatus.Ok || taken.Item is null)
        {
            return PickupResult.Fail(taken.Status switch
            {
                GroundItemMutationStatus.NotFound => "Objet introuvable.",
                GroundItemMutationStatus.OutOfRange => "Hors portee.",
                GroundItemMutationStatus.AlreadyTaken => "Deja ramasse.",
                _ => "Ramassage echoue.",
            });
        }

        var item = await _items.LoadPublishedByIdAsync(taken.Item.ItemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            return PickupResult.Fail("Objet inconnu.");
        }

        var added = await _inventory.TryAddAsync(
            characterId,
            taken.Item.ItemId,
            taken.Item.Quantity,
            item.MaxStack,
            ct).ConfigureAwait(false);
        if (added.Status != InventoryMutationStatus.Ok)
        {
            await _groundItems.DropAsync(
                taken.Item.MapId,
                taken.Item.PixelX,
                taken.Item.PixelY,
                taken.Item.ItemId,
                taken.Item.Quantity,
                taken.Item.OwnerCharacterId,
                ct).ConfigureAwait(false);
            return PickupResult.Fail(added.ErrorMessage ?? "Inventaire plein.");
        }

        return PickupResult.Ok(added.Snapshot!);
    }

    public Task<IReadOnlyList<GroundItemRecord>> ListGroundOnMapAsync(int mapId, CancellationToken ct = default)
        => _groundItems.ListOnMapAsync(mapId, ct);

    private async Task PersistEquipmentAsync(Session session, CancellationToken ct)
    {
        if (session.CharacterGuid is not Guid characterId)
        {
            return;
        }

        var record = await _characters.FindByIdAsync(characterId, ct).ConfigureAwait(false);
        if (record is null)
        {
            return;
        }

        await _characters.SaveAsync(session.ToCharacterPatch(record), ct).ConfigureAwait(false);
    }

    private static int FindSlotWithItem(InventorySnapshot snapshot, Guid itemId)
    {
        foreach (var slot in snapshot.Slots)
        {
            if (slot.ItemId == itemId)
            {
                return slot.SlotIndex;
            }
        }

        return 0;
    }
}

public sealed record EquipResult(bool Success, string Message, InventorySnapshot? Inventory = null, EquipmentRecord? Equipment = null)
{
    public static EquipResult Ok(InventorySnapshot inv, EquipmentRecord equip) => new(true, "Equipe.", inv, equip);
    public static EquipResult Fail(string message) => new(false, message);
}

public sealed record UnequipResult(bool Success, string Message, InventorySnapshot? Inventory = null, EquipmentRecord? Equipment = null)
{
    public static UnequipResult Ok(InventorySnapshot inv, EquipmentRecord equip) => new(true, "Desequipe.", inv, equip);
    public static UnequipResult Fail(string message) => new(false, message);
}

public sealed record DropResult(bool Success, string Message, InventorySnapshot? Inventory = null, GroundItemRecord? GroundItem = null)
{
    public static DropResult Ok(InventorySnapshot inv, GroundItemRecord ground) => new(true, "Depose.", inv, ground);
    public static DropResult Fail(string message) => new(false, message);
}

public sealed record PickupResult(bool Success, string Message, InventorySnapshot? Inventory = null)
{
    public static PickupResult Ok(InventorySnapshot inv) => new(true, "Ramasse.", inv);
    public static PickupResult Fail(string message) => new(false, message);
}
