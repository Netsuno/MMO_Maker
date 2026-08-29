using Frog.Core.Events;

namespace Frog.Core.Models;

/// <summary>Définition catalogue d'un événement carte (pages, conditions, commandes typées).</summary>
public sealed class MapEventDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CatalogSlug { get; set; }
    public int? EditorAliasId { get; set; }
    public IReadOnlyList<MapEventPageDefinition> Pages { get; set; } = Array.Empty<MapEventPageDefinition>();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Le nom de l'événement est requis.";
            return false;
        }

        if (Name.Length > 128)
        {
            error = "Le nom de l'événement ne doit pas dépasser 128 caractères.";
            return false;
        }

        if (CatalogSlug is { Length: > 64 })
        {
            error = "Le slug catalogue ne doit pas dépasser 64 caractères.";
            return false;
        }

        var orders = new HashSet<int>();
        for (var i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            if (!page.Validate(out error))
            {
                error = $"Page {i}: {error}";
                return false;
            }

            if (!orders.Add(page.PageOrder))
            {
                error = $"Ordre de page dupliqué: {page.PageOrder}.";
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class MapEventPageDefinition
{
    public int PageOrder { get; set; }
    public int Priority { get; set; }
    public string TriggerKind { get; set; } = Phase8MapEventTriggerKinds.Action;
    public string MovementKind { get; set; } = MapEventMovementKinds.Fixed;
    public IReadOnlyList<MapEventRouteWaypoint> RouteWaypoints { get; set; } = Array.Empty<MapEventRouteWaypoint>();
    public byte AppearanceGraphicId { get; set; }
    public byte AppearanceDirection { get; set; }
    public bool BlocksCollision { get; set; } = true;
    public IReadOnlyList<MapEventConditionDefinition> Conditions { get; set; } = Array.Empty<MapEventConditionDefinition>();
    public IReadOnlyList<MapEventCommandDefinition> Commands { get; set; } = Array.Empty<MapEventCommandDefinition>();

    public bool Validate(out string? error)
    {
        if (PageOrder < 0)
        {
            error = "PageOrder doit être >= 0.";
            return false;
        }

        if (!Phase8MapEventTriggerKinds.IsSupported(TriggerKind))
        {
            error = $"TriggerKind invalide: {TriggerKind}.";
            return false;
        }

        if (!MapEventMovementKinds.IsSupported(MovementKind))
        {
            error = $"MovementKind invalide: {MovementKind}.";
            return false;
        }

        if (MovementKind == MapEventMovementKinds.Route)
        {
            if (RouteWaypoints.Count is < 2 or > MapEventRuntimeLimits.MaxRouteWaypoints)
            {
                error = $"Route: entre 2 et {MapEventRuntimeLimits.MaxRouteWaypoints} waypoints.";
                return false;
            }
        }

        if (Conditions.Count > MapEventRuntimeLimits.MaxConditionsPerPage)
        {
            error = $"Trop de conditions (max {MapEventRuntimeLimits.MaxConditionsPerPage}).";
            return false;
        }

        if (Commands.Count > MapEventRuntimeLimits.MaxCommandsPerPage)
        {
            error = $"Trop de commandes (max {MapEventRuntimeLimits.MaxCommandsPerPage}).";
            return false;
        }

        foreach (var c in Conditions)
        {
            if (!c.Validate(out error))
            {
                return false;
            }
        }

        foreach (var cmd in Commands)
        {
            if (!cmd.Validate(out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class MapEventRouteWaypoint
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int WaitMs { get; set; }
}

public sealed class MapEventPlacementDefinition
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public Guid EventDefinitionId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string TriggerKind { get; set; } = Phase8MapEventTriggerKinds.Action;
    public string MovementKind { get; set; } = MapEventMovementKinds.Fixed;
    public IReadOnlyList<MapEventRouteWaypoint> RouteWaypoints { get; set; } = Array.Empty<MapEventRouteWaypoint>();

    public bool Validate(out string? error)
    {
        if (MapId == Guid.Empty || EventDefinitionId == Guid.Empty)
        {
            error = "MapId et EventDefinitionId sont requis.";
            return false;
        }

        if (TileX < 0 || TileY < 0)
        {
            error = "Coordonnées tuile invalides.";
            return false;
        }

        if (!Phase8MapEventTriggerKinds.IsSupported(TriggerKind))
        {
            error = $"TriggerKind invalide: {TriggerKind}.";
            return false;
        }

        error = null;
        return true;
    }
}
