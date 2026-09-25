using System.IO;
using Frog.Application.Maps;

namespace Frog.Editor.Services;

/// <summary>
/// Bibliothèque de modèles à côté du catalogue TileAsset
/// (<c>MmoMaker/GameData/map-templates/map-templates.json</c>).
/// </summary>
public static class EditorMapTemplateStore
{
    internal static string? OverrideDirectoryForTest { get; set; }

    public static string DirectoryPath()
    {
        if (!string.IsNullOrWhiteSpace(OverrideDirectoryForTest))
        {
            return OverrideDirectoryForTest;
        }

        var assets = TileAssetCatalogue.DefaultStoreDirectory();
        var parent = Directory.GetParent(assets)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
        {
            parent = assets;
        }

        return Path.Combine(parent, "map-templates");
    }

    public static bool TryLoad(out MapStampTemplateLibrary library, out string? error)
        => MapStampTemplateLibrary.TryLoad(DirectoryPath(), out library, out error);
}
