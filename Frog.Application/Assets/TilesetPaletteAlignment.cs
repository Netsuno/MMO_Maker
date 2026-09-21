using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>
/// Aligne <see cref="TilesetDefinition.Id"/> (Guid) ↔ <see cref="Frog.Core.Models.Tile.TilesetId"/> (int).
/// <see cref="TilesetDefinition.EditorPaletteId"/> reste le pont ; s’il manque, on le déduit
/// des ids utilisés sur la carte (SHA, puis correspondance 1-1).
/// </summary>
public static class TilesetPaletteAlignment
{
    public static int? ResolveClientTilesetId(
        TilesetDefinition definition,
        IReadOnlyCollection<int> mapUsedIds,
        IReadOnlyDictionary<string, int>? sha256ToPalette = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.EditorPaletteId is int pal && pal > 0)
        {
            return pal;
        }

        if (sha256ToPalette is not null
            && !string.IsNullOrWhiteSpace(definition.Sha256Hex)
            && sha256ToPalette.TryGetValue(definition.Sha256Hex.ToUpperInvariant(), out var bySha)
            && bySha > 0)
        {
            return bySha;
        }

        if (mapUsedIds.Count == 1 && definition.PngBytes is { Length: > 0 })
        {
            return mapUsedIds.First();
        }

        return null;
    }

    public static IReadOnlyDictionary<string, int> BuildShaToPalette(IEnumerable<MapTilesetFile> files)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(f => f.Id > 0 && f.PngBytes.Length > 0))
        {
            map[TilesetDefinition.ComputeSha256Hex(file.PngBytes).ToUpperInvariant()] = file.Id;
        }

        return map;
    }
}
