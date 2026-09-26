using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Résultat d’un collage : tuiles posées et cases vidées (trous du rectangle).</summary>
public readonly record struct TilePasteResult(int Painted, int Cleared)
{
    public bool Changed => Painted > 0 || Cleared > 0;
}

/// <summary>
/// Presse-papiers tuiles indépendant de l’UI.
/// Par défaut le rectangle couvre toutes les couches (comme RPG Maker) ; une copie couche active reste possible.
/// Les attributs déjà portés par la tuile (blocage, warp, ressource) sont copiés en mémoire.
/// </summary>
public sealed class TileClipboardBuffer
{
    private readonly List<LayerStamp> _layers = new();

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Vrai après Ctrl+Maj+C (une seule couche, collée sur la couche active).</summary>
    public bool IsSingleLayer { get; private set; }

    public bool HasContent => _layers.Exists(layer => layer.Tiles.Count > 0);

    public int CapturedLayerCount => _layers.Count;

    public bool CapturesLayer(int layerIndex) => _layers.Exists(layer => layer.LayerIndex == layerIndex);

    public IReadOnlyList<Tile> Snapshot()
        => _layers
            .SelectMany(layer => layer.Tiles)
            .Select(tile => MapEditOperations.CloneTileAt(tile, tile.X, tile.Y))
            .ToList();

    public void CopyFromLayer(Map map, int layerIndex, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        Clear();
        if (width <= 0 || height <= 0 || layerIndex < 0 || layerIndex >= map.Layers.Count)
        {
            return;
        }

        var tiles = Capture(map.Layers[layerIndex], left, top, width, height);
        if (tiles.Count == 0)
        {
            return;
        }

        _layers.Add(new LayerStamp(layerIndex, tiles));
        Width = width;
        Height = height;
        IsSingleLayer = true;
    }

    /// <summary>
    /// Capture le rectangle sur chaque couche, y compris les couches vides (pour effacer les trous au collage).
    /// Les couches verrouillées sont incluses : le collage les réécrit seulement si la destination est éditable.
    /// </summary>
    public void CopyAllLayers(Map map, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(map);
        Clear();
        if (width <= 0 || height <= 0 || map.Layers.Count == 0)
        {
            return;
        }

        var stamps = new List<LayerStamp>(map.Layers.Count);
        var any = false;
        for (var i = 0; i < map.Layers.Count; i++)
        {
            var tiles = Capture(map.Layers[i], left, top, width, height);
            if (tiles.Count > 0)
            {
                any = true;
            }

            stamps.Add(new LayerStamp(i, tiles));
        }

        if (!any)
        {
            return;
        }

        _layers.AddRange(stamps);
        Width = width;
        Height = height;
        IsSingleLayer = false;
    }

    /// <summary>
    /// Colle sur une couche. Tampon mono-couche : ignore l’index source et vise <paramref name="layerIndex"/>.
    /// Tampon multi-couches : n’écrit que l’empreinte de cet index (trous compris).
    /// </summary>
    public TilePasteResult PasteToLayer(
        Map map,
        int layerIndex,
        int anchorTileX,
        int anchorTileY,
        int mapWidth,
        int mapHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!HasContent)
        {
            return default;
        }

        if (IsSingleLayer)
        {
            return _layers.Count == 1
                ? Apply(_layers[0], map, layerIndex, anchorTileX, anchorTileY, mapWidth, mapHeight)
                : default;
        }

        var stamp = _layers.Find(layer => layer.LayerIndex == layerIndex);
        return stamp is null
            ? default
            : Apply(stamp, map, layerIndex, anchorTileX, anchorTileY, mapWidth, mapHeight);
    }

    /// <summary>
    /// Restaure chaque couche capturée sur le même index. Tampon mono-couche : aucun effet
    /// (utiliser <see cref="PasteToLayer"/> pour viser la couche active).
    /// </summary>
    public TilePasteResult PasteAllLayers(Map map, int anchorTileX, int anchorTileY, int mapWidth, int mapHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!HasContent || IsSingleLayer)
        {
            return default;
        }

        var painted = 0;
        var cleared = 0;
        foreach (var stamp in _layers)
        {
            var result = Apply(stamp, map, stamp.LayerIndex, anchorTileX, anchorTileY, mapWidth, mapHeight);
            painted += result.Painted;
            cleared += result.Cleared;
        }

        return new TilePasteResult(painted, cleared);
    }

    public bool CanPasteAllLayers(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!HasContent || IsSingleLayer)
        {
            return false;
        }

        return _layers.Exists(stamp => MapEditOperations.IsLayerEditable(map, stamp.LayerIndex));
    }

    public bool TryTransform(TileSelectionTransformKind kind)
    {
        if (!HasContent || Width <= 0 || Height <= 0)
        {
            return false;
        }

        var (newWidth, newHeight) = TileSelectionTransform.TransformSize(Width, Height, kind);
        foreach (var stamp in _layers)
        {
            if (stamp.Tiles.Count == 0)
            {
                continue;
            }

            var result = TileSelectionTransform.Apply(stamp.Tiles, Width, Height, kind);
            stamp.Tiles.Clear();
            stamp.Tiles.AddRange(result.Tiles);
        }

        Width = newWidth;
        Height = newHeight;
        return true;
    }

    public void Clear()
    {
        _layers.Clear();
        Width = 0;
        Height = 0;
        IsSingleLayer = false;
    }

    private TilePasteResult Apply(
        LayerStamp stamp,
        Map map,
        int layerIndex,
        int anchorTileX,
        int anchorTileY,
        int mapWidth,
        int mapHeight)
    {
        if (!MapEditOperations.IsLayerEditable(map, layerIndex) || Width <= 0 || Height <= 0)
        {
            return default;
        }

        var byCell = new Dictionary<(int X, int Y), Tile>();
        foreach (var tile in stamp.Tiles)
        {
            byCell[(tile.X, tile.Y)] = tile;
        }

        var layer = map.Layers[layerIndex];
        var limitW = Math.Min(mapWidth, map.Width);
        var limitH = Math.Min(mapHeight, map.Height);
        var painted = 0;
        var cleared = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var gx = anchorTileX + x;
                var gy = anchorTileY + y;
                if (gx < 0 || gy < 0 || gx >= limitW || gy >= limitH)
                {
                    continue;
                }

                if (byCell.TryGetValue((x, y), out var template))
                {
                    MapEditOperations.PaintTile(map, layerIndex, gx, gy, template);
                    painted++;
                    continue;
                }

                if (layer.TileAt(gx, gy) is null)
                {
                    continue;
                }

                MapEditOperations.EraseTile(map, layerIndex, gx, gy);
                cleared++;
            }
        }

        return new TilePasteResult(painted, cleared);
    }

    private static List<Tile> Capture(Layer layer, int left, int top, int width, int height)
    {
        var list = new List<Tile>();
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var tile = layer.TileAt(x, y);
                if (tile is null)
                {
                    continue;
                }

                list.Add(MapEditOperations.CloneTileAt(tile, x - left, y - top));
            }
        }

        return list;
    }

    private sealed class LayerStamp
    {
        public LayerStamp(int layerIndex, List<Tile> tiles)
        {
            LayerIndex = layerIndex;
            Tiles = tiles;
        }

        public int LayerIndex { get; }

        public List<Tile> Tiles { get; }
    }
}
