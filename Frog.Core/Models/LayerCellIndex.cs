#nullable enable
namespace Frog.Core.Models;

/// <summary>
/// Index (x, y) → tuile. Le pinceau et le dessin du viewport ne parcourent plus toute la couche.
/// La liste <see cref="Layer.Tiles"/> reste la séquence sérialisée.
/// </summary>
public sealed partial class Layer
{
    private Dictionary<long, int>? _cells;
    private int _indexedCount = -1;
    private Tile? _indexedHead;

    /// <summary>Change quand une case est remplacée, retirée, ou que l’index est invalidé.</summary>
    public int CellEditEpoch { get; private set; }

    /// <summary>Oublie l’index (déplacement de tuile, réécriture de la liste).</summary>
    public void InvalidateCellIndex()
    {
        _cells = null;
        _indexedCount = -1;
        _indexedHead = null;
        CellEditEpoch++;
    }

    /// <summary>La tuile a changé d’aspect sans quitter sa case (raccord d’autotile).</summary>
    internal void BumpCellVisualEpoch() => CellEditEpoch++;

    public Tile? TileAt(int x, int y)
    {
        EnsureCells();
        if (!_cells!.TryGetValue(Pack(x, y), out var index) || (uint)index >= (uint)Tiles.Count)
        {
            return null;
        }

        var tile = Tiles[index];
        if (tile is not null && tile.X == x && tile.Y == y)
        {
            return tile;
        }

        RebuildCells();
        CellEditEpoch++;
        if (_cells.TryGetValue(Pack(x, y), out index) && (uint)index < (uint)Tiles.Count)
        {
            return Tiles[index];
        }

        return null;
    }

    /// <summary>Pose <paramref name="tile"/> en (x, y). Une seule tuile par case. Les doublons sont retirés.</summary>
    public void ReplaceTileAt(int x, int y, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        tile.X = x;
        tile.Y = y;
        EnsureCells();
        if (_cells!.Count != Tiles.Count)
        {
            Tiles.RemoveAll(t => t is not null && t.X == x && t.Y == y);
            Tiles.Add(tile);
            RebuildCells();
            CellEditEpoch++;
            return;
        }

        var key = Pack(x, y);
        if (_cells.TryGetValue(key, out var index))
        {
            Tiles[index] = tile;
        }
        else
        {
            _cells[key] = Tiles.Count;
            Tiles.Add(tile);
        }

        SyncIndexMeta();
        CellEditEpoch++;
    }

    /// <summary>Retire la tuile en (x, y). Faux si la case est vide.</summary>
    public bool RemoveTileAt(int x, int y)
    {
        EnsureCells();
        if (_cells!.Count != Tiles.Count)
        {
            var removed = Tiles.RemoveAll(t => t is not null && t.X == x && t.Y == y) > 0;
            RebuildCells();
            if (removed)
            {
                CellEditEpoch++;
            }

            return removed;
        }

        if (!_cells.Remove(Pack(x, y), out var index))
        {
            return false;
        }

        var last = Tiles.Count - 1;
        if (index != last)
        {
            var moved = Tiles[last];
            Tiles[index] = moved;
            if (moved is not null)
            {
                _cells[Pack(moved.X, moved.Y)] = index;
            }
        }

        Tiles.RemoveAt(last);
        SyncIndexMeta();
        CellEditEpoch++;
        return true;
    }

    private void EnsureCells()
    {
        if (_cells is not null
            && _indexedCount == Tiles.Count
            && (_indexedCount == 0 || ReferenceEquals(_indexedHead, Tiles[0])))
        {
            return;
        }

        RebuildCells();
    }

    private void RebuildCells()
    {
        var cells = new Dictionary<long, int>(Tiles.Count);
        for (var i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            if (tile is null)
            {
                continue;
            }

            cells[Pack(tile.X, tile.Y)] = i;
        }

        _cells = cells;
        SyncIndexMeta();
    }

    private void SyncIndexMeta()
    {
        _indexedCount = Tiles.Count;
        _indexedHead = Tiles.Count == 0 ? null : Tiles[0];
    }

    private static long Pack(int x, int y) => ((long)x << 32) | (uint)y;
}
