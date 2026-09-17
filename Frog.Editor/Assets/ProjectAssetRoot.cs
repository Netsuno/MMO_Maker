using System.IO;
using Microsoft.Extensions.Configuration;

namespace Frog.Editor.Assets;

/// <summary>Racine projet des assets visuels (chemins logiques relatifs).</summary>
public static class ProjectAssetRoot
{
    public const string EnvVariable = "FROG_PROJECT_ASSET_ROOT";

    public static string Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv.Trim());
        }

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();
            var configured = config["Editor:AssetRoot"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured.Trim());
            }
        }
        catch
        {
            // fall through to default
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets"));
    }
}
