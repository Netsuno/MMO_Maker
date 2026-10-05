using System.Text.Json;
using System.Text.Json.Serialization;

using Frog.Core.Models;

namespace Frog.Application.Content;

/// <summary>Ordre des tuiles choisies, sans les pixels (ceux-ci vivent dans <c>content.tiles</c>).</summary>
public static class ComposedTilesetMemberCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(IReadOnlyList<ComposedTileRef> tiles)
    {
        var rows = tiles.Select(tile => new Row
        {
            Id = tile.TileAssetId.Trim().ToLowerInvariant(),
            Name = string.IsNullOrWhiteSpace(tile.DisplayName) ? null : tile.DisplayName.Trim(),
        }).ToArray();
        return JsonSerializer.Serialize(rows, Options);
    }

    public static IReadOnlyList<Row> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Row>();
        }

        var rows = JsonSerializer.Deserialize<Row[]>(json, Options) ?? Array.Empty<Row>();
        return rows;
    }

    public sealed class Row
    {
        public string Id { get; set; } = string.Empty;

        public string? Name { get; set; }
    }
}
