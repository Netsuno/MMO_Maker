using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frog.Core.Events;

/// <summary>Codec JSON validé pour pages/conditions/commandes d'événement.</summary>
public static class MapEventPagesCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string SerializePages(IReadOnlyList<Models.MapEventPageDefinition> pages) =>
        JsonSerializer.Serialize(pages, Json);

    public static bool TryDeserializePages(string? json, out IReadOnlyList<Models.MapEventPageDefinition> pages, out string? error)
    {
        pages = Array.Empty<Models.MapEventPageDefinition>();
        error = null;
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return true;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<Models.MapEventPageDefinition>>(json, Json);
            if (list is null)
            {
                error = "Pages JSON invalides.";
                return false;
            }

            if (list.Count > Models.MapEventRuntimeLimits.MaxPagesPerEvent)
            {
                error = $"Trop de pages (max {Models.MapEventRuntimeLimits.MaxPagesPerEvent}).";
                return false;
            }

            pages = list;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
