using System.Drawing;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Presse‑papiers tuiles de l’éditeur (indépendant du presse‑papiers Windows).</summary>
public static class EditorTileClipboard
{
    internal static readonly TileClipboardBuffer Buffer = new();

    public static bool HasContent => Buffer.HasContent;

    public static int Width => Buffer.Width;

    public static int Height => Buffer.Height;

    public static void CopyFromLayer(Map map, int layerIndex, Rectangle tileBounds)
        => Buffer.CopyFromLayer(map, layerIndex, tileBounds.Left, tileBounds.Top, tileBounds.Width, tileBounds.Height);

    /// <summary>Colle avec ancrage tuile supérieure gauche. Retourne le nombre de tuiles posées.</summary>
    public static int PasteToLayer(Map map, int layerIndex, int anchorTileX, int anchorTileY, int mapWidth, int mapHeight)
        => Buffer.PasteToLayer(map, layerIndex, anchorTileX, anchorTileY, mapWidth, mapHeight);

    public static bool TryTransform(TileSelectionTransformKind kind) => Buffer.TryTransform(kind);

    public static IReadOnlyList<Tile> Snapshot() => Buffer.Snapshot();

    public static void Clear() => Buffer.Clear();
}
