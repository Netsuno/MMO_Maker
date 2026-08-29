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
}
