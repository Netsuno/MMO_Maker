namespace Frog.Application.Assets;

/// <summary>
/// Résout un chemin logique d’asset vers un chemin absolu sous la racine projet,
/// en rejetant les traversées de répertoire.
/// </summary>
public static class ProjectAssetPathResolver
{
    public enum ResolveStatus
    {
        Success,
        EmptyPath,
        TraversalRejected,
        NotFound,
    }

    public sealed record ResolveResult(ResolveStatus Status, string? AbsolutePath, string? ErrorMessage);

    public static ResolveResult TryResolve(string assetRoot, string? logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            return new ResolveResult(ResolveStatus.EmptyPath, null, "Chemin logique vide.");
        }

        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            return new ResolveResult(ResolveStatus.TraversalRejected, null, "Racine d’assets non configurée.");
        }

        var normalized = logicalPath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(logicalPath.Trim()) || normalized.StartsWith('/'))
        {
            return new ResolveResult(
                ResolveStatus.TraversalRejected,
                null,
                "Chemin logique invalide (chemin absolu interdit).");
        }

        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized))
        {
            return new ResolveResult(
                ResolveStatus.TraversalRejected,
                null,
                "Chemin logique invalide (traversée interdite).");
        }

        string rootFull;
        try
        {
            rootFull = Path.GetFullPath(assetRoot);
        }
        catch (Exception ex)
        {
            return new ResolveResult(
                ResolveStatus.TraversalRejected,
                null,
                "Racine d’assets invalide : " + ex.Message);
        }

        var candidate = Path.GetFullPath(Path.Combine(rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return new ResolveResult(
                ResolveStatus.TraversalRejected,
                null,
                "Chemin logique hors de la racine d’assets.");
        }

        if (!File.Exists(candidate))
        {
            return new ResolveResult(ResolveStatus.NotFound, candidate, "Fichier introuvable.");
        }

        return new ResolveResult(ResolveStatus.Success, candidate, null);
    }
}
