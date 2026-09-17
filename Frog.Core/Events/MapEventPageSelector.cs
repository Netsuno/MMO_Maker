using System.Linq;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Sélection de page événement selon trigger et conditions (P8-G1).</summary>
public static class MapEventPageSelector
{
    public static async Task<MapEventPageDefinition?> SelectBestPageAsync(
        IReadOnlyList<MapEventPageDefinition> pages,
        string? placementTrigger,
        Func<MapEventConditionDefinition, Task<bool>> evaluateConditionAsync)
    {
        MapEventPageDefinition? best = null;
        var bestPriority = int.MinValue;

        foreach (var page in pages.OrderBy(p => p.PageOrder))
        {
            if (placementTrigger is not null
                && !string.Equals(page.TriggerKind, placementTrigger, StringComparison.Ordinal))
            {
                continue;
            }

            var pass = true;
            foreach (var condition in page.Conditions)
            {
                if (!await evaluateConditionAsync(condition).ConfigureAwait(false))
                {
                    pass = false;
                    break;
                }
            }

            if (!pass)
            {
                continue;
            }

            if (page.Priority > bestPriority)
            {
                bestPriority = page.Priority;
                best = page;
            }
        }

        return best;
    }
}
