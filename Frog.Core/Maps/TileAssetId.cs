using System.Security.Cryptography;

using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Identité stable d’une tuile 48×48, indépendante de la position (x, y) dans la feuille source.
/// Algorithme : SHA-256 complet (32 octets, pas de troncature) du buffer normalisé
/// (<see cref="TilePixelNormalizer"/> : RGBA8 row-major prémultiplié, 9216 octets).
/// Forme texte : 64 hexadécimaux minuscules. L’identifiant tout à zéro est réservé (<see cref="None"/>) :
/// il signifie « pas de graphique », pas un hash de pixels.
/// </summary>
public readonly struct TileAssetId : IEquatable<TileAssetId>, IComparable<TileAssetId>
{
    public const int ByteLength = 32;
    public const int HexLength = 64;

    private readonly byte[]? _bytes;

    private TileAssetId(byte[] bytes) => _bytes = bytes;

    public static TileAssetId None { get; } = new(new byte[ByteLength]);

    public bool IsNone
    {
        get
        {
            var bytes = _bytes;
            if (bytes is not { Length: ByteLength })
            {
                return true;
            }

            foreach (var value in bytes)
            {
                if (value != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Hash du RGBA droit 48×48 après prémultiplication. Refuse toute autre taille.</summary>
    public static TileAssetId FromStraightRgba(ReadOnlySpan<byte> straightRgba)
    {
        var normalized = TilePixelNormalizer.PremultiplyRgba(straightRgba);
        return FromNormalizedRgba(normalized);
    }

    /// <summary>Hash d’un buffer déjà prémultiplié (48×48×4). Ne redimensionne pas.</summary>
    public static TileAssetId FromNormalizedRgba(ReadOnlySpan<byte> normalizedRgba)
    {
        if (normalizedRgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
        {
            throw new ArgumentException(
                $"TileAssetId exige {TileAssetMetrics.CanonicalPixelByteCount} octets normalisés. {TileSizeMigrationPolicy.NoSilentUpscale}",
                nameof(normalizedRgba));
        }

        Span<byte> hash = stackalloc byte[ByteLength];
        SHA256.HashData(normalizedRgba, hash);
        return FromHashBytes(hash);
    }

    public static TileAssetId FromHashBytes(ReadOnlySpan<byte> sha256)
    {
        if (sha256.Length != ByteLength)
        {
            throw new ArgumentException("Un TileAssetId fait 32 octets (SHA-256 complet).", nameof(sha256));
        }

        var copy = sha256.ToArray();
        return new TileAssetId(copy);
    }

    public static TileAssetId Parse(string hex)
    {
        if (!TryParse(hex, out var id))
        {
            throw new FormatException("TileAssetId : 64 caractères hexadécimaux attendus.");
        }

        return id;
    }

    public static bool TryParse(string? hex, out TileAssetId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != HexLength)
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromHexString(hex);
            if (bytes.Length != ByteLength)
            {
                return false;
            }

            id = new TileAssetId(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string ToHex() => Convert.ToHexString(AsBytes()).ToLowerInvariant();

    public byte[] ToByteArray() => (byte[])AsBytes().Clone();

    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
        {
            throw new ArgumentException("Destination trop courte pour un TileAssetId.", nameof(destination));
        }

        AsBytes().CopyTo(destination);
    }

    public int CompareTo(TileAssetId other)
    {
        var left = AsBytes();
        var right = other.AsBytes();
        for (var i = 0; i < ByteLength; i++)
        {
            var cmp = left[i].CompareTo(right[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    public bool Equals(TileAssetId other) => AsBytes().AsSpan().SequenceEqual(other.AsBytes());

    public override bool Equals(object? obj) => obj is TileAssetId other && Equals(other);

    public override int GetHashCode()
    {
        var bytes = AsBytes();
        return HashCode.Combine(bytes[0], bytes[1], bytes[2], bytes[3], bytes[28], bytes[29], bytes[30], bytes[31]);
    }

    public override string ToString() => IsNone ? "none" : ToHex();

    public static bool operator ==(TileAssetId left, TileAssetId right) => left.Equals(right);

    public static bool operator !=(TileAssetId left, TileAssetId right) => !left.Equals(right);

    private byte[] AsBytes() => _bytes is { Length: ByteLength } bytes ? bytes : NoneBytes();

    private static byte[] NoneBytes() => None._bytes!;
}
