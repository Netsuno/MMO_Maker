using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Frog.Server.Config;

using Microsoft.Extensions.Options;

namespace Frog.Server.Content;

/// <summary>
/// Canal HTTP additif : manifeste du paquet publié et téléchargement des octets <c>.frogpack</c>.
/// N’entre pas dans le Hello TCP (<see cref="Frog.Core.Constants.FrogWireProtocol.Version"/> = 11).
/// </summary>
public sealed class TilePackContentHttp
{
    public const string AdminHeaderName = "X-Frog-TilePack-Admin";

    private readonly TilePackPublishService _publish;
    private readonly IOptions<TilePackOptions> _options;

    public TilePackContentHttp(TilePackPublishService publish, IOptions<TilePackOptions> options)
    {
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TilePackHttpResult> HandleAsync(
        string method,
        string pathAndQuery,
        string? contentType,
        string? adminToken,
        ReadOnlyMemory<byte> body,
        string? ifNoneMatch,
        CancellationToken cancellationToken = default)
    {
        var path = pathAndQuery;
        string query = string.Empty;
        var q = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            path = pathAndQuery[..q];
            query = pathAndQuery[(q + 1)..];
        }

        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        var slug = Query(query, "slug");
        var version = Query(query, "version");
        var verb = method.ToUpperInvariant();

        if (verb == "GET" && path.Equals("/content/tile-packs/current", StringComparison.Ordinal))
        {
            return await ManifestAsync(slug, cancellationToken).ConfigureAwait(false);
        }

        if (verb == "GET" && path.Equals("/content/tile-packs/current.frogpack", StringComparison.Ordinal))
        {
            return await DownloadAsync(slug, ifNoneMatch, cancellationToken).ConfigureAwait(false);
        }

        if (verb == "POST" && path.Equals("/content/tile-packs/yank", StringComparison.Ordinal))
        {
            if (!AdminOk(adminToken))
            {
                return Json(HttpStatusCode.Forbidden, new { error = "Jeton d’administration requis." });
            }

            return await YankAsync(body, cancellationToken).ConfigureAwait(false);
        }

        if (verb == "POST" && path.Equals("/content/tile-packs", StringComparison.Ordinal))
        {
            if (!AdminOk(adminToken))
            {
                return Json(HttpStatusCode.Forbidden, new { error = "Jeton d’administration requis." });
            }

            return await PublishAsync(contentType, slug, version, body, cancellationToken).ConfigureAwait(false);
        }

        return Json(HttpStatusCode.NotFound, new { error = "Route contenu inconnue." });
    }

    private async Task<TilePackHttpResult> ManifestAsync(string? slug, CancellationToken cancellationToken)
    {
        var manifest = await _publish.GetCurrentManifestAsync(slug, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return Json(HttpStatusCode.NotFound, new { error = "Aucun paquet de tuiles publié." });
        }

        return Json(HttpStatusCode.OK, new
        {
            slug = manifest.Slug,
            version = manifest.Version,
            status = "published",
            tileCount = manifest.TileCount,
            tileSizePixels = manifest.TileSizePixels,
            frogpackSha256 = manifest.FrogpackSha256,
            ed25519Signature = manifest.Ed25519Signature,
            ed25519PublicKeyId = manifest.Ed25519PublicKeyId,
            downloadPath = manifest.DownloadPath,
            protocolVersion = manifest.ProtocolVersion,
            publishedAtUtc = manifest.PublishedAtUtc,
        }, etag: manifest.FrogpackSha256);
    }

    private async Task<TilePackHttpResult> DownloadAsync(string? slug, string? ifNoneMatch, CancellationToken cancellationToken)
    {
        var manifest = await _publish.GetCurrentManifestAsync(slug, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return Json(HttpStatusCode.NotFound, new { error = "Aucun paquet de tuiles publié." });
        }

        if (NoneMatch(ifNoneMatch, manifest.FrogpackSha256))
        {
            return new TilePackHttpResult(HttpStatusCode.NotModified, "application/octet-stream", Array.Empty<byte>(), manifest.FrogpackSha256);
        }

        var bytes = await _publish.GetCurrentPackBytesAsync(slug, cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return Json(HttpStatusCode.NotFound, new { error = "Le paquet publié n’a pas d’octets." });
        }

        var fileName = manifest.Slug + "-" + manifest.Version + ".frogpack";
        return new TilePackHttpResult(
            HttpStatusCode.OK,
            "application/octet-stream",
            bytes,
            manifest.FrogpackSha256,
            "attachment; filename=\"" + fileName + "\"");
    }

    private async Task<TilePackHttpResult> PublishAsync(
        string? contentType,
        string? querySlug,
        string? queryVersion,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        if (body.Length > TilePackPublishService.MaxPackBytes)
        {
            return Json(HttpStatusCode.RequestEntityTooLarge, new { error = "Corps trop grand." });
        }

        var type = contentType ?? string.Empty;
        if (type.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(querySlug) || string.IsNullOrWhiteSpace(queryVersion))
            {
                return Json(HttpStatusCode.BadRequest, new { error = "slug et version sont requis." });
            }

            var uploaded = await _publish.PublishUploadedPackAsync(querySlug, queryVersion, body, cancellationToken)
                .ConfigureAwait(false);
            return FromPublish(uploaded);
        }

        TilePackPublishBody? document;
        try
        {
            document = JsonSerializer.Deserialize<TilePackPublishBody>(body.Span, JsonOptions);
        }
        catch (JsonException)
        {
            return Json(HttpStatusCode.BadRequest, new { error = "JSON illisible." });
        }

