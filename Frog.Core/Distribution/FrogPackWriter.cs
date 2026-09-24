using System.Buffers.Binary;
using System.Security.Cryptography;

using Frog.Core.Maps;

namespace Frog.Core.Distribution;

/// <summary>
/// Écrit un <c>.frogpack</c> V1 complet. Les tuiles sont dédupliquées par <see cref="TileAssetId"/>
/// puis triées par identifiant. La signature Ed25519 porte sur le SHA-256 du corps (en-tête + manifeste + blobs).
/// </summary>
public static class FrogPackWriter
{
    public static byte[] Write(IReadOnlyList<TileAsset> tiles, ReadOnlySpan<byte> privateSeed)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var publicKey = FrogPackKeys.PublicKeyFromSeed(privateSeed);
        var ordered = CanonicalTiles(tiles);
        if (ordered.Count > FrogPackFormat.MaxTileCount)
        {
            throw new ArgumentException($"Un frogpack V1 contient au plus {FrogPackFormat.MaxTileCount} tuiles.", nameof(tiles));
        }

        var blobSize = Frog.Core.Constants.TileAssetMetrics.CanonicalPixelByteCount;
        var bodyLength = FrogPackFormat.HeaderLength
            + (ordered.Count * FrogPackFormat.ManifestEntryLength)
            + (ordered.Count * blobSize);
        var body = new byte[bodyLength];

        body[0] = (byte)'F';
        body[1] = (byte)'P';
        body[2] = (byte)'K';
        body[3] = (byte)'1';
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), FrogPackFormat.FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), FrogPackFormat.FlagsNone);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(8), (ushort)Frog.Core.Constants.TileAssetMetrics.TargetTileSizePixels);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(12), (uint)ordered.Count);

        var manifest = FrogPackFormat.HeaderLength;
        var blobs = manifest + (ordered.Count * FrogPackFormat.ManifestEntryLength);
        for (var i = 0; i < ordered.Count; i++)
        {
            var tile = ordered[i];
            var entry = manifest + (i * FrogPackFormat.ManifestEntryLength);
            tile.Id.CopyTo(body.AsSpan(entry, TileAssetId.ByteLength));
            var offset = (ulong)i * (ulong)blobSize;
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(entry + FrogPackFormat.BlobOffsetField), offset);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(entry + FrogPackFormat.BlobSizeField), (uint)blobSize);
            var hash = SHA256.HashData(tile.NormalizedRgba);
            if (!tile.Id.Equals(TileAssetId.FromHashBytes(hash)))
            {
                throw new InvalidDataException("Pixels de tuile incohérents avec le TileAssetId.");
            }

            hash.CopyTo(body.AsSpan(entry + FrogPackFormat.BlobHashField, FrogPackFormat.ContentHashLength));
            tile.NormalizedRgba.CopyTo(body.AsSpan(blobs + (i * blobSize), blobSize));
        }

        var contentHash = SHA256.HashData(body);
        var signature = FrogPackKeys.Sign(privateSeed, contentHash);
        if (signature.Length != FrogPackFormat.SignatureLength)
        {
            throw new InvalidDataException("Signature Ed25519 inattendue.");
        }

        var file = new byte[body.Length + FrogPackFormat.TrailerLength];
        body.CopyTo(file, 0);
        contentHash.CopyTo(file.AsSpan(body.Length));
        publicKey.CopyTo(file.AsSpan(body.Length + FrogPackFormat.ContentHashLength));
        signature.CopyTo(file.AsSpan(body.Length + FrogPackFormat.ContentHashLength + FrogPackFormat.PublicKeyLength));
        return file;
    }

    private static List<TileAsset> CanonicalTiles(IReadOnlyList<TileAsset> tiles)
    {
        var unique = new Dictionary<TileAssetId, TileAsset>();
        foreach (var tile in tiles)
        {
            if (tile is null)
            {
                throw new ArgumentException("Tuile nulle.", nameof(tiles));
            }

            if (tile.Id.IsNone)
            {
                throw new ArgumentException("TileAssetId.None ne peut pas entrer dans un frogpack.", nameof(tiles));
            }

            if (tile.NormalizedRgba.Length != Frog.Core.Constants.TileAssetMetrics.CanonicalPixelByteCount)
            {
                throw new ArgumentException("Tuile hors 48×48 normalisé.", nameof(tiles));
            }

            if (unique.TryGetValue(tile.Id, out var existing)
                && !existing.NormalizedRgba.AsSpan().SequenceEqual(tile.NormalizedRgba))
            {
                throw new InvalidDataException("Même TileAssetId, pixels différents.");
            }

            unique[tile.Id] = tile;
        }

        var ordered = unique.Values.ToList();
        ordered.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return ordered;
    }
}
