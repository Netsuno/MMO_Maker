using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>Case d’aperçu : id qu’un raccord poserait, sans modifier la carte.</summary>
public readonly record struct AutotilePreviewCell(int X, int Y, TileAssetId Id);

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

    /// <summary>
    /// Ids visibles si <paramref name="brushId"/> était posé sur <paramref name="stamp"/>,
    /// sans modifier la carte. Le tampon est toujours inclus. Un voisin n’est inclus que si son rôle change.
    /// </summary>
    public static IReadOnlyList<AutotilePreviewCell> PreviewStamp(
        Map map,
        int layerIndex,
        TileAssetId brushId,
        IReadOnlyList<(int X, int Y)> stamp)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(stamp);
        if (brushId.IsNone || (uint)layerIndex >= (uint)map.Layers.Count)
        {
            return Array.Empty<AutotilePreviewCell>();
        }

        var stampCells = new List<(int X, int Y)>();
        var stampSet = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in stamp)
        {
            if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height || !stampSet.Add((x, y)))
            {
                continue;
            }

            stampCells.Add((x, y));
        }

        if (stampCells.Count == 0)
        {
            return Array.Empty<AutotilePreviewCell>();
        }

        if (map.TileFlags is not { Count: > 0 } table)
        {
            var plain = new AutotilePreviewCell[stampCells.Count];
            for (var i = 0; i < stampCells.Count; i++)
            {
                plain[i] = new AutotilePreviewCell(stampCells[i].X, stampCells[i].Y, brushId);
            }

            return plain;
        }

        var ids = new Dictionary<(int X, int Y), TileAssetId>();
        foreach (var tile in map.Layers[layerIndex].Tiles)
        {
            if (!tile.AssetId.IsNone)
            {
                ids[(tile.X, tile.Y)] = tile.AssetId;
            }
        }

        var original = new Dictionary<(int X, int Y), TileAssetId>(ids);
        foreach (var cell in stampCells)
        {
            ids[cell] = brushId;
        }

        var affected = new List<(int X, int Y)>();
        var seen = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in stampCells)
        {
            Consider(x, y);
            Consider(x, y - 1);
            Consider(x + 1, y);
            Consider(x, y + 1);
            Consider(x - 1, y);
        }

        var result = new List<AutotilePreviewCell>();
        foreach (var (x, y) in affected)
        {
            var resolved = Resolve(table, ids, x, y);
            var inStamp = stampSet.Contains((x, y));
            var changed = !original.TryGetValue((x, y), out var before) || before != resolved;
            if (inStamp || changed)
            {
                result.Add(new AutotilePreviewCell(x, y, resolved));
            }
        }

        return result;

        void Consider(int x, int y)
        {
            if (!seen.Add((x, y)) || !ids.ContainsKey((x, y)))
            {
                return;
            }

            affected.Add((x, y));
        }
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
        if (tile.AssetId.IsNone || map.TileFlags is not { } table || !table.TryGetExplicit(tile.AssetId, out var flags))
        {
            return false;
        }

        if (flags.AutotileRole == AutotileRole.None || string.IsNullOrEmpty(flags.AutotileGroup))
        {
            return false;
        }

        var mask = NeighborMask(
            table,
            (nx, ny) => index.TryGetValue((nx, ny), out var neighbor) ? neighbor.AssetId : TileAssetId.None,
            tile.X,
            tile.Y,
            flags.AutotileGroup);
        return TryPick(table, flags.AutotileGroup, mask, out id);
    }

    private static TileAssetId Resolve(
        TileAssetFlagTable table,
        IReadOnlyDictionary<(int X, int Y), TileAssetId> ids,
        int x,
        int y)
    {
        var current = ids[(x, y)];
        if (!table.TryGetExplicit(current, out var flags)
            || flags.AutotileRole == AutotileRole.None
            || string.IsNullOrEmpty(flags.AutotileGroup))
        {
            return current;
        }

        var mask = NeighborMask(
            table,
            (nx, ny) => ids.TryGetValue((nx, ny), out var id) ? id : TileAssetId.None,
            x,
            y,
            flags.AutotileGroup);
        return TryPick(table, flags.AutotileGroup, mask, out var picked) ? picked : current;
    }

    private static int NeighborMask(
        TileAssetFlagTable table,
        Func<int, int, TileAssetId> idAt,
        int x,
        int y,
        string group)
    {
        var mask = 0;
        if (ConnectedId(table, idAt(x, y - 1), group))
        {
            mask |= North;
        }

        if (ConnectedId(table, idAt(x + 1, y), group))
        {
            mask |= East;
        }

        if (ConnectedId(table, idAt(x, y + 1), group))
        {
            mask |= South;
        }

        if (ConnectedId(table, idAt(x - 1, y), group))
        {
            mask |= West;
        }

        return mask;
    }

    private static bool ConnectedId(TileAssetFlagTable table, TileAssetId id, string group)
    {
        if (id.IsNone || !table.TryGetExplicit(id, out var flags))
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
