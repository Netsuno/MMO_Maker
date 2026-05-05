// #TODO (FR) : Définir la structure de données d’une carte selon l’héritage VB6
// (largeur, hauteur, couches, tuiles, attributs, événements, métadonnées).
#nullable enable
namespace Frog.Core.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Frog.Core.Enums;
using Frog.Core.Interfaces;

/// <summary>
/// Représente une carte (map) logique. Sert d’unité d’édition (Editor), d’affichage (Client)
/// et d’instance côté serveur (Server). Les types numériques doivent rester compatibles
/// avec le format binaire hérité du projet VB6.
/// </summary>
public sealed class Map : IValidatable
{
    /// <summary>Largeur de la carte en tuiles (doit être > 0).</summary>
    public int Width { get; set; }
    /// <summary>Hauteur de la carte en tuiles (doit être > 0).</summary>
    public int Height { get; set; }

    /// <summary>
    /// Couches de rendu/attributs. L’ordre et le nombre doivent rester cohérents avec l’éditeur VB6
    /// (Ground/Mask/Mask2/Fringe/Fringe2/Attributes). Voir <see cref="Enums.LayerType"/>.
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

                if (t.Type == TileType.Warp)
                {
                    if (t.WarpTargetMapId < 0)
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
