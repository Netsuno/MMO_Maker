namespace Frog.Application.Assets;

/// <summary>Dossiers logiques sous la racine projet (pas de second catalogue folklore).</summary>
public static class ProjectAssetKind
{
    public const string Tiles = "tiles";
    public const string Sprites = "sprites";
    public const string Icons = "icons";
    public const string Other = "other";
    public const string Prefabs = "prefabs";

    public static bool IsKnown(string? kind)
        => kind is Tiles or Sprites or Icons or Other or Prefabs;
}
