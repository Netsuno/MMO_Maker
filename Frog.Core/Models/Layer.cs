// #TODO (FR) : Définir précisément le rôle de chaque couche (rendu vs attributs).
#nullable enable
namespace Frog.Core.Models;

using System.Collections.Generic;
using Frog.Core.Enums;

/// <summary>
/// Couche de la carte (ex. Ground, Mask, Fringe, Attributes…).
/// </summary>
public sealed class Layer
{
    public LayerType LayerType { get; set; }

    /// <summary>Nom affiché dans l’éditeur (vide = libellé dérivé du <see cref="LayerType"/>).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Si faux, la couche n’est pas dessinée sur le canvas (comme l’œil dans RPG Maker).</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Si vrai, aucune modification de tuiles sur cette couche (pinceau, gomme, collage, etc.).</summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Conteneur des tuiles. Choix de structure (liste vs tableau 2D vs tableau plat) à stabiliser
    /// en fonction du format binaire et des performances d’édition.
    /// </summary>
    public List<Tile> Tiles { get; } = new();

    /// <summary>Libellé pour l’UI : <see cref="DisplayName"/> ou le nom du type moteur.</summary>
    public string GetDisplayLabel() =>
        string.IsNullOrWhiteSpace(DisplayName) ? LayerType.ToString() : DisplayName.Trim();
}
