using Frog.Core.Constants;

namespace Frog.Core.Distribution;

/// <summary>
/// Manifeste VERSION local / distant (scaffolding launcher). Pas un installateur.
/// Format : lignes <c>key=value</c> (<c>product</c>, <c>protocol</c>, <c>minClient</c>).
/// </summary>
public sealed record ClientVersionManifest(
    string ProductVersion,
    ushort ProtocolVersion,
    string? MinClientVersion = null)
{
    public const string ProductKey = "product";
    public const string ProtocolKey = "protocol";
    public const string MinClientKey = "minClient";

    public static ClientVersionManifest CurrentScaffolding { get; } = new(
        "10.3.0",
        FrogWireProtocol.Version,
        "10.3.0");

    public static ClientVersionManifest Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string? product = null;
        string? minClient = null;
        ushort? protocol = null;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#' || !trimmed.Contains('='))
            {
                continue;
            }

            var split = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                continue;
            }

            if (split[0].Equals(ProductKey, StringComparison.OrdinalIgnoreCase))
            {
                product = split[1];
            }
            else if (split[0].Equals(ProtocolKey, StringComparison.OrdinalIgnoreCase)
                     && ushort.TryParse(split[1], out var parsed))
            {
                protocol = parsed;
            }
            else if (split[0].Equals(MinClientKey, StringComparison.OrdinalIgnoreCase))
            {
                minClient = split[1];
            }
        }

        if (string.IsNullOrWhiteSpace(product) || protocol is null)
        {
            throw new FormatException("VERSION requires product= and protocol=.");
        }

        return new ClientVersionManifest(product, protocol.Value, minClient);
    }

    public static ClientVersionManifest LoadFile(string path)
        => Parse(File.ReadAllText(path));

    public string ToVersionFile()
    {
        var min = string.IsNullOrWhiteSpace(MinClientVersion) ? ProductVersion : MinClientVersion;
        return $"{ProductKey}={ProductVersion}{Environment.NewLine}{ProtocolKey}={ProtocolVersion}{Environment.NewLine}{MinClientKey}={min}{Environment.NewLine}";
    }

    public static VersionCheckResult Compare(ClientVersionManifest local, ClientVersionManifest remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        if (local.ProtocolVersion != remote.ProtocolVersion)
        {
            return new VersionCheckResult(
                VersionCheckOutcome.IncompatibleProtocol,
                $"Protocole {local.ProtocolVersion} (local) vs {remote.ProtocolVersion} (distant). Mettez à jour client et serveur ensemble.");
        }

        var minRemote = remote.MinClientVersion ?? remote.ProductVersion;
        if (CompareProduct(local.ProductVersion, minRemote) < 0)
        {
            return new VersionCheckResult(
                VersionCheckOutcome.UpdateRequired,
                $"Client {local.ProductVersion} < min {minRemote}. Mise à jour requise.");
        }

        if (CompareProduct(local.ProductVersion, remote.ProductVersion) < 0)
        {
            return new VersionCheckResult(
                VersionCheckOutcome.UpdateAvailable,
                $"Mise à jour disponible : {remote.ProductVersion} (local {local.ProductVersion}).");
        }

        return new VersionCheckResult(VersionCheckOutcome.Current, "Client à jour.");
    }

    public static int CompareProduct(string left, string right)
    {
        var a = ParseNumericPrefix(left);
        var b = ParseNumericPrefix(right);
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var av = i < a.Length ? a[i] : 0;
            var bv = i < b.Length ? b[i] : 0;
            if (av != bv)
            {
                return av.CompareTo(bv);
            }
        }

        return 0;
    }

    private static int[] ParseNumericPrefix(string value)
    {
        var parts = (value ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nums = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var span = parts[i];
            var end = 0;
            while (end < span.Length && char.IsDigit(span[end]))
            {
                end++;
            }

            nums[i] = end == 0 ? 0 : int.Parse(span[..end]);
        }

        return nums;
    }
}

public enum VersionCheckOutcome
{
    Current = 0,
    UpdateAvailable = 1,
    UpdateRequired = 2,
    IncompatibleProtocol = 3,
}

public sealed record VersionCheckResult(VersionCheckOutcome Outcome, string Message);
