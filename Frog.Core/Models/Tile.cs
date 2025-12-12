// #TODO (FR) : Définir les attributs de tuile (animation, collisions, triggers, région, etc.).
#nullable enable
using System.Collections.Generic;
namespace Frog.Core.Models;

using Frog.Core.Enums;

/// <summary>
/// Tuile individuelle placée sur une couche. Les coordonnées (X, Y) sont exprimées en tuiles.
/// </summary>
public sealed class Tile
{
    /// <summary>Coordonnée X (en tuiles).</summary>
    public int X { get; set; }
    /// <summary>Coordonnée Y (en tuiles).</summary>
    public int Y { get; set; }
    /// <summary>Type logique de la tuile (ex. Ground, Block, Warp…)</summary>
    public TileType Type { get; set; }
    public int WarpTargetMapId { get; set; }
    public int WarpTargetX { get; set; }
    public int WarpTargetY { get; set; }

    /// <summary>Identifiant du tileset source.</summary>
    public int TilesetId { get; set; }
    /// <summary>Position X source dans le tileset (pixels ou index tuile selon le format final).</summary>
    public int SrcX { get; set; }
    /// <summary>Position Y source dans le tileset.</summary>
    public int SrcY { get; set; }

    public List<ITileAttribute> Attributes { get; } = new();

    // #TODO (FR) : Drapeaux : collision, blocage NPC/joueur, zone/region id, identifiant d’attribut/script.
}

