using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Gameplay;

public sealed class InventoryGameplayService(
    IInventoryRepository inventory,
    IInventoryTransferRepository transfers,
    IGroundItemRepository groundItems,
    IPublishedItemCatalog items,
    IEquipmentRepository equipment)
{
    private readonly IInventoryRepository _inventory = inventory;
    private readonly IInventoryTransferRepository _transfers = transfers;
    private readonly IGroundItemRepository _groundItems = groundItems;
    private readonly IPublishedItemCatalog _items = items;
    private readonly IEquipmentRepository _equipment = equipment;

    public async Task SyncEquippedItemsToSessionAsync(Session session, CancellationToken ct = default)
    {
        if (!session.HasActiveCharacter())
        {
            return;
        }

        var equipped = await _equipment.GetAsync(session.RequireCharacterGuid(), ct).ConfigureAwait(false);
        session.EquippedWeaponItemId = equipped.WeaponItemId;
        session.EquippedArmorItemId = equipped.ArmorItemId;
    }

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
        var result = await _transfers.TryEquipAsync(characterId, inventorySlotIndex, ct).ConfigureAwait(false);
        if (!result.Success || result.Inventory is null || result.Equipment is null)
        {
            return EquipResult.Fail(result.Message);
        }

        session.EquippedWeaponItemId = result.Equipment.WeaponItemId;
        session.EquippedArmorItemId = result.Equipment.ArmorItemId;
        return EquipResult.Ok(result.Inventory, result.Equipment);
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
        var result = await _transfers.TryUnequipAsync(characterId, slot, ct).ConfigureAwait(false);
        if (!result.Success || result.Inventory is null || result.Equipment is null)
        {
            return UnequipResult.Fail(result.Message);
        }

        session.EquippedWeaponItemId = result.Equipment.WeaponItemId;
        session.EquippedArmorItemId = result.Equipment.ArmorItemId;
        return UnequipResult.Ok(result.Inventory, result.Equipment);
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
        var result = await _transfers.TryDropAsync(
            characterId,
            slotIndex,
            quantity,
            session.CurrentMapId,
            session.PixelX,
            session.PixelY,
            ct).ConfigureAwait(false);
        if (!result.Success || result.Inventory is null || result.GroundItem is null)
        {
            return DropResult.Fail(result.Message);
        }

        return DropResult.Ok(result.Inventory, result.GroundItem);
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
        var result = await _transfers.TryPickupAsync(
            characterId,
            groundItemId,
            session.CurrentMapId,
            session.PixelX,
            session.PixelY,
            GameplayLimits.GroundPickupRangePixels,
            ct).ConfigureAwait(false);
        if (!result.Success || result.Inventory is null)
        {
            return PickupResult.Fail(result.Message);
        }

        return PickupResult.Ok(result.Inventory, result.ItemId ?? Guid.Empty);
    }

    public Task<IReadOnlyList<GroundItemRecord>> ListGroundOnMapAsync(int mapId, CancellationToken ct = default)
        => _groundItems.ListOnMapAsync(mapId, ct);
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

public sealed record PickupResult(bool Success, string Message, InventorySnapshot? Inventory = null, Guid? ItemId = null)
{
    public static PickupResult Ok(InventorySnapshot inv, Guid itemId) => new(true, "Ramasse.", inv, itemId);
    public static PickupResult Fail(string message) => new(false, message);
}
