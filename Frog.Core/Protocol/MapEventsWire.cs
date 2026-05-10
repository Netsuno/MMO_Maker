using System.Text.Json.Serialization;

namespace Frog.Core.Protocol;

/// <summary>Élément JSON pour <see cref="Frog.Core.Enums.PacketId.MapEventsResult"/>.</summary>
public sealed class MapEventWireEntry
{
    [JsonPropertyName("placementId")]
    public long PlacementId { get; set; }

    [JsonPropertyName("catalogId")]
    public int CatalogId { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("tileX")]
    public int TileX { get; set; }

    [JsonPropertyName("tileY")]
    public int TileY { get; set; }

    /// <summary><see cref="MapEventTriggerKinds"/> ; défaut <c>interact</c> si absent du JSON.</summary>
    [JsonPropertyName("triggerKind")]
    public string TriggerKind { get; set; } = MapEventTriggerKinds.Interact;

    /// <summary>Clé catalogue optionnelle (runtime scripts créateur — non exécutée dans le MVP actuel).</summary>
    [JsonPropertyName("scriptKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScriptKey { get; set; }
}
