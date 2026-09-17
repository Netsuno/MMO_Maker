// #TODO (FR) : Définir les attributs de tuile (animation, collisions, triggers, région, etc.).
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
namespace Frog.Core.Models;

using Frog.Core.Enums;

/// <summary>
/// Tuile individuelle placée sur une couche. Les coordonnées (X, Y) sont exprimées en tuiles.
/// </summary>
public sealed class Tile
{
    /// <summary>Coordonnée X (en tuiles).</summary>
    [Category("Position")]
    [Description("Colonne de la tuile sur la carte (0 = gauche).")]
    public int X { get; set; }

    /// <summary>Coordonnée Y (en tuiles).</summary>
    [Category("Position")]
    [Description("Ligne de la tuile sur la carte (0 = haut).")]
    public int Y { get; set; }

    /// <summary>Type logique de la tuile (ex. Ground, Block, Warp…)</summary>
    [Category("Jeu")]
    [Description("Rôle moteur : sol, blocage, warp, ressource, script…")]
    public TileType Type { get; set; }

    [Category("Warp")]
    [Description("Identifiant canonique de la carte de destination.")]
    public Guid WarpTargetMapId { get; set; }

    [Category("Warp")]
    [Description("Tuile X sur la carte de destination.")]
    public int WarpTargetX { get; set; }

    [Category("Warp")]
    [Description("Tuile Y sur la carte de destination.")]
    public int WarpTargetY { get; set; }

    /// <summary>Identifiant du tileset source.</summary>
    [Category("Graphique")]
    [Description("Référence au tileset chargé dans l’éditeur (PNG).")]
    public int TilesetId { get; set; }

    /// <summary>Position X source dans le tileset (pixels ou index tuile selon le format final).</summary>
    [Category("Graphique")]
    [Description("Découpe horizontale dans l’image du tileset (pixels).")]
    public int SrcX { get; set; }

    /// <summary>Position Y source dans le tileset.</summary>
    [Category("Graphique")]
    [Description("Découpe verticale dans l’image du tileset (pixels).")]
    public int SrcY { get; set; }

    public List<ITileAttribute> Attributes { get; } = new();

    [Category("Jeu")]
    [Description("Identifiant de script optionnel (futur moteur d’événements).")]
    public string? ScriptId { get; set; }

    // #TODO (FR) : Drapeaux : collision, blocage NPC/joueur, zone/region id, identifiant d’attribut/script.
}

