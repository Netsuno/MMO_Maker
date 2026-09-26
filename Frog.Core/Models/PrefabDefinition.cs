#nullable enable
using System.Collections.Generic;

namespace Frog.Core.Models;

/// <summary>Facing optionnel d’un prefab (sud par défaut si omis).</summary>
public enum PrefabFacing
{
    South = 0,
    West = 1,
    East = 2,
    North = 3,
}

/// <summary>Variante visuelle (souvent une orientation) d’un prefab.</summary>
public sealed class PrefabFacingVariant
{
    public PrefabFacing Facing { get; set; } = PrefabFacing.South;

    /// <summary>Nom de fichier seul sous <c>Prefabs/</c> (ex. <c>sofa-south.png</c>).</summary>
    public string SpriteFileName { get; set; } = string.Empty;

    /// <summary>Empreinte tuiles spécifique à cette variante (0 = héritage définition).</summary>
    public int FootprintWidthTiles { get; set; }

    public int FootprintHeightTiles { get; set; }

    public int WidthPixels { get; set; }

    public int HeightPixels { get; set; }
}

/// <summary>
/// Définition réutilisable d’un objet monde (canapé, clôture…).
/// Empreinte en tuiles et/ou en pixels ; variantes de facing optionnelles.
/// </summary>
public sealed class PrefabDefinition
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int FootprintWidthTiles { get; set; }

    public int FootprintHeightTiles { get; set; }

    public int WidthPixels { get; set; }

    public int HeightPixels { get; set; }

    public List<PrefabFacingVariant> Variants { get; set; } = new();
}

/// <summary>Catalogue de prefabs (fichiers <c>Prefabs/catalog.json</c>).</summary>
public sealed class PrefabCatalog
{
    public int CatalogVersion { get; set; } = 1;

    public List<PrefabDefinition> Prefabs { get; set; } = new();
}

/// <summary>Instance posée sur une carte (coin haut-gauche en tuiles).</summary>
public sealed class PrefabPlacement
{
    public string PrefabId { get; set; } = string.Empty;

    public PrefabFacing Facing { get; set; } = PrefabFacing.South;

    public int TileX { get; set; }

    public int TileY { get; set; }

    /// <summary>Copie identifiant, facing et position. Les champs hors position passent par <see cref="CopyIdentityFrom"/>.</summary>
    public PrefabPlacement Clone()
    {
        var copy = new PrefabPlacement();
        copy.CopyIdentityFrom(this);
        copy.TileX = TileX;
        copy.TileY = TileY;
        return copy;
    }

    /// <summary>
    /// Recopie les paramètres d’instance autres que la position (id catalogue, facing).
    /// Appelé après <c>TryPlace</c> pour qu’un décalage ne réécrive pas le facing.
    /// </summary>
    public void CopyIdentityFrom(PrefabPlacement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        PrefabId = source.PrefabId;
        Facing = source.Facing;
    }
}

/// <summary>Sidecar <c>{carte}.prefabs.json</c> — additif, hors blob <c>.fmap</c>.</summary>
public sealed class PrefabPlacementDocument
{
    public int DocumentVersion { get; set; } = 1;

    public List<PrefabPlacement> Placements { get; set; } = new();
}
