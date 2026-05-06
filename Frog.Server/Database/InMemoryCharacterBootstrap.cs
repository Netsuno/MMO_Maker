using System.Collections.Concurrent;
using Frog.Core.Models;

namespace Frog.Server.Database;

/// <summary>Mémoire : liste de persos par compte (au minimum un « Hero » par <see cref="EnsureDefaultHero"/>).</summary>
public sealed class InMemoryCharacterBootstrap : ICharacterBootstrap
{
    private readonly ConcurrentDictionary<string, object> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<(string Id, string Name)>> _chars =
        new(StringComparer.OrdinalIgnoreCase);

    private object Gate(string username) => _gates.GetOrAdd(username, static _ => new object());

    public string EnsureDefaultHero(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        lock (Gate(username))
        {
            return EnsureDefaultHeroCore(username);
        }
    }

    private string EnsureDefaultHeroCore(string username)
    {
        if (!_chars.TryGetValue(username, out var list))
        {
            list = new List<(string Id, string Name)>();
            _chars[username] = list;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Name, "Hero", StringComparison.OrdinalIgnoreCase))
            {
                return list[i].Id;
            }
        }

        var id = Guid.NewGuid().ToString();
        list.Insert(0, (id, "Hero"));
        return id;
    }

    public IReadOnlyList<CharacterSlotInfo> ListCharacters(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        lock (Gate(username))
        {
            EnsureDefaultHeroCore(username);
            var list = _chars[username];
            var copy = new CharacterSlotInfo[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                copy[i] = new CharacterSlotInfo(list[i].Id, list[i].Name);
            }

            return copy;
        }
    }

    public bool IsCharacterOwned(string username, string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        lock (Gate(username))
        {
            if (!_chars.TryGetValue(username, out var list))
            {
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Id, characterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool TryCreateCharacter(string username, string displayName, out string characterId, out string errorMessage)
    {
        characterId = string.Empty;
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (!CharacterDisplayNameRules.TryNormalize(displayName, out var name, out errorMessage))
        {
            return false;
        }

        lock (Gate(username))
        {
            EnsureDefaultHeroCore(username);
            var list = _chars[username];
            if (list.Count >= 8)
            {
                errorMessage = "Nombre max. de persos atteint (8).";
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Ce nom de perso est deja utilise.";
                    return false;
                }
            }

            var id = Guid.NewGuid().ToString();
            list.Add((id, name));
            characterId = id;
            return true;
        }
    }
}
