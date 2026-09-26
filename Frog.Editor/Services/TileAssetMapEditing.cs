using System;
using System.IO;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>
/// Édition des cartes adressées par <see cref="TileAssetId"/>. Les cartes feuille restent sur
/// <see cref="MapSerializer.Serialize"/> (v5). Aucune cellule ne porte à la fois Src et un id.
/// </summary>
public static class TileAssetMapEditing
{
    public static Map CreateMap(string name, int width, int height)
    {
        var map = MapFormat.CreateTileAssetMap(name, width, height);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        return map;
    }

    public static bool IsTileAssetMap(Map? map) => map?.GraphicIdentity == TileGraphicIdentity.TileAsset;

    /// <summary>
    /// Passe une carte feuille vide (ou seulement logique, sans Src) en identité TileAsset.
    /// Refuse toute coordonnée de feuille : pas de migration v5 → v6.
    /// </summary>
    public static bool TryAdoptTileAssetIdentity(Map map, out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.GraphicIdentity == TileGraphicIdentity.TileAsset)
        {
            if (map.TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
            {
                error = "Carte TileAsset : tileSizePixels doit rester 48.";
                return false;
            }

            error = null;
            return true;
        }

        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.TilesetId != 0 || tile.SrcX != 0 || tile.SrcY != 0)
                {
                    error = "Cette carte contient des coordonnées de feuille (Src). Elle reste en v5. Ré-auteur les tuiles en TileAsset ; pas de conversion automatique.";
                    return false;
                }

                if (!tile.AssetId.IsNone)
                {
                    error = "Un TileAssetId est déjà posé sur une carte feuille. Les deux identités ne cohabitent pas.";
                    return false;
                }
            }
        }

        map.GraphicIdentity = TileGraphicIdentity.TileAsset;
        map.TileSizePixels = TileAssetMetrics.TargetTileSizePixels;
        error = null;
        return true;
    }

    public static Tile CreateBrushTile(int x, int y, TileAssetId id, TileType type)
    {
        if (id.IsNone)
        {
            throw new ArgumentException("Le pinceau TileAsset exige un id.", nameof(id));
        }

        return new Tile
        {
            X = x,
            Y = y,
            AssetId = id,
            Type = type,
        };
    }

    public static bool TryPaint(Map map, int layerIndex, int x, int y, Tile brush, bool joinAutotiles = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(brush);
        if (map.GraphicIdentity != TileGraphicIdentity.TileAsset || brush.AssetId.IsNone)
        {
            return false;
        }

        if (brush.TilesetId != 0 || brush.SrcX != 0 || brush.SrcY != 0)
        {
            return false;
        }

        MapEditOperations.PaintTile(map, layerIndex, x, y, brush);
        if (joinAutotiles)
        {
            AutotileJoin.ReconcileNeighborhood(map, layerIndex, x, y);
        }

        return true;
    }

    public static byte[] Write(Map map) => MapFormat.Write(map);

    /// <summary>v6 via <see cref="MapFormat.Write"/>, v5 via <see cref="MapSerializer"/>.</summary>
    public static byte[] WriteEditorMap(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map.GraphicIdentity == TileGraphicIdentity.TileAsset
            ? MapFormat.Write(map)
            : new MapSerializer().Serialize(map);
    }

    public static Map ReadEditorMap(ReadOnlySpan<byte> data) => MapFormat.Read(data);
}
