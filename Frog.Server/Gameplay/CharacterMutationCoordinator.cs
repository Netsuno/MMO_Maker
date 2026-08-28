using System.Collections.Concurrent;

namespace Frog.Server.Gameplay;

/// <summary>Serialise les mutations async par personnage (validation + etat session + persistance).</summary>
public sealed class CharacterMutationCoordinator
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public async Task<T> RunExclusiveAsync<T>(
        Guid characterId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(characterId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
