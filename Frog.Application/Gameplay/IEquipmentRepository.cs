using Frog.Core.Gameplay;

namespace Frog.Application.Gameplay;

public sealed record EquipmentRecord(
    Guid CharacterId,
    Guid? WeaponItemId,
    Guid? ArmorItemId);

public enum EquipmentMutationStatus
{
    Ok,
    InvalidSlot,
    InvalidItem,
    WrongItemType,
    EmptySlot,
    CharacterNotFound,
}

public sealed record EquipmentMutationResult(
    EquipmentMutationStatus Status,
    EquipmentRecord? Equipment = null,
    string? ErrorMessage = null);

public interface IEquipmentRepository
{
    Task<EquipmentRecord> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<EquipmentMutationResult> EquipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<EquipmentMutationResult> UnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default);
}
