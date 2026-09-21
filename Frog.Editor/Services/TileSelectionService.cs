using System.Drawing;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>Rotation / miroir de la sélection couche tuile (in situ si rectangle commis, sinon presse-papiers).</summary>
public static class TileSelectionService
{
    public static bool TryTransform(
        Map map,
        int layerIndex,
        Rectangle? committedSelection,
        TileSelectionTransformKind kind,
        out Rectangle? newSelection)
    {
        ArgumentNullException.ThrowIfNull(map);
        newSelection = committedSelection;

        if (committedSelection is { Width: > 0, Height: > 0 } rect
            && MapEditOperations.IsLayerEditable(map, layerIndex)
            && MapEditOperations.CountTilesInRect(map, layerIndex, rect.X, rect.Y, rect.Width, rect.Height) > 0)
        {
            if (!MapEditOperations.TryTransformLayerRect(
                    map,
                    layerIndex,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    kind,
                    out var newWidth,
                    out var newHeight))
            {
                return false;
            }

            var next = new Rectangle(rect.X, rect.Y, newWidth, newHeight);
            EditorTileClipboard.CopyFromLayer(map, layerIndex, next);
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
