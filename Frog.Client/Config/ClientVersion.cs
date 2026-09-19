using System.Reflection;

namespace Frog.Client.Config;

/// <summary>Version affichée (csproj <c>Version</c> 10.3.0).</summary>
internal static class ClientVersion
{
    public static string Display
    {
        get
        {
            var asm = typeof(ClientVersion).Assembly;
            var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            var version = asm.GetName().Version;
            return version is null ? "10.3.0" : version.ToString(3);
        }
    }
}
