using System.IO;
using System.Windows.Forms;
using Frog.Application.Assets;
using Frog.Editor.Assets;
using Frog.Editor.Forms.GameData;

namespace Frog.Editor.Services;

/// <summary>Import fichier → racine projet (chemins logiques Game Data / tileset cache).</summary>
internal static class GameDataAssetImport
{
    public static ProjectAssetImportResult ImportFromPath(string sourcePath, string kind)
    {
        var root = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();
        return ProjectAssetImporter.Import(sourcePath, root, kind);
    }

    public static bool TryPickAndImport(IWin32Window? owner, string kind, out ProjectAssetImportResult result)
    {
        result = new ProjectAssetImportResult(false, null, null, null, 0, 0, "Annulé.");
        if (!string.IsNullOrWhiteSpace(EditorTestHooks.OverrideImportSourcePath))
        {
            result = ImportFromPath(EditorTestHooks.OverrideImportSourcePath, kind);
            return result.Success;
        }

        using var ofd = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Tous les fichiers|*.*",
            Title = "Importer un asset projet",
        };
        if (ofd.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        result = ImportFromPath(ofd.FileName, kind);
        return result.Success;
    }

    public static void ApplyToPathField(TextBox path, AssetPreviewControl? preview, ProjectAssetImportResult imported)
    {
        if (!imported.Success || string.IsNullOrWhiteSpace(imported.LogicalPath))
        {
            return;
        }

        path.Text = imported.LogicalPath;
        if (preview is not null)
        {
            preview.LogicalPath = imported.LogicalPath;
        }
    }
}
