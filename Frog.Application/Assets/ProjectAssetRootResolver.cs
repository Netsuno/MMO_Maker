namespace Frog.Application.Assets;

/// <summary>Racine disque des assets projet (éditeur, serveur catalogue, tests).</summary>
public static class ProjectAssetRootResolver
{
    public const string EnvVariable = "FROG_PROJECT_ASSET_ROOT";

    public static string Resolve(string? configured = null)
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv.Trim());
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets"));
    }
}
