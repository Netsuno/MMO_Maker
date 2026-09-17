using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Acquisition et progression des métiers (Phase 8).</summary>
public sealed class ProfessionGameplayService(
    IPublishedProfessionCatalog professions,
    ICharacterProfessionRepository progress)
{
    public async Task<(bool Success, string Message)> TryAcquireProfessionAsync(
        Guid characterId,
        Guid professionId,
        CancellationToken cancellationToken = default)
    {
        if (characterId == Guid.Empty || professionId == Guid.Empty)
        {
            return (false, "Paramètres invalides.");
        }

        var profession = await professions.TryGetPublishedByIdAsync(professionId, cancellationToken)
            .ConfigureAwait(false);
        if (profession is null)
        {
            return (false, "Métier inconnu.");
        }

        var existing = await progress.TryGetAsync(characterId, professionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return (true, $"Métier {profession.Name} déjà acquis (niv. {existing.Level}).");
        }

        await progress.UpsertAsync(
                new CharacterProfessionProgress
                {
                    CharacterId = characterId,
                    ProfessionId = professionId,
                    Level = 1,
                    Experience = 0,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return (true, $"Métier {profession.Name} acquis.");
    }
}
