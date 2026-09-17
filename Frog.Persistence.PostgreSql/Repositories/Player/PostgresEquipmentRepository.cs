using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresEquipmentRepository : IEquipmentRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresEquipmentRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<EquipmentRecord> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.PlayerCharacters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                .ConfigureAwait(false);
            return entity is null
                ? new EquipmentRecord(characterId, null, null)
                : new EquipmentRecord(
                    characterId,
                    entity.EquippedWeaponItemId,
                    entity.EquippedArmorItemId);
        }, cancellationToken);

    public Task<EquipmentMutationResult> EquipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        Guid itemId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor) || itemId == Guid.Empty)
            {
                return new EquipmentMutationResult(EquipmentMutationStatus.InvalidSlot);
            }

            var entity = await db.PlayerCharacters
                .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return new EquipmentMutationResult(EquipmentMutationStatus.CharacterNotFound);
            }

            if (slot == EquipmentSlotKind.Weapon)
            {
                entity.EquippedWeaponItemId = itemId;
            }
            else
            {
                entity.EquippedArmorItemId = itemId;
            }

            entity.UpdatedAtUtc = _clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new EquipmentMutationResult(
                EquipmentMutationStatus.Ok,
                new EquipmentRecord(characterId, entity.EquippedWeaponItemId, entity.EquippedArmorItemId));
        }, cancellationToken);

    public Task<EquipmentMutationResult> UnequipAsync(
        Guid characterId,
        EquipmentSlotKind slot,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (slot is not (EquipmentSlotKind.Weapon or EquipmentSlotKind.Armor))
            {
                return new EquipmentMutationResult(EquipmentMutationStatus.InvalidSlot);
            }

            var entity = await db.PlayerCharacters
                .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return new EquipmentMutationResult(EquipmentMutationStatus.CharacterNotFound);
            }

            var has = slot == EquipmentSlotKind.Weapon ? entity.EquippedWeaponItemId : entity.EquippedArmorItemId;
            if (has is null)
            {
                return new EquipmentMutationResult(EquipmentMutationStatus.EmptySlot);
            }

            if (slot == EquipmentSlotKind.Weapon)
            {
                entity.EquippedWeaponItemId = null;
            }
            else
            {
                entity.EquippedArmorItemId = null;
            }

            entity.UpdatedAtUtc = _clock.GetUtcNow();
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new EquipmentMutationResult(
                EquipmentMutationStatus.Ok,
                new EquipmentRecord(characterId, entity.EquippedWeaponItemId, entity.EquippedArmorItemId));
        }, cancellationToken);
}
