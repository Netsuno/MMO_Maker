using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;

namespace Frog.Tests;

/// <summary>Decorateur de test pour simuler echec/annulation de persistance personnage.</summary>
internal sealed class BlockableCharacterRepository(ICharacterRepository inner) : ICharacterRepository
{
    private readonly ICharacterRepository _inner = inner;

    public Func<CharacterRecord, bool>? ShouldFailSave { get; set; }

    public Task<IReadOnlyList<CharacterRecord>> ListByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
        => _inner.ListByAccountAsync(accountId, cancellationToken);

    public Task<CharacterRecord?> FindByIdAsync(Guid characterId, CancellationToken cancellationToken = default)
        => _inner.FindByIdAsync(characterId, cancellationToken);

    public Task<bool> IsOwnedByAccountAsync(
        Guid accountId,
        Guid characterId,
        CancellationToken cancellationToken = default)
        => _inner.IsOwnedByAccountAsync(accountId, characterId, cancellationToken);

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
        => _inner.CreateAsync(
            accountId,
            displayName,
            classId,
            stats,
            maxHp,
            maxMp,
            startingSpellId,
            mapId,
            pixelX,
            pixelY,
            cancellationToken);

    public Task SaveAsync(CharacterRecord character, CancellationToken cancellationToken = default)
    {
        if (ShouldFailSave?.Invoke(character) == true)
        {
            throw new IOException("Blocked character save for test.");
        }

        return _inner.SaveAsync(character, cancellationToken);
    }
}
