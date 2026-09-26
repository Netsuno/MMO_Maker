using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Monstre publié, proposé dans la table de rencontres.</summary>
public sealed record MapEncounterTroopChoice(Guid MonsterId, int? AliasId, string Label);

/// <summary>
/// Catalogue PNJ/monstres déjà en place. S’il est absent ou illisible, la liste est vide
/// et l’éditeur laisse saisir un nom ou un identifiant.
/// </summary>
public static class MapEncounterTroopCatalog
{
    public static async Task<IReadOnlyList<MapEncounterTroopChoice>> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bundle = EditorNpcRepositoryFactory.CreateBundle();
            var rows = await bundle.Repository.ListSummariesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return rows
                .Where(row => row.Kind == NpcKind.Monster)
                .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(row => new MapEncounterTroopChoice(row.NpcId, row.EditorAliasId, row.Name))
                .ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<MapEncounterTroopChoice>();
        }
    }
}
