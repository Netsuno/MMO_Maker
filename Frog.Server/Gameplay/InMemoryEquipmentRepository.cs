using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly ConcurrentDictionary<Guid, EquipmentRecord> _byCharacter = new();

    public Task<EquipmentRecord> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byCharacter.GetOrAdd(characterId, id => new EquipmentRecord(id, null, null)));

    public Task<EquipmentMutationResult> EquipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor) || itemId == Guid.Empty)
        {
            return Task.FromResult(new EquipmentMutationResult(EquipmentMutationStatus.InvalidSlot));
        }

        var current = _byCharacter.GetOrAdd(characterId, id => new EquipmentRecord(id, null, null));
        var updated = slot == EquipmentSlotKind.Weapon
            ? current with { WeaponItemId = itemId }
            : current with { ArmorItemId = itemId };
        _byCharacter[characterId] = updated;
        return Task.FromResult(new EquipmentMutationResult(EquipmentMutationStatus.Ok, updated));
    }

    public Task<EquipmentMutationResult> UnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default)
    {
        if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor))
        {
            return Task.FromResult(new EquipmentMutationResult(EquipmentMutationStatus.InvalidSlot));
        }

        var current = _byCharacter.GetOrAdd(characterId, id => new EquipmentRecord(id, null, null));
        var has = slot == EquipmentSlotKind.Weapon ? current.WeaponItemId : current.ArmorItemId;
        if (has is null)
        {
            return Task.FromResult(new EquipmentMutationResult(EquipmentMutationStatus.EmptySlot));
        }

        var updated = slot == EquipmentSlotKind.Weapon
            ? current with { WeaponItemId = null }
            : current with { ArmorItemId = null };
        _byCharacter[characterId] = updated;
        return Task.FromResult(new EquipmentMutationResult(EquipmentMutationStatus.Ok, updated));
    }
}
