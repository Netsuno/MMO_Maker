// #TODO (FR) : Définir la structure de données d’une carte selon l’héritage VB6
// (largeur, hauteur, couches, tuiles, attributs, événements, métadonnées).
#nullable enable
namespace Frog.Core.Models;

using Frog.Core.Interfaces;
using System.Collections.Generic;

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
    /// Valide l’intégrité de base de la carte (dimensions, bornes, couches).
    /// Étendra plus tard : cohérence des tuiles, warps, zones de collisions, etc.
    /// </summary>
    public bool Validate(out string? error)
    {
        // #TODO (FR) : Étendre les contrôles (bornes max, nombre de couches, contenu des tuiles).
        if (Width <= 0 || Height <= 0)
        {
            error = "Les dimensions de la carte doivent être > 0.";
            return false;
        }
        error = null;
        return true;
    }
}
