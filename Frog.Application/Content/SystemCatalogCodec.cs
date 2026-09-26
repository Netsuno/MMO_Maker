using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>JSON camelCase du catalogue Système, même convention que les payloads Phase 8.</summary>
public static class SystemCatalogCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize(SystemCatalogEntry entry) =>
        JsonSerializer.Serialize(entry, JsonOptions);

    public static bool TryRead(string json, out SystemCatalogEntry entry, out string? error)
    {
        entry = new SystemCatalogEntry();
        error = null;
        try
        {
            entry = JsonSerializer.Deserialize<SystemCatalogEntry>(json, JsonOptions) ?? new SystemCatalogEntry();
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
