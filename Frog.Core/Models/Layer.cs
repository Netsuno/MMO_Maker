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
    /// <summary>
    /// Conteneur des tuiles. Choix de structure (liste vs tableau 2D vs tableau plat) à stabiliser
    /// en fonction du format binaire et des performances d’édition.
    /// </summary>
    public List<Tile> Tiles { get; } = new();
}
