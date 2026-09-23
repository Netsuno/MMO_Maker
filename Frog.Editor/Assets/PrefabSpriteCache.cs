using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frog.Application.Prefabs;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Editor.Assets;

/// <summary>Bitmaps prefab chargés depuis <c>Prefabs/</c> (même layout que le client).</summary>
internal static class PrefabSpriteCache
{
    private static readonly Dictionary<string, Bitmap> Bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource> Previews = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tests : force le dossier <c>Prefabs/</c> (sprites absents → pas d’aperçu).</summary>
    internal static string? OverrideDirectoryForTest { get; set; }

    internal static void ResetCacheForTest()
    {
        foreach (var bitmap in Bitmaps.Values)
        {
            bitmap.Dispose();
        }

        Bitmaps.Clear();
        Previews.Clear();
    }

    public static string ResolvePrefabsDirectory()
    {
        if (!string.IsNullOrWhiteSpace(OverrideDirectoryForTest))
        {
            return OverrideDirectoryForTest;
        }

        var nextToExe = Path.Combine(AppContext.BaseDirectory ?? ".", MapPrefabPackage.FolderName);
        if (Directory.Exists(nextToExe))
        {
            return nextToExe;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory ?? ".");
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "prefabs");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return nextToExe;
    }

    public static PrefabCatalog LoadCatalog()
    {
        var path = Path.Combine(ResolvePrefabsDirectory(), MapPrefabPackage.CatalogFileName);
        var fromDisk = PrefabCatalogJson.TryDeserializeFromFile(path);
        return fromDisk ?? BuiltInPrefabCatalog.Create();
    }

    public static bool TryGet(string? fileName, out Bitmap? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var key = Path.GetFileName(fileName.Trim());
        if (Bitmaps.TryGetValue(key, out var cached))
        {
            bitmap = cached;
            return true;
        }

        var path = Path.Combine(ResolvePrefabsDirectory(), key);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var tmp = new Bitmap(path);
            var clone = new Bitmap(tmp);
            Bitmaps[key] = clone;
            bitmap = clone;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<PrefabSpriteFile> SnapshotPngFiles(PrefabCatalog catalog)
    {
        var files = new List<PrefabSpriteFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab?.Variants is null)
            {
                continue;
            }

            foreach (var variant in prefab.Variants)
            {
                var name = Path.GetFileName(variant.SpriteFileName?.Trim() ?? string.Empty);
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                {
                    continue;
                }

                var path = Path.Combine(ResolvePrefabsDirectory(), name);
                if (!File.Exists(path))
                {
                    continue;
                }

                files.Add(new PrefabSpriteFile(name, File.ReadAllBytes(path)));
            }
        }

        return files;
    }

    /// <summary>
    /// Aperçu WPF figé à partir d’un sprite déjà en cache (ou chargé depuis le dossier prefab).
    /// Retourne null si le fichier est absent ou illisible — l’UI n’affiche alors pas de vignette.
    /// </summary>
    public static ImageSource? TryCreatePreview(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var key = Path.GetFileName(fileName.Trim());
        if (Previews.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!TryGet(key, out var bitmap) || bitmap is null)
        {
            return null;
        }

        try
        {
            using var clone = new Bitmap(bitmap);
            using var stream = new MemoryStream();
            clone.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Previews[key] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }
}
