using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>
/// Le groupe de départ ne référence que des héros publiés.
/// La carte de départ, si elle est renseignée, doit exister dans le dépôt.
/// </summary>
public static class SystemSettingsReferenceChecks
{
    public static async Task<string?> ValidateAsync(
        SystemDefinition definition,
        IPublishedActorCatalog? actors,
        IMapRepository? maps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var slots = definition.PartySlots();
        var filled = new List<Guid>();
        foreach (var slot in slots)
        {
            if (slot is Guid actorId)
            {
                filled.Add(actorId);
            }
        }

        if (filled.Count > 0)
        {
            if (actors is null)
            {
                return "Le catalogue de héros est indisponible.";
            }

            var published = await actors.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
            var ids = published.Select(actor => actor.Id).ToHashSet();
            for (var index = 0; index < slots.Count; index++)
            {
                if (slots[index] is Guid actorId && !ids.Contains(actorId))
                {
                    return $"Le héros {index + 1} du groupe de départ doit être publié.";
                }
            }
        }

        if (definition.StartMapId is Guid mapId)
        {
            if (maps is null)
            {
                return "Le dépôt de cartes est indisponible.";
            }

            var map = await maps.LoadByIdAsync(mapId, cancellationToken).ConfigureAwait(false);
            if (map is null)
            {
                return "La carte de départ est introuvable.";
            }
        }

        return null;
    }
}
