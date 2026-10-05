using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>
/// Palette de placement : les tilesets locaux restent, les lignes publiées s’ajoutent.
/// Un brouillon ne doit pas être passé : l’appelant ne fournit que le catalogue publié.
/// </summary>
public static class ComposedTilesetPlacement
{
    public static IReadOnlyList<WorkingTileset> Merge(
        IReadOnlyList<WorkingTileset>? local,
        IEnumerable<ComposedTilesetDefinition>? published)
    {
        var result = new List<WorkingTileset>();
        if (local is not null)
        {
            foreach (var set in local)
            {
                if (set is null || set.ServerTilesetId != Guid.Empty)
                {
                    continue;
                }

                if (!set.Validate(out _))
                {
                    continue;
                }

                var copy = new WorkingTileset
                {
                    Name = set.Name,
                    ServerTilesetId = Guid.Empty,
                };
                copy.Tiles.AddRange(set.Tiles);
                result.Add(copy);
            }
        }

        if (published is null)
        {
            return result;
        }

        foreach (var definition in published)
        {
            if (definition is null || !definition.Validate(out _))
            {
                continue;
            }

            var working = new WorkingTileset
            {
                Name = definition.Name.Trim(),
                ServerTilesetId = definition.Id,
            };
            foreach (var tile in definition.Tiles)
            {
                working.Tiles.Add(TileAssetId.Parse(tile.TileAssetId));
            }

            if (!working.Validate(out _))
            {
                continue;
            }

            result.Add(working);
        }

        return result;
    }
}
