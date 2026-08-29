namespace Frog.Application.Events;

/// <summary>Interrupteurs et variables perso (Phase 8 — source de vérité serveur).</summary>
public interface ICharacterWorldStateRepository
{
    Task<bool?> GetSwitchAsync(Guid characterId, string switchId, CancellationToken cancellationToken = default);

    Task SetSwitchAsync(Guid characterId, string switchId, bool value, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, bool>> GetAllSwitchesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);
}
