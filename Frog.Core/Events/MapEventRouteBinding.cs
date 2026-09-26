using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Core.Events;

/// <summary>
/// La trajectoire éditée sur la page est appliquée au fil lorsque le placement n’a pas déjà un trajet.
/// Un placement « route » (y compris après un décalage de carte) reste tel quel.
/// Les champs restent dans le JSON d’événement déjà publié : pas d’opcode, Hello inchangé.
/// </summary>
public static class MapEventRouteBinding
{
    public static bool Repeats(bool? routeRepeat) => routeRepeat != false;

    public static void ApplyPageRoute(MapEventWireEntry entry, MapEventPageDefinition? page)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.Equals(entry.MovementKind, MapEventMovementKinds.Route, StringComparison.Ordinal)
            && entry.RouteWaypoints is { Count: >= 2 })
        {
            return;
        }

        if (page is null
            || !string.Equals(page.MovementKind, MapEventMovementKinds.Route, StringComparison.Ordinal)
            || page.RouteWaypoints.Count < 2)
        {
            return;
        }

        entry.MovementKind = MapEventMovementKinds.Route;
        entry.RouteWaypoints = page.RouteWaypoints;
        entry.RouteRepeat = page.RouteRepeat;
        entry.RouteSkipIfBlocked = page.RouteSkipIfBlocked;
    }
}
