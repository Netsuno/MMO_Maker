using System.Collections.Concurrent;

namespace Frog.Server.Database;

/// <summary>Mémoire : un UUID stable par nom de compte (développement sans MariaDB).</summary>
public sealed class InMemoryCharacterBootstrap : ICharacterBootstrap
{
    private readonly ConcurrentDictionary<string, string> _heroIdByUser = new(StringComparer.OrdinalIgnoreCase);

    public string EnsureDefaultHero(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return _heroIdByUser.GetOrAdd(username, static _ => Guid.NewGuid().ToString());
    }
}
