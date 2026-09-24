using System.Drawing;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>
/// Rotation / miroir de la sélection. In situ sur toutes les couches éditables du rectangle
/// (ou la couche active si demandé), sinon sur le presse-papiers.
/// </summary>
public static class TileSelectionService
{
    public static bool TryTransform(
        Map map,
        int layerIndex,
        Rectangle? committedSelection,
        TileSelectionTransformKind kind,
        out Rectangle? newSelection,
        bool activeLayerOnly = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        newSelection = committedSelection;

        if (committedSelection is { Width: > 0, Height: > 0 } rect)
        {
            int? onlyLayer = activeLayerOnly ? layerIndex : null;
            if (!MapEditOperations.TryTransformMapRect(
                    map,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    kind,
                    onlyLayer,
                    out var newWidth,
                    out var newHeight))
            {
                return false;
            }

            var next = new Rectangle(rect.X, rect.Y, newWidth, newHeight);
            if (activeLayerOnly)
            {
                EditorTileClipboard.CopyFromLayer(map, layerIndex, next);
            }
            else
            {
                EditorTileClipboard.CopyAllLayers(map, next);
            }

            newSelection = next;
            return true;
        }

        if (!EditorTileClipboard.TryTransform(kind))
        {
            return false;
        }

        return true;
    }
}
