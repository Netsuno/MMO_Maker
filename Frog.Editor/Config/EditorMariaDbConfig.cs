using Microsoft.Extensions.Configuration;

namespace Frog.Editor.Config;

/// <summary>Charge les mêmes clés que le serveur (<c>MariaDb:Enabled</c>, <c>MariaDb:ConnectionString</c>) depuis le répertoire de l’exécutable éditeur.</summary>
public static class EditorMariaDbConfig
{
    public static bool TryGetEnabledConnection(out string connectionString, out string userHint)
    {
        connectionString = string.Empty;
        userHint =
            "Créez appsettings.Local.json à côté de Frog.Editor.exe (copiez appsettings.Local.json.example) avec MariaDb.Enabled=true et une ConnectionString valide.";

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
                .Build();

            if (!configuration.GetValue("MariaDb:Enabled", false))
            {
                return false;
            }

            var cs = configuration["MariaDb:ConnectionString"];
            if (string.IsNullOrWhiteSpace(cs))
            {
                return false;
            }

            connectionString = cs.Trim();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
