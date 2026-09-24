#nullable enable
namespace Frog.Core.Models;

/// <summary>
/// Métadonnées d’animation de tileset, stockées à côté de la carte (<c>.anims.json</c>)
/// ou de l’image (<c>.anim.json</c>). Hors <c>.fmap</c> : la tuile posée garde la 1re frame.
/// </summary>
public sealed class TilesetAnimationDocument
{
    public const int CurrentVersion = 1;

    public int DocumentVersion { get; set; } = CurrentVersion;

    public List<TilesetAnimationSet> Tilesets { get; set; } = new();
}

/// <summary>Bandes animées d’un tileset (identifiant palette éditeur).</summary>
public sealed class TilesetAnimationSet
{
    public int TilesetId { get; set; }

    /// <summary>Durée d’une pose, en millisecondes.</summary>
    public int FrameDurationMs { get; set; } = 200;

    public List<AnimatedTileStrip> Strips { get; set; } = new();
}

/// <summary>
/// Suite de frames à partir de <see cref="OriginX"/> / <see cref="OriginY"/>.
/// <see cref="Layout"/> <c>horizontal</c> (défaut, style RPG Maker) ou <c>vertical</c>.
/// </summary>
public sealed class AnimatedTileStrip
{
    public int OriginX { get; set; }

    public int OriginY { get; set; }

    public int FrameCount { get; set; }

    public string Layout { get; set; } = "horizontal";
}
