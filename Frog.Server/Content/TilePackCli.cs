using Frog.Core.Distribution;
using Frog.Server.Config;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Frog.Server.Content;

/// <summary>
/// Crochet de ligne de commande, sans ouvrir le socket de jeu.
/// <c>--tilepack-keygen</c> affiche une paire Ed25519 locale.
/// <c>--tilepack-publish</c> signe ou vérifie puis écrit PostgreSQL.
/// </summary>
public static class TilePackCli
{
    public static bool IsKeygen(string[] args)
        => args.Length > 0 && string.Equals(args[0], "--tilepack-keygen", StringComparison.Ordinal);

    public static bool IsPublish(string[] args)
        => args.Any(a => string.Equals(a, "--tilepack-publish", StringComparison.Ordinal));

    public static string[] KeygenLines()
    {
        var keys = FrogPackKeys.Generate();
        return
        [
            "FROG_TILEPACK_PUBLIC_KEY_HEX=" + Convert.ToHexString(keys.PublicKey).ToLowerInvariant(),
            "FROG_TILEPACK_PRIVATE_SEED_HEX=" + Convert.ToHexString(keys.PrivateSeed).ToLowerInvariant(),
            "Épinglez la clé publique (TilePack:PublicKeyHex). Gardez la graine hors dépôt. FrogWireProtocol.Version reste 11.",
        ];
    }

    public static int RunKeygen()
    {
        foreach (var line in KeygenLines())
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    public static bool TryParsePublish(string[] args, out TilePackCliPublish parsed, out string[] hostArgs, out string? error)
    {
        parsed = new TilePackCliPublish(string.Empty, string.Empty, null, null, false);
        error = null;
        string? slug = null;
        string? version = null;
        string? folder = null;
        string? frogpack = null;
        var fromCatalogue = false;
        var host = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--tilepack-publish")
            {
                continue;
            }

            if (arg is "--from-catalogue")
            {
                fromCatalogue = true;
                continue;
            }

            if (arg is "--slug" or "--version" or "--folder" or "--frogpack")
            {
                if (i + 1 >= args.Length)
                {
                    error = arg + " attend une valeur.";
                    hostArgs = Array.Empty<string>();
                    return false;
                }

                var value = args[++i];
                switch (arg)
                {
                    case "--slug":
                        slug = value;
                        break;
                    case "--version":
                        version = value;
                        break;
                    case "--folder":
                        folder = value;
                        break;
                    default:
                        frogpack = value;
                        break;
                }

                continue;
            }

            host.Add(arg);
        }

        hostArgs = host.ToArray();
        var sources = (folder is not null ? 1 : 0) + (frogpack is not null ? 1 : 0) + (fromCatalogue ? 1 : 0);
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(version) || sources != 1)
        {
            error = "Usage: Frog.Server --tilepack-publish --slug <slug> --version <version> (--folder <dir> | --frogpack <file> | --from-catalogue)";
            return false;
        }

        parsed = new TilePackCliPublish(slug, version, folder, frogpack, fromCatalogue);
        return true;
    }

    public static async Task<int> RunPublishAsync(IServiceProvider services, TilePackCliPublish publish, CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<TilePackOptions>>().Value;
        if (!options.TryGetPinnedPublicKey(out _))
        {
            Console.Error.WriteLine("Clé publique épinglée absente. Voir Frog.Server/Docs/tile-pack-publish.md.");
            return 2;
        }

        var service = services.GetRequiredService<TilePackPublishService>();
        TilePackPublishResult result;
        if (publish.Folder is not null)
        {
            result = await service.PublishPngDirectoryAsync(
                publish.Slug,
                publish.Version,
                publish.Folder,
                enforceImportRoot: false,
                cancellationToken).ConfigureAwait(false);
        }
        else if (publish.FrogpackPath is not null)
        {
            var bytes = await File.ReadAllBytesAsync(publish.FrogpackPath, cancellationToken).ConfigureAwait(false);
            result = await service.PublishUploadedPackAsync(publish.Slug, publish.Version, bytes, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            result = await service.PublishStoredCatalogueAsync(publish.Slug, publish.Version, cancellationToken)
                .ConfigureAwait(false);
        }

        if (result is TilePackPublishResult.Published published)
        {
            Console.WriteLine("published " + published.Manifest.Slug + " " + published.Manifest.Version);
            Console.WriteLine("tileCount " + published.Manifest.TileCount);
            Console.WriteLine("tileSizePixels " + published.Manifest.TileSizePixels);
            Console.WriteLine("frogpackSha256 " + published.Manifest.FrogpackSha256);
            Console.WriteLine("protocolVersion " + published.Manifest.ProtocolVersion);
            return 0;
        }

        Console.Error.WriteLine(((TilePackPublishResult.Rejected)result).Reason);
        return 1;
    }
}

public readonly record struct TilePackCliPublish(
    string Slug,
    string Version,
    string? Folder,
    string? FrogpackPath,
    bool FromCatalogue);
