using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>
/// JSON des jalons de route d’un placement (camelCase : tileX, tileY, waitMs).
/// Même forme que le jsonb <c>route_waypoints_json</c>.
/// </summary>
public static class MapEventRouteWaypointCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IReadOnlyList<MapEventRouteWaypoint>? waypoints)
    {
        if (waypoints is null || waypoints.Count == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(waypoints, Json);
    }

    public static bool TryDeserialize(string? json, out IReadOnlyList<MapEventRouteWaypoint> waypoints, out string? error)
    {
        waypoints = Array.Empty<MapEventRouteWaypoint>();
        error = null;
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return true;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<MapEventRouteWaypoint>>(json, Json);
            if (list is null)
            {
                error = "Itinéraire JSON illisible.";
                return false;
            }

            waypoints = list;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