        if (document is null || string.IsNullOrWhiteSpace(document.Slug) || string.IsNullOrWhiteSpace(document.Version))
        {
            return Json(HttpStatusCode.BadRequest, new { error = "slug et version sont requis." });
        }

        var sources = 0;
        if (document.FromCatalogue)
        {
            sources++;
        }

        if (!string.IsNullOrWhiteSpace(document.Folder))
        {
            sources++;
        }

        if (document.Tiles is { Count: > 0 })
        {
            sources++;
        }

        if (sources != 1)
        {
            return Json(HttpStatusCode.BadRequest, new { error = "Indiquer exactement une source : tiles, folder ou fromCatalogue." });
        }

        TilePackPublishResult result;
        if (document.FromCatalogue)
        {
            result = await _publish.PublishStoredCatalogueAsync(document.Slug, document.Version, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(document.Folder))
        {
            result = await _publish.PublishPngDirectoryAsync(
                document.Slug,
                document.Version,
                document.Folder,
                enforceImportRoot: true,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var tiles = new List<Frog.Core.Maps.TileAsset>();
            var names = new List<string?>();
            foreach (var tile in document.Tiles!)
            {
                byte[] rgba;
                try
                {
                    rgba = Convert.FromBase64String(tile.RgbaBase64 ?? string.Empty);
                }
                catch (FormatException)
                {
                    return Json(HttpStatusCode.BadRequest, new { error = "rgbaBase64 illisible." });
                }

                try
                {
                    tiles.Add(Frog.Core.Maps.TileAsset.FromStraightRgba(rgba));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
                {
                    return Json(HttpStatusCode.BadRequest, new { error = ex.Message });
                }

                names.Add(tile.DisplayName);
            }

            result = await _publish.PublishTilesAsync(document.Slug, document.Version, tiles, names, cancellationToken)
                .ConfigureAwait(false);
        }

        return FromPublish(result);
    }

    private async Task<TilePackHttpResult> YankAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        TilePackYankBody? document;
        try
        {
            document = JsonSerializer.Deserialize<TilePackYankBody>(body.Span, JsonOptions);
        }
        catch (JsonException)
        {
            return Json(HttpStatusCode.BadRequest, new { error = "JSON illisible." });
        }

        if (document is null || string.IsNullOrWhiteSpace(document.Slug) || string.IsNullOrWhiteSpace(document.Version))
        {
            return Json(HttpStatusCode.BadRequest, new { error = "slug et version sont requis." });
        }

        var yanked = await _publish.YankAsync(document.Slug, document.Version, cancellationToken).ConfigureAwait(false);
        return yanked
            ? Json(HttpStatusCode.OK, new { status = "yanked", slug = document.Slug, version = document.Version })
            : Json(HttpStatusCode.NotFound, new { error = "Paquet publié introuvable." });
    }

    private bool AdminOk(string? provided)
    {
        var expected = _options.Value.AdminToken;
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var left = Encoding.UTF8.GetBytes(provided);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static TilePackHttpResult FromPublish(TilePackPublishResult result)
    {
        if (result is TilePackPublishResult.Published published)
        {
            return Json(HttpStatusCode.OK, new
            {
                status = "published",
                slug = published.Manifest.Slug,
                version = published.Manifest.Version,
                tileCount = published.Manifest.TileCount,
                tileSizePixels = published.Manifest.TileSizePixels,
                frogpackSha256 = published.Manifest.FrogpackSha256,
                ed25519Signature = published.Manifest.Ed25519Signature,
                ed25519PublicKeyId = published.Manifest.Ed25519PublicKeyId,
                downloadPath = published.Manifest.DownloadPath,
                protocolVersion = published.Manifest.ProtocolVersion,
            });
        }

        var rejected = (TilePackPublishResult.Rejected)result;
        return Json(HttpStatusCode.BadRequest, new { error = rejected.Reason });
    }

    private static bool NoneMatch(string? header, string sha)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        foreach (var part in header.Split(','))
        {
            var token = part.Trim();
            if (token.StartsWith("W/", StringComparison.Ordinal))
            {
                token = token[2..].Trim();
            }

            token = token.Trim('"');
            if (token == "*" || string.Equals(token, sha, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Query(string query, string key)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var name = eq >= 0 ? part[..eq] : part;
            if (!name.Equals(key, StringComparison.Ordinal))
            {
                continue;
            }

            var value = eq >= 0 ? part[(eq + 1)..] : string.Empty;
            return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
        }

        return null;
    }

    private static TilePackHttpResult Json(HttpStatusCode status, object payload, string? etag = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        return new TilePackHttpResult(status, "application/json; charset=utf-8", bytes, etag);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed record TilePackHttpResult(
    HttpStatusCode StatusCode,
    string ContentType,
    byte[] Body,
    string? ETag,
    string? ContentDisposition = null);

public sealed class TilePackPublishBody
{
    public string? Slug { get; set; }

    public string? Version { get; set; }

    public string? Folder { get; set; }

    public bool FromCatalogue { get; set; }

    public List<TilePackTileBody>? Tiles { get; set; }
}

public sealed class TilePackTileBody
{
    public string? RgbaBase64 { get; set; }

    public string? DisplayName { get; set; }
}

public sealed class TilePackYankBody
{
    public string? Slug { get; set; }

    public string? Version { get; set; }
}
