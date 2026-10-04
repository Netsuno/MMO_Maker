using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Frog.Client.Config;
using Frog.Core.Distribution;
using Frog.Core.Models;

namespace Frog.Client.Assets;

/// <summary>
/// Télécharge les tilesets composés publiés. La charge est signée (Ed25519, même clé que le frogpack).
/// Un catalogue JSON brut est refusé. Hello TCP ne change pas.
/// </summary>
public sealed class ComposedTilesetClientLoader : IDisposable
{
    public const string PackPath = ComposedTilesetPackFormat.HttpPath;

    private readonly TilePackClientOptions _options;
    private readonly ITilePackTransport _transport;
    private readonly bool _ownsTransport;
    private IReadOnlyList<ComposedTilesetDefinition> _tilesets = Array.Empty<ComposedTilesetDefinition>();

    public ComposedTilesetClientLoader(TilePackClientOptions options)
        : this(options, new HttpTilePackTransport(), ownsTransport: true)
    {
    }

    public ComposedTilesetClientLoader(TilePackClientOptions options, ITilePackTransport transport)
        : this(options, transport, ownsTransport: false)
    {
    }

    private ComposedTilesetClientLoader(TilePackClientOptions options, ITilePackTransport transport, bool ownsTransport)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _ownsTransport = ownsTransport;
    }

    public IReadOnlyList<ComposedTilesetDefinition> Tilesets => _tilesets;

    public static IReadOnlyList<ComposedTilesetDefinition> Load(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> trustedPublicKey)
    {
        if (ComposedTilesetPackFormat.IsUnprotectedCatalog(payload))
        {
            throw new FrogPackRejectedException("Catalogue brut non protégé refusé.");
        }

        return ComposedTilesetPackReader.Read(payload, trustedPublicKey);
    }

    public async Task<ComposedTilesetLoadResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!TryDecodePublicKey(_options.PublicKeyHex, out var publicKey, out var keyError))
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Rejected, keyError);
        }

        if (!TilePackClientOptions.TryNormalizeBaseUrl(_options.ContentBaseUrl, out var baseUrl))
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Rejected, "URL de contenu invalide.");
        }

        var url = new Uri(baseUrl.TrimEnd('/') + PackPath);
        TilePackHttpGet response;
        try
        {
            response = await _transport.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Unavailable, "Charge tilesets indisponible.");
        }

        if (response.StatusCode == (int)HttpStatusCode.NotFound
            || response.StatusCode == (int)HttpStatusCode.ServiceUnavailable)
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Unavailable, "Aucun tileset composé publié.");
        }

        if (response.StatusCode != (int)HttpStatusCode.OK || response.Body.Length == 0)
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Rejected, "Réponse tilesets inutilisable.");
        }

        try
        {
            var loaded = Load(response.Body, publicKey);
            _tilesets = loaded;
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Loaded, loaded.Count + " tileset(s).");
        }
        catch (FrogPackRejectedException ex)
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Rejected, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _tilesets = Array.Empty<ComposedTilesetDefinition>();
            return new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Rejected, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsTransport && _transport is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool TryDecodePublicKey(string? hex, out byte[] publicKey, out string error)
    {
        publicKey = Array.Empty<byte>();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(hex))
        {
            error = "Clé publique Ed25519 absente.";
            return false;
        }

        var trimmed = hex.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (trimmed.Length != FrogPackKeys.PublicKeyLength * 2)
        {
            error = "Clé publique Ed25519 invalide.";
            return false;
        }

        try
        {
            publicKey = Convert.FromHexString(trimmed);
        }
        catch (FormatException)
        {
            error = "Clé publique Ed25519 invalide.";
            return false;
        }

        if (publicKey.Length != FrogPackKeys.PublicKeyLength)
        {
            error = "Clé publique Ed25519 invalide.";
            return false;
        }

        return true;
    }
}

public enum ComposedTilesetLoadKind
{
    Loaded,
    Rejected,
    Unavailable,
}

public sealed record ComposedTilesetLoadResult(ComposedTilesetLoadKind Kind, string Detail);
