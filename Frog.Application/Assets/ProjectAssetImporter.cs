using Frog.Core.Models;

namespace Frog.Application.Assets;

public sealed record ProjectAssetImportResult(
    bool Success,
    string? LogicalPath,
    string? AbsolutePath,
    string? Sha256Hex,
    int WidthPixels,
    int HeightPixels,
    string? Error);

/// <summary>
/// Copie un fichier image vers la racine projet sous <see cref="ProjectAssetKind"/>,
/// calcule SHA-256 et (PNG) dimensions IHDR.
/// </summary>
public static class ProjectAssetImporter
{
    public static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];

    public static ProjectAssetImportResult Import(
        string sourcePath,
        string assetRoot,
        string kind,
        string? preferredFileName = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return Fail("Fichier source introuvable.");
        }

        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return Fail("Racine d’assets non configurée.");
        }

        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (!ProjectAssetKind.IsKnown(kind))
        {
            return Fail("Catégorie d’asset inconnue (tiles, sprites, icons, other).");
        }

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext)
            || !AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return Fail("Extension refusée (png, jpg, jpeg, bmp, gif, webp).");
        }

        string rootFull;
        try
        {
            rootFull = Path.GetFullPath(assetRoot);
        }
        catch (Exception ex)
        {
            return Fail("Racine d’assets invalide : " + ex.Message);
        }

        Directory.CreateDirectory(rootFull);
        var destDir = Path.Combine(rootFull, kind);
        Directory.CreateDirectory(destDir);

        var baseName = SanitizeFileName(
            string.IsNullOrWhiteSpace(preferredFileName)
                ? Path.GetFileName(sourcePath)
                : preferredFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "asset" + ext.ToLowerInvariant();
        }

        if (!baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            baseName += ext.ToLowerInvariant();
        }

        var destPath = UniqueDestination(destDir, baseName);
        try
        {
            File.Copy(sourcePath, destPath, overwrite: false);
        }
        catch (Exception ex)
        {
            return Fail("Copie impossible : " + ex.Message);
        }

        var bytes = File.ReadAllBytes(destPath);
        var sha = TilesetDefinition.ComputeSha256Hex(bytes);
        PngImageHeader.TryRead(bytes, out var width, out var height);

        var logical = (kind + "/" + Path.GetFileName(destPath)).Replace('\\', '/');
        var probe = ProjectAssetPathResolver.TryResolve(rootFull, logical);
        if (probe.Status != ProjectAssetPathResolver.ResolveStatus.Success)
        {
            try
            {
                File.Delete(destPath);
            }
            catch
            {
                // best-effort
            }

            return Fail(probe.ErrorMessage ?? "Chemin logique rejeté après copie.");
        }

        return new ProjectAssetImportResult(
            Success: true,
            LogicalPath: logical,
            AbsolutePath: destPath,
            Sha256Hex: sha,
            WidthPixels: width,
            HeightPixels: height,
            Error: null);
    }

    private static ProjectAssetImportResult Fail(string error)
        => new(false, null, null, null, 0, 0, error);

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var chars = trimmed.Select(c => Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' ? '_' : c)
            .ToArray();
        var cleaned = new string(chars);
        if (cleaned.Contains("..", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("..", "_", StringComparison.Ordinal);
        }

        return cleaned;
    }

    private static string UniqueDestination(string destDir, string fileName)
    {
        var candidate = Path.Combine(destDir, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; i < 10_000; i++)
        {
            candidate = Path.Combine(destDir, $"{stem}-{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(destDir, $"{stem}-{Guid.NewGuid():N}{ext}");
    }
}
