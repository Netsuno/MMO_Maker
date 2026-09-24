// #TODO (FR) : Définir précisément le rôle de chaque couche (rendu vs attributs).
#nullable enable
namespace Frog.Core.Models;

using System.Collections.Generic;
using System.ComponentModel;
using Frog.Core.Enums;

/// <summary>
/// Couche de la carte (ex. Sol, Masque, Frange, Attributs…).
/// </summary>
public sealed class Layer
{
    [DisplayName("Type")]
    public LayerType LayerType { get; set; }

    /// <summary>Nom affiché dans l’éditeur (vide = libellé français du <see cref="LayerType"/>).</summary>
    [DisplayName("Nom affiché")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Si faux, la couche n’est pas dessinée sur le canvas (comme l’œil dans RPG Maker).</summary>
    [DisplayName("Visible")]
    public bool Visible { get; set; } = true;

    /// <summary>Si vrai, aucune modification de tuiles sur cette couche (pinceau, gomme, collage, etc.).</summary>
    [DisplayName("Verrouillée")]
    public bool Locked { get; set; }

    /// <summary>
    /// Conteneur des tuiles. Choix de structure (liste vs tableau 2D vs tableau plat) à stabiliser
    /// en fonction du format binaire et des performances d’édition.
    /// </summary>
    public List<Tile> Tiles { get; } = new();

    /// <summary>Libellé pour l’UI : <see cref="DisplayName"/> ou le nom français du type.</summary>
    public string GetDisplayLabel() =>
        string.IsNullOrWhiteSpace(DisplayName) ? LayerTypeLabels.French(LayerType) : DisplayName.Trim();
}
