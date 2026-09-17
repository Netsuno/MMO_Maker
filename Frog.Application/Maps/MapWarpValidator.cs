using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Validation des destinations warp contre un catalogue de cartes connues.</summary>
public static class MapWarpValidator
{
    public static bool ValidateWarpTargets(
        Map map,
        IReadOnlyDictionary<Guid, (int Width, int Height)> targetMaps,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(targetMaps);

        for (var li = 0; li < map.Layers.Count; li++)
        {
            var layer = map.Layers[li];
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type != TileType.Warp)
                {
                    continue;
                }

                if (tile.WarpTargetMapId == Guid.Empty)
                {
                    errorMessage =
                        $"Warp sur ({tile.X}, {tile.Y}) / couche « {layer.GetDisplayLabel()} » : identifiant de carte cible invalide.";
                    return false;
                }

                if (!targetMaps.TryGetValue(tile.WarpTargetMapId, out var dims))
                {
                    errorMessage =
                        $"Warp sur ({tile.X}, {tile.Y}) / couche « {layer.GetDisplayLabel()} » : carte cible {tile.WarpTargetMapId} introuvable.";
                    return false;
                }

                if (tile.WarpTargetX < 0
                    || tile.WarpTargetY < 0
                    || tile.WarpTargetX >= dims.Width
                    || tile.WarpTargetY >= dims.Height)
                {
                    errorMessage =
                        $"Warp sur ({tile.X}, {tile.Y}) / couche « {layer.GetDisplayLabel()} » : destination ({tile.WarpTargetX}, {tile.WarpTargetY}) hors limites de la carte cible ({dims.Width}×{dims.Height}).";
                    return false;
                }
            }
        }

        errorMessage = null;
        return true;
    }
}
