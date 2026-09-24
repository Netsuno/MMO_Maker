#nullable enable
namespace Frog.Core.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Interfaces;
using Frog.Core.Maps;

/// <summary>
/// Représente une carte (map) logique. Unité d’édition (Editor), d’affichage (Client)
/// et d’instance côté serveur (Server).
/// </summary>
public sealed class Map : IValidatable
{
    /// <summary>Largeur de la carte en tuiles (doit être > 0).</summary>
    public int Width { get; set; }
    /// <summary>Hauteur de la carte en tuiles (doit être > 0).</summary>
    public int Height { get; set; }

    /// <summary>
    /// Couches de rendu/attributs. Voir <see cref="Enums.LayerType"/>.
    /// </summary>
    public List<Layer> Layers { get; } = new();

    /// <summary>Nom lisible par l’utilisateur (utile dans l’éditeur et pour le debug).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Si activé sur la carte, les joueurs ne se bloquent pas mutuellement (même tuile ou passage).
    /// Les collisions bloc / limites carte restent appliquées.
    /// </summary>
    [Description("Autoriser plusieurs joueurs sur la même tuile (pas de collision joueur joueur).")]
    public bool AllowPlayerOverlap { get; set; }

    /// <summary>
    /// Identité graphique écrite dans le <c>.fmap</c>.
    /// <see cref="TileGraphicIdentity.SheetSource"/> (défaut) → v5, coordonnées de feuille.
    /// <see cref="TileGraphicIdentity.TileAsset"/> → v6, <see cref="Tile.AssetId"/> uniquement.
    /// </summary>
    public TileGraphicIdentity GraphicIdentity { get; set; } = TileGraphicIdentity.SheetSource;

    /// <summary>
    /// Taille de tuile déclarée par l’en-tête v6. 0 pour les fichiers v3–v5 : le consommateur garde
    /// <see cref="WorldMetrics.DefaultTileSizePixels"/> (32). Une carte TileAsset écrit 48, sans upscale implicite.
    /// </summary>
    public int TileSizePixels { get; set; }

    /// <summary>
    /// Valide l’intégrité de la carte : dimensions, couches, tuiles dans les bornes, doublons par couche, warps.
    /// </summary>
    public bool Validate(out string? errorMessage)
    {
        if (Width <= 0 || Height <= 0)
        {
            errorMessage = "Les dimensions de la carte doivent être > 0.";
            return false;
        }

        if (Layers.Count == 0)
        {
            errorMessage = "La carte doit avoir au moins une couche.";
            return false;
        }

        if (Layers.Count > 1024)
        {
            errorMessage = "Nombre de couches trop élevé (> 1024).";
            return false;
        }

        if (GraphicIdentity == TileGraphicIdentity.TileAsset)
        {
            if (TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
            {
                errorMessage =
                    $"Carte TileAsset : tileSizePixels doit être {TileAssetMetrics.TargetTileSizePixels} (pas de mise à l’échelle silencieuse depuis {WorldMetrics.DefaultTileSizePixels}).";
                return false;
            }
        }
        else if (GraphicIdentity == TileGraphicIdentity.SheetSource)
        {
            if (TileSizePixels != 0)
            {
                errorMessage = "Carte feuille (v5) : tileSizePixels reste 0. La taille 48 se déclare seulement en identité TileAsset.";
                return false;
            }
        }
        else
        {
            errorMessage = "Identité graphique de carte inconnue.";
            return false;
        }

        for (var li = 0; li < Layers.Count; li++)
        {
            var layer = Layers[li];
            if (!Enum.IsDefined(typeof(LayerType), layer.LayerType))
            {
                errorMessage = $"Couche {li} ({layer.GetDisplayLabel()}) : type de couche invalide.";
                return false;
            }

            var occupied = new HashSet<(int X, int Y)>();
            foreach (var t in layer.Tiles)
            {
                if (t.X < 0 || t.Y < 0 || t.X >= Width || t.Y >= Height)
                {
                    errorMessage =
                        $"Tuile hors carte sur la couche « {layer.GetDisplayLabel()} » ({li}) : ({t.X}, {t.Y}).";
                    return false;
                }

                if (!occupied.Add((t.X, t.Y)))
                {
                    errorMessage =
                        $"Tuiles superposées sur la couche « {layer.GetDisplayLabel()} » ({li}) : ({t.X}, {t.Y}).";
                    return false;
                }

                var hasSheetSource = t.TilesetId != 0 || t.SrcX != 0 || t.SrcY != 0;
                var hasAssetId = !t.AssetId.IsNone;
                if (GraphicIdentity == TileGraphicIdentity.TileAsset && hasSheetSource)
                {
                    errorMessage =
                        $"Tuile ({t.X}, {t.Y}) / couche « {layer.GetDisplayLabel()} » : v6 stocke un TileAssetId, pas Src+Id.";
                    return false;
                }

                if (GraphicIdentity == TileGraphicIdentity.SheetSource && hasAssetId)
                {
                    errorMessage =
                        $"Tuile ({t.X}, {t.Y}) / couche « {layer.GetDisplayLabel()} » : TileAssetId présent sur une carte v5. Passer la carte en identité TileAsset pour écrire le format v6.";
                    return false;
                }

                if (t.Type == TileType.Warp)
                {
                    if (t.WarpTargetMapId == Guid.Empty)
                    {
                        errorMessage =
                            $"Warp sur ({t.X}, {t.Y}) / couche « {layer.GetDisplayLabel()} » : identifiant de carte cible invalide.";
                        return false;
                    }

                    if (t.WarpTargetX < 0 || t.WarpTargetY < 0)
                    {
                        errorMessage =
                            $"Warp sur ({t.X}, {t.Y}) / couche « {layer.GetDisplayLabel()} » : coordonnées de destination invalides.";
                        return false;
                    }
                }
            }
        }

        errorMessage = null;
        return true;
    }
}
