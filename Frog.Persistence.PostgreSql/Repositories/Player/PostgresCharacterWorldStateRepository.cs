using Frog.Application.Events;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresCharacterWorldStateRepository(FrogDbContextGate gate) : ICharacterWorldStateRepository
{
    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public Task<bool?> GetSwitchAsync(Guid characterId, string switchId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(switchId))
            {
                return (bool?)null;
            }

            var row = await db.PlayerCharacterWorldSwitches.AsNoTracking()
                .Where(s => s.CharacterId == characterId && s.SwitchKey == switchId)
                .Select(s => (bool?)s.Value)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            return row;
        }, cancellationToken);

    public Task SetSwitchAsync(
        Guid characterId,
        string switchId,
        bool value,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(switchId))
            {
                return;
            }

            var existing = await db.PlayerCharacterWorldSwitches
                .FirstOrDefaultAsync(s => s.CharacterId == characterId && s.SwitchKey == switchId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.PlayerCharacterWorldSwitches.Add(new Entities.Player.CharacterWorldSwitchEntity
                {
                    CharacterId = characterId,
                    SwitchKey = switchId,
                    Value = value,
                });
            }
            else
            {
                existing.Value = value;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken);

    public Task<IReadOnlyDictionary<string, bool>> GetAllSwitchesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyDictionary<string, bool>>(async (db, ct) =>
        {
            if (characterId == Guid.Empty)
            {
                return new Dictionary<string, bool>();
            }

            var rows = await db.PlayerCharacterWorldSwitches.AsNoTracking()
                .Where(s => s.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.SwitchKey, r => r.Value, StringComparer.Ordinal);
        }, cancellationToken);

    public Task<int?> GetVariableAsync(Guid characterId, string variableId, CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
            {
                return (int?)null;
            }

            var row = await db.PlayerCharacterWorldVariables.AsNoTracking()
                .Where(v => v.CharacterId == characterId && v.VariableKey == variableId)
                .Select(v => (int?)v.Value)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            return row;
        }, cancellationToken);

    public Task SetVariableAsync(
        Guid characterId,
        string variableId,
        int value,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
            {
                return;
            }

            var existing = await db.PlayerCharacterWorldVariables
                .FirstOrDefaultAsync(v => v.CharacterId == characterId && v.VariableKey == variableId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.PlayerCharacterWorldVariables.Add(new Entities.Player.CharacterWorldVariableEntity
                {
                    CharacterId = characterId,
                    VariableKey = variableId,
                    Value = value,
                });
            }
            else
            {
                existing.Value = value;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken);

    public Task AddVariableAsync(
        Guid characterId,
        string variableId,
        int delta,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || string.IsNullOrWhiteSpace(variableId))
            {
                return;
            }

            var existing = await db.PlayerCharacterWorldVariables
                .FirstOrDefaultAsync(v => v.CharacterId == characterId && v.VariableKey == variableId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.PlayerCharacterWorldVariables.Add(new Entities.Player.CharacterWorldVariableEntity
                {
                    CharacterId = characterId,
                    VariableKey = variableId,
                    Value = delta,
                });
            }
            else
            {
                existing.Value = checked(existing.Value + delta);
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken);

    public Task<IReadOnlyDictionary<string, int>> GetAllVariablesAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync<IReadOnlyDictionary<string, int>>(async (db, ct) =>
        {
            if (characterId == Guid.Empty)
            {
                return new Dictionary<string, int>();
            }

            var rows = await db.PlayerCharacterWorldVariables.AsNoTracking()
                .Where(v => v.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows.ToDictionary(r => r.VariableKey, r => r.Value, StringComparer.Ordinal);
        }, cancellationToken);
}
