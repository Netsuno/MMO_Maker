using System.IO;
using Frog.Application.Assets;
using Microsoft.Extensions.Configuration;

namespace Frog.Editor.Assets;

/// <summary>Racine projet des assets visuels (chemins logiques relatifs).</summary>
public static class ProjectAssetRoot
{
    public const string EnvVariable = ProjectAssetRootResolver.EnvVariable;

    public static string Resolve()
    {
        string? configured = null;
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();
            configured = config["Editor:AssetRoot"];
        }
        catch
        {
            // fall through
        }

        return ProjectAssetRootResolver.Resolve(configured);
    }
}
