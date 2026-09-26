using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>
/// Raccord d’un groupe d’autotile sur une couche de carte v6.
/// Le masque regarde les quatre voisins du même groupe. Le bord de carte ne raccorde pas.
/// Si le rôle exact n’est pas auteur, on retombe sur le centre, puis sur l’isolée.
/// Le blob <c>.fmap</c> ne stocke que le <see cref="TileAssetId"/> résolu.
/// </summary>
public static class AutotileJoin
{
    public const int North = 1;
    public const int East = 2;
    public const int South = 4;
    public const int West = 8;

    public static AutotileRole PreferredRole(int mask) => mask switch
    {
        0 => AutotileRole.Isolated,
        15 => AutotileRole.Center,
        14 => AutotileRole.North,
        13 => AutotileRole.East,
        11 => AutotileRole.South,
        7 => AutotileRole.West,
        12 => AutotileRole.NorthEast,
        6 => AutotileRole.NorthWest,
        9 => AutotileRole.SouthEast,
        3 => AutotileRole.SouthWest,
        10 => AutotileRole.Horizontal,
        5 => AutotileRole.Vertical,
        1 => AutotileRole.South,
        2 => AutotileRole.West,
        4 => AutotileRole.North,
        8 => AutotileRole.East,
        _ => AutotileRole.None,
    };

    public static bool TryPick(TileAssetFlagTable table, string group, int mask, out TileAssetId id)
    {
        ArgumentNullException.ThrowIfNull(table);
        var preferred = PreferredRole(mask);
        if (preferred != AutotileRole.None && TryFind(table, group, preferred, out id))
        {
            return true;
        }

        if (preferred != AutotileRole.Center && TryFind(table, group, AutotileRole.Center, out id))
        {
            return true;
        }

        if (preferred != AutotileRole.Isolated && TryFind(table, group, AutotileRole.Isolated, out id))
        {
            return true;
        }

        id = TileAssetId.None;
        return false;
    }

    /// <summary>Recalcule la case peinte et ses quatre voisins. Retourne le nombre d’ids changés.</summary>
    public static int ReconcileNeighborhood(Map map, int layerIndex, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.TileFlags is not { Count: > 0 } || (uint)layerIndex >= (uint)map.Layers.Count)
        {
            return 0;
        }

        var index = Index(map.Layers[layerIndex]);
        return Apply(map, index, new[]
        {
            (x, y),
            (x, y - 1),
            (x + 1, y),
            (x, y + 1),
            (x - 1, y),
        });
    }

    /// <summary>Recalcule toute la couche (pot, ligne, gomme).</summary>
    public static int ReconcileLayer(Map map, int layerIndex)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.TileFlags is not { Count: > 0 } || (uint)layerIndex >= (uint)map.Layers.Count)
        {
            return 0;
        }

        var layer = map.Layers[layerIndex];
        var index = Index(layer);
        var cells = new List<(int X, int Y)>(layer.Tiles.Count);
        foreach (var tile in layer.Tiles)
        {
            cells.Add((tile.X, tile.Y));
        }

        return Apply(map, index, cells);
    }

    private static int Apply(Map map, Dictionary<(int X, int Y), Tile> index, IReadOnlyList<(int X, int Y)> cells)
    {
        var pending = new List<(Tile Tile, TileAssetId Id)>();
        var seen = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in cells)
        {
            if (!seen.Add((x, y)) || !index.TryGetValue((x, y), out var tile))
            {
                continue;
            }

            if (TryReplacement(map, index, tile, out var id) && id != tile.AssetId)
            {
                pending.Add((tile, id));
            }
        }

        foreach (var (tile, id) in pending)
        {
            tile.AssetId = id;
        }

        return pending.Count;
    }

    private static bool TryReplacement(
        Map map,
        Dictionary<(int X, int Y), Tile> index,
        Tile tile,
        out TileAssetId id)
    {
        id = TileAssetId.None;
        if (tile.AssetId.IsNone || map.TileFlags is null || !map.TileFlags.TryGetExplicit(tile.AssetId, out var flags))
        {
            return false;
        }

        if (flags.AutotileRole == AutotileRole.None || string.IsNullOrEmpty(flags.AutotileGroup))
        {
            return false;
        }

        var mask = 0;
        if (Connected(map.TileFlags, index, tile.X, tile.Y - 1, flags.AutotileGroup))
        {
            mask |= North;
        }

        if (Connected(map.TileFlags, index, tile.X + 1, tile.Y, flags.AutotileGroup))
        {
            mask |= East;
        }

        if (Connected(map.TileFlags, index, tile.X, tile.Y + 1, flags.AutotileGroup))
        {
            mask |= South;
        }

        if (Connected(map.TileFlags, index, tile.X - 1, tile.Y, flags.AutotileGroup))
        {
            mask |= West;
        }

        return TryPick(map.TileFlags, flags.AutotileGroup, mask, out id);
    }

    private static bool Connected(
        TileAssetFlagTable table,
        Dictionary<(int X, int Y), Tile> index,
        int x,
        int y,
        string group)
    {
        if (!index.TryGetValue((x, y), out var tile) || tile.AssetId.IsNone)
        {
            return false;
        }

        if (!table.TryGetExplicit(tile.AssetId, out var flags))
        {
            return false;
        }

        return flags.AutotileRole != AutotileRole.None
               && string.Equals(flags.AutotileGroup, group, StringComparison.Ordinal);
    }

    private static bool TryFind(TileAssetFlagTable table, string group, AutotileRole role, out TileAssetId id)
    {
        foreach (var candidate in table.Ids.OrderBy(value => value.ToHex(), StringComparer.Ordinal))
        {
            var flags = table.Get(candidate);
            if (flags.AutotileRole == role
                && string.Equals(flags.AutotileGroup, group, StringComparison.Ordinal))
            {
                id = candidate;
                return true;
            }
        }

        id = TileAssetId.None;
        return false;
    }

    private static Dictionary<(int X, int Y), Tile> Index(Layer layer)
    {
        var index = new Dictionary<(int X, int Y), Tile>();
        foreach (var tile in layer.Tiles)
        {
            index[(tile.X, tile.Y)] = tile;
        }

        return index;
    }
}
