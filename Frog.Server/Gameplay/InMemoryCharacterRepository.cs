using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Database;

namespace Frog.Server.Gameplay;

/// <summary>Dépôt personnages en mémoire (playtest / sans PostgreSQL).</summary>
public sealed class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly ConcurrentDictionary<Guid, CharacterRecord> _byId = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _byAccount = new();
    private readonly object _gate = new();

    public Task<IReadOnlyList<CharacterRecord>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_byAccount.TryGetValue(accountId, out var ids))
            {
                return Task.FromResult<IReadOnlyList<CharacterRecord>>(Array.Empty<CharacterRecord>());
            }

            var list = new List<CharacterRecord>(ids.Count);
            foreach (var id in ids)
            {
                if (_byId.TryGetValue(id, out var record))
                {
                    list.Add(record);
                }
            }

            return Task.FromResult<IReadOnlyList<CharacterRecord>>(list);
        }
    }

    public Task<CharacterRecord?> FindByIdAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(characterId, out var record);
        return Task.FromResult(record);
    }

    public Task<bool> IsOwnedByAccountAsync(
        Guid accountId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _byId.TryGetValue(characterId, out var record) && record.AccountId == accountId);
    }

    public Task<CharacterCreateResult> CreateAsync(
        Guid accountId,
        string displayName,
        Guid classId,
        CharacterStats stats,
        int maxHp,
        int maxMp,
        Guid? startingSpellId,
        int mapId,
        int pixelX,
        int pixelY,
        CancellationToken cancellationToken = default)
    {
        if (!CharacterDisplayNameRules.TryNormalize(displayName, out var name, out var err))
        {
            return Task.FromResult(new CharacterCreateResult(CharacterCreateStatus.InvalidName, ErrorMessage: err));
        }

        if (classId == Guid.Empty)
        {
            return Task.FromResult(new CharacterCreateResult(
                CharacterCreateStatus.InvalidClass,
                ErrorMessage: "Classe invalide."));
        }

        lock (_gate)
        {
            var ids = _byAccount.GetOrAdd(accountId, static _ => new List<Guid>());
            if (ids.Count >= GameplayLimits.MaxCharactersPerAccount)
            {
                return Task.FromResult(new CharacterCreateResult(
                    CharacterCreateStatus.SlotLimitReached,
                    ErrorMessage: "Nombre max. de persos atteint (8)."));
            }

            foreach (var id in ids)
            {
                if (_byId.TryGetValue(id, out var existing)
                    && string.Equals(existing.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new CharacterCreateResult(
                        CharacterCreateStatus.DuplicateName,
                        ErrorMessage: "Ce nom de perso est deja utilise."));
                }
            }

            var now = DateTimeOffset.UtcNow;
            var record = new CharacterRecord(
                Guid.NewGuid(),
                accountId,
                name,
                classId,
                mapId,
                pixelX,
                pixelY,
                ProgressionCurve.MinLevel,
                0,
                maxHp,
                maxHp,
                maxMp,
                maxMp,
                0,
                false,
                stats,
                startingSpellId,
                null,
                null,
                now,
                now);
            _byId[record.Id] = record;
            ids.Add(record.Id);
            return Task.FromResult(new CharacterCreateResult(CharacterCreateStatus.Created, record));
        }
    }

    public Task SaveAsync(CharacterRecord character, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        lock (_gate)
        {
            _byId[character.Id] = character with { UpdatedAtUtc = DateTimeOffset.UtcNow };
            var ids = _byAccount.GetOrAdd(character.AccountId, static _ => new List<Guid>());
            if (!ids.Contains(character.Id))
            {
                ids.Add(character.Id);
            }
        }

        return Task.CompletedTask;
    }
}
