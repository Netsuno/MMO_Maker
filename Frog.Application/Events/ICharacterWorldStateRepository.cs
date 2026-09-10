namespace Frog.Application.Events;

/// <summary>Interrupteurs et variables perso (Phase 8 — source de vérité serveur).</summary>
public interface ICharacterWorldStateRepository
{
    Task<bool?> GetSwitchAsync(Guid characterId, string switchId, CancellationToken cancellationToken = default);

    Task SetSwitchAsync(Guid characterId, string switchId, bool value, CancellationToken cancellationToken = default);

    /// <summary>Réserve un interrupteur (false→true) ; retourne false si déjà true (grant unique déjà consommé).</summary>
    Task<bool> TryClaimSwitchAsync(
        Guid characterId,
        string switchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, bool>> GetAllSwitchesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);

    Task<int?> GetVariableAsync(Guid characterId, string variableId, CancellationToken cancellationToken = default);

    Task SetVariableAsync(
        Guid characterId,
        string variableId,
        int value,
        CancellationToken cancellationToken = default);

    Task AddVariableAsync(
        Guid characterId,
        string variableId,
        int delta,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetAllVariablesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default);
}
