using System.Net;
using System.Security.Cryptography;

using Frog.Core.Distribution;

namespace Frog.Server.Config;

/// <summary>
/// Publication <c>.frogpack</c> V1. La clé publique est épinglée ; la graine privée ne sert qu’à signer.
/// Les variables d’environnement écrasent le JSON (secrets hors dépôt).
/// Hello TCP reste 11 : ce canal est HTTP, additif.
/// </summary>
public sealed class TilePackOptions
{
    public const string SectionName = "TilePack";

    public const string PublicKeyEnvironmentVariable = "FROG_TILEPACK_PUBLIC_KEY_HEX";
    public const string PrivateSeedEnvironmentVariable = "FROG_TILEPACK_PRIVATE_SEED_HEX";
    public const string AdminTokenEnvironmentVariable = "FROG_TILEPACK_ADMIN_TOKEN";
    public const string EnabledEnvironmentVariable = "FROG_TILEPACK_CONTENT_ENABLED";
    public const string PortEnvironmentVariable = "FROG_TILEPACK_CONTENT_PORT";
    public const string ImportRootEnvironmentVariable = "FROG_TILEPACK_PNG_IMPORT_ROOT";

    /// <summary>Écoute HTTP du manifeste et du téléchargement. Désactivé par défaut pour ne pas ouvrir un port au démarrage.</summary>
    public bool Enabled { get; set; }

    public string BindAddress { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 6080;

    /// <summary>Sans ce drapeau, le canal contenu refuse tout bind autre que loopback.</summary>
    public bool AllowNonLoopbackContentBind { get; set; }

    /// <summary>Clé publique Ed25519 épinglée, 32 octets en hex minuscule (64 caractères).</summary>
    public string PublicKeyHex { get; set; } = string.Empty;

    /// <summary>Graine privée Ed25519, 32 octets en hex. Vide : l’upload vérifié reste possible, pas la signature locale.</summary>
    public string PrivateSeedHex { get; set; } = string.Empty;

    /// <summary>Jeton requis sur POST. Vide : la publication HTTP est refusée.</summary>
    public string AdminToken { get; set; } = string.Empty;

    /// <summary>Racine autorisée pour <c>POST …/from-folder</c>. Vide : le dossier HTTP est refusé.</summary>
    public string PngImportRoot { get; set; } = string.Empty;

    public void ApplyEnvironmentOverrides()
    {
        var enabled = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
        if (IsTruthy(enabled))
        {
            Enabled = true;
        }
        else if (IsFalsy(enabled))
        {
            Enabled = false;
        }

        var port = Environment.GetEnvironmentVariable(PortEnvironmentVariable);
        if (int.TryParse(port, out var parsedPort))
        {
            Port = parsedPort;
        }

        OverrideIfPresent(PublicKeyEnvironmentVariable, value => PublicKeyHex = value);
        OverrideIfPresent(PrivateSeedEnvironmentVariable, value => PrivateSeedHex = value);
        OverrideIfPresent(AdminTokenEnvironmentVariable, value => AdminToken = value);
        OverrideIfPresent(ImportRootEnvironmentVariable, value => PngImportRoot = value);
    }

    public bool TryValidate(out string? error)
    {
        error = null;
        if (!string.IsNullOrWhiteSpace(PublicKeyHex) && !TryParseKey(PublicKeyHex, FrogPackKeys.PublicKeyLength, out _))
        {
            error = "TilePack:PublicKeyHex doit être 32 octets hex (64 caractères).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(PrivateSeedHex) && !TryParseKey(PrivateSeedHex, FrogPackKeys.PrivateSeedLength, out _))
        {
            error = "TilePack:PrivateSeedHex doit être 32 octets hex (64 caractères).";
            return false;
        }

        if (TryGetPinnedPublicKey(out var pinned)
            && TryGetPrivateSeed(out var seed)
            && !CryptographicOperations.FixedTimeEquals(pinned, FrogPackKeys.PublicKeyFromSeed(seed)))
        {
            error = "La graine privée ne correspond pas à la clé publique Ed25519 épinglée.";
            return false;
        }

        if (!Enabled)
        {
            return true;
        }

        if (Port is <= 0 or > 65535)
        {
            error = "TilePack:Port invalide.";
            return false;
        }

        if (!IPAddress.TryParse(BindAddress, out var ip))
        {
            error = "TilePack:BindAddress invalide.";
            return false;
        }

        var loopback = IPAddress.IsLoopback(ip);
        if (!loopback && !AllowNonLoopbackContentBind)
        {
            error = "Bind contenu hors loopback : TilePack:AllowNonLoopbackContentBind=true est requis.";
            return false;
        }

        return true;
    }

    public bool TryGetPinnedPublicKey(out byte[] publicKey)
        => TryParseKey(PublicKeyHex, FrogPackKeys.PublicKeyLength, out publicKey);

    public bool TryGetPrivateSeed(out byte[] seed)
        => TryParseKey(PrivateSeedHex, FrogPackKeys.PrivateSeedLength, out seed);

    public string PinnedPublicKeyId()
        => TryGetPinnedPublicKey(out var key)
            ? Convert.ToHexString(key).ToLowerInvariant()
            : string.Empty;

    public static bool TryParseKey(string? hex, int expectedBytes, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var trimmed = hex.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (trimmed.Length != expectedBytes * 2)
        {
            return false;
        }

        try
        {
            var parsed = Convert.FromHexString(trimmed);
            if (parsed.Length != expectedBytes)
            {
                return false;
            }

            key = parsed;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void OverrideIfPresent(string variable, Action<string> apply)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            apply(value.Trim());
        }
    }

    private static bool IsTruthy(string? value)
        => value is not null
           && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || value.Equals("on", StringComparison.OrdinalIgnoreCase));

    private static bool IsFalsy(string? value)
        => value is not null
           && (value.Equals("0", StringComparison.OrdinalIgnoreCase)
               || value.Equals("false", StringComparison.OrdinalIgnoreCase)
               || value.Equals("no", StringComparison.OrdinalIgnoreCase)
               || value.Equals("off", StringComparison.OrdinalIgnoreCase));
}

public sealed class TilePackOptionsValidator : Microsoft.Extensions.Options.IValidateOptions<TilePackOptions>
{
    public Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, TilePackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.TryValidate(out var error)
            ? Microsoft.Extensions.Options.ValidateOptionsResult.Success
            : Microsoft.Extensions.Options.ValidateOptionsResult.Fail(error ?? "TilePack invalide.");
    }
}
