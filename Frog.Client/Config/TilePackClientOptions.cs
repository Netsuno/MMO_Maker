using System;
using System.IO;

namespace Frog.Client.Config;

/// <summary>
/// Canal HTTP du paquet de tuiles publié. Lu au démarrage du client.
/// Les variables d’environnement écrasent <see cref="UserSettings"/>.
/// </summary>
public sealed class TilePackClientOptions
{
    public const string ContentBaseUrlEnvironmentVariable = "FROG_TILEPACK_CONTENT_BASE_URL";

    public const string PublicKeyHexEnvironmentVariable = "FROG_TILEPACK_PUBLIC_KEY_HEX";

    public const string SlugEnvironmentVariable = "FROG_TILEPACK_SLUG";

    /// <summary>Surcharge du dossier de cache (tests). Défaut : %LocalAppData%\MmoMaker\Content\tile-assets.</summary>
    public const string CacheDirectoryEnvironmentVariable = "FROG_TILEPACK_CACHE_DIR";

    public const string DefaultContentBaseUrl = "http://127.0.0.1:6080";

    public string ContentBaseUrl { get; init; } = DefaultContentBaseUrl;

    /// <summary>Clé publique Ed25519 épinglée, 64 hex. Vide : aucune tuile du paquet n’est peinte.</summary>
    public string PublicKeyHex { get; init; } = string.Empty;

    /// <summary>Slug optionnel (<c>?slug=</c>). Vide : le serveur renvoie le paquet publié le plus récent.</summary>
    public string Slug { get; init; } = string.Empty;

    public static TilePackClientOptions Resolve(UserSettings? settings)
    {
        var urlRaw = First(
            Environment.GetEnvironmentVariable(ContentBaseUrlEnvironmentVariable),
            settings?.TilePackContentBaseUrl,
            DefaultContentBaseUrl);
        if (!TryNormalizeBaseUrl(urlRaw, out var url))
        {
            url = DefaultContentBaseUrl;
        }

        var key = First(
            Environment.GetEnvironmentVariable(PublicKeyHexEnvironmentVariable),
            settings?.TilePackPublicKeyHex,
            string.Empty);
        var slug = First(
            Environment.GetEnvironmentVariable(SlugEnvironmentVariable),
            settings?.TilePackSlug,
            string.Empty);

        return new TilePackClientOptions
        {
            ContentBaseUrl = url,
            PublicKeyHex = key.Trim(),
            Slug = slug.Trim(),
        };
    }

    public static string ResolveCacheDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable(CacheDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            return Path.GetFullPath(overrideDir);
        }

        return DefaultCacheDirectory();
    }

    public static string DefaultCacheDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "MmoMaker", "Content", "tile-assets");
    }

    public static bool TryNormalizeBaseUrl(string? raw, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        url = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static string First(string? primary, string? secondary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return secondary.Trim();
        }

        return fallback;
    }
}
